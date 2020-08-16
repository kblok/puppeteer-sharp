using Newtonsoft.Json;
using PuppeteerSharp.Helpers.Json;

namespace PuppeteerSharp.Messaging
{
    /// <summary>
    /// Stages of the request to handle.
    /// </summary>
    [JsonConverter(typeof(FlexibleStringEnumConverter), Request)]
    public enum RequestFetchStage
    {
        /// <summary>
        /// Request will intercept before the request is sent.
        /// </summary>
        Request,

        /// <summary>
        /// Response will intercept after the response is received (but before response body is received).
        /// </summary>
        Response
    }
}