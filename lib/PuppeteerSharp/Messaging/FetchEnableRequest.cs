namespace PuppeteerSharp.Messaging
{
    /// <summary>
    /// The configuration details for enabling request interceptions via Fetch.enable method.
    /// </summary>
    public class FetchEnableRequest
    {
        /// <summary>
        /// Gets or sets whether the authRequired events will be issued and requests will be paused expecting a call to continueWithAuth.
        /// </summary>
        public bool HandleAuthRequests { get; set; }

        /// <summary>
        /// Gets or sets the patterns/filters for intercepting requests.
        /// The fetchRequested event will only be raised when the specified patterns are satisfied
        /// and will be paused until clients response.
        /// If not set, all requests will be affected.
        /// </summary>
        public Pattern[] Patterns { get; set; }

        /// <summary>
        /// The details of a pattern/filter for intercepting requests.
        /// </summary>
        public class Pattern
        {
            /// <summary>
            /// The pattern of the URLs that will be intercepted.
            /// Wildcards ('*' -> zero or more, '?' -> exactly one) are allowed. Escape character is backslash. Omitting is equivalent to "*".
            /// </summary>
            public string UrlPattern { get; set; }
            
            /// <summary>
            /// Criteria for intercepting only requests matching the specified resource type.
            /// </summary>
            public ResourceType? ResourceType { get; set; }

            /// <summary>
            /// Stage at which to begin intercepting requests.
            /// If not specified, the default is <see cref="RequestFetchStage.Request"/>.
            /// </summary>
            public RequestFetchStage RequestStage { get; set; }
        }
    }
}
