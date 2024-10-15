using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Helpers
{
    public sealed class SevenZipHelper : ProcessHandler
    {
        private readonly StringBuilder _zipFileList = new StringBuilder();
        private string _destinationFolder = string.Empty;

        private const string ErrorMessageAddendum = "This is most likely an issue with the '$env:chocolateyPackageName' package and not with Chocolatey itself. Please follow up with the package maintainer(s) directly.";

        public SevenZipHelper(PSCmdlet cmdlet, CancellationToken pipelineStopToken)
            : base(cmdlet, pipelineStopToken)
        {
        }

        public int Run7zip(string path, string path64, string packageName, string destination, string specificFolder, bool disableLogging)
        {
            if (path is null && path64 is null)
            {
                throw new ArgumentException("Parameters are incorrect; either -Path or -Path64 must be specified.");
            }

            var bitnessMessage = string.Empty;
            var zipFilePath = path;
            packageName = string.IsNullOrEmpty(packageName)
                ? Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyPackageName)
                : packageName;
            var zipExtractionLogPath = string.Empty;

            var forceX86 = PSHelper.IsEqual(Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyForceX86), "true");
            if (ArchitectureWidth.Matches(32) || forceX86)
            {
                if (PSHelper.ConvertTo<bool>(path))
                {
                    Cmdlet.ThrowTerminatingError(new RuntimeException($"32-bit archive is not supported for {packageName}").ErrorRecord);
                }

                if (PSHelper.ConvertTo<bool>(path64))
                {
                    bitnessMessage = "32-bit ";
                }
            }
            else if (PSHelper.ConvertTo<bool>(path64))
            {
                zipFilePath = path64;
                bitnessMessage = "64 bit ";
            }

            if (PSHelper.ConvertTo<bool>(packageName))
            {
                var libPath = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyPackageFolder);
                if (!PSHelper.ContainerExists(Cmdlet, libPath))
                {
                    PSHelper.NewDirectory(Cmdlet, libPath);
                }

                zipExtractionLogPath = PSHelper.CombinePaths(Cmdlet, libPath, $"{PSHelper.GetFileName(zipFilePath)}.txt");
            }

            var envChocolateyPackageName = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyPackageName);
            var envChocolateyInstallDirectoryPackage = Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyInstallDirectoryPackage);

            if (PSHelper.ConvertTo<bool>(envChocolateyPackageName) && envChocolateyPackageName == envChocolateyInstallDirectoryPackage)
            {
                PSHelper.WriteWarning(Cmdlet, "Install Directory override not available for zip packages at this time.\n If this package also runs a native installer using Chocolatey\n functions, the directory will be honored.");
            }

            PSHelper.WriteHost(Cmdlet, $"Extracting {bitnessMessage}{zipFilePath} to {destination}...");

            PSHelper.EnsureDirectoryExists(Cmdlet, destination);

            var exePath = PSHelper.CombinePaths(Cmdlet, PSHelper.GetInstallLocation(Cmdlet), "tools", "7z.exe");

            if (!PSHelper.ItemExists(Cmdlet, exePath))
            {
                EnvironmentHelper.UpdateSession(Cmdlet);
                exePath = PSHelper.CombinePaths(Cmdlet, EnvironmentHelper.GetVariable(EnvironmentVariables.ChocolateyInstall), @"tools\7zip.exe");
            }

            exePath = PSHelper.GetFullPath(Cmdlet, exePath);

            PSHelper.WriteDebug(Cmdlet, $"7zip found at '{exePath}'");

            // 32-bit 7z would not find C:\Windows\System32\config\systemprofile\AppData\Local\Temp,
            // because it gets translated to C:\Windows\SysWOW64\... by the WOW redirection layer.
            // Replace System32 with sysnative, which does not get redirected.
            // 32-bit 7z is required so it can see both architectures
            if (ArchitectureWidth.Matches(64))
            {
                var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var sysNativePath = PSHelper.CombinePaths(Cmdlet, EnvironmentHelper.GetVariable("SystemRoot"), "SysNative");
                zipFilePath = PSHelper.Replace(zipFilePath, Regex.Escape(systemPath), sysNativePath);
                destination = PSHelper.Replace(destination, Regex.Escape(systemPath), sysNativePath);
            }

            var workingDirectory = PSHelper.GetCurrentDirectory(Cmdlet);
            if (workingDirectory is null)
            {
                PSHelper.WriteDebug(Cmdlet, "Unable to use current location for Working Directory. Using Cache Location instead.");
                workingDirectory = EnvironmentHelper.GetVariable("TEMP");
            }

            var loggingOption = disableLogging ? "-bb0" : "-bb1";

            var options = $"x -aoa -bd {loggingOption} -o\"{destination}\" -y \"{zipFilePath}\"";
            if (PSHelper.ConvertTo<bool>(specificFolder))
            {
                options += $" \"{specificFolder}\"";
            }

            PSHelper.WriteDebug(Cmdlet, $"Executing command ['{exePath}' {options}]");

            _destinationFolder = destination;

            var exitCode = StartProcess(exePath, workingDirectory, options, sensitiveStatements: null, elevated: false, ProcessWindowStyle.Hidden, noNewWindow: true);

            PSHelper.SetExitCode(Cmdlet, exitCode);

            if (!(string.IsNullOrEmpty(zipExtractionLogPath) || disableLogging))
            {
                PSHelper.SetContent(Cmdlet, zipExtractionLogPath, _zipFileList.ToString(), Encoding.UTF8);
            }

            PSHelper.WriteDebug(Cmdlet, $"7z exit code: {exitCode}");

            if (exitCode != 0)
            {
                var reason = GetExitCodeReason(exitCode);
                // TODO: Replace RuntimeException with more specific exception
                Cmdlet.ThrowTerminatingError(new RuntimeException($"{reason} {ErrorMessageAddendum}").ErrorRecord);
            }

            EnvironmentHelper.SetVariable(EnvironmentVariables.ChocolateyPackageInstallLocation, destination);

            return exitCode;
        }

        private string GetExitCodeReason(int exitCode)
        {
            switch (exitCode)
            {
                case 1:
                    return "Some files could not be extracted.";
                case 2:
                    return "7-Zip encountered a fatal error while extracting the files.";
                case 7:
                    return "7-Zip command line error.";
                case 8:
                    return "7-Zip out of memory.";
                case 255:
                    return "Extraction cancelled by the user.";
                default:
                    return $"7-Zip signalled an unknown error (code {exitCode})";
            };
        }

        protected override void ProcessOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!(e.Data is null))
            {
                var line = e.Data;

                ProcessMessages?.Add((line, false));

                if (line.StartsWith("- "))
                {
                    _zipFileList.AppendLine(_destinationFolder + '\\' + line.Substring(2));
                }
            }
        }
    }
}
