using System;
using System.Collections.Generic;
using System.Net;

namespace otavaSocket
{
    /// Class for managing general data of a client
    public class Session
    {
        /// true if the user authenticated (application dependent)
        public bool Authorized { get; set; }
        /// time of the last connection
        public DateTime LastConnection { get; set; }
        /// data about the user used by the app
        public Dictionary<string, string> SessionData { get; set; }

        /// time in seconds after which an inactive session is discarded
        public static int SessionLifetime = 300;

        public Session()
        {
            SessionData = new Dictionary<string, string>();
            Authorized = false;
            UpdateLastConnectionTime();
        }

        /// Update last connection time to the current time
        public void UpdateLastConnectionTime()
        {
            LastConnection = DateTime.Now;
        }

        /// Checks whether the session is old
        /**
         * @return True if the session was inactive for at least
         * SessionLifetime seconds, false otherwise
         */
        public bool isExpired()
        {
            return (DateTime.Now - LastConnection).TotalSeconds > SessionLifetime;
        }

    }

    /// Class for managing sessions while the server is running
    public class SessionManager
    {
        /// A Dictionary of currently active sessions
        public Dictionary<IPAddress, Session> ActiveSessions { get; set; }

        public SessionManager()
        {
            ActiveSessions = new Dictionary<IPAddress, Session>();
        }

        /// Gets the Session object for the given endpoint
        /**
         * Tries to get an existing session from ActiveSessions,
         * if it doesnt exist, creates it.
         *
         * @param endPoint identifier of a client for whom we need the session
         * @return The corresponding session
         */
        public Session GetSession(IPEndPoint endPoint)
        {
            Session session;
            if (!ActiveSessions.TryGetValue(endPoint.Address, out session))
            {
                Console.WriteLine($"ADDING SESSION: {endPoint.Address.ToString()}");
                session = new Session();
                ActiveSessions.Add(endPoint.Address, session);
            }
            return session;
        }
    }
}
