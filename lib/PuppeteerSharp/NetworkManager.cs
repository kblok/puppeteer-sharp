using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PuppeteerSharp.Helpers;
using PuppeteerSharp.Helpers.Json;
using PuppeteerSharp.Messaging;

namespace PuppeteerSharp
{
    internal class NetworkManager
    {
        #region Private members

        private readonly CDPSession _client;
        private readonly ConcurrentDictionary<string, Request> _requestIdToRequest = new ConcurrentDictionary<string, Request>();
        private readonly ConcurrentDictionary<string, RequestWillBeSentPayload> _requestIdToRequestWillBeSentEvent =
            new ConcurrentDictionary<string, RequestWillBeSentPayload>();
        private readonly ConcurrentDictionary<string, string> _requestIdToInterceptionId = new ConcurrentDictionary<string, string>();
        private readonly ILogger _logger;
        private Dictionary<string, string> _extraHTTPHeaders;
        private bool _offine;
        private Credentials _credentials;
        private readonly List<string> _attemptedAuthentications = new List<string>();
        private bool _userRequestInterceptionEnabled;
        private bool _protocolRequestInterceptionEnabled;
        private readonly bool _ignoreHTTPSErrors;
        private bool _userCacheDisabled;
        #endregion

        internal NetworkManager(CDPSession client, bool ignoreHTTPSErrors, FrameManager frameManager)
        {
            FrameManager = frameManager;
            _client = client;
            _ignoreHTTPSErrors = ignoreHTTPSErrors;
            _client.MessageReceived += Client_MessageReceived;
            _logger = _client.Connection.LoggerFactory.CreateLogger<NetworkManager>();
        }

        #region Public Properties
        internal Dictionary<string, string> ExtraHTTPHeaders => _extraHTTPHeaders?.Clone();
        internal event EventHandler<ResponseCreatedEventArgs> Response;
        internal event EventHandler<RequestEventArgs> Request;
        internal event EventHandler<RequestEventArgs> RequestFinished;
        internal event EventHandler<RequestEventArgs> RequestFailed;
        internal FrameManager FrameManager { get; set; }
        #endregion

        #region Public Methods

        internal async Task InitializeAsync()
        {
            await _client.SendAsync("Network.enable").ConfigureAwait(false);
            if (_ignoreHTTPSErrors)
            {
                await _client.SendAsync("Security.setIgnoreCertificateErrors", new SecuritySetIgnoreCertificateErrorsRequest
                {
                    Ignore = true
                }).ConfigureAwait(false);
            }
        }

        internal Task AuthenticateAsync(Credentials credentials)
        {
            _credentials = credentials;
            return UpdateProtocolRequestInterceptionAsync();
        }

        internal Task SetExtraHTTPHeadersAsync(Dictionary<string, string> extraHTTPHeaders)
        {
            _extraHTTPHeaders = new Dictionary<string, string>();

            foreach (var item in extraHTTPHeaders)
            {
                _extraHTTPHeaders[item.Key.ToLower()] = item.Value;
            }
            return _client.SendAsync("Network.setExtraHTTPHeaders", new NetworkSetExtraHTTPHeadersRequest
            {
                Headers = _extraHTTPHeaders
            });
        }

        internal async Task SetOfflineModeAsync(bool value)
        {
            if (_offine != value)
            {
                _offine = value;

                await _client.SendAsync("Network.emulateNetworkConditions", new NetworkEmulateNetworkConditionsRequest
                {
                    Offline = value,
                    Latency = 0,
                    DownloadThroughput = -1,
                    UploadThroughput = -1
                }).ConfigureAwait(false);
            }
        }

        internal Task SetUserAgentAsync(string userAgent)
            => _client.SendAsync("Network.setUserAgentOverride", new NetworkSetUserAgentOverrideRequest
            {
                UserAgent = userAgent
            });

        internal Task SetCacheEnabledAsync(bool enabled)
        {
            _userCacheDisabled = !enabled;
            return UpdateProtocolCacheDisabledAsync();
        }

        internal Task SetRequestInterceptionAsync(bool value, params FetchEnableRequest.Pattern[] fetchRequestPatterns)
        {
            _userRequestInterceptionEnabled = value;
            return UpdateProtocolRequestInterceptionAsync(fetchRequestPatterns);
        }

        #endregion

        #region Private Methods

        private Task UpdateProtocolCacheDisabledAsync()
            => _client.SendAsync("Network.setCacheDisabled", new NetworkSetCacheDisabledRequest
            {
                CacheDisabled = _userCacheDisabled || _protocolRequestInterceptionEnabled
            });

        private async void Client_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                switch (e.MessageID)
                {
                    case "Fetch.requestPaused":
                        await OnRequestPausedAsync(e.MessageData.ToObject<FetchRequestPausedResponse>(true)).ConfigureAwait(false);
                        break;
                    case "Fetch.authRequired":
                        await OnAuthRequiredAsync(e.MessageData.ToObject<FetchAuthRequiredResponse>(true)).ConfigureAwait(false);
                        break;
                    case "Network.requestWillBeSent":
                        await OnRequestWillBeSentAsync(e.MessageData.ToObject<RequestWillBeSentPayload>(true)).ConfigureAwait(false);
                        break;
                    case "Network.requestServedFromCache":
                        OnRequestServedFromCache(e.MessageData.ToObject<RequestServedFromCacheResponse>(true));
                        break;
                    case "Network.responseReceived":
                        OnResponseReceived(e.MessageData.ToObject<ResponseReceivedResponse>(true));
                        break;
                    case "Network.loadingFinished":
                        OnLoadingFinished(e.MessageData.ToObject<LoadingFinishedResponse>(true));
                        break;
                    case "Network.loadingFailed":
                        OnLoadingFailed(e.MessageData.ToObject<LoadingFailedResponse>(true));
                        break;
                }
            }
            catch (Exception ex)
            {
                var message = $"NetworkManager failed to process {e.MessageID}. {ex.Message}. {ex.StackTrace}";
                _logger.LogError(ex, message);
                _client.Close(message);
            }
        }

        private void OnLoadingFailed(LoadingFailedResponse e)
        {
            // For certain requestIds we never receive requestWillBeSent event.
            // @see https://crbug.com/750469
            if (_requestIdToRequest.TryGetValue(e.RequestId, out var request))
            {
                request.Failure = e.ErrorText;
                request.Response?.BodyLoadedTaskWrapper.TrySetResult(true);
                _requestIdToRequest.TryRemove(request.RequestId, out _);

                if (request.InterceptionId != null)
                {
                    _attemptedAuthentications.Remove(request.InterceptionId);
                }

                RequestFailed?.Invoke(this, new RequestEventArgs
                {
                    Request = request
                });
            }
        }

        private void OnLoadingFinished(LoadingFinishedResponse e)
        {
            // For certain requestIds we never receive requestWillBeSent event.
            // @see https://crbug.com/750469
            if (_requestIdToRequest.TryGetValue(e.RequestId, out var request))
            {
                request.Response?.BodyLoadedTaskWrapper.TrySetResult(true);
                _requestIdToRequest.TryRemove(request.RequestId, out _);

                if (request.InterceptionId != null)
                {
                    _attemptedAuthentications.Remove(request.InterceptionId);
                }

                RequestFinished?.Invoke(this, new RequestEventArgs
                {
                    Request = request
                });
            }
        }

        private void OnResponseReceived(ResponseReceivedResponse e)
        {
            // FileUpload sends a response without a matching request.
            if (_requestIdToRequest.TryGetValue(e.RequestId, out var request))
            {
                var response = new Response(
                    _client,
                    request,
                    e.Response);

                request.Response = response;

                Response?.Invoke(this, new ResponseCreatedEventArgs
                {
                    Response = response
                });
            }
        }

        private async Task OnAuthRequiredAsync(FetchAuthRequiredResponse e)
        {
            var response = "Default";
            if (_attemptedAuthentications.Contains(e.RequestId))
            {
                response = "CancelAuth";
            }
            else if (_credentials != null)
            {
                response = "ProvideCredentials";
                _attemptedAuthentications.Add(e.RequestId);
            }
            var credentials = _credentials ?? new Credentials();
            try
            {
                await _client.SendAsync("Fetch.continueWithAuth", new ContinueWithAuthRequest
                {
                    RequestId = e.RequestId,
                    AuthChallengeResponse = new ContinueWithAuthRequestChallengeResponse
                    {
                        Response = response,
                        Username = credentials.Username,
                        Password = credentials.Password
                    }
                }).ConfigureAwait(false);
            }
            catch (PuppeteerException ex)
            {
                _logger.LogError(ex.ToString());
            }
        }

        private async Task OnRequestPausedAsync(FetchRequestPausedResponse e)
        {
            if (!_userRequestInterceptionEnabled && _protocolRequestInterceptionEnabled)
            {
                try
                {
                    await _client.SendAsync("Fetch.continueRequest", new FetchContinueRequestRequest
                    {
                        RequestId = e.RequestId
                    }).ConfigureAwait(false);
                }
                catch (PuppeteerException ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }

            var requestId = e.NetworkId;
            var interceptionId = e.RequestId;

            if (!string.IsNullOrEmpty(requestId))
            {
                if (_requestIdToRequestWillBeSentEvent.TryRemove(requestId, out RequestWillBeSentPayload requestWillBeSentEvent))
                {
                    MergePayloads(requestWillBeSentEvent, e);
                    await OnRequestAsync(requestWillBeSentEvent, interceptionId).ConfigureAwait(false);
                }
                else
                {
                    _requestIdToInterceptionId[requestId] = interceptionId;
                }
            }
        }

        private void MergePayloads(RequestWillBeSentPayload requestWillBeSentPayload, FetchRequestPausedResponse fetchRequestPausedResponse)
        {
            requestWillBeSentPayload.Type = fetchRequestPausedResponse.ResourceType;
            requestWillBeSentPayload.Response = new ResponsePayload
            {
                Status = fetchRequestPausedResponse.ResponseStatusCode,
                StatusText = fetchRequestPausedResponse.ResponseStatusCode.ToString(),
                Headers = fetchRequestPausedResponse.ResponseHeadersDictionary
            };
        }

        private async Task OnRequestAsync(RequestWillBeSentPayload e, string interceptionId)
        {
            Request request;
            var redirectChain = new List<Request>();
            if (e.Response != null)
            {
                _requestIdToRequest.TryGetValue(e.RequestId, out request);
                // If we connect late to the target, we could have missed the requestWillBeSent event.
                if (request != null)
                {
                    HandleRequestRedirect(request, e.Response);
                    redirectChain = request.RedirectChainList;
                }
            }
            if (!_requestIdToRequest.TryGetValue(e.RequestId, out var currentRequest) ||
              currentRequest.Frame == null)
            {
                var frame = !string.IsNullOrEmpty(e.FrameId) ? await FrameManager.TryGetFrameAsync(e.FrameId).ConfigureAwait(false) : null;

                request = new Request(
                    _client,
                    frame,
                    interceptionId,
                    _userRequestInterceptionEnabled,
                    e,
                    redirectChain);

                _requestIdToRequest[e.RequestId] = request;

                Request?.Invoke(this, new RequestEventArgs
                {
                    Request = request
                });
            }
        }

        private void OnRequestServedFromCache(RequestServedFromCacheResponse response)
        {
            if (_requestIdToRequest.TryGetValue(response.RequestId, out var request))
            {
                request.FromMemoryCache = true;
            }
        }

        private void HandleRequestRedirect(Request request, ResponsePayload responseMessage)
        {
            var response = new Response(
                _client,
                request,
                responseMessage);

            request.Response = response;
            request.RedirectChainList.Add(request);
            response.BodyLoadedTaskWrapper.TrySetException(
                new PuppeteerException("Response body is unavailable for redirect responses"));

            if (request.RequestId != null)
            {
                _requestIdToRequest.TryRemove(request.RequestId, out _);
            }

            if (request.InterceptionId != null)
            {
                _attemptedAuthentications.Remove(request.InterceptionId);
            }

            Response?.Invoke(this, new ResponseCreatedEventArgs
            {
                Response = response
            });

            RequestFinished?.Invoke(this, new RequestEventArgs
            {
                Request = request
            });
        }

        private async Task OnRequestWillBeSentAsync(RequestWillBeSentPayload e)
        {
            // Request interception doesn't happen for data URLs with Network Service.
            if (_protocolRequestInterceptionEnabled && e.IsInterceptable)
            {
                if (_requestIdToInterceptionId.TryRemove(e.RequestId, out string interceptionId))
                {
                    await OnRequestAsync(e, interceptionId).ConfigureAwait(false);
                }
                else
                {
                    // Under load, we may get to this section more than once
                    _requestIdToRequestWillBeSentEvent.TryAdd(e.RequestId, e);
                }
                return;
            }
            await OnRequestAsync(e, null).ConfigureAwait(false);
        }

        private async Task UpdateProtocolRequestInterceptionAsync(params FetchEnableRequest.Pattern[] fetchRequestPatterns)
        {
            var enabled = _userRequestInterceptionEnabled || _credentials != null;

            if (enabled == _protocolRequestInterceptionEnabled)
            {
                return;
            }
            _protocolRequestInterceptionEnabled = enabled;
            if (enabled)
            {
                if (fetchRequestPatterns == null || !fetchRequestPatterns.Any())
                {
                    fetchRequestPatterns = new[] { new FetchEnableRequest.Pattern { UrlPattern = "*" } };
                }

                await Task.WhenAll(
                    UpdateProtocolCacheDisabledAsync(),
                    _client.SendAsync("Fetch.enable", new FetchEnableRequest
                    {
                        HandleAuthRequests = true,
                        Patterns = fetchRequestPatterns
                    })
                ).ConfigureAwait(false);
            }
            else
            {
                await Task.WhenAll(
                    UpdateProtocolCacheDisabledAsync(),
                    _client.SendAsync("Fetch.disable")
                ).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
