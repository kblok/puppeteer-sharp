﻿using Newtonsoft.Json;

namespace PuppeteerSharp
{
    /// <summary>
    /// Cookie data.
    /// </summary>
    /// <seealso cref="Page.SetContentAsync(string)"/>
    /// <seealso cref="Page.DeleteCookieAsync(CookieParam[])"/>
    /// <seealso cref="Page.GetCookiesAsync(string[])"/>
    public class CookieParam
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [JsonProperty(Constants.NAME)]
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [JsonProperty(Constants.VALUE)]
        public string Value { get; set; }
        /// <summary>
        /// Gets or sets the domain.
        /// </summary>
        /// <value>The domain.</value>
        [JsonProperty(Constants.DOMAIN, NullValueHandling = NullValueHandling.Ignore)]
        public string Domain { get; set; }
        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>The URL.</value>
        [JsonProperty(Constants.URL, NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }
        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        [JsonProperty(Constants.PATH, NullValueHandling = NullValueHandling.Ignore)]
        public string Path { get; set; }
        /// <summary>
        /// Gets or sets the expiration.
        /// </summary>
        /// <value>Expiration.</value>
        [JsonProperty(Constants.EXPIRES, NullValueHandling = NullValueHandling.Ignore)]
        public double? Expires { get; set; }
        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        [JsonProperty(Constants.SIZE, NullValueHandling = NullValueHandling.Ignore)]
        public int? Size { get; set; }
        /// <summary>
        /// Gets or sets if it's HTTP only.
        /// </summary>
        /// <value>Whether it's http only or not.</value>
        [JsonProperty(Constants.HTTP_ONLY, NullValueHandling = NullValueHandling.Ignore)]
        public bool? HttpOnly { get; set; }
        /// <summary>
        /// Gets or sets if it's secure.
        /// </summary>
        /// <value>Whether it's secure or not.</value>
        [JsonProperty(Constants.SECURE, NullValueHandling = NullValueHandling.Ignore)]
        public bool? Secure { get; set; }
        /// <summary>
        /// Gets or sets if it's session only.
        /// </summary>
        /// <value>Whether it's session only or not.</value>
        [JsonProperty(Constants.SESSION, NullValueHandling = NullValueHandling.Ignore)]
        public bool? Session { get; set; }
    }
}