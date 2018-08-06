﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PuppeteerSharp.Helpers;
using PuppeteerSharp.Messaging;

namespace PuppeteerSharp
{
    /// <summary>
    /// Provides methods to interact with a browser in Chromium.
    /// </summary>
    /// <example>
    /// An example of using a <see cref="Browser"/> to create a <see cref="Page"/>:
    /// <code>
    /// <![CDATA[
    /// var browser = await Puppeteer.LaunchAsync(new LaunchOptions());
    /// var page = await browser.NewPageAsync();
    /// await page.GoToAsync("https://example.com");
    /// await browser.CloseAsync();
    /// ]]>
    /// </code>
    /// An example of disconnecting from and reconnecting to a <see cref="Browser"/>:
    /// <code>
    /// <![CDATA[
    /// var browser = await Puppeteer.LaunchAsync(new LaunchOptions());
    /// var browserWSEndpoint = browser.WebSocketEndpoint;
    /// browser.Disconnect();
    /// var browser2 = await Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint = browserWSEndpoint });
    /// await browser2.CloseAsync();
    /// ]]>
    /// </code>
    /// </example>
    public class Browser : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Browser"/> class.
        /// </summary>
        /// <param name="connection">The connection</param>
        /// <param name="ignoreHTTPSErrors">The option to ignoreHTTPSErrors</param>
        /// <param name="setDefaultViewport">The option to setDefaultViewport</param>
        /// <param name="process">The chrome process</param>
        /// <param name="closeCallBack">An async function called before closing</param>
        public Browser(
            Connection connection,
            bool ignoreHTTPSErrors,
            bool setDefaultViewport,
            Process process,
            Func<Task> closeCallBack)
        {
            Process = process;
            Connection = connection;
            IgnoreHTTPSErrors = ignoreHTTPSErrors;
            _setDefaultViewport = setDefaultViewport;
            TargetsMap = new Dictionary<string, Target>();
            ScreenshotTaskQueue = new TaskQueue();

            Connection.Closed += (object sender, EventArgs e) => Disconnected?.Invoke(this, new EventArgs());
            Connection.MessageReceived += Connect_MessageReceived;

            _closeCallBack = closeCallBack;
            _logger = Connection.LoggerFactory.CreateLogger<Browser>();
        }

        #region Private members
        internal readonly Dictionary<string, Target> TargetsMap;
        internal readonly bool _setDefaultViewport;

        private readonly Func<Task> _closeCallBack;
        private readonly ILogger<Browser> _logger;
        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// Raised when puppeteer gets disconnected from the Chromium instance. This might happen because one of the following
        /// - Chromium is closed or crashed
        /// - <see cref="Disconnect"/> method was called
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Raised when the url of a target changes
        /// </summary>
        public event EventHandler<TargetChangedArgs> TargetChanged;

        /// <summary>
        /// Raised when a target is created, for example when a new page is opened by <c>window.open</c> <see href="https://developer.mozilla.org/en-US/docs/Web/API/Window/open"/> or <see cref="NewPageAsync"/>.
        /// </summary>
        public event EventHandler<TargetChangedArgs> TargetCreated;

        /// <summary>
        /// Raised when a target is destroyed, for example when a page is closed
        /// </summary>
        public event EventHandler<TargetChangedArgs> TargetDestroyed;

        /// <summary>
        /// Gets the Browser websocket url
        /// </summary>
        /// <remarks>
        /// Browser websocket endpoint which can be used as an argument to <see cref="Puppeteer.ConnectAsync(ConnectOptions, ILoggerFactory)"/>.
        /// The format is <c>ws://${host}:${port}/devtools/browser/[id]</c>
        /// You can find the <c>webSocketDebuggerUrl</c> from <c>http://${host}:${port}/json/version</c>.
        /// Learn more about the devtools protocol <see href="https://chromedevtools.github.io/devtools-protocol"/> 
        /// and the browser endpoint <see href="https://chromedevtools.github.io/devtools-protocol/#how-do-i-access-the-browser-target"/>
        /// </remarks>
        public string WebSocketEndpoint => Connection.Url;

        /// <summary>
        /// Gets the spawned browser process. Returns <c>null</c> if the browser instance was created with <see cref="Puppeteer.ConnectAsync(ConnectOptions, ILoggerFactory)"/> method.
        /// </summary>
        public Process Process { get; }

        /// <summary>
        /// Gets or Sets whether to ignore HTTPS errors during navigation
        /// </summary>
        public bool IgnoreHTTPSErrors { get; set; }

        /// <summary>
        /// Gets a value indicating if the browser is closed
        /// </summary>
        public bool IsClosed { get; internal set; }

        internal TaskQueue ScreenshotTaskQueue { get; set; }
        internal Connection Connection { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new page
        /// </summary>
        /// <returns>Task which resolves to a new <see cref="Page"/> object</returns>
        public async Task<Page> NewPageAsync()
        {
            string targetId = (await Connection.SendAsync("Target.createTarget", new Dictionary<string, object>
            {
                ["url"] = "about:blank"
            }).ConfigureAwait(false)).targetId.ToString();

            var target = TargetsMap[targetId];
            await target.InitializedTask.ConfigureAwait(false);
            return await target.PageAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns An Array of all active targets
        /// </summary>
        /// <returns>An Array of all active targets</returns>
        public Target[] Targets() => TargetsMap.Values.Where(target => target.IsInitialized).ToArray();

        /// <summary>
        /// Returns a Task which resolves to an array of all open pages.
        /// </summary>
        /// <returns>Task which resolves to an array of all open pages.</returns>
        public async Task<Page[]> PagesAsync()
            => (await Task.WhenAll(Targets().Select(target => target.PageAsync())).ConfigureAwait(false)).Where(x => x != null).ToArray();

        /// <summary>
        /// Gets the browser's version
        /// </summary>
        /// <returns>For headless Chromium, this is similar to <c>HeadlessChrome/61.0.3153.0</c>. For non-headless, this is similar to <c>Chrome/61.0.3153.0</c></returns>
        /// <remarks>
        /// the format of <see cref="GetVersionAsync"/> might change with future releases of Chromium
        /// </remarks>
        public async Task<string> GetVersionAsync()
        {
            dynamic version = await Connection.SendAsync("Browser.getVersion").ConfigureAwait(false);
            return version.product.ToString();
        }

        /// <summary>
        /// Gets the browser's original user agent
        /// </summary>
        /// <returns>Task which resolves to the browser's original user agent</returns>
        /// <remarks>
        /// Pages can override browser user agent with <see cref="Page.SetUserAgentAsync(string)"/>
        /// </remarks>
        public async Task<string> GetUserAgentAsync()
        {
            dynamic version = await Connection.SendAsync("Browser.getVersion").ConfigureAwait(false);
            return version.userAgent.ToString();
        }

        /// <summary>
        /// Disconnects Puppeteer from the browser, but leaves the Chromium process running. After calling <see cref="Disconnect"/>, the browser object is considered disposed and cannot be used anymore
        /// </summary>
        public void Disconnect() => Connection.Dispose();

        /// <summary>
        /// Closes Chromium and all of its pages (if any were opened). The browser object itself is considered disposed and cannot be used anymore
        /// </summary>
        /// <returns>Task</returns>
        public async Task CloseAsync()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            Connection.StopReading();

            var closeTask = _closeCallBack();

            if (closeTask != null)
            {
                await closeTask.ConfigureAwait(false);
            }

            Disconnect();
            Closed?.Invoke(this, new EventArgs());
        }

        #endregion

        #region Private Methods

        internal void ChangeTarget(Target target) => TargetChanged?.Invoke(this, new TargetChangedArgs
        {
            Target = target
        });

        private async void Connect_MessageReceived(object sender, MessageEventArgs e)
        {
            switch (e.MessageID)
            {
                case "Target.targetCreated":
                    await CreateTargetAsync(e.MessageData.ToObject<TargetCreatedResponse>()).ConfigureAwait(false);
                    return;

                case "Target.targetDestroyed":
                    await DestroyTargetAsync(e.MessageData.ToObject<TargetDestroyedResponse>()).ConfigureAwait(false);
                    return;

                case "Target.targetInfoChanged":
                    ChangeTargetInfo(e.MessageData.ToObject<TargetCreatedResponse>());
                    return;
            }
        }

        private void ChangeTargetInfo(TargetCreatedResponse e)
        {
            if (!TargetsMap.ContainsKey(e.TargetInfo.TargetId))
            {
                throw new InvalidTargetException("Target should exists before ChangeTargetInfo");
            }

            var target = TargetsMap[e.TargetInfo.TargetId];
            target.TargetInfoChanged(e.TargetInfo);
        }

        private async Task DestroyTargetAsync(TargetDestroyedResponse e)
        {
            if (!TargetsMap.ContainsKey(e.TargetId))
            {
                throw new InvalidTargetException("Target should exists before DestroyTarget");
            }

            var target = TargetsMap[e.TargetId];
            TargetsMap.Remove(e.TargetId);

            target.CloseTaskWrapper.TrySetResult(true);

            if (await target.InitializedTask.ConfigureAwait(false))
            {
                TargetDestroyed?.Invoke(this, new TargetChangedArgs
                {
                    Target = target
                });
            }
        }

        private async Task CreateTargetAsync(TargetCreatedResponse e)
        {
            var target = new Target(
                e.TargetInfo,
                () => Connection.CreateSessionAsync(e.TargetInfo.TargetId),
                this);

            if (TargetsMap.ContainsKey(e.TargetInfo.TargetId))
            {
                _logger.LogError("Target should not exist before targetCreated");
            }

            TargetsMap[e.TargetInfo.TargetId] = target;

            if (await target.InitializedTask.ConfigureAwait(false))
            {
                TargetCreated?.Invoke(this, new TargetChangedArgs
                {
                    Target = target
                });
            }
        }

        internal static async Task<Browser> CreateAsync(
            Connection connection,
            bool ignoreHTTPSErrors,
            bool appMode,
            Process process,
            Func<Task> closeCallBack)
        {
            var browser = new Browser(connection, ignoreHTTPSErrors, appMode, process, closeCallBack);
            await connection.SendAsync("Target.setDiscoverTargets", new
            {
                discover = true
            }).ConfigureAwait(false);

            return browser;
        }
        #endregion

        #region IDisposable
        /// <inheritdoc />
        public void Dispose() => CloseAsync().GetAwaiter().GetResult();
        #endregion
    }
}