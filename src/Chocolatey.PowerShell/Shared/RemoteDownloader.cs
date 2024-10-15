using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Environment = System.Environment;

namespace chocolatey.licensed.infrastructure.app.commandresources
{
    public class RemoteDownloader
    {
        private readonly PSCmdlet _cmdlet;

        public RemoteDownloader(PSCmdlet cmdlet)
        {
            _cmdlet = cmdlet;
        }

        public string GetRemoteFileName(string url, string userAgent, string defaultName, PSHost host)
        {
            var originalFileName = defaultName;

            if (string.IsNullOrWhiteSpace(url))
            {
                PSHelper.WriteDebug(_cmdlet, "Url was null, using the default name");
                return defaultName;
            }

            var uri = new Uri(url);
            var fileName = string.Empty;

            if (uri.IsFile)
            {
                fileName = PSHelper.GetFileName(uri.LocalPath);
                PSHelper.WriteDebug(_cmdlet, "Url is local file, returning fileName.");
                return fileName;
            }

            var request = (HttpWebRequest)WebRequest.Create(url);

            if (request == null)
            {
                PSHelper.WriteDebug(_cmdlet, "Request was null, using the default name.");
                return defaultName;
            }

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
            var chocolateyRequestTimeout = Environment.GetEnvironmentVariable("ChocolateyRequestTimeout");
            if (!(string.IsNullOrWhiteSpace(chocolateyRequestTimeout)))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting request timeout to '{chocolateyRequestTimeout}'");
                var requestTimeoutInt = -1;
                int.TryParse(chocolateyRequestTimeout, out requestTimeoutInt);
                if (requestTimeoutInt <= 0)
                {
                    requestTimeoutInt = 30000;
                }

                request.Timeout = requestTimeoutInt;
            }

            var chocolateyResponseTimeout = Environment.GetEnvironmentVariable("ChocolateyResponseTimeout");
            if (!(string.IsNullOrWhiteSpace(chocolateyResponseTimeout)))
            {
                PSHelper.WriteDebug(_cmdlet, $"Setting read/write timeout to '{chocolateyResponseTimeout}'");
                var responseTimeoutInt = -1;
                int.TryParse(chocolateyResponseTimeout, out responseTimeoutInt);
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

            var containsABadCharacter = new Regex("[" + Regex.Escape(string.Join("", Path.GetInvalidFileNameChars())) + "\\=\\;]");

            //var manager = new BitsManager();
            //manager.EnumJobs(JobOwner.CurrentUser);

            //var newJob = manager.CreateJob("TestJob", JobType.Download);

            //string remoteFile = @"http://www.pdrrelaunch.com/img/New Text Document.txt";
            //string localFile = @"C:\temp\Test Folder\New Text Document.txt";
            //newJob.AddFile(remoteFile, localFile);
            //newJob.Resume();

            //var downloadManager = new SharpBits.Base.BitsManager();
            //var job = downloadManager.CreateJob("name", JobType.Download);
            //job.AddCredentials(new BitsCredentials {});

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                if (response is null)
                {
                    PSHelper.WriteWarning(_cmdlet, "Response was null, using the default name.");
                    return defaultName;
                }

                var header = response.Headers["Content-Disposition"];
                var headerLocation = response.Headers["Location"];

                // start with content-disposition header
                if (!string.IsNullOrWhiteSpace(header))
                {
                    var fileHeaderName = "filename=";
                    var index = header.LastIndexOf(fileHeaderName, StringComparison.OrdinalIgnoreCase);
                    if (index > -1)
                    {
                        PSHelper.WriteDebug(_cmdlet, $"Using header 'Content-Disposition' ({header}) to determine file name.");
                        fileName = header.Substring(index + fileHeaderName.Length).Replace("\"", string.Empty);
                    }
                }
                if (containsABadCharacter.IsMatch(fileName)) fileName = string.Empty;

                // If empty, check location header next
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    if (!string.IsNullOrWhiteSpace(headerLocation))
                    {
                        PSHelper.WriteDebug(_cmdlet, $"Using header 'Location' ({headerLocation}) to determine file name.");
                        fileName = PSHelper.GetFileName(headerLocation);
                    }
                }
                if (containsABadCharacter.IsMatch(fileName)) fileName = string.Empty;

                // Next comes using the response url value
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    var responseUrl = response.ResponseUri?.ToString() ?? string.Empty;
                    if (!responseUrl.Contains("?"))
                    {
                        PSHelper.WriteDebug(_cmdlet, $"Using response url to determine file name ('{responseUrl}').");
                        fileName = PSHelper.GetFileName(responseUrl);
                    }
                }

                if (containsABadCharacter.IsMatch(fileName))
                {
                    fileName = string.Empty;
                }

                // Next comes using the request url value
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    var requestUrl = url;
                    var extension = Path.GetExtension(requestUrl);
                    if (!requestUrl.Contains("?") && !string.IsNullOrWhiteSpace(extension))
                    {
                        PSHelper.WriteDebug(_cmdlet, $"Using request url to determine file name ('{requestUrl}').");
                        fileName = PSHelper.GetFileName(requestUrl);
                    }
                }

                // when all else fails, default the name
                if (string.IsNullOrWhiteSpace(fileName) || containsABadCharacter.IsMatch(fileName))
                {
                    PSHelper.WriteDebug(_cmdlet, $"File name is null or illegal. Using the default name '{originalFileName}' instead.");
                    fileName = defaultName;
                }

                PSHelper.WriteDebug(_cmdlet, $"File name determined from url is '{fileName}'");

                return fileName;
            }
            catch (Exception ex)
            {
                if (request != null)
                {
                    request.ServicePoint.MaxIdleTime = 0;
                    request.Abort();
                    GC.Collect();
                }

                PSHelper.WriteDebug(
                    _cmdlet,
                    string.Format(
                        "Url request/response failed - file name will be the default name '{0}'. {1}  {2}",
                        originalFileName,
                        Environment.NewLine,
                        ex.Message));

                return defaultName;
            }
            finally
            {
                response?.Close();
            }
        }

        public void DownloadFile(string url, string filePath, string userAgent)
        {
            DownloadFile(url, filePath, userAgent, showProgress: true);
        }

        // this is different than GetWebFileCmdlet - resolve differences before setting it up.
        public void DownloadFile(string url, string filePath, string userAgent, bool showProgress)
        {
            var uri = new Uri(url);
            var request = (HttpWebRequest)WebRequest.Create(url);
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
            // 30 seconds
            request.Timeout = 30000;
            // 45 minutes
            request.ReadWriteTimeout = 2700000;

            // http://stackoverflow.com/questions/518181/too-many-automatic-redirections-were-attempted-error-message-when-using-a-httpw
            request.CookieContainer = new CookieContainer();
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                _cmdlet.WriteDebug("Setting the UserAgent to '{userAgent}'");
                request.UserAgent = userAgent;
            }

            var fileSystem = new DotNetFileSystem();

            try
            {
                var downloadDirectory = fileSystem.GetDirectoryName(filePath);
                fileSystem.EnsureDirectoryExists(downloadDirectory);
            }
            catch (Exception ex)
            {
                this.Log().Debug("Error creating directory for '{0}': {1}".FormatWith(filePath, ex.Message));
            }

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();

                if (response == null)
                {
                    this.Log().Warn(() => "No response from server at '{0}'.".FormatWith(url));
                    return;
                }

                try
                {
                    var contentType = response.Headers["Content-Type"];
                    if (contentType.ContainsSafe("text/html") || contentType.ContainsSafe("text/plain"))
                    {
                        this.Log().Warn("'{0}' is of content type '{1}'".FormatWith(fileSystem.GetFileName(filePath), contentType.ToStringSafe()));
                        fileSystem.WriteFile(filePath + ".istext", "{0} has content type {1}".FormatWith(fileSystem.GetFileName(filePath), contentType.ToStringSafe()), Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    this.Log().Debug("Error getting content type - {0}".FormatWith(ex.Message));
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    double goal = response.ContentLength;
                    var goalFormatted = FormatFileSize(goal);

                    var reader = response.GetResponseStream();

                    fileSystem.EnsureDirectoryExists(fileSystem.GetDirectoryName(filePath));

                    var writer = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    var buffer = new byte[1048576]; // 1MB

                    double total = 0;
                    int count = 0;
                    int iterationLoop = 0;

                    //todo: clean up with http://stackoverflow.com/a/955947/18475
                    do
                    {
                        iterationLoop++;
                        count = reader.Read(buffer, 0, buffer.Length);
                        writer.Write(buffer, 0, count);
                        total += count;

                        if (!showProgress)
                        {
                            continue;
                        }

                        if (total != goal && goal > 0 && iterationLoop % 10 == 0)
                        {
                            var progressPercentage = (total / goal * 100);

                            // http://stackoverflow.com/a/888569/18475
                            Console.Write("\rDownloading: {0}% - {1}".FormatWith(progressPercentage.ToString("n2"), "Saving {0} of {1}.".FormatWith(FormatFileSize(total), goalFormatted, total, goal)).PadRight(Console.WindowWidth));
                        }

                        if (total == goal)
                        {
                            Console.Write("\rDownloading: 100% - {0}".FormatWith(goalFormatted).PadRight(Console.WindowWidth));
                        }
                    }
                    while (count > 0);

                    reader.Close();
                    writer.Flush();
                    writer.Close();
                    reader.Dispose();
                    writer.Dispose();

                    this.Log().Info("");
                    this.Log().Info(() => "Download of '{0}' completed.".FormatWith(fileSystem.GetFileName(filePath)));
                }
            }
            catch (Exception ex)
            {
                if (request != null)
                {
                    request.ServicePoint.MaxIdleTime = 0;
                    request.Abort();
                }

                throw new Exception("The remote file either doesn't exist, is unauthorized, or is forbidden for url '{0}'. {1}  {2}".FormatWith(url, Environment.NewLine, ex.Message));
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
        }

        private string FormatFileSize(double size)
        {
            IList<string> units = new List<string>(new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB" });
            foreach (var unit in units)
            {
                if (size < 1024)
                {
                    return string.Format("{0:0.##} {1}", size, unit);
                }

                size /= 1024;
            }

            return string.Format("{0:0.##} YB", size);
        }
    }
}
