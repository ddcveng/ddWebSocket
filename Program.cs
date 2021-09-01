using System;
using System.Text;

namespace otavaSocket
{
    class ChatHub : Hub
    {
        protected override void OnReceive(MemoryNode message, Session session)
        {
            string username;
            session.SessionData.TryGetValue("username", out username);
            string added = $" {username}";
            Encoding.ASCII.GetBytes(added, 0, added.Length, message.data, message.length);
            //for (int i = 0; i < added.Length; i++)
            //{
            //    message.data[message.length+i] = (byte)added[i];
            //}
            message.length += added.Length;
        }
    }
    class Program
    {
        public static string ProgramDir = Environment.CurrentDirectory;
        public static string UserPath = ProgramDir+@"/Data/Users.json";
        public static string ChatRoomPath = ProgramDir+@"/Data/ChatRooms.json";
        public static string WebRootDir = ProgramDir+@"/wwwroot";

        public static void Main()
        {
            Session.SessionLifetime = 300;
            WebServer server = new WebServer(WebRootDir, 5555);
            server.UseWebSockets<ChatHub>();

            server.AddRoute(new Route { Path = "startchat", Verb = "POST", Controller= new AnonymousController(Handlers.startchat)});
            server.AddRoute(new Route { Path = "welcome", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.GetDefaultHandler("welcome.html")), NeedsResources=true });
            server.AddRoute(new Route { Path = "api/chatinit", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.InitializeChatroom) });
            server.AddRoute(new Route { Path = "api/login", Verb = "POST", Controller = new AnonymousController(Handlers.LoginHandler) });
            server.AddRoute(new Route { Path = "api/createRoom", Verb = "POST", Controller = new AuthorizedExpirableController(Handlers.CreateRoom) });
            server.AddRoute(new Route { Path = "api/messages", Verb = "GET", Controller = new AuthorizedController(Handlers.GetMessages) });
            server.AddRoute(new Route { Path = "api/messages", Verb = "POST", Controller = new AuthorizedController(Handlers.AddMessage) });
            server.AddRoute(new Route { Path = "api/seticon", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.SetIcon) });
            server.AddRoute(new Route { Path = "api/join", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.JoinRoom) });
            server.AddRoute(new Route { Path = "api/getuser", Verb = "GET", Controller = new AuthorizedController(Handlers.GetCurrentUserData) });
            server.AddRoute(new Route { Path = "api/logout", Verb = "GET", Controller = new AuthorizedController(Handlers.Logout) });

            server.Start();
        }
    }
}
