using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace otavaSocket
{
    public class Route
    {
        public string Verb { get; set; }
        public string Path { get; set; }
        public bool RequestResources { get; set; }
        public BaseController Controller { get; set; }
    }

    public enum ServerStatus
    {
        OK=200,
        ExpiredSession=440,
        NotAuthorized=401,
        NotFound=404,
        ServerError=500,
        UnknownType=400,
        Redirect=300
    }

    public class ResponseData
    {
        //public bool Complete { get; set; }
        public byte[] Data { get; set; }
        public string ContentType { get; set; }
        public Encoding Encoding { get; set; }
        public ServerStatus Status { get; set; }
        public string Redirect { get; set; }
    }

    public struct ExtensionInfo
    {
        public string ContentType { get; set; }
        public Func<string, string, ExtensionInfo, ResponseData> Loader { get; set; }
    }

    public class Router
    {
        // Srdce programu, spracuje url z poziadavky a najde chcene data
        private string _webRootPath;
        private Dictionary<string, ExtensionInfo> supportedExtensions;
        private List<Route> routes;

        public Router(string webRootPath)
        {
            _webRootPath = webRootPath;
            routes = new List<Route>();
            supportedExtensions = new Dictionary<string, ExtensionInfo>()
            {
                {"ico", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/ico"}},
                {"png", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/png"}},
                {"jpg", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/jpg"}},
                {"gif", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/gif"}},
                {"bmp", new ExtensionInfo() {Loader=ImageLoader, ContentType="image/bmp"}},
                {"html", new ExtensionInfo() {Loader=PageLoader, ContentType="text/html"}},
                {"css", new ExtensionInfo() {Loader=FileLoader, ContentType="text/css"}},
                {"js", new ExtensionInfo() {Loader=FileLoader, ContentType="text/javascript"}},
                {"", new ExtensionInfo() {Loader=PageLoader, ContentType="text/html"}},
            };
        }

        public void AddRoute(Route r)
        {
            routes.Add(r);
        }

        private ResponseData ImageLoader(string filename, string ext, ExtensionInfo extInfo)
        {
            if (File.Exists(filename))
            {
                using var fStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
                using var bReader = new BinaryReader(fStream);

                BinaryReader br = new BinaryReader(fStream);
                ResponseData ret = new ResponseData() {
                    Data = br.ReadBytes((int)fStream.Length),
                    ContentType = extInfo.ContentType,
                    Status = ServerStatus.OK
                };

                return ret;
            }
            return new ResponseData() { Status = ServerStatus.NotFound };
        }

        private ResponseData PageLoader(string filename, string ext, ExtensionInfo extInfo)
        {
            // toto by mohlo byt dakde v Route riesene
            if (filename == _webRootPath)
            {
                filename = Path.Join(_webRootPath, "Index.html");
                ext = "html";
            }
            else if (string.IsNullOrEmpty(ext))
            {
                filename += ".html";
            }
            string partialFilename = Path.GetRelativePath(_webRootPath, filename);
            filename = Path.Join(_webRootPath, "pages", partialFilename);
            return FileLoader(filename, ext, extInfo);
        }

        private ResponseData FileLoader(string filename, string ext, ExtensionInfo extInfo)
        {
            ResponseData fileData;
            if (File.Exists(filename))
            {
                fileData = new ResponseData()
                {
                    Data = File.ReadAllBytes(filename),
                    ContentType = extInfo.ContentType,
                    Encoding = Encoding.UTF8,
                    Status = ServerStatus.OK
                };
            }
            else
            {
                fileData = new ResponseData() { Status = ServerStatus.NotFound };
            }
            return fileData;
        }

        public ResponseData GetStaticFile(string pathToFile)
        {
            ResponseData ret;
            ExtensionInfo extInfo;

            int t = pathToFile.LastIndexOf('.');
            string ext = "";
            if (t != -1)
                ext = pathToFile.Substring(t + 1);

            if (supportedExtensions.TryGetValue(ext, out extInfo))
            {
                string fullpath = Path.Join(_webRootPath, pathToFile);
                ret = extInfo.Loader(fullpath, ext, extInfo);
            }
            else
            {
                ret = new ResponseData() { Status = ServerStatus.UnknownType };
            }

            return ret;
        }

        //TODO / -> index.html bude route, teda route musi mat string ako RequestResource
        public ResponseData Route(Session session, string verb, string dest, Dictionary<string, string> kwargs)
        {
            ResponseData ret;

            // handle registrered routes
            int routeInx = routes.FindIndex(r => dest == r.Path && verb == r.Verb);
            if (routeInx != -1)
            {
                Route route = routes[routeInx];
                ret = route.Controller.Handle(session, kwargs);

                if (ret.Status == ServerStatus.OK && route.RequestResources) {
                    ret = GetStaticFile(dest);
                }
            }
            else
            {
                ret = GetStaticFile(dest);
            }

            return ErrorHandler(ret);
        }

        // Will the error pages be always present?
        // Can reading from disk fail, and if so what do I do about it?
        public ResponseData ErrorHandler(ResponseData responseData)
        {
            if (responseData.Status == ServerStatus.OK) {
                return responseData;
            }

            string path = @"errors";
            switch (responseData.Status)
            {
                case ServerStatus.NotFound:
                    path = Path.Join(path, "NotFound.html");
                    break;
                case ServerStatus.UnknownType:
                    path = Path.Join(path, "UnknownType.html");
                    break;
                case ServerStatus.ServerError:
                    path = Path.Join(path, "InternalError.html");
                    break;
                case ServerStatus.NotAuthorized:
                    path = Path.Join(path, "Unauthorized.html");
                    break;
                case ServerStatus.ExpiredSession:
                    path = Path.Join(path, "LoginTimeout.html");
                    break;
                default:
                    path = Path.Join(path, "NotFound.html");
                    break;
            }

            return GetStaticFile(path);
        }
    }
}
