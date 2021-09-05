using System;
using otavaSocket;

namespace chatApp
{
    class Program
    {
        /// This should be pointing to the chatApp directory
        /// in VisualStudio the CurrentDirectory is in chatApp/bin/Debug/net5.0
        /// so just go 3 folders up from that
        public static string ProgramDir = Environment.CurrentDirectory + @"/../../..";
        public static string UserPath = ProgramDir+@"/Data/Users.json";
        public static string ChatRoomPath = ProgramDir+@"/Data/ChatRooms.json";
        public static string WebRootDir = ProgramDir+@"/wwwroot";

        public static void Main()
        {
            Session.SessionLifetime = 300;
            WebServer server = new WebServer(WebRootDir, 80);
            server.UseWebSockets<ChatHub>();

            server.AddRoute(new Route { Path = "welcome", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.GetDefaultHandler("welcome.html")), NeedsResources=true });
            server.AddRoute(new Route { Path = "api/chatinit", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.InitializeChatroom) });
            server.AddRoute(new Route { Path = "welcome", Verb = "POST", Controller = new AnonymousController(Handlers.LoginHandler), NeedsResources=true});
            server.AddRoute(new Route { Path = "api/createRoom", Verb = "POST", Controller = new AuthorizedExpirableController(Handlers.CreateRoom) });
            server.AddRoute(new Route { Path = "api/messages", Verb = "GET", Controller = new AuthorizedController(Handlers.GetMessages) });
            server.AddRoute(new Route { Path = "api/join", Verb = "GET", Controller = new AuthorizedExpirableController(Handlers.JoinRoom) });
            server.AddRoute(new Route { Path = "api/getuser", Verb = "GET", Controller = new AuthorizedController(Handlers.GetCurrentUserData) });
            server.AddRoute(new Route { Path = "api/logout", Verb = "GET", Controller = new AuthorizedController(Handlers.Logout) });

            server.Start();
            
            Console.ReadLine();
            //server.Stop();
        }
    }
}
