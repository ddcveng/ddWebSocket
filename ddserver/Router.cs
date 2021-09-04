using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace otavaSocket
{
    /// Represents a route on the webserver
    /**
     * By default the webserver only serves static files
     * present on the server. By using routes you can add
     * custom uri targets(.../api/adduser for example) that
     * execute custom code when requested and may return arbitrary
     * responses to the browser
     */
    public class Route
    {
        /// HTTP Method this Route will respond to (e.g. GET)
        public string Verb { get; set; }
        /// Relative path that this Route will respond to (e.g. /api/dostuff)
        public string Path { get; set; }
        /// Controller to use when processing the request
        public BaseController Controller { get; set; }
        /// A bool value indicating whether this Route needs Static file resources
        /** Default false */
        public bool NeedsResources { get; set; }
    }

    /// Enum containing some of the standard HTTP response status codes
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

    /// Represents a response from the server
    public class ResponseData
    {
        /// Raw response body
        public byte[] Data { get; set; }
        /// Header value specifying the type of content in Data
        public string ContentType { get; set; }
        /// Header value specifying the encoding of the data in Data
        public Encoding Encoding { get; set; }
        /// HTTP Status Code
        public ServerStatus Status { get; set; }
        /// String indicating where to redirect
        public string Redirect { get; set; }
        /// String name of the static file requested
        public string RequestedResource { get; set; }
    }

    /// Contains information about a filetype
    /**
     * Specifies how to load the file and what type of content it is
     */
    public class ExtensionInfo
    {
        /// String specifying what ContentType header value to use when
        /// serving files of this type
        public string ContentType { get; set; }
        /// A function delegate that can load the file into memory
        public Func<string, string, ExtensionInfo, ResponseData> Loader { get; set; }
    }

    /// Class for handling HTTP responses
    /**
     * Contains information about the content available on the server
     * and methods that on request serve the corresponding data
     * to the webserver, which then sends it to the browser
     */
    public class Router
    {
        /// Path to the folder all the website files are stored
        private readonly string _webRootPath;
        /// Dictionary with information about supported extensions
        private readonly Dictionary<string, ExtensionInfo> supportedExtensions;
        /// A collection of registered routes
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

        /// Add a route to registered routes
        public void AddRoute(Route r)
        {
            routes.Add(r);
        }

        /// Method used to load image files
        /**
         * Could also be used to load general binary files
         * but currently the only binary files supported are image files
         *
         * @param filename The file to load
         * @param ext The file extension
         * @param extInfo ExtensionInfo object corresponding to the extension
         */
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

        /// Load a page
        /**
         * prefixes the filename with "pages" since the pages are in that
         * subdirectory.
         * If the filename is none or more precisely its just the base directory
         * it gets replaced by Index.html
         *
         * @param filename The file to load
         * @param ext The file extension
         * @param extInfo ExtensionInfo object corresponding to the extension
         */
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

        /// Loads a text file
        /**
         * @param filename The file to load
         * @param ext The file extension
         * @param extInfo ExtensionInfo object corresponding to the extension
         */
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

        /// Load a static file
        /**
         * Loads the file specified using a Loader based on
         * the file extension.
         *
         * @param pathToFile Path to the file to be loaded
         */
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

        /// Find the resources corrersponding to the request and return them
        /**
         * First checks if there is a Route that can handle the request and
         * executes it if so. If no route was found the default behavior is
         * to try and serve the static file at webRootDir+dest.
         *
         * @param session The session corresponding to this connection
         * @param verb The HTTP method of the request
         * @param dest The path component of the requested url
         * @param kwargs Dictionary containing the key value pairs provided by the request
         */
        public ResponseData Route(Session session, string verb, string dest, Dictionary<string, string> kwargs)
        {
            ResponseData response;

            // handle registrered routes
            int routeInx = routes.FindIndex(r => dest == r.Path && verb == r.Verb);
            if (routeInx != -1)
            {
                Route route = routes[routeInx];
                response = route.Controller.Handle(session, kwargs);

                if (route.NeedsResources &&
                    response.Status == ServerStatus.OK)
                {
                    response = GetStaticFile(response.RequestedResource);
                }
            }
            else
            {
                response = GetStaticFile(dest);
            }

            ServerStatus responseStatus = response.Status;
            response = ErrorHandler(response);
            response.Status = responseStatus;
            return response;
        }

        /// Serve an error page if needed
        /**
         * Serves the correct error page based on the Status Code of
         * the response given.
         *
         * @param responseData The response object to check for errors
         * @return ResponseData with the correct error data or the original
         * data if there were no errors
         */
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
