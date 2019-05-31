using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace SharpReverseProxy {
    public class ProxyOptions {
        public List<IProxyRule> ProxyRules { get; set; } = new List<IProxyRule>();
        public HttpMessageHandler BackChannelMessageHandler { get; set; }
        public Action<ProxyResult> Reporter { get; set; } = result => { };

        public bool FollowRedirects { get; set; } = true;
        public bool AddForwardedHeader { get; set; } = false;

        public ProxyOptions() {}

        public ProxyOptions(List<IProxyRule> rules, Action<ProxyResult> reporter = null) {
            ProxyRules = rules;
            if (reporter != null) {
                Reporter = reporter;
            }
        }

        public void AddProxyRule(IProxyRule rule) {
            ProxyRules.Add(rule);
        }
    }
}