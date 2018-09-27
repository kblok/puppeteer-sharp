﻿using Newtonsoft.Json;

namespace PuppeteerSharp
{
    internal class ContextPayload
    {
        [JsonProperty(Constants.ID)]
        internal int Id { get; set; }
        [JsonProperty("auxData")]
        internal ContextPayloadAuxData AuxData { get; set; }
    }
}