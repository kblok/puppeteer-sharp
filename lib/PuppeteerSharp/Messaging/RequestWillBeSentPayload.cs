using System;
using Newtonsoft.Json;

namespace PuppeteerSharp.Messaging
{
    internal class RequestWillBeSentPayload
    {
        public string RequestId { get; set; }
        public string LoaderId { get; set; }
        public Payload Request { get; set; }

        public ResponsePayload Response { get; set; }
        public ResourceType Type { get; set; }
        public string FrameId { get; set; }

        internal bool IsInterceptable => !Request.Url.StartsWith("data:", StringComparison.InvariantCultureIgnoreCase);
    }
}
