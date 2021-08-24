﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace otavaSocket
{
    public class WebServer
    {
        // Spracuváva prichádzajúce HTTP requesty
        // Hlavný objekt programu, volá ostatné moduly
        private readonly HttpListener _listener;
        private readonly ushort _port;
        private readonly Router _router;
        private readonly bool _running = true;
        private readonly SessionManager _sm;
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
            Console.WriteLine("Started http listener on port {0}", _port);
            _listener.Start();
            while(_running)
            {
               //TODO: figure out how this works
               Task listenTask = HandleRequestsAsync();
               listenTask.GetAwaiter().GetResult();
            }
            _listener.Stop();
        }

        private async Task HandleRequestsAsync()
        {
            HttpListenerContext ctx = await _listener.GetContextAsync();
            HttpListenerRequest request = ctx.Request;
            HttpListenerResponse response = ctx.Response;
            Log(request);
            Session session = _sm.GetSession(request.RemoteEndPoint);
            string route = request.RawUrl.Substring(1).Split("?")[0];
            var kwargs = GetParams(request);

            ResponseData resp = _router.Route(session, request.HttpMethod, route, kwargs);

            session.UpdateLastConnectionTime();

            //TODO: How does this redirect work?
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
