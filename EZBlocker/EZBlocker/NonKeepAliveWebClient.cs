using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EZBlocker
{
    class NonKeepAliveWebClient : WebClient
    {
        // aso http://stackoverflow.com/a/2361811 & http://stackoverflow.com/a/4699289
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest)
            {
                (request as HttpWebRequest).KeepAlive = false;
            }
            return request;
        }
    }
}
