using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace otavaSocket
{
    public class Route
    {
        public string Verb { get; set; }
        public string Path { get; set; }
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
        public bool Complete { get; set; }
        public byte[] Data { get; set; }
        public string ContentType { get; set; }
        public Encoding Encoding { get; set; }
        public ServerStatus Status { get; set; }
        public string Redirect { get; set; }
    }

    public class ExtensionInfo
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
                FileStream fStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
                BinaryReader br = new BinaryReader(fStream);
                ResponseData ret = new ResponseData() { 
                    Data = br.ReadBytes((int)fStream.Length), 
                    ContentType = extInfo.ContentType,
                    Status = ServerStatus.OK
                };
                br.Close();
                fStream.Close();

                return ret;
            }
            return new ResponseData() { Status = ServerStatus.NotFound };
        }

        private ResponseData PageLoader(string filename, string ext, ExtensionInfo extInfo)
        {
            if (filename == _webRootPath)
            {
                filename = Path.Join(_webRootPath, "Index.html");
                ext = "html";
                //extInfo = supportedExtensions.GetValueOrDefault(ext);
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
            ResponseData ret;
            if (File.Exists(filename))
            {
                ret = new ResponseData()
                {
                    Data = File.ReadAllBytes(filename),
                    ContentType = extInfo.ContentType,
                    Encoding = Encoding.UTF8,
                    Status = ServerStatus.OK
                };
            }
            else
            {
                ret = new ResponseData() { Status = ServerStatus.NotFound };
            }
            return ret;
        }

        public ResponseData Route(Session session, string verb, string dest, Dictionary<string, string> kwargs)
        {
            ResponseData ret;
            int t = dest.LastIndexOf('.');
            string ext = "";
            if (t != -1)
                ext = dest.Substring(t + 1);
            ExtensionInfo extInfo;
            //TODO: add protected folders
            Route route = routes.FirstOrDefault(r => dest == r.Path && verb == r.Verb);
            if(route != null)
            {
                ret = route.Controller.Handle(session, kwargs);
                if (ret.Complete)
                {
                    //ajax...
                    return ret;
                }
            }

            //no handler found or we still need the file data, get the requested file
            if (supportedExtensions.TryGetValue(ext, out extInfo))
            {
                string fullpath = Path.Join(_webRootPath, dest);
                ret = extInfo.Loader(fullpath, ext, extInfo);
            }
            else
            {
                ret = new ResponseData() { Status = ServerStatus.UnknownType };
            }

            return ret;
        }

        public string ErrorHandler(ServerStatus status)
        {
            string ret = @"errors";
            switch (status)
            {
                case ServerStatus.NotFound:
                    ret = Path.Join(ret, "NotFound.html");
                    break;
                case ServerStatus.UnknownType:
                    ret = Path.Join(ret, "UnknownType.html");
                    break;
                case ServerStatus.ServerError:
                    ret = Path.Join(ret, "InternalError.html");
                    break;
                case ServerStatus.NotAuthorized:
                    ret = Path.Join(ret, "Unauthorized.html");
                    break;
                case ServerStatus.ExpiredSession:
                    ret = Path.Join(ret, "LoginTimeout.html");
                    break;
                default:
                    ret = Path.Join(ret, "NotFound.html");
                    break;
            }
            return ret;
        }
    }
}
