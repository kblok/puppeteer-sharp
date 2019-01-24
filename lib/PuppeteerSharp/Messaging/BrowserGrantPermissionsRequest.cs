﻿using System.Collections.Generic;

namespace PuppeteerSharp.Messaging
{
    internal class BrowserGrantPermissionsRequest
    {
        public string Origin { get; set; }
        public string BrowserContextId { get; set; }
        public IEnumerable<OverridePermission> Permissions { get; set; }
    }
}
