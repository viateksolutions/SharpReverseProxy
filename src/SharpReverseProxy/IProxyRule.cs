using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SharpReverseProxy
{
    public interface IProxyRule
    {
        List<string> AuthenticationSchemes { get; }
        bool PreProcessResponse { get; }
        bool RequiresAuthentication { get; }

        bool Matcher(Uri uri);
        void Modifier(HttpRequestMessage request, ClaimsPrincipal claims);
        Task ResponseModifier(HttpResponseMessage response, HttpContext context);
    }
}