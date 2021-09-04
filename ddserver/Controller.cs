using System;
using System.Collections.Generic;

namespace otavaSocket
{
    using Handler = Func<Session, Dictionary<string, string>, ResponseData>;

    /// Base class for all controllers
    /**
     * A controller contains a Handler delegate, which is user defined
     * and called in the Handle method.
     *
     * The Handle method is overridden by concrete Controllers to provide
     * access control and choose when the Handler delegate should be called.
     */
    public abstract class BaseController
    {
        protected Handler Action;
        public BaseController(Func<Session, Dictionary<string, string>,  ResponseData> handler)
        {
            Action = handler;
        }
        public abstract ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs);
    }

    /// Basic concrete controller that allows everyone to see the resource
    public class AnonymousController : BaseController
    {
        public AnonymousController(Handler handler) : base(handler)
        {}

        public override ResponseData Handle(Session session, Dictionary<string, string> keyValuePairs)
        {
            return Action(session, keyValuePairs);
        }
    }

    /// Concrete controller requiring Authorization
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

    /// Concrete controller requiring the session is not expired in addition
    /// to authorization
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
