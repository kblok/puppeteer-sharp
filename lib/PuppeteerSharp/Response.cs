using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PuppeteerSharp.Messaging;
using PuppeteerSharp.Helpers.Json;

namespace PuppeteerSharp
{
    /// <summary>
    /// <see cref="Response"/> class represents responses which are received by page.
    /// </summary>
    /// <seealso cref="Page.GoAsync(int, NavigationOptions)"/>
    /// <seealso cref="Page.GoForwardAsync(NavigationOptions)"/>
    /// <seealso cref="Page.ReloadAsync(int?, WaitUntilNavigation[])"/>
    /// <seealso cref="Page.Response"/>
    /// <seealso cref="Page.WaitForResponseAsync(Func{Response, bool}, WaitForOptions)"/>
    public class Response
    {
        private readonly CDPSession _client;
        private readonly bool _fromDiskCache;
        private byte[] _buffer;

        internal Response(
            CDPSession client,
            Request request,
            ResponsePayload responseMessage)
        {
            _client = client;
            Request = request;
            Status = responseMessage.Status;
            StatusText = responseMessage.StatusText;
            Url = request.Url;
            _fromDiskCache = responseMessage.FromDiskCache;
            FromServiceWorker = responseMessage.FromServiceWorker;

            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            if (responseMessage.Headers != null)
            {
                foreach (var keyValue in responseMessage.Headers)
                {
                    Headers[keyValue.Key] = keyValue.Value;
                }
            }
            SecurityDetails = responseMessage.SecurityDetails;
            RemoteAddress = new RemoteAddress
            {
                IP = responseMessage.RemoteIPAddress,
                Port = responseMessage.RemotePort
            };

            BodyLoadedTaskWrapper = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        #region Properties

        /// <summary>
        /// Contains the URL of the response.
        /// </summary>
        public string Url { get; }
        /// <summary>
        /// An object with HTTP headers associated with the response. All header names are lower-case.
        /// </summary>
        /// <value>The headers.</value>
        public Dictionary<string, string> Headers { get; }
        /// <summary>
        /// Contains the status code of the response
        /// </summary>
        /// <value>The status.</value>
        public HttpStatusCode Status { get; }
        /// <summary>
        /// Contains a boolean stating whether the response was successful (status in the range 200-299) or not.
        /// </summary>
        /// <value><c>true</c> if ok; otherwise, <c>false</c>.</value>
        public bool Ok => Status == 0 || ((int)Status >= 200 && (int)Status <= 299);
        /// <summary>
        /// A matching <see cref="Request"/> object.
        /// </summary>
        /// <value>The request.</value>
        public Request Request { get; }
        /// <summary>
        /// True if the response was served from either the browser's disk cache or memory cache.
        /// </summary>
        public bool FromCache => _fromDiskCache || (Request?.FromMemoryCache ?? false);
        /// <summary>
        /// Gets or sets the security details.
        /// </summary>
        /// <value>The security details.</value>
        public SecurityDetails SecurityDetails { get; }
        /// <summary>
        /// Gets a value indicating whether the <see cref="Response"/> was served by a service worker.
        /// </summary>
        /// <value><c>true</c> if the <see cref="Response"/> was served by a service worker; otherwise, <c>false</c>.</value>
        public bool FromServiceWorker { get; }
        /// <summary>
        /// Contains the status text of the response (e.g. usually an "OK" for a success).
        /// </summary>
        /// <value>The status text.</value>
        public string StatusText { get; }
        /// <summary>
        /// Remove server address.
        /// </summary>
        public RemoteAddress RemoteAddress { get; }
        internal TaskCompletionSource<bool> BodyLoadedTaskWrapper { get; }

        /// <summary>
        /// A <see cref="Frame"/> that initiated this request. Or null if navigating to error pages.
        /// </summary>
        public Frame Frame => Request.Frame;

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a Task which resolves to a buffer with response body
        /// </summary>
        /// <returns>A Task which resolves to a buffer with response body</returns>
        public async ValueTask<byte[]> BufferAsync()
        {
            if (_buffer == null)
            {
                var success = await BodyLoadedTaskWrapper.Task.ConfigureAwait(false);

                if (!success)
                {
                    throw new PuppeteerException("Response body is unavailable for redirect responses");
                }

                try
                {
                    var response = await _client.SendAsync<NetworkGetResponseBodyResponse>("Network.getResponseBody", new NetworkGetResponseBodyRequest
                    {
                        RequestId = Request.RequestId
                    }).ConfigureAwait(false);

                    _buffer = response.Base64Encoded
                        ? Convert.FromBase64String(response.Body)
                        : Encoding.UTF8.GetBytes(response.Body);
                }
                catch (Exception ex)
                {
                    throw new BufferException("Unable to get response body", ex);
                }
            }

            return _buffer;
        }

        /// <summary>
        /// Returns a Task which resolves to a text representation of response body
        /// </summary>
        /// <returns>A Task which resolves to a text representation of response body</returns>
        public async Task<string> TextAsync() => Encoding.UTF8.GetString(await BufferAsync().ConfigureAwait(false));

        /// <summary>
        /// Returns a Task which resolves to a <see cref="JObject"/> representation of response body
        /// </summary>
        /// <seealso cref="JsonAsync{T}"/>
        /// <returns>A Task which resolves to a <see cref="JObject"/> representation of response body</returns>
        public async Task<JObject> JsonAsync() => JObject.Parse(await TextAsync().ConfigureAwait(false));

        /// <summary>
        /// Returns a Task which resolves to a <typeparamref name="T"/> representation of response body
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <seealso cref="JsonAsync"/>
        /// <returns>A Task which resolves to a <typeparamref name="T"/> representation of response body</returns>
        public async Task<T> JsonAsync<T>() => (await JsonAsync().ConfigureAwait(false)).ToObject<T>(true);

        #endregion
    }
}