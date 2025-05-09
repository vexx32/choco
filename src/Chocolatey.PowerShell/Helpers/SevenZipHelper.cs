// Copyright © 2017 - 2025 Chocolatey Software, Inc
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
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Chocolatey.PowerShell.Shared;
using PackageVariables = chocolatey.StringResources.EnvironmentVariables.Package;
using SystemVariables = chocolatey.StringResources.EnvironmentVariables.System;

namespace Chocolatey.PowerShell.Helpers
{
    public sealed class SevenZipHelper : ProcessHandler
    {
        private readonly StringBuilder _zipFileList = new StringBuilder();
        private string _destinationFolder = string.Empty;
        private const string ErrorMessageAddendum = "This is most likely an issue with the '{0}' package and not with Chocolatey itself. Please follow up with the package maintainer(s) directly.";

        public SevenZipHelper(PSCmdlet cmdlet, CancellationToken pipelineStopToken)
            : base(cmdlet, pipelineStopToken)
        {
            ProcessOutputReceived += (sender, output) =>
            {
                if (output.Message.StartsWith("- "))
                {
                    _zipFileList.AppendLine(_destinationFolder + '\\' + output.Message.Substring(2));
                }
            };
        }

        /// <summary>
        /// Run 7-Zip to extract files to the <paramref name="destination"/> directory.
        /// </summary>
        /// <param name="path">The path to the archive to extract.</param>
        /// <param name="path64">The path to the archive to extract for 64-bit architecture specifically.</param>
        /// <param name="packageName">The package that is being currently installed. Will default to <c>ChocolateyPackageName</c> environment variable if not provided.</param>
        /// <param name="destination">The destination directory to extract files to.</param>
        /// <param name="filesToExtract">A path or glob pattern to filter the files extracted from the archive.</param>
        /// <param name="disableLogging">If true, disables logging output to the console while extracting files. Logging</param>
        /// <returns>The destination path where the files were extracted to.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="SevenZipException"></exception>
        public string ExtractFiles(string path, string path64, string packageName, string destination, string filesToExtract, bool disableLogging)
        {
            if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(path64))
            {
                throw new ArgumentException("Parameters are incorrect; either -Path or -Path64 must be specified.");
            }

            var bitnessMessage = string.Empty;
            var zipFilePath = path;
            packageName = string.IsNullOrEmpty(packageName)
                ? Environment.GetEnvironmentVariable(PackageVariables.ChocolateyPackageName)
                : packageName;
            var zipExtractionLogPath = string.Empty;

            var forceX86 = PSHelper.IsEqual(Environment.GetEnvironmentVariable(PackageVariables.ChocolateyForceX86), "true");
            if (ArchitectureWidth.Matches(32) || forceX86)
            {
                if (string.IsNullOrEmpty(path))
                {
                    Cmdlet.ThrowTerminatingError(new RuntimeException($"32-bit archive is not supported for {packageName}").ErrorRecord);
                }

                if (!string.IsNullOrEmpty(path64))
                {
                    bitnessMessage = "32-bit ";
                }
            }
            else if (!string.IsNullOrEmpty(path64))
            {
                zipFilePath = path64;
                bitnessMessage = "64 bit ";
            }

            if (!string.IsNullOrEmpty(packageName))
            {
                var libPath = Environment.GetEnvironmentVariable(PackageVariables.ChocolateyPackageFolder);
                if (!PSHelper.ContainerExists(Cmdlet, libPath))
                {
                    PSHelper.NewDirectory(Cmdlet, libPath);
                }

                zipExtractionLogPath = PSHelper.CombinePaths(Cmdlet, libPath, $"{PSHelper.GetFileName(zipFilePath)}.txt");
            }

            var envChocolateyPackageName = Environment.GetEnvironmentVariable(PackageVariables.ChocolateyPackageName);
            var envChocolateyInstallDirectoryPackage = Environment.GetEnvironmentVariable(PackageVariables.ChocolateyInstallDirectoryPackage);

            if (!string.IsNullOrEmpty(envChocolateyPackageName) && PSHelper.IsEqual(envChocolateyPackageName, envChocolateyInstallDirectoryPackage))
            {
                Cmdlet.WriteWarning("Install Directory override not available for zip packages at this time. If this package also runs a native installer using Chocolatey functions, the directory will be honored.");
            }

            PSHelper.WriteHost(Cmdlet, $"Extracting {bitnessMessage}{zipFilePath} to {destination}...");

            PSHelper.EnsureDirectoryExists(Cmdlet, destination);

            var exePath = PSHelper.CombinePaths(Cmdlet, PSHelper.GetInstallLocation(Cmdlet), "tools", "7z.exe");

            if (!PSHelper.ItemExists(Cmdlet, exePath))
            {
                EnvironmentHelper.UpdateSession(Cmdlet);
                exePath = PSHelper.CombinePaths(Cmdlet, EnvironmentHelper.GetVariable(SystemVariables.ChocolateyInstall), @"tools\7zip.exe");
            }

            exePath = PSHelper.GetUnresolvedPath(Cmdlet, exePath);

            if (!PSHelper.ItemExists(Cmdlet, exePath))
            {
                throw new FileNotFoundException("Could not locate the 7z.exe or 7zip.exe executables.", exePath);
            }

            Cmdlet.WriteDebug($"7zip found at '{exePath}'");

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
            if (string.IsNullOrEmpty(workingDirectory))
            {
                Cmdlet.WriteDebug("Unable to use current location for Working Directory. Using Cache Location instead.");
                workingDirectory = EnvironmentHelper.GetVariable("TEMP");
            }

            var loggingOption = disableLogging ? "-bb0" : "-bb1";

            var options = $"x -aoa -bd {loggingOption} -o\"{destination}\" -y \"{zipFilePath}\"";
            if (!string.IsNullOrEmpty(filesToExtract))
            {
                options += $" \"{filesToExtract}\"";
            }

            Cmdlet.WriteDebug($"Executing command ['{exePath}' {options}]");

            _destinationFolder = destination;

            var exitCode = StartProcess(exePath, workingDirectory, options, sensitiveStatements: null, elevated: false, ProcessWindowStyle.Hidden, noNewWindow: true);

            PSHelper.SetExitCode(Cmdlet, exitCode);

            if (!(string.IsNullOrEmpty(zipExtractionLogPath) || disableLogging))
            {
                PSHelper.SetContent(Cmdlet, zipExtractionLogPath, _zipFileList.ToString(), Encoding.UTF8);
            }

            Cmdlet.WriteDebug($"7z exit code: {exitCode}");

            if (exitCode != 0)
            {
                var error = GetExitCodeException(exitCode);
                var disclaimer = string.Format(ErrorMessageAddendum, EnvironmentHelper.GetVariable(PackageVariables.ChocolateyPackageName));

                throw new SevenZipException($"{error.Message} {disclaimer}", error);
            }

            EnvironmentHelper.SetVariable(PackageVariables.ChocolateyPackageInstallLocation, destination);

            return destination;
        }

        private Exception GetExitCodeException(int exitCode)
        {
            switch (exitCode)
            {
                case 1:
                    return new ApplicationFailedException($"Some files could not be extracted. (Code {exitCode})");
                case 2:
                    return new ApplicationException($"7-Zip encountered a fatal error while extracting the files. (Code {exitCode})");
                case 7:
                    return new ArgumentException($"7-Zip command line error. (Code {exitCode})");
                case 8:
                    return new OutOfMemoryException($"7-Zip exited with an out of memory error. (Code {exitCode})");
                case 255:
                    return new OperationCanceledException($"7-Zip extraction was cancelled by the user. (Code {exitCode})");
                default:
                    return new Exception($"7-Zip exited with an unknown error. (Code {exitCode})");
            };
        }
    }
}
