﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PuppeteerSharp
{
    public class Response
    {
        private readonly CDPSession _client;
        //TODO: In puppeteer this is a buffer but as I don't know the real implementation yet
        //I will consider this a string
        
        public Response(CDPSession client, Request request, HttpStatusCode status, Dictionary<string, object> headers, SecurityDetails securityDetails)
        {
            _client = client;
            Request = request;
            Status = status;
            Ok = (int)status >= 200 && (int)status <= 299;
            Url = request.Url;

            Headers = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> keyValue in headers)
            {
                Headers[keyValue.Key] = keyValue.Value;
            }
            SecurityDetails = securityDetails;
        }

        #region Properties

        /// <summary>
        /// Contains the URL of the response.
        /// </summary>
        public string Url { get; internal set; }
        public Dictionary<string, object> Headers { get; internal set; }
        public HttpStatusCode? Status { get; internal set; }
        public bool Ok { get; }
        public Task<string> ContentTask => ContentTaskWrapper.Task;
        public TaskCompletionSource<string> ContentTaskWrapper { get; internal set; }
        public Request Request { get; internal set; }
        public SecurityDetails SecurityDetails { get; internal set; }
        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a Task which resolves to a buffer with response body
        /// </summary>
        /// <returns>A Task which resolves to a buffer with response body</returns>
        public Task<string> BufferAsync()
        {
            if (ContentTaskWrapper == null)
            {
                ContentTaskWrapper = new TaskCompletionSource<string>();

                Request.CompleteTask.ContinueWith(async (task) =>
                {
                    try
                    {
                        var response = await _client.SendAsync("Network.getResponseBody", new Dictionary<string, object>
                        {
                            {"requestId", Request.RequestId}
                        });

                        ContentTaskWrapper.SetResult(response.body.ToString());
                    }
                    catch (Exception ex)
                    {
                        ContentTaskWrapper.SetException(new BufferException("Unable to get response body", ex));
                    }
                });
            }

            return ContentTaskWrapper.Task;
        }

        /// <summary>
        /// Returns a Task which resolves to a text representation of response body
        /// </summary>
        /// <returns>A Task which resolves to a text representation of response body</returns>
        public Task<string> TextAsync() => BufferAsync();

        /// <summary>
        /// Returns a Task which resolves to a <see cref="JObject"/> representation of response body
        /// </summary>
        /// <seealso cref="JsonAsync{T}"/>
        /// <returns>A Task which resolves to a <see cref="JObject"/> representation of response body</returns>
        public async Task<JObject> JsonAsync() => JObject.Parse(await TextAsync());

        /// <summary>
        /// Returns a Task which resolves to a <typeparamref name="T"/> representation of response body
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <seealso cref="JsonAsync"/>
        /// <returns>A Task which resolves to a <typeparamref name="T"/> representation of response body</returns>
        public async Task<T> JsonAsync<T>() => (await JsonAsync()).ToObject<T>();

        #endregion

    }
}