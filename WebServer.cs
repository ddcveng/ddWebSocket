using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace otavaSocket
{
    public class WebServer
    {
        // Spracuváva prichádzajúce HTTP requesty
        // Hlavný objekt programu, volá ostatné moduly
        private readonly HttpListener _listener;
        private readonly ushort _port;
        private readonly Router _router;
        private readonly SessionManager _sm;
        private readonly int maxSimultaneousConnections = 10;
        private bool _running = true;
        public static int SessionLifetime { get; set; }

        public WebServer(string webRootFolder, ushort port = 5555, int sessionExpireTime = 60)
        {
            _port = port;
            SessionLifetime = sessionExpireTime;
            _router = new Router(webRootFolder);
            _sm = new SessionManager();
            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", _port));
        }

        public void AddRoute(IEnumerable<Route> routes)
        {
            foreach (var route in routes)
            {
                _router.AddRoute(route);
            }
        }

        public void AddRoute(Route route)
        {
            _router.AddRoute(route);
        }

        public void Start()
        {
            Task.Run(RunServer);

            Console.ReadLine();
            Console.WriteLine("Server shutting down...");
            _running = false;
        }

        private void RunServer()
        {
            using var semaphore = new SemaphoreSlim(maxSimultaneousConnections, maxSimultaneousConnections);

            _listener.Start();
            Console.WriteLine("Started http listener on port {0}", _port);
            Console.WriteLine("Press ENTER to end the program.");

            while(_running)
            {
                semaphore.Wait();
                //Console.WriteLine($"LOCK: {semaphore.CurrentCount}");
                HandleRequestsAsync(semaphore);
            }
            _listener.Close();
        }

        //!!!!!! exceptions here crash the server, but should probably just fail the request;
        private async void HandleRequestsAsync(SemaphoreSlim semaphore)
        {
            HttpListenerContext ctx = await _listener.GetContextAsync();

            //we have a connection, release the semaphore
            semaphore.Release();
            //Console.WriteLine($"RELEASE: {semaphore.CurrentCount}");

            HttpListenerRequest request = ctx.Request;
            HttpListenerResponse response = ctx.Response;
            Log(request);
            Session session = _sm.GetSession(request.RemoteEndPoint);
            string route = request.RawUrl.Substring(1).Split("?")[0];
            var kwargs = GetParams(request);

            //TODO: This should probably use await
            ResponseData resp = _router.Route(session, request.HttpMethod, route, kwargs);

            session.UpdateLastConnectionTime();

            //TODO: Use this for actual redirects and to just conditional resource
            //      loading
            if (string.IsNullOrEmpty(resp.Redirect))
            {
                response.ContentType = resp.ContentType;
                response.ContentEncoding = resp.Encoding;
                response.ContentLength64 = resp.Data.LongLength;
                response.StatusCode = (int)resp.Status;
                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(resp.Data, 0, resp.Data.Length);
                }
            }
            else
            {
                response.StatusCode = (int)ServerStatus.Redirect;
                response.Redirect("http://" + request.UserHostName + resp.Redirect);
            }
            //Send it
            response.Close();

            _sm.RemoveInvalidSessions();
        }

        public void Log(HttpListenerRequest req)
        {
            Console.WriteLine(req.HttpMethod + " " + req.RawUrl);
        }

        public void Log(string text)
        {
            Console.WriteLine(text);
        }

        private Dictionary<string, string> GetParams(HttpListenerRequest request)
        {
            //TODO: get/post parsing
            var kwargs = new Dictionary<string, string>();
            if (request.HttpMethod == "GET")
            {
                var t = request.QueryString;
                foreach (var key in t.AllKeys)
                {
                    kwargs.Add(key, t[key]);
                }
            }
            else
            {
                string raw = "";
                using (var reader = new StreamReader(request.InputStream,
                                                     request.ContentEncoding))
                {
                    raw = reader.ReadToEnd();
                }
                if (raw.Length > 0)
                {
                    Log(raw);
                    string[] pairs = raw.Split('&');
                    foreach (var pair in pairs)
                    {
                        var t = pair.Split('=');
                        kwargs.Add(t[0], t[1]);
                    }
                }
            }
            return kwargs;
        }
    }
}
