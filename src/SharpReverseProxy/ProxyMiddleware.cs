using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SharpReverseProxy
{
    public class ProxyMiddleware {
        private readonly RequestDelegate _next;
        private readonly HttpClient _httpClient;
        private readonly ProxyOptions _options;

        public ProxyMiddleware(RequestDelegate next, IOptions<ProxyOptions> options, IEnumerable<IProxyRule> rules) {
            _next = next;
            _options = options.Value;
            _options.ProxyRules.AddRange(rules);
            _httpClient = new HttpClient(_options.BackChannelMessageHandler ?? new HttpClientHandler {
                AllowAutoRedirect = _options.FollowRedirects
            });
        }

        public async Task Invoke(HttpContext context) {
            var uri = GeRequestUri(context);
            var resultBuilder = new ProxyResultBuilder(uri);

            var matchedRule = _options.ProxyRules.FirstOrDefault(r => r.Matcher(uri));
            if (matchedRule == null) {
                await _next(context);
                _options.Reporter.Invoke(resultBuilder.NotProxied(context.Response.StatusCode));
                return;
            }

            if (matchedRule.RequiresAuthentication && !UserIsAuthenticated(context)) {
                AuthenticateResult authResult = AuthenticateResult.Fail("Authentication failure");
                foreach (var authScheme in matchedRule.AuthenticationSchemes)
                {
                    authResult = await context.AuthenticateAsync(authScheme);
                    if(authResult.Succeeded)
                    {
                        context.User = authResult.Principal;
                        break;
                    }
                }

                if (!authResult.Succeeded)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    _options.Reporter.Invoke(resultBuilder.NotAuthenticated());
                    return;
                }
            }

            var proxyRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), uri);
            SetProxyRequestBody(proxyRequest, context);
            SetProxyRequestHeaders(proxyRequest, context);

            matchedRule.Modifier(proxyRequest, context.User);

            proxyRequest.Headers.Host = !proxyRequest.RequestUri.IsDefaultPort 
                ? $"{proxyRequest.RequestUri.Host}:{proxyRequest.RequestUri.Port}"
                : proxyRequest.RequestUri.Host;

            try {
                await ProxyTheRequest(context, proxyRequest, matchedRule);
            }
            catch (HttpRequestException) {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            _options.Reporter.Invoke(resultBuilder.Proxied(proxyRequest.RequestUri, context.Response.StatusCode));
        }

        private async Task ProxyTheRequest(HttpContext context, HttpRequestMessage proxyRequest, IProxyRule proxyRule) {
            using (var responseMessage = await _httpClient.SendAsync(proxyRequest,
                                                                     HttpCompletionOption.ResponseHeadersRead,
                                                                     context.RequestAborted)) {

                if(proxyRule.PreProcessResponse) { 
                    context.Response.StatusCode = (int)responseMessage.StatusCode;
                    context.Response.ContentType = responseMessage.Content?.Headers.ContentType?.MediaType;
                    foreach (var header in responseMessage.Headers) {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                    // SendAsync removes chunking from the response. 
                    // This removes the header so it doesn't expect a chunked response.
                    context.Response.Headers.Remove("transfer-encoding");

                    if (responseMessage.Content != null) {
                        foreach (var contentHeader in responseMessage.Content.Headers) {
                            context.Response.Headers[contentHeader.Key] = contentHeader.Value.ToArray();
                        }
                        await responseMessage.Content.CopyToAsync(context.Response.Body);
                    }
                }
                
                await proxyRule.ResponseModifier(responseMessage, context);
            }
        }

        private static Uri GeRequestUri(HttpContext context) {
            var request = context.Request;
            var uriString = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
            return new Uri(uriString);
        }

        private static void SetProxyRequestBody(HttpRequestMessage requestMessage, HttpContext context) {
            var requestMethod = context.Request.Method;
            if (HttpMethods.IsGet(requestMethod) ||
                HttpMethods.IsHead(requestMethod) ||
                HttpMethods.IsDelete(requestMethod) ||
                HttpMethods.IsTrace(requestMethod)) {
                return;
            }
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        private void SetProxyRequestHeaders(HttpRequestMessage requestMessage, HttpContext context) {
            foreach (var header in context.Request.Headers) {
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray())) {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            if (_options.AddForwardedHeader) {
                requestMessage.Headers.TryAddWithoutValidation("Forwarded", $"for={context.Connection.RemoteIpAddress}");
                requestMessage.Headers.TryAddWithoutValidation("Forwarded", $"host={requestMessage.Headers.Host}");
                requestMessage.Headers.TryAddWithoutValidation("Forwarded", string.Format("proto={0}", context.Request.IsHttps ? "https" : "http"));
            }
        }

        private bool UserIsAuthenticated(HttpContext context) {
            return context.User.Identities.FirstOrDefault()?.IsAuthenticated ?? false;
        }
    }

}
