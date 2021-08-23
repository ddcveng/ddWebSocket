using System;
using System.Collections.Generic;

namespace otavaSocket
{
    public abstract class BaseController
    {
        protected Func<Session, Dictionary<string, string>, ResponseData> Action;
        public BaseController(Func<Session, Dictionary<string, string>,  ResponseData> handler)
        {
            Action = handler;
        }
        public abstract ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs);
    }

    public class AnonymousController : BaseController
    {
        public AnonymousController(Func<Session, Dictionary<string, string>, ResponseData> handler) : base(handler)
        {}

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            return Action(session, keyValuePairs);
        }
    }

    public class AuthorizedController : BaseController
    {
        public AuthorizedController(Func<Session, Dictionary<string, string>, ResponseData> handler) : base(handler)
        { }

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            if (session.Authorized)
            {
                return Action(session, keyValuePairs);
            }
            else
            {
                return new ResponseData { Status = ServerStatus.NotAuthorized, Complete=true};
            }
        }
    }

    public class AuthorizedExpirableController : BaseController
    {
        public AuthorizedExpirableController(Func<Session, Dictionary<string, string>, ResponseData> handler) : base(handler)
        { }

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            if (!session.Authorized)
            {
                return new ResponseData { Status = ServerStatus.NotAuthorized, Complete = true };
            }
            else if (session.isExpired(WebServer.SessionLifetime))
            {
                session.Authorized = false;
                session.SessionData.Clear();
                return new ResponseData { Status = ServerStatus.ExpiredSession, Complete=true };
            }
            return Action(session, keyValuePairs);
        }
    }

}
