using System;
using System.Collections.Generic;
using System.Net;

namespace otavaSocket
{
    public class Session
    {
        public bool Authorized { get; set; }
        public DateTime LastConnection { get; set; }
        public Dictionary<string, string> SessionData { get; set; }
        public int MessageOffset { get; set; } //toto je shit
        public bool Valid { get; set; }//toto neviem ale asi tiez

        public Session()
        {
            SessionData = new Dictionary<string, string>();
            Authorized = false;
            MessageOffset = 0;
            Valid = true;
            UpdateLastConnectionTime();
        }
 
        public void UpdateLastConnectionTime()
        {
            LastConnection = DateTime.Now;
        }

        public bool isExpired(int expirationTime)
        {
            return (DateTime.Now - LastConnection).TotalSeconds > expirationTime;
        }

    }

    public class SessionManager
    {
        // Datova struktura na pracu so Sessionami
        public Dictionary<IPAddress, Session> ActiveSessions { get; set; }

        public SessionManager()
        {
            ActiveSessions = new Dictionary<IPAddress, Session>();
        }

        public Session GetSession(IPEndPoint endPoint)
        {
            Session session;
            if (!ActiveSessions.TryGetValue(endPoint.Address, out session))
            {
                session = new Session();
                ActiveSessions.Add(endPoint.Address, session);
            }
            return session;
        }

        public void RemoveInvalidSessions()
        {
            foreach (var (ID, session) in ActiveSessions)
            {
                if (!session.Valid) 
                    ActiveSessions.Remove(ID);
            }
        }
    }
}
