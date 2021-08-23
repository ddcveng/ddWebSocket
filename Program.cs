namespace otavaSocket
{
    class Program
    {
        public const string ProgramDir = @"/home/ddcveng/projects/websocket";
        public const string UserPath = ProgramDir+@"/Data/Users.json";
        public const string ChatRoomPath = ProgramDir+@"/Data/ChatRooms.json";
        public const string WebRootDir = ProgramDir+@"/wwwroot";

        public static void Main()
        {
            WebServer server = new WebServer(WebRootDir, 5555, 300);

            server.AddRoute(new Route() { Path = "welcome", Verb = "GET", Controller = new AuthorizedExpirableController(Handler.DefaultHandler), RequestResources=true });
            server.AddRoute(new Route() { Path = "api/chatinit", Verb = "GET", Controller = new AuthorizedExpirableController(Handler.InitializeChatroom) });
            server.AddRoute(new Route() { Path = "api/login", Verb = "POST", Controller = new AnonymousController(Handler.LoginHandler) });
            server.AddRoute(new Route() { Path = "api/createRoom", Verb = "POST", Controller = new AuthorizedExpirableController(Handler.CreateRoom) });
            server.AddRoute(new Route() { Path = "api/messages", Verb = "GET", Controller = new AuthorizedController(Handler.GetMessages) });
            server.AddRoute(new Route() { Path = "api/messages", Verb = "POST", Controller = new AuthorizedController(Handler.AddMessage) });
            server.AddRoute(new Route() { Path = "api/seticon", Verb = "GET", Controller = new AuthorizedExpirableController(Handler.SetIcon) });
            server.AddRoute(new Route() { Path = "api/join", Verb = "GET", Controller = new AuthorizedExpirableController(Handler.JoinRoom) });
            server.AddRoute(new Route() { Path = "api/getuser", Verb = "GET", Controller = new AuthorizedController(Handler.GetCurrentUserData) });
            server.AddRoute(new Route() { Path = "api/logout", Verb = "GET", Controller = new AuthorizedController(Handler.Logout) });

            server.Start();
        }
    }
}
