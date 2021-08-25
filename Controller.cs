using System;
using System.Collections.Generic;

namespace otavaSocket
{
    using Handler = Func<Session, Dictionary<string, string>, ResponseData>;

    public abstract class BaseController
    {
        protected Handler Action;
        public BaseController(Func<Session, Dictionary<string, string>,  ResponseData> handler)
        {
            Action = handler;
        }
        public abstract ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs);
    }

    public class AnonymousController : BaseController
    {
        public AnonymousController(Handler handler) : base(handler)
        {}

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            return Action(session, keyValuePairs);
        }
    }

    public class AuthorizedController : BaseController
    {
        public AuthorizedController(Handler handler) : base(handler)
        {}

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            if (session.Authorized)
            {
                return Action(session, keyValuePairs);
            }
            return new ResponseData { Status = ServerStatus.NotAuthorized };
        }
    }

    public class AuthorizedExpirableController : BaseController
    {
        public AuthorizedExpirableController(Handler handler) : base(handler)
        {}

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            if (!session.Authorized)
            {
                return new ResponseData { Status = ServerStatus.NotAuthorized };
            }
            else if (session.isExpired())
            {
                session.Authorized = false;
                session.SessionData.Clear();
                return new ResponseData { Status = ServerStatus.ExpiredSession };
            }
            return Action(session, keyValuePairs);
        }
    }

}
