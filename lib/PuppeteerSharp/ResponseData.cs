using System.Collections.Generic;
using System.Net;
using System.Text;

namespace PuppeteerSharp
{
    /// <summary>
    /// Response that will fulfill a request.
    /// </summary>
    public struct ResponseData
    {
        /// <summary>
        /// Response body (text content)
        /// </summary>
        /// <value>Body as text.</value>
        public string Body
        {
            get => Encoding.UTF8.GetString(BodyData);
            set => BodyData = Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Response body (binary content)
        /// </summary>
        /// <value>The body as binary.</value>
        public byte[] BodyData { get; set; }
        /// <summary>
        /// Response headers. Header values will be converted to a string.
        /// </summary>
        /// <value>Headers.</value>
        public Dictionary<string, object> Headers { get; set; }
        /// <summary>
        /// If set, equals to setting <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Type"/> response header
        /// </summary>
        /// <value>The Content-Type.</value>
        public string ContentType { get; set; }
        /// <summary>
        /// Response status code.
        /// </summary>
        /// <value>Status Code.</value>
        public HttpStatusCode? Status { get; set; }
        /// <summary>
        /// Multiple Response headers with same Header name. Header values will be converted to a string.
        /// </summary>
        /// <value>Headers.</value>
        public List<KeyValuePair<string, object>> MultipleHeaders { get; set; }
    }
}
