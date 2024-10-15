using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Management.Automation;
using System.Net;
using System.Text;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Extensions;

namespace Chocolatey.PowerShell.Shared
{
    public static class ProxySettings
    {
        public static IWebProxy GetProxy(PSCmdlet cmdlet, Uri uri)
        {
            var explicitProxy = Environment.GetEnvironmentVariable("chocolateyProxyLocation");
            var explicitProxyUser = Environment.GetEnvironmentVariable("chocolateyProxyUser");
            var explicitProxyPassword = Environment.GetEnvironmentVariable("chocolateyProxyPassword");
            var explicitProxyBypassList = Environment.GetEnvironmentVariable("chocolateyProxyBypassList");
            var explicitProxyBypassOnLocal = Environment.GetEnvironmentVariable("chocolateyProxyBypassOnLocal");
            var defaultCredentials = CredentialCache.DefaultCredentials;

            if (explicitProxy != null)
            {
                var proxy = new WebProxy(explicitProxy, BypassOnLocal: PSHelper.IsEqual("true", explicitProxyBypassOnLocal));
                if (!string.IsNullOrWhiteSpace(explicitProxyPassword))
                {
                    var securePassword = explicitProxyPassword.ToSecureStringSafe();
                    proxy.Credentials = new NetworkCredential(explicitProxyUser, securePassword);
                }

                if (!string.IsNullOrWhiteSpace(explicitProxyBypassList))
                {
                    proxy.BypassList = explicitProxyBypassList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }

                PSHelper.WriteHost(cmdlet, $"Using explicit proxy server '{explicitProxy}'.");

                return proxy;
            }

            var webClient = new WebClient();
            if (webClient.Proxy != null && !webClient.Proxy.IsBypassed(uri))
            {
                var credentials = defaultCredentials;
                if (credentials == null && cmdlet.Host != null)
                {
                    PSHelper.WriteDebug(cmdlet, "Default credentials were null. Attempting backup method");
                    PSCredential cred = cmdlet.Host.UI.PromptForCredential("Enter username/password", "", "", "");
                    credentials = cred.GetNetworkCredential();
                }

                var proxyAddress = webClient.Proxy.GetProxy(uri).Authority;
                var proxy = new WebProxy(proxyAddress, BypassOnLocal: true)
                {
                    Credentials = credentials
                };

                PSHelper.WriteHost(cmdlet, $"Using system proxy server '{proxyAddress}'.");
                return proxy;
            }

            return null;
        }
    }
}
