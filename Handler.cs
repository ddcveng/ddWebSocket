using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace otavaSocket
{//TODO: make handlers async
    class Handler
    {
        //basic GET request without any post processing
        public static ResponseData DefaultHandler(Session session, Dictionary<string, string> kwargs)
        {
            session.MessageOffset = 0;
            return new ResponseData() { Complete = false };
        }

        // POST -> api/login | username and password in form data
        public static ResponseData LoginHandler(Session session, Dictionary<string, string> kwargs)
        {
            string username = kwargs["username"];
            string password = kwargs["password"];
            string submitButton = kwargs["operation"];
            string Status = "";
            var user = JSONFileService.GetAll<User>().FirstOrDefault(user => user.Username == username);
            if (user != null)
            {
                if (submitButton == "register")
                {
                    Status = "Username already taken!";
                }
                else if (AesEncryptor.Compare(password, user))
                {
                    //successful login, redirect user to the app
                    session.Authorized = true;
                    session.SessionData.TryAdd("UserID", user.ID.ToString());
                    session.SessionData.TryAdd("Username", user.Username);
                    var dataPacket = new JSONPacket();
                    if (user.Icon != null)
                    {
                        dataPacket.Redirect = "/welcome";
                        dataPacket.HasIcon = true;
                    }
                    else
                    {
                        dataPacket.HasIcon = false;
                    }
                    var ret = new ResponseData()
                    {
                        Data = Encoding.UTF8.GetBytes(dataPacket.ToString()),
                        ContentType = "text/json",
                        Complete = true,
                        Status = ServerStatus.OK,
                        Encoding = Encoding.UTF8
                    };
                    return ret;

                }
                else
                {
                    Status = "Wrong password!";
                }
            }
            else
            {
                if (submitButton == "login")
                {
                    Status = "No such user exists!";
                }
                else
                {
                    if (ParseCredentials(username, password))
                    {
                        Status = "Registered new user!";
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
                        Status = "Username and password cannot be empty!";
                    }
                }
            }
            var dataWrapper = new JSONPacket() { Data = Status, Redirect = null, HasIcon = true };
            return new ResponseData()
            {
                ContentType = "text/json",
                Encoding = Encoding.UTF8,
                Data = Encoding.UTF8.GetBytes(dataWrapper.ToString()),
                Status = ServerStatus.OK,
                Complete = true
            };
        }

        public static bool ParseCredentials(string username, string pass)
        {
            return username.Length > 0 && pass.Length > 0;
        }

        // GET -> api/chatinit
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
                Complete = true,
                Encoding = Encoding.UTF8,
                Status = ServerStatus.OK,
                Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userChatRooms))
            };
        }

        // POST -> api/createRoom | roomName in form data
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
                Complete = true
            };
        }

        // GET -> api/messages | id in params
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
                        session.MessageOffset = 0;
                }
                Guid ID = Guid.Parse(id);
                ChatRoom cr = JSONFileService.GetAll<ChatRoom>().First(c => c.ID == ID);
                ret.Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cr.Messages));
                ret.ContentType = "text/json";
                ret.Complete = true;
                ret.Encoding = Encoding.UTF8;
                ret.Status = ServerStatus.OK;
            }
            else
            {
                ret.Status = ServerStatus.UnknownType; //bad request
                ret.Complete = true;
            }
            return ret;
        }

        // POST -> api/messages | message text and user icon in form data
        public static ResponseData AddMessage(Session session, Dictionary<string, string> kwargs)
        {
            string currRoom;
            if (session.SessionData.TryGetValue("currentRoom", out currRoom))
            {
                string messageBody = kwargs["message"];
                string icon = kwargs["icon"];
                Message message = new Message()
                {
                    Sender = session.SessionData["Username"],
                    Body = messageBody,
                    TimeSent = DateTime.Now,
                    Icon = icon
                };
                JSONFileService.Update(Guid.Parse(currRoom), message);
                kwargs["id"] = currRoom;
                return GetMessages(session, kwargs);
            }
            return new ResponseData() { Status = ServerStatus.ServerError, Complete = true };
        }


        // GET -> api/seticon | icon in params
        public static ResponseData SetIcon(Session session, Dictionary<string, string> kwargs)
        {
            string icon = kwargs["icon"];
            JSONFileService.Update(Guid.Parse(session.SessionData["UserID"]), icon);
            JSONPacket jp = new JSONPacket() { HasIcon = true, Redirect = "/welcome" };
            return new ResponseData()
            {
                Data = Encoding.UTF8.GetBytes(jp.ToString()),
                Complete = true,
                ContentType = "text/json",
                Encoding = Encoding.UTF8,
                Status = ServerStatus.OK
            };
        }

        // GET -> api/join?id=...
        public static ResponseData JoinRoom(Session session, Dictionary<string, string> kwargs)
        {
            Guid RoomId;
            ServerStatus status = ServerStatus.UnknownType;
            if (Guid.TryParse(kwargs["id"], out RoomId)){
                Guid UserID = Guid.Parse(session.SessionData["UserID"]);
                JSONFileService.Update<ChatRoom>(RoomId, UserID);
                JSONFileService.Update<User>(UserID, RoomId);
                status = ServerStatus.OK;
            }
            return new ResponseData()
            {
                Complete = true,
                ContentType = "text/json",
                Encoding = Encoding.UTF8,
                Status = status,
                Data = new byte[]{}
            };
        }

        // GET -> api/getuser
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
                Complete = true,
                Status = ServerStatus.OK
            };
        }

        // GET -> api/logout
        public static ResponseData Logout(Session session, Dictionary<string, string> kwargs)
        {
            session.Valid = false;
            Console.WriteLine("lgout");
            return new ResponseData() {Redirect = "/Index.html", Complete=true, Status=ServerStatus.OK};
        }
    }
}
