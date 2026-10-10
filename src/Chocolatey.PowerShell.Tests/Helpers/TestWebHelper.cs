// Copyright © 2017 - 2026 Chocolatey Software, Inc
// Copyright © 2011 - 2017 RealDimensions Software, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
// You may obtain a copy of the License at
//
// 	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading;
using Chocolatey.PowerShell.Helpers;

namespace Chocolatey.PowerShell.Tests.Helpers
{
    public class TestWebHelper : WebHelper
    {
        public TestWebHelper(PSCmdlet cmdlet, WebRequestOptions options)
            : base(cmdlet, options)
        {
        }

        public TestWebHelper(PSCmdlet cmdlet, WebRequestOptions options, CancellationToken cancellationToken)
            : base(cmdlet, options, cancellationToken)
        {
        }

        /// <summary>
        /// The proxy reported as the system proxy, so that specs do not depend on the machine's settings.
        /// </summary>
        public IWebProxy SystemProxy { get; set; }

        /// <summary>
        /// When set, requests are sent through this handler instead of the network.
        /// </summary>
        public HttpMessageHandler MessageHandler { get; set; }

        public int Read(Stream stream, byte[] buffer)
        {
            return ReadBuffer(stream, buffer);
        }

        public void SetReadTimeout(TimeSpan? timeout)
        {
            ReadTimeout = timeout;
        }

        public HttpClientHandler CreateRequestHandler()
        {
            return CreateHandler(Options);
        }

        public HttpRequestMessage CreateMessage(HttpClientHandler handler)
        {
            return CreateRequestMessage(Options, handler);
        }

        public TimeSpan RequestTimeout()
        {
            return GetRequestTimeout();
        }

        public TimeSpan? ResponseTimeout()
        {
            return GetReadTimeout();
        }

        public Dictionary<string, string> ReadResponseHeaders(HttpResponseMessage response)
        {
            Response = response;
            return GetResponseHeaders();
        }

        public void ApplyProxyConfiguration(HttpClientHandler handler, Uri requestUri)
        {
            base.SetProxyConfiguration(handler, requestUri);
        }

        protected override IWebProxy GetSystemProxy()
        {
            return SystemProxy;
        }

        protected override void SetProxyConfiguration(HttpClientHandler handler, Uri requestUri)
        {
        }

        protected override HttpClient CreateHttpClient(HttpClientHandler handler, TimeSpan timeout)
        {
            if (MessageHandler is null)
            {
                return base.CreateHttpClient(handler, timeout);
            }

            return new HttpClient(MessageHandler)
            {
                Timeout = timeout
            };
        }
    }
}
