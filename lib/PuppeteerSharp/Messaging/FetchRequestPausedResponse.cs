using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace PuppeteerSharp.Messaging
{
    internal class FetchRequestPausedResponse
    {
        public string RequestId { get; set; }
        public string NetworkId { get; set; }
        public ResourceType ResourceType { get; set; }
        public HttpStatusCode ResponseStatusCode { get; set; }
        public Header[] ResponseHeaders { get; set; }

        public Dictionary<string, string> ResponseHeadersDictionary => ResponseHeaders
            .Select(h => h.Name)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(
                k => k,
                v => string.Join(",", ResponseHeaders
                    .Where(h => string.Equals(h.Name, v, StringComparison.CurrentCultureIgnoreCase))
                    .Select(h => h.Value)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)));
    }
}