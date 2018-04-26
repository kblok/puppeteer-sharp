﻿using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PuppeteerSharp.Tests.Network
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class NetworkEventTests : PuppeteerPageBaseTest
    {
        [Fact]
        public async Task PageEventsRequest()
        {
            var requests = new List<Request>();
            Page.RequestCreated += (sender, e) => requests.Add(e.Request);
            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.Single(requests);
            Assert.Equal(TestConstants.EmptyPage, requests[0].Url);
            Assert.Equal(ResourceType.Document, requests[0].ResourceType);
            Assert.Equal("GET", requests[0].Method);
            Assert.NotNull(requests[0].Response);
            Assert.Equal(Page.MainFrame, requests[0].Frame);
            Assert.Equal(TestConstants.EmptyPage, requests[0].Frame.Url);
        }

        [Fact]
        public async Task PageEventsRequestShouldReportPostData()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            Server.SetRoute("/post", context => Task.CompletedTask);
            Request request = null;
            Page.RequestCreated += (sender, e) => request = e.Request;
            await Page.EvaluateExpressionHandle("fetch('./post', { method: 'POST', body: JSON.stringify({ foo: 'bar'})})");
            Assert.NotNull(request);
            Assert.Equal("{\"foo\":\"bar\"}", request.PostData);
        }

        [Fact]
        public async Task PageEventsResponse()
        {
            var responses = new List<Response>();
            Page.ResponseCreated += (sender, e) => responses.Add(e.Response);
            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.Single(responses);
            Assert.Equal(TestConstants.EmptyPage, responses[0].Url);
            Assert.Equal(HttpStatusCode.OK, responses[0].Status);
            Assert.NotNull(responses[0].Request);
        }

        [Fact]
        public async Task PageEventsResponseShouldProvideBody()
        {
            Response response = null;
            Page.ResponseCreated += (sender, e) => response = e.Response;
            await Page.GoToAsync(TestConstants.ServerUrl + "/simple.json");
            Assert.NotNull(response);
            Assert.Equal("{\"foo\": \"bar\"}\r\n", await response.TextAsync());
            Assert.Equal(JObject.Parse("{\"foo\": \"bar\"}\r\n"), await response.JsonAsync());
        }

        [Fact]
        public async Task PageEventsResponseShouldNotReportBodyUnlessRequestIsFinished()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            // Setup server to trap request.
            var serverResponseCompletion = new TaskCompletionSource<bool>();
            HttpResponse serverResponse = null;
            Server.SetRoute("/get", context =>
            {
                serverResponse = context.Response;
                context.Response.WriteAsync("hello ");
                return serverResponseCompletion.Task;
            });
            // Setup page to trap response.
            Response pageResponse = null;
            var requestFinished = false;
            Page.ResponseCreated += (sender, e) => pageResponse = e.Response;
            Page.RequestFinished += (sender, e) => requestFinished = true;
            // send request and wait for server response
            Task WaitForPageResponseEvent()
            {
                var completion = new TaskCompletionSource<bool>();
                Page.ResponseCreated += (sender, e) => completion.SetResult(true);
                return completion.Task;
            }
            await Task.WhenAll(
                Page.EvaluateExpressionAsync("fetch('/get', { method: 'GET'})"),
                WaitForPageResponseEvent()
            );

            Assert.NotNull(serverResponse);
            Assert.NotNull(pageResponse);
            Assert.Equal(HttpStatusCode.OK, pageResponse.Status);
            Assert.False(requestFinished);

            var responseText = pageResponse.TextAsync();
            // Write part of the response and wait for it to be flushed.
            await serverResponse.WriteAsync("wor");
            // Finish response.
            await serverResponse.WriteAsync("ld!");
            serverResponseCompletion.SetResult(true);
            Assert.Equal("hello world!", await responseText);
        }

        [Fact]
        public async Task PageEventsRequestFailed()
        {
            await Page.SetRequestInterceptionAsync(true);
            Page.RequestCreated += async (sender, e) =>
            {
                if (e.Request.Url.EndsWith("css"))
                    await e.Request.Abort();
                else
                    await e.Request.Continue();
            };
            var failedRequests = new List<Request>();
            Page.RequestFailed += (sender, e) => failedRequests.Add(e.Request);
            await Page.GoToAsync(TestConstants.ServerUrl + "/one-style.html");

            Assert.Single(failedRequests);
            Assert.Equal("one-style.css", failedRequests[0].Url);
            Assert.Null(failedRequests[0].Response);
            Assert.Equal(ResourceType.StyleSheet, failedRequests[0].ResourceType);
            Assert.Equal("net::ERR_FAILED", failedRequests[0].Failure);
            Assert.NotNull(failedRequests[0].Frame);
        }

        [Fact]
        public async Task PageEventsRequestFinished()
        {

        }

        [Fact]
        public async Task ShouldFireEventsInProperOrder()
        {

        }

        [Fact]
        public async Task ShouldSupportRedirects()
        {

        }
    }
}
