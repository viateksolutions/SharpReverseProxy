using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SharpReverseProxy
{
    public class GenericProxyRule : IProxyRule
    {
        public List<string> AuthenticationSchemes { get; set; } = new List<string>();
        public bool PreProcessResponse { get; set; } = true;
        public bool RequiresAuthentication { get; set; } = false;

        public bool Matcher(Uri uri)
        {
            throw new NotImplementedException();
        }

        public void Modifier(HttpRequestMessage request, ClaimsPrincipal claims)
        {
            throw new NotImplementedException();
        }

        public Task ResponseModifier(HttpResponseMessage response, HttpContext context)
        {
            throw new NotImplementedException();
        }
    }
}
