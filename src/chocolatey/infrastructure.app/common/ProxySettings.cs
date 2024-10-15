// Copyright © 2017-2019 Chocolatey Software, Inc ("Chocolatey")
// Copyright © 2015-2017 RealDimensions Software, LLC
// 
// Chocolatey Professional, Chocolatey for Business, and Chocolatey Architect are licensed software.
// 
// =====================================================================
// End-User License Agreement
// Chocolatey Professional, Chocolatey for Service Providers, Chocolatey for Business,
// and/or Chocolatey Architect
// =====================================================================
// 
// IMPORTANT- READ CAREFULLY: This Chocolatey Software ("Chocolatey") End-User License Agreement
// ("EULA") is a legal agreement between you ("END USER") and Chocolatey for all Chocolatey products,
// controls, source code, demos, intermediate files, media, printed materials, and "online" or electronic
// documentation (collectively "SOFTWARE PRODUCT(S)") contained with this distribution.
// 
// Chocolatey grants to you as an individual or entity, a personal, nonexclusive license to install and use the
// SOFTWARE PRODUCT(S). By installing, copying, or otherwise using the SOFTWARE PRODUCT(S), you
// agree to be bound by the terms of this EULA. If you do not agree to any part of the terms of this EULA, DO
// NOT INSTALL, USE, OR EVALUATE, ANY PART, FILE OR PORTION OF THE SOFTWARE PRODUCT(S).
// 
// In no event shall Chocolatey be liable to END USER for damages, including any direct, indirect, special,
// incidental, or consequential damages of any character arising as a result of the use or inability to use the
// SOFTWARE PRODUCT(S) (including but not limited to damages for loss of goodwill, work stoppage, computer
// failure or malfunction, or any and all other commercial damages or losses).
// 
// The liability of Chocolatey to END USER for any reason and upon any cause of action related to the
// performance of the work under this agreement whether in tort or in contract or otherwise shall be limited to the
// amount paid by the END USER to Chocolatey pursuant to this agreement.
// 
// ALL SOFTWARE PRODUCT(S) are licensed not sold. If you are an individual, you must acquire an individual
// license for the SOFTWARE PRODUCT(S) from Chocolatey or its authorized resellers. If you are an entity, you
// must acquire an individual license for each machine running the SOFTWARE PRODUCT(S) within your
// organization from Chocolatey or its authorized resellers. Both virtual and physical machines running the
// SOFTWARE PRODUCT(S) or benefitting from licensed features such as Package Builder or Package
// Internalizer must be counted in the SOFTWARE PRODUCT(S) licenses quantity of the organization.
namespace chocolatey.infrastructure.app.common
{
    using System;
    using System.Management.Automation;
    using System.Management.Automation.Host;
    using System.Net;

    public static class ProxySettings
    {
        public static IWebProxy GetProxy(Uri uri, PSHost host)
        {
            var explicitProxy = Environment.GetEnvironmentVariable("chocolateyProxyLocation");
            var explicitProxyUser = Environment.GetEnvironmentVariable("chocolateyProxyUser");
            var explicitProxyPassword = Environment.GetEnvironmentVariable("chocolateyProxyPassword");
            var explicitProxyBypassList = Environment.GetEnvironmentVariable("chocolateyProxyBypassList");
            var explicitProxyBypassOnLocal = Environment.GetEnvironmentVariable("chocolateyProxyBypassOnLocal");
            var defaultCredentials = CredentialCache.DefaultCredentials;

            if (explicitProxy != null)
            {
                var proxy = new WebProxy(explicitProxy, BypassOnLocal: explicitProxyBypassOnLocal.ToStringSafe().IsEqualTo("true"));
                if (!string.IsNullOrWhiteSpace(explicitProxyPassword))
                {
                    var securePassword = explicitProxyPassword.ToSecureStringSafe();
                    proxy.Credentials = new NetworkCredential(explicitProxyUser, securePassword);
                }
                if (!string.IsNullOrWhiteSpace(explicitProxyBypassList))
                {
                    proxy.BypassList = explicitProxyBypassList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }

                "chocolatey".Log().Info(() => "Using explicit proxy server '{0}'.".FormatWith(explicitProxy));

                return proxy;
            }

            var webClient = new WebClient();
            if (webClient.Proxy != null && !webClient.Proxy.IsBypassed(uri))
            {
                var credentials = defaultCredentials;
                if (credentials == null && host != null)
                {
                    "chocolatey".Log().Debug(() => "Default credentials were null. Attempting backup method");
                    PSCredential cred = host.UI.PromptForCredential("Enter username/password", "", "", "");
                    credentials = cred.GetNetworkCredential();
                }

                var proxyAddress = webClient.Proxy.GetProxy(uri).Authority;
                var proxy = new WebProxy(proxyAddress, BypassOnLocal: true)
                {
                    Credentials = credentials
                };

                "chocolatey".Log().Info(() => "Using system proxy server '{0}'.".FormatWith(proxyAddress));
                return proxy;
            }

            return null;
        }
    }
}
