using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;

namespace otavaSocket
{
    /// The main webserver object
    /**
     *  Create an instance of this class
     *  and call the Start() method to
     *  start the server
     */
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly ushort _port;
        private readonly Router _router;
        private readonly SessionManager _sm;
        private readonly int maxSimultaneousConnections = 10;
        private bool _running = true;
        private bool useWebSockets = false;
        private Hub hub;

        /// Initialize a webserver with the given parameters
        /**
         * @param port The port number to start the server on
         * @param webRootFolder Path to the folder containing hosted data
         */
        public WebServer(string webRootFolder, ushort port = 5555)
        {
            _port = port;
            _router = new Router(webRootFolder);
            _sm = new SessionManager();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{_port}/");
        }

        /// Register multiple routes
        public void AddRoute(IEnumerable<Route> routes)
        {
            foreach (var route in routes)
            {
                _router.AddRoute(route);
            }
        }

        /// Register a route
        /**
         * Configure a Route object and then
         * call this method on it to activate it on the server
         *
         * @param route The route object to register
         */
        public void AddRoute(Route route)
        {
            _router.AddRoute(route);
        }

        /// Activate WebSocket use on the server
        /*
         * @tparam T Hub implementation to use for WebSocket communication
         */
        public void UseWebSockets<T>() where T : Hub, new()
        {
            hub = new T();
            useWebSockets = true;
        }

        /// Start the server
        public void Start()
        {
            _listener.Start();
            Console.WriteLine("Started http listener on port {0}", _port);

            Task.Factory.StartNew(RunServerAsync);
        }

        /// Stop the server
        /**
         * Blocks until all the tasks have finished, which may be never
         * because I sadly can't cancel them properly.
         */
        public void Stop()
        {
            _running = false;
            _listener.Close();
            hub.Stop().Wait();
        }

        /// Start and infinite loop to handle connections
        /**
         * Start maxSimultaneousConnections connections in parallel
         * using a semaphore to regulate their number
         */
        private void RunServerAsync()
        {

            SemaphoreSlim semaphore = new SemaphoreSlim(maxSimultaneousConnections, maxSimultaneousConnections);

            while(_running)
            {
                semaphore.Wait();
                HandleRequestsAsync(semaphore);
            }
            //_listener.Close();
            //await Task.WhenAll(tasks);;
            //await hub.Stop();
        }

        /// Accept a HTTP request and respond to it accordingly
        /**
         * If the HTTP request was a WebSocket upgrade, completes it
         * and sends the WebSocket to the hub.
         *
         * Otherwise asks the Router for a response and sends it back to the
         * client.
         *
         * In the end, always releases 1 spot on the semaphore
         * @param semaphore The semaphore to release on
         */
        private async void HandleRequestsAsync(SemaphoreSlim semaphore)
        {
            HttpListenerContext ctx = await _listener.GetContextAsync();

            HttpListenerRequest  request  = ctx.Request;
            HttpListenerResponse response = ctx.Response;

            Session session = _sm.GetSession(request.RemoteEndPoint);
            session.UpdateLastConnectionTime();
            Log(request);

            if (useWebSockets && request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext ws_ctx = await ctx.AcceptWebSocketAsync(subProtocol: null);
                lock (session)
                {
                    hub.AddClient(ws_ctx.WebSocket, session);
                }
            }
            else
            {
                string route = request.RawUrl.Substring(1).Split("?")[0];
                var kwargs = GetParams(request);

                ResponseData resp;
                lock (session)
                {
                    resp = _router.Route(session, request.HttpMethod, route, kwargs);
                }

                if (string.IsNullOrEmpty(resp.Redirect))
                {
                    response.ContentType = resp.ContentType;
                    response.ContentEncoding = resp.Encoding;
                    response.ContentLength64 = resp.Data.LongLength;
                    response.StatusCode = (int)resp.Status;
                    using (var output = response.OutputStream)
                    {
                        output.Write(resp.Data, 0, resp.Data.Length);
                    }
                }
                else
                {
                    response.StatusCode = (int)ServerStatus.Redirect;
                    response.Redirect("http://" + request.UserHostName + resp.Redirect);
                }
                response.Close();
            }

            //_sm.RemoveInvalidSessions();
            // allow another connection through
            semaphore.Release();
        }

        /// Helper function for printing out the request
        private void Log(HttpListenerRequest req)
        {
            Console.WriteLine(req.HttpMethod + " " + req.RawUrl);
            //Console.WriteLine($"is ws: {req.IsWebSocketRequest}");
            //Console.WriteLine($"headers: {req.Headers}");
        }

        private void Log(string text)
        {
            Console.WriteLine(text);
        }

        /// Helper function for parsing data from the browser
        /**
         * Parses key-value pairs from a GET request's query or a
         * POST request form into a C# Dictionary<string, string>.
         */
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
