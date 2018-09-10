﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using Newtonsoft.Json;
using PuppeteerSharp.Helpers;

namespace PuppeteerSharp
{
    /// <summary>
    /// Payload information.
    /// </summary>
    public class Payload
    {
        internal Payload()
        {
            Headers = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets or sets the HTTP method.
        /// </summary>
        /// <value>HTTP method.</value>
        [JsonProperty("method"), JsonConverter(typeof(HttpMethodConverter))]
        public HttpMethod Method { get; set; }
        /// <summary>
        /// Gets or sets the post data.
        /// </summary>
        /// <value>The post data.</value>
        [JsonProperty("postData")]
        public object PostData { get; set; }
        /// <summary>
        /// Gets or sets the HTTP headers.
        /// </summary>
        /// <value>HTTP headers.</value>
        [JsonProperty("headers")]
        public Dictionary<string, object> Headers { get; set; }
        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>The URL.</value>
        [JsonProperty("url")]
        public string Url { get; set; }

        internal string Hash
        {
            get
            {
                var normalizedUrl = Url;

                try
                {
                    // Decoding is necessary to normalize URLs.
                    // The method will throw if the URL is malformed. In this case,
                    // consider URL to be normalized as-is.
                    normalizedUrl = HttpUtility.UrlDecode(Url);
                }
                catch
                {
                }

                var hash = new Payload
                {
                    Url = Url,
                    Method = Method,
                    PostData = PostData
                };

                if (!normalizedUrl.StartsWith("data:", StringComparison.Ordinal))
                {
                    foreach (var item in Headers.OrderBy(kv => kv.Key))
                    {
                        if (item.Key.Equals("accept", StringComparison.OrdinalIgnoreCase)
                            || item.Key.Equals("referer", StringComparison.OrdinalIgnoreCase)
                            || item.Key.Equals("x-devtools-emulate-network-conditions-client-id", StringComparison.OrdinalIgnoreCase)
                            || item.Key.Equals("cookie", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        hash.Headers[item.Key] = item.Value;
                    }
                }

                return JsonConvert.SerializeObject(hash);
            }
        }
    }
}