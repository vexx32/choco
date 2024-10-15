using Chocolatey.PowerShell.Extensions;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Chocolatey.PowerShell.Helpers
{
    public class WebHelper
    {
        private readonly PSCmdlet _cmdlet;

        public WebHelper(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet;
        }

        protected string GetChocolateyWebFile(
            string packageName,
            string fileFullPath,
            string url,
            string url64Bit,
            string checksum,
            ChecksumType? checksumType,
            string checksum64,
            ChecksumType? checksumType64,
            Hashtable options,
            bool getOriginalFileName,
            bool forceDownload)
        {
            // user provided url overrides
            var urlOverride = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyUrlOverride);
            if (!string.IsNullOrWhiteSpace(urlOverride))
            {
                url = urlOverride;
            }

            var url64bitOverride = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyUrl64BitOverride);
            if (!string.IsNullOrWhiteSpace(url64bitOverride))
            {
                url64Bit = url64bitOverride;
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                url = url.Replace("//", "/").Replace(":/", "://");
            }

            if (!string.IsNullOrWhiteSpace(url64Bit))
            {
                url64Bit = url64Bit.Replace("//", "/").Replace(":/", "://");
            }

            // user provided checksum values
            var checksum32Override = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyChecksum32);
            if (!string.IsNullOrWhiteSpace(checksum32Override))
            {
                checksum = checksum32Override;
            }

            var checksumType32Override = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyChecksumType32);
            if (Enum.TryParse<ChecksumType>(checksumType32Override, out var type))
            {
                checksumType = type;
            }

            var checksum64Override = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyChecksum64);
            if (!string.IsNullOrWhiteSpace(checksum64Override))
            {
                checksum64 = checksum64Override;
            }

            var checksumType64Override = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyChecksumType64);
            if (Enum.TryParse<ChecksumType>(checksumType64Override, out var type64))
            {
                checksumType64 = type64;
            }

            var checksum32 = checksum;
            var checksumType32 = checksumType;

            var is64BitProcess = IntPtr.Size == 8;
            var bitWidth = is64BitProcess ? "64" : "32";
            PSHelper.WriteDebug(_cmdlet, $"CPU is {bitWidth} bit");

            var url32Bit = url;

            // by default do not specify bit package
            var bitPackage = string.Empty;
            if (!PSHelper.IsEqual(url32Bit, url64Bit) && !string.IsNullOrWhiteSpace(url64Bit))
            {
                bitPackage = "32 bit";
            }

            if (is64BitProcess && !string.IsNullOrWhiteSpace(url64Bit))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting url to '{url64Bit}' and bitPackage to '64'.");
                bitPackage = "64 bit";
                url = url64Bit;
                // only set checksum/checksumType that will be used if the urls are different
                if (!PSHelper.IsEqual(url32Bit, url64Bit))
                {
                    checksum = checksum64;
                    if (!(checksumType64 is null))
                    {
                        checksumType = checksumType64;
                    }
                }
            }

            if (PSHelper.IsEqual(Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyForceX86), "true"))
            {
                PSHelper.WriteDebug(_cmdlet, "User specified '-x86' so forcing 32-bit");
                if (!PSHelper.IsEqual(url32Bit, url64Bit))
                {
                    bitPackage = "32 bit";
                }

                url = url32Bit;
                checksum = checksum32;
                checksumType = checksumType32;
            }

            // If we're on 32 bit or attempting to force 32 bit and there is no
            // 32 bit url, we need to throw an error.
            if (string.IsNullOrWhiteSpace(url))
            {
                var architecture = string.IsNullOrWhiteSpace(bitPackage) ? "32 bit" : bitPackage;
                _cmdlet.ThrowTerminatingError(new RuntimeException($"This package does not support {architecture} architecture.").ErrorRecord);
            }

            // determine if url can be SSL/TLS
            if (url?.ToLower().StartsWith("http://") == true)
            {
                try
                {
                    var httpsUrl = url.Replace("http://", "https://");
                    var sslHeaders = GetWebHeaders(httpsUrl);
                    if (sslHeaders.Count != 0)
                    {
                        url = httpsUrl;
                        PSHelper.WriteWarning(_cmdlet, "Url has SSL/TLS available, switching to HTTPS for download.");
                    }
                }
                catch (Exception ex)
                {
                    PSHelper.WriteDebug(_cmdlet, $"Url does not have HTTPS available: {ex.Message}");
                }
            }

            if (getOriginalFileName)
            {
                try
                {
                    // remove \chocolatey\chocolatey\
                    // Reason: https://github.com/chocolatey/choco/commit/ae2e8571ab9440e715effba9b34c25aceac34dac
                    fileFullPath = Regex.Replace(fileFullPath, @"\\chocolatey\\chocolatey\\", @"\chocolatey\", RegexOptions.IgnoreCase);
                    var fileDirectory = PSHelper.GetParentDirectory(_cmdlet, fileFullPath);
                    var originalFileName = PSHelper.GetFileName(fileFullPath);
                    fileFullPath = PSHelper.CombinePaths(_cmdlet, fileDirectory, GetWebFileName(url, originalFileName));
                }
                catch (Exception ex)
                {
                    PSHelper.WriteHost(_cmdlet, $"Attempt to use original download file name failed for '{url}'");
                    PSHelper.WriteDebug(_cmdlet, $" Error was '{ex.Message}'.");
                }
            }

            try
            {
                PSHelper.EnsureDirectoryExists(_cmdlet, PSHelper.GetDirectoryName(_cmdlet, fileFullPath));
            }
            catch (Exception ex)
            {
                PSHelper.WriteHost(_cmdlet, $"Attempt to create directory failed for '{url}'");
                PSHelper.WriteDebug(_cmdlet, $" Error was '{ex.Message}'.");
            }

            var urlIsRemote = true;
            var headers = new Hashtable();
            if (url?.ToLower().StartsWith("http") == true)
            {
                try
                {
                    headers = GetWebHeaders(url);
                }
                catch (Exception ex)
                {
                    PSHelper.WriteHost(_cmdlet, $"Attempt to get headers for '{url}' failed.\n  {ex.Message}");
                }

                var needsDownload = true;
                FileInfo fileInfoCached = null;
                if (PSHelper.ItemExists(_cmdlet, fileFullPath))
                {
                    fileInfoCached = PSHelper.GetFileInfo(_cmdlet, fileFullPath);
                }

                if (PSHelper.ItemExists(_cmdlet, fileFullPath) && !forceDownload)
                {
                    if (!string.IsNullOrWhiteSpace(checksum))
                    {
                        PSHelper.WriteHost(_cmdlet, "File appears to be downloaded already. Verifying with package checksum to determine if it needs to be re-downloaded.");
                        if (ChecksumValidator.IsValid(_cmdlet, fileFullPath, checksum, checksumType, url, out _))
                        {
                            needsDownload = false;
                        }
                        else
                        {
                            PSHelper.WriteDebug(_cmdlet, "Existing file failed checksum. Will be re-downloaded from url.");
                        }
                    }
                    else if (headers.Count != 0 && headers.ContainsKey("Content-Length"))
                    {
                        long fileInfoCachedLength = fileInfoCached?.Length ?? 0;
                        if (PSHelper.IsEqual(PSHelper.ConvertTo<string>(fileInfoCachedLength), PSHelper.ConvertTo<string>(headers["Content-Length"])))
                        {
                            needsDownload = false;
                        }
                    }
                }

                if (needsDownload)
                {
                    PSHelper.WriteHost(
                        _cmdlet,
                        string.Format(
                            "Downloading {0} {1}{2}  from '{3}'.",
                            packageName,
                            bitPackage,
                            Environment.NewLine,
                            url));
                    GetWebFile(url, fileFullPath, options);
                }
                else
                {
                    PSHelper.WriteDebug(
                        _cmdlet,
                        string.Format(
                            "{0}'s requested file has already been downloaded. Using cached copy at{1}  '{2}'.",
                            packageName,
                            Environment.NewLine,
                            fileFullPath));
                }
            }
            else if (url?.ToLower().StartsWith("ftp") == true)
            {
                // ftp the file
                PSHelper.WriteHost(_cmdlet, string.Format("Ftp-ing {0}{1}  from '{2}'.", packageName, Environment.NewLine, url));
                _cmdlet.InvokeCommand.InvokeScript($"Get-FtpFile -Url '{url}' -FileName '{fileFullPath}'");
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                // copy the file
                if (url?.ToLower().StartsWith("file:") == true)
                {
                    var uri = new Uri(url);
                    url = uri.LocalPath;
                }

                PSHelper.WriteHost(_cmdlet, string.Format("Copying {0}{1}  from '{2}'", packageName, Environment.NewLine, url));
                PSHelper.CopyFile(_cmdlet, url, fileFullPath, overwriteExisting: true);
                urlIsRemote = false;
            }

            // give it a sec or two to finish up file operations on the file system
            Thread.Sleep(2000);

            // validate the file now exists locally
            // If the file exists we should be able to assume that `url` is not null by this point.
            if (!PSHelper.ItemExists(_cmdlet, fileFullPath))
            {
                throw new FileNotFoundException($"Chocolatey expected a file to be downloaded to '{fileFullPath}', but nothing exists at that location.");
            }

            CheckVirusEngineResults(url, fileFullPath);
            var fileInfo = PSHelper.GetFileInfo(_cmdlet, fileFullPath);

            if (headers.Count != 0 && string.IsNullOrWhiteSpace(checksum))
            {
                long fileInfoLength = fileInfo.Length;
                //validate content length since we don't have checksum to validate against
                PSHelper.WriteDebug(_cmdlet, $"Checking that '{fileFullPath}' is the size we expect it to be.");
                if (headers.ContainsKey("Content-Length")
                    && !PSHelper.IsEqual(fileInfoLength.ToString(), PSHelper.ConvertTo<string>(headers["Content-Length"])))
                {
                    _cmdlet.ThrowTerminatingError(new RuntimeException(
                        string.Format(
                            "Chocolatey expected a file at '{0}' to be of length '{1}' but the length was '{2}'.",
                            fileFullPath,
                            PSHelper.ConvertTo<string>(headers["Content-Length"]),
                            fileInfoLength.ToString())).ErrorRecord);
                }

                if (headers.ContainsKey("X-Checksum-Sha1"))
                {
                    var remoteChecksum = PSHelper.ConvertTo<string>(headers["X-Checksum-Sha1"]);
                    PSHelper.WriteDebug(_cmdlet, $"Verifying remote checksum of '{remoteChecksum}' for '{fileFullPath}'.");
                    ChecksumValidator.AssertChecksumValid(_cmdlet, fileFullPath, checksum, ChecksumType.Sha1, url);
                }
            }

            // skip requirement for embedded files if checksum is not provided
            // require checksum check if the url is remote
            if (!string.IsNullOrWhiteSpace(checksum) || urlIsRemote)
            {
                PSHelper.WriteDebug(_cmdlet, $"Verifying package provided checksum of '{checksum}' for '{fileFullPath}'.");
                ChecksumValidator.AssertChecksumValid(_cmdlet, fileFullPath, checksum, checksumType, url);
            }

            return fileFullPath;
        }

        public Hashtable GetWebHeaders(string url, string userAgent = WebResources.DefaultUserAgent)
        {
            PSHelper.WriteDebug(_cmdlet, $"Running licensed 'Get-WebHeaders' with url:'{url}', userAgent:'{userAgent}'");

            if (string.IsNullOrEmpty(url))
            {
                return new Hashtable();
            }

            //todo compare original url headers to new headers - report when different

            var downloadUrl = GetDownloadUrl(url);
            var uri = new Uri(downloadUrl);
            var request = (HttpWebRequest)WebRequest.Create(uri);

            // $request.Method = "HEAD"
            var webClient = new WebClient();
            var defaultCredentials = CredentialCache.DefaultCredentials;
            if (defaultCredentials != null)
            {
                request.Credentials = defaultCredentials;
                webClient.Credentials = defaultCredentials;
            }

            var proxy = ProxySettings.GetProxy(_cmdlet, uri);
            if (proxy != null)
            {
                request.Proxy = proxy;
            }

            request.Accept = "*/*";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 20;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 30000;
            var chocolateyRequestTimeout = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyRequestTimeout);
            if (!string.IsNullOrWhiteSpace(chocolateyRequestTimeout))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting request timeout to '{chocolateyRequestTimeout}'");
                int.TryParse(chocolateyRequestTimeout, out var requestTimeoutInt);
                if (requestTimeoutInt <= 0)
                {
                    requestTimeoutInt = 30000;
                }

                request.Timeout = requestTimeoutInt;
            }

            var chocolateyResponseTimeout = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyResponseTimeout);
            if (!string.IsNullOrWhiteSpace(chocolateyResponseTimeout))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting read/write timeout to '{chocolateyResponseTimeout}'");
                int.TryParse(chocolateyResponseTimeout, out var responseTimeoutInt);
                if (responseTimeoutInt <= 0)
                {
                    responseTimeoutInt = 300000;
                }

                request.ReadWriteTimeout = responseTimeoutInt;
            }

            // http://stackoverflow.com/questions/518181/too-many-automatic-redirections-were-attempted-error-message-when-using-a-httpw
            request.CookieContainer = new CookieContainer();
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting the UserAgent to '{userAgent}'");
                request.UserAgent = userAgent;
            }

            var requestHeadersDebugInfo = new StringBuilder().AppendLine("Request headers:");
            var requestHeaders = request.Headers.AllKeys;
            foreach (var header in requestHeaders)
            {
                var value = request.Headers.Get(header)?.ToString();
                requestHeadersDebugInfo.AppendLine(
                    !string.IsNullOrWhiteSpace(value)
                        ? $"  {header}={value}"
                        : $"  {header}");
            }

            PSHelper.WriteDebug(_cmdlet, requestHeadersDebugInfo.ToString());

            var headers = new Hashtable();

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                if (response == null)
                {
                    PSHelper.WriteWarning(_cmdlet, $"No response from server at '{uri}'.");
                    return headers;
                }

                var responseHeadersDebugInfo = new StringBuilder().AppendLine("Response Headers:");
                var responseHeaders = response.Headers.AllKeys;
                foreach (var header in responseHeaders)
                {
                    var value = response.Headers.Get(header)?.ToString();
                    responseHeadersDebugInfo.AppendLine(
                        !string.IsNullOrWhiteSpace(value)
                            ? $"  {header}={value}"
                            : $"  {header}");

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        headers.Add(header, value);
                    }
                }

                OnSuccessfulWebRequest(url, downloadUrl);
            }
            catch (Exception ex)
            {
                if (request != null)
                {
                    request.ServicePoint.MaxIdleTime = 0;
                    request.Abort();
                    GC.Collect();
                }

                throw new RuntimeException(
                    string.Format(
                        "The remote file either doesn't exist, is unauthorized, or is forbidden for url '{0}'. {1}  {2}",
                        uri,
                        Environment.NewLine,
                        ex.Message),
                    ex);
            }
            finally
            {
                response?.Close();
            }

            return headers;
        }

        protected virtual void OnSuccessfulWebRequest(string url, string downloadUrl)
        {
        }

        protected void GetWebFile(string url, string fileName, Hashtable options)
        {
            GetWebFile(url, fileName, userAgent: WebResources.DefaultUserAgent, options);
        }

        protected void GetWebFile(string url, string fileName, string userAgent, Hashtable options)
        {
            if (string.IsNullOrEmpty(url)) return;

            fileName = PSHelper.GetFullPath(_cmdlet, fileName);

            var downloadUrl = GetDownloadUrl(url, writeWarning: true);
            var uri = new Uri(downloadUrl);

            if (uri.IsFile)
            {
                PSHelper.WriteDebug(_cmdlet, "Url is local file, setting destination.");
                if (!PSHelper.IsEqual(uri.LocalPath, fileName))
                {
                    PSHelper.CopyFile(_cmdlet, uri.LocalPath, fileName, true);
                }

                return;
            }

            var request = (HttpWebRequest)WebRequest.Create(uri);
            var webClient = new WebClient();
            var defaultCredentials = CredentialCache.DefaultCredentials;
            if (defaultCredentials != null)
            {
                request.Credentials = defaultCredentials;
                webClient.Credentials = defaultCredentials;
            }

            var proxy = ProxySettings.GetProxy(_cmdlet, uri);
            if (proxy != null)
            {
                request.Proxy = proxy;
            }

            request.Accept = "*/*";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 20;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 30000;
            var chocolateyRequestTimeout = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyRequestTimeout);
            if (!(string.IsNullOrWhiteSpace(chocolateyRequestTimeout)))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting request timeout to '{chocolateyRequestTimeout}'");
                int.TryParse(chocolateyRequestTimeout, out var requestTimeoutInt);
                if (requestTimeoutInt <= 0)
                {
                    requestTimeoutInt = 30000;
                }

                request.Timeout = requestTimeoutInt;
            }

            var chocolateyResponseTimeout = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyResponseTimeout);
            if (!string.IsNullOrWhiteSpace(chocolateyResponseTimeout))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting read/write timeout to '{chocolateyResponseTimeout}'");
                int.TryParse(chocolateyResponseTimeout, out var responseTimeoutInt);
                if (responseTimeoutInt <= 0)
                {
                    responseTimeoutInt = 300000;
                }

                request.ReadWriteTimeout = responseTimeoutInt;
            }

            // http://stackoverflow.com/questions/518181/too-many-automatic-redirections-were-attempted-error-message-when-using-a-httpw
            request.CookieContainer = new CookieContainer();
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting the UserAgent to '{userAgent}'");
                request.UserAgent = userAgent;
            }

            if (options != null && options.Count != 0 && options.ContainsKey("Headers"))
            {
                var headers = options["Headers"] as Hashtable;
                if (headers?.Count > 0)
                {
                    PSHelper.WriteDebug(_cmdlet, "Setting custom headers");
                    SetRequestFields(_cmdlet, request, headers);
                }
            }

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();

                if (response == null)
                {
                    PSHelper.WriteWarning(_cmdlet, $"No response from server at '{uri}'.");
                    return;
                }

                var binaryIsTextCheckFile = fileName + ".istext";

                if (PSHelper.ItemExists(_cmdlet, binaryIsTextCheckFile))
                {
                    try
                    {
                        PSHelper.RemoveItem(_cmdlet, binaryIsTextCheckFile);
                    }
                    catch (Exception e)
                    {
                        PSHelper.WriteWarning(_cmdlet, $"Unable to remove .istext file: {e.Message}");
                    }
                }

                try
                {
                    var contentType = response.Headers["Content-Type"] ?? string.Empty;
                    if (IsPlainTextOrHtml(contentType))
                    {
                        var name = PSHelper.GetFileName(fileName);
                        var message = $"'{name}' has content type '{contentType}'";
                        PSHelper.WriteWarning(_cmdlet, message);
                        PSHelper.SetContent(_cmdlet, binaryIsTextCheckFile, message, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    PSHelper.WriteDebug(_cmdlet, $"Error getting content type - {ex.Message}");
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    double goal = response.ContentLength;
                    var goalFormatted = goal.AsFileSizeString();

                    PSHelper.EnsureDirectoryExists(_cmdlet, PSHelper.GetParentDirectory(_cmdlet, fileName));

                    using (var reader = GetDownloadStream(response.GetResponseStream()))
                    using (var writer = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[ChunkSize];

                        double total = 0;
                        int count = 0;
                        int iterationLoop = 0;

                        var progress = new ProgressRecord(0, $"Downloading '{uri}' to '{fileName}'", "Preparing to save the file.")
                        {
                            PercentComplete = 0
                        };

                        do
                        {
                            iterationLoop++;
                            count = reader.Read(buffer, 0, buffer.Length);
                            writer.Write(buffer, 0, count);
                            total += count;

                            if (goal > 0 && iterationLoop % 10 == 0)
                            {
                                var progressPercentage = total / goal * 100;

                                progress.StatusDescription = $"Saving {total.AsFileSizeString()} of {goalFormatted}";
                                progress.PercentComplete = (int)progressPercentage;
                                _cmdlet.WriteProgress(progress);
                            }
                        }
                        while (count > 0);

                        progress.Activity = $"Completed download of '{uri}'.";
                        progress.StatusDescription = $"Completed download of '{PSHelper.GetFileName(fileName)}' ({goalFormatted}).";
                        progress.PercentComplete = 100;

                        _cmdlet.WriteProgress(progress);

                        writer.Flush();

                        PSHelper.WriteHost(_cmdlet, "");
                        PSHelper.WriteHost(_cmdlet, $"Download of '{PSHelper.GetFileName(fileName)}' ({goalFormatted}) completed.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (request != null)
                {
                    request.ServicePoint.MaxIdleTime = 0;
                    request.Abort();
                }

                PSHelper.SetExitCode(_cmdlet, 404);
                throw new RuntimeException($"The remote file either doesn't exist, is unauthorized, or is forbidden for url '{uri}'. \n  {ex.Message}", ex);
            }
            finally
            {
                response?.Close();
            }
        }

        private void SetRequestFields(PSCmdlet cmdlet, HttpWebRequest request, Hashtable headers)
        {
            foreach (DictionaryEntry header in headers)
            {
                var key = PSHelper.ConvertTo<string>(header.Key);
                var value = PSHelper.ConvertTo<string>(header.Value);
                PSHelper.WriteDebug(cmdlet, $" * {key}={value}");

                switch (key.ToLower())
                {
                    case "accept":
                        request.Accept = value;
                        break;
                    case "referer":
                        request.Referer = value;
                        break;
                    case "cookie":
                        if (request.CookieContainer is null)
                        {
                            request.CookieContainer = new CookieContainer();
                        }

                        request.CookieContainer.SetCookies(request.RequestUri, value);
                        break;
                    case "useragent":
                        request.UserAgent = value;
                        break;
                    default:
                        request.Headers.Add(key, value);
                        break;
                }
            }
        }

        protected virtual void CheckVirusEngineResults(string url, string fileFullPath)
        {
        }

        /// <summary>
        /// Extension point to allow rewrapping the request stream for downloads in downstream code.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected virtual Stream GetDownloadStream(Stream reader)
        {
            return reader;
        }

        /// <summary>
        /// Extension point to allow altering the chunk size used during downloads.
        /// </summary>
        protected virtual int ChunkSize { get; set; } = 1048576; // 1MB

        protected string GetDownloadUrl(string url)
        {
            return GetDownloadUrl(url, writeWarning: false);
        }

        /// <summary>
        /// Extension point to allow overriding the download URL in downstream code.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        protected virtual string GetDownloadUrl(string url, bool writeWarning)
        {
            return url;
        }

        protected bool IsPlainTextOrHtml(string contentType)
        {
            return contentType.Contains("text/html") || contentType.Contains("text/plain");
        }
    }
}
