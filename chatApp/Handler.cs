using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using otavaSocket;

namespace chatApp
{
    /// Signature of a Handler delegate
    using Handler = Func<Session, Dictionary<string, string>, ResponseData>;

    /// Hub implementation used for the example
    class ChatHub : Hub
    {
        /**
         * Saves the received message to file. Also creates a json object
         * for the message and returns its serialized version for sending
         * back to the clients
         */
        protected override string OnReceive(string message, Session session, string senderID)
        {
            string currRoom;
            if (session.SessionData.TryGetValue("currentRoom", out currRoom))
            {
                //Console.WriteLine("bol currentRoom");
                Message messagePacket = new Message()
                {
                    Sender = senderID,
                    Body = message,
                    TimeSent = DateTime.Now,
                };
                JSONFileService.Update(Guid.Parse(currRoom), messagePacket);
                return JsonSerializer.Serialize(messagePacket);
            }
            return "";
        }
    }

    /// Class housing all of the handler delegates
    /**
     * All of the handlers have a signature as defined on top of this file.
     *
     * They return a ResponseData object that can be directly sent back to
     * the browser or it can contain a request to load some static data and send
     * that instead.
     *
     * Handler parameters:
     * - session
     *   The session object associated with the connection
     * - kwargs
     *   A Dictionary containing data provided by the request as key value pairs.
     *   This means a GET request's url query will be found here as well as
     *   POST request's form data.
     */
    class Handlers
    {
        /// Helper function for routes that are just aliases to static resources
        /**
         * You can for example direct the route /chat/createRoom to the file createRoom.html
         */
        public static Handler GetDefaultHandler(string requestedPage)
        {
            Handler handlerFunc = (Session s, Dictionary<string, string> kwargs) =>
            {
                return new ResponseData { Status = ServerStatus.OK, RequestedResource = requestedPage };
            };

            return handlerFunc;
        }

        /// Handler function managing access to the app
        /**
         * POST -> api/login | username and password in form data
         *
         * Used on the login page. Takes the credentials the user put in
         * and tries to either register that user or log in as that user.
         *
         * Result of these oprations can be seen on standard output. Nothing can be
         * seen on the webpage because writing javascript is pain.
         */
        public static ResponseData LoginHandler(Session session, Dictionary<string, string> kwargs)
        {
            string username = kwargs["username"];
            string password = kwargs["password"];
            string submitButton = kwargs["operation"];
            var user = JSONFileService.GetAll<User>().FirstOrDefault(user => user.Username == username);
            if (user != null)
            {
                if (submitButton == "register")
                {
                    Console.WriteLine("Username already Taken!");
                }
                else if (AesEncryptor.Compare(password, user))
                {
                    //successful login, redirect user to the app
                    Console.WriteLine("Logging in...");
                    session.Authorized = true;
                    session.SessionData.TryAdd("UserID", user.ID.ToString());
                    session.SessionData.TryAdd("Username", user.Username);
                    return new ResponseData {
                        Status = ServerStatus.OK,
                        RequestedResource = "welcome.html"
                    };
                }
                else
                {
                    Console.WriteLine("Bad password!") ;
                }
            }
            else
            {
                if (submitButton == "login")
                {
                    Console.WriteLine("No such user exists");
                }
                else
                {
                    if (ParseCredentials(username, password))
                    {
                        Console.WriteLine($"Registered user {username}");
                        user = new User
                        {
                            Username = username,
                            Password = password,
                            DateCreated = DateTime.UtcNow.ToString()
                        };
                        AesEncryptor.Encrypt(user);
                        JSONFileService.Add(user);
                    }
                    else
                    {
                        Console.WriteLine("Username and Password cannot be empty");
                    }
                }
            }
            return new ResponseData
            {
                Status = ServerStatus.OK,
                RequestedResource = "login.html"
            };
        }

        private static bool ParseCredentials(string username, string pass)
        {
            return username.Length > 0 && pass.Length > 0;
        }

        /// Handler for initializing a chatroom with data
        /**
         * GET -> api/chatinit
         *
         * Gets the chatrooms available for the current user.
         */
        public static ResponseData InitializeChatroom(Session session, Dictionary<string, string> kwargs)
        {
            Guid userID = Guid.Parse(session.SessionData["UserID"]);
            User user = JSONFileService.GetAll<User>().First(r => r.ID == userID);
            IEnumerable<ChatRoom> allChatRooms = JSONFileService.GetAll<ChatRoom>();
            List<ChatRoom> userChatRooms = new List<ChatRoom>();
            foreach (var chatRoomID in user.IDList)
            {
                ChatRoom cr = allChatRooms.Single(cr => chatRoomID == cr.ID);
                cr.Minimize();
                userChatRooms.Add(cr);
            }
            if (userChatRooms.Count() != 0)
            {
                session.SessionData["currentRoom"] = userChatRooms[0].ID.ToString();
            }

            return new ResponseData()
            {
                ContentType = "text/json",
                Encoding = Encoding.UTF8,
                Status = ServerStatus.OK,
                Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userChatRooms))
            };
        }

        /// Handler for creating a new chatroom in the app
        /**
         * POST -> api/createRoom | roomName in form data
         *
         * Creates a new room in the app with the name provided and returns
         * the result of this operaion.
         */
        public static ResponseData CreateRoom(Session session, Dictionary<string, string> kwargs)
        {
            string roomName = kwargs["roomName"];
            Guid userID = Guid.Parse(session.SessionData["UserID"]);
            ChatRoom chatRoom = new ChatRoom()
            {
                Name = roomName,
                IDList = new List<Guid>() { userID }
            };
            JSONFileService.Add(chatRoom);

            JSONFileService.Update<User>(userID, chatRoom.ID);

            string status = string.Format("Created chatroom {0}", roomName);
            return new ResponseData()
            {
                ContentType = "text",
                Encoding = Encoding.UTF8,
                Data = Encoding.UTF8.GetBytes(status),
                Status = ServerStatus.OK,
            };
        }

        /// Handler for getting all messsages for a given chatroom
        /**
         * GET -> api/messages | id in params
         *
         * Finds the chatroom using the id provided and returns a list of all
         * the messages.
         */
        public static ResponseData GetMessages(Session session, Dictionary<string, string> kwargs)
        {
            string id;
            ResponseData ret = new ResponseData();
            if (kwargs.TryGetValue("id", out id) && id != "")
            {
                string temp;
                if (session.SessionData.TryGetValue("currentRoom", out temp) && temp != id)
                {
                        session.SessionData["currentRoom"] = id;
                }
                Guid ID = Guid.Parse(id);
                ChatRoom cr = JSONFileService.GetAll<ChatRoom>().First(c => c.ID == ID);
                ret.Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cr.Messages));
                ret.ContentType = "text/json";
                ret.Encoding = Encoding.UTF8;
                ret.Status = ServerStatus.OK;
            }
            else
            {
                ret.Status = ServerStatus.UnknownType; //bad request
            }
            return ret;
        }

        /// Handler for adding a user to a chatroom
        /**
         * If a user has the ID of a chatroom, they can join via this method on
         *
         * GET -> api/join?id=...
         *
         * This method updates both the user and the chatroom with the needed references
         * and returns a status message.
         */
        public static ResponseData JoinRoom(Session session, Dictionary<string, string> kwargs)
        {
            Guid RoomId;
            ServerStatus status = ServerStatus.UnknownType;
            if (Guid.TryParse(kwargs["id"], out RoomId)){
                Guid UserID = Guid.Parse(session.SessionData["UserID"]);
                Console.WriteLine($"user {UserID} joined room {RoomId}");
                JSONFileService.Update<ChatRoom>(RoomId, UserID);
                JSONFileService.Update<User>(UserID, RoomId);
                status = ServerStatus.OK;
            }
            return new ResponseData()
            {
                ContentType = "text/json",
                Encoding = Encoding.UTF8,
                Status = status,
                Data = Encoding.ASCII.GetBytes("[{\"join\":\"ok\"}]")
            };
        }

        /// Handler for getting info about the logged in user
        /**
         * GET -> api/getuser
         *
         * Provide the browser with data about the current user, excluding the password
         */
        public static ResponseData GetCurrentUserData(Session session, Dictionary<string, string> kwargs)
        {
            Guid userID = Guid.Parse(session.SessionData["UserID"]);
            User user = JSONFileService.GetAll<User>().First(u => u.ID == userID);
            user.IDList.Clear();
            user.Password = "";
            return new ResponseData() {
                Data = Encoding.UTF8.GetBytes(user.ToString()),
                Encoding = Encoding.UTF8,
                ContentType = "text/json",
                Status = ServerStatus.OK
            };
        }

        /// Handler for logging out the user
        /**
         * GET -> api/logout
         *
         * Sets the user session to unauthorized
         * and redirects the browser back to the front page
         */
        public static ResponseData Logout(Session session, Dictionary<string, string> kwargs)
        {
            //session.Valid = false;
            session.Authorized = false;
            Console.WriteLine("lgout");
            return new ResponseData() {Redirect = "/Index.html", Status=ServerStatus.OK};
        }
    }
}
