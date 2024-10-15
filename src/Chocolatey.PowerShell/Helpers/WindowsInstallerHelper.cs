using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Helpers
{
    public class WindowsInstallerHelper
    {
        public static void Install(
            PSCmdlet cmdlet,
            string packageName,
            string file,
            string file64,
            string fileType,
            string[] silentArguments,
            bool useOnlySilentArguments,
            int[] validExitCodes,
            CancellationToken cancellationToken)
        {
            var silentArgs = string.Join(" ", silentArguments);

            string bitnessMessage = string.Empty;

            var filePath = file;
            if (ArchitectureWidth.Matches(32) || EnvironmentHelper.GetVariable(EnvironmentVariables.ChocolateyForceX86).ToLower() == "true")
            {
                if (!PSHelper.ConvertTo<bool>(file))
                {
                    // TODO: Replace RuntimeException
                    cmdlet.ThrowTerminatingError(new RuntimeException($"32-bit installation is not supported for {packageName}").ErrorRecord);
                }

                if (PSHelper.ConvertTo<bool>(file64))
                {
                    bitnessMessage = "32-bit ";
                }
            }
            else if (PSHelper.ConvertTo<bool>(file64))
            {
                filePath = file64;
                bitnessMessage = "64-bit ";
            }

            if (string.IsNullOrEmpty(filePath))
            {
                // TODO: Replace RuntimeException
                cmdlet.ThrowTerminatingError(new RuntimeException("Package parameters incorrect, either File or File64 must be specified.").ErrorRecord);
            }

            PSHelper.WriteHost(cmdlet, $"Installing {bitnessMessage}{packageName}...");

            if (string.IsNullOrEmpty(fileType))
            {
                PSHelper.WriteDebug(cmdlet, "No FileType supplied. Using the file extension to determine FileType");
                fileType = Path.GetExtension(filePath).Replace(".", string.Empty);
            }

            if (!IsKnownInstallerType(fileType))
            {
                PSHelper.WriteWarning(cmdlet, $"FileType '{fileType}' is unrecognised, using 'exe' instead.");
                fileType = "exe";
            }

            EnvironmentHelper.SetVariable(EnvironmentVariables.ChocolateyInstallerType, fileType);

            var additionalInstallArgs = EnvironmentHelper.GetVariable(EnvironmentVariables.ChocolateyInstallArguments);
            if (additionalInstallArgs == null)
            {
                additionalInstallArgs = string.Empty;
            }
            else
            {
                if (_installDirectoryRegex.IsMatch(additionalInstallArgs))
                {
                    const string installOverrideWarning = @"
Pro / Business supports a single, ubiquitous install directory option.
 Stop the hassle of determining how to pass install directory overrides
 to install arguments for each package / installer type.
 Check out Pro / Business - https://chocolatey.org/compare
";
                    PSHelper.WriteWarning(cmdlet, installOverrideWarning);
                }
            }

            var overrideArguments = useOnlySilentArguments || PSHelper.ConvertTo<bool>(EnvironmentHelper.GetVariable(EnvironmentVariables.ChocolateyInstallOverride));

            // remove \chocolatey\chocolatey\
            // might be a slight issue here if the download path is the older
            silentArgs = silentArgs.Replace(@"\chocolatey\chocolatey\", @"\chocolatey\");
            additionalInstallArgs = additionalInstallArgs.Replace(@"\chocolatey\chocolatey\", @"\chocolatey\");

            var updatedFilePath = filePath.Replace(@"\chocolatey\chocolatey\", @"\chocolatey\");
            if (PSHelper.ItemExists(cmdlet, updatedFilePath))
            {
                filePath = updatedFilePath;
            }

            var ignoreFile = filePath + ".ignore";
            var chocolateyInstall = EnvironmentHelper.GetVariable(EnvironmentVariables.ChocolateyInstall);
            if (PSHelper.ConvertTo<bool>(chocolateyInstall)
                && Regex.IsMatch(ignoreFile, Regex.Escape(chocolateyInstall)))
            {
                try
                {
                    PSHelper.SetContent(cmdlet, ignoreFile, string.Empty);
                }
                catch
                {
                    PSHelper.WriteWarning(cmdlet, $"Unable to generate '{ignoreFile}");
                }
            }

            var workingDirectory = PSHelper.GetCurrentDirectory(cmdlet);
            try
            {
                workingDirectory = PSHelper.GetParentDirectory(cmdlet, filePath);
            }
            catch
            {
                PSHelper.WriteWarning(cmdlet, $"Unable to set the working directory for installer to location of '{filePath}");
                workingDirectory = EnvironmentHelper.GetVariable("TEMP");
            }

            try
            {
                // make sure any logging folder exists
                foreach (var argString in new[] { silentArgs, additionalInstallArgs })
                {
                    foreach (Match match in _pathRegex.Matches(argString))
                    {
                        var directory = match.Groups[1]?.Value;
                        if (string.IsNullOrEmpty(directory))
                        {
                            continue;
                        }

                        directory = PSHelper.GetParentDirectory(cmdlet, PSHelper.GetFullPath(cmdlet, directory));
                        PSHelper.WriteDebug(cmdlet, $"Ensuring {directory} exists");
                        PSHelper.EnsureDirectoryExists(cmdlet, directory);
                    }
                }
            }
            catch (Exception ex)
            {
                PSHelper.WriteDebug(cmdlet, $"Error ensuring directories exist - {ex.Message}");
            }

            string args;
            if (PSHelper.ConvertTo<bool>(overrideArguments))
            {
                PSHelper.WriteHost(cmdlet, $"Overriding package arguments with '{additionalInstallArgs}' (replacing '{silentArgs}')");
                args = additionalInstallArgs;
            }
            else
            {
                args = $"{silentArgs} {additionalInstallArgs}";
            }

            if (PSHelper.IsEqual(fileType, "msi"))
            {
                InstallMsi(cmdlet, filePath, workingDirectory, args, validExitCodes, cancellationToken);
            }
            else if (PSHelper.IsEqual(fileType, "msp"))
            {
                InstallMsp(cmdlet, filePath, workingDirectory, args, validExitCodes, cancellationToken);
            }
            else if (PSHelper.IsEqual(fileType, "msu"))
            {
                InstallMsu(cmdlet, filePath, workingDirectory, args, validExitCodes, cancellationToken);
            }
            else if (PSHelper.IsEqual(fileType, "exe"))
            {
                RunInstaller(cmdlet, filePath, workingDirectory, args, validExitCodes, cancellationToken);
            }

            PSHelper.WriteHost(cmdlet, $"{packageName} has been installed");
        }

        private static void RunInstaller(
            PSCmdlet cmdlet,
            string filePath,
            string workingDirectory,
            string arguments,
            int[] validExitCodes,
            CancellationToken cancellationToken)
        {
            var helper = new StartChocolateyProcessHelper(cmdlet, cancellationToken, filePath);
            var exitCode = helper.Start(arguments, workingDirectory, validExitCodes);
            EnvironmentHelper.SetVariable(EnvironmentVariables.ChocolateyExitCode, exitCode.ToString());
        }

        private static void InstallMsu(
            PSCmdlet cmdlet,
            string filePath,
            string workingDirectory,
            string arguments,
            int[] validExitCodes,
            CancellationToken cancellationToken)
        {
            var msuArgs = $"\"{filePath}\" {arguments}";

            var wusaExe = PSHelper.CombinePaths(cmdlet, EnvironmentHelper.GetVariable("SystemRoot"), @"System32\wusa.exe");
            RunInstaller(cmdlet, wusaExe, workingDirectory, msuArgs, validExitCodes, cancellationToken);
        }

        private static void InstallMsi(
            PSCmdlet cmdlet,
            string filePath,
            string workingDirectory,
            string arguments,
            int[] validExitCodes,
            CancellationToken cancellationToken)
        {
            var msiArgs = $"/i \"{filePath}\" {arguments}";
            RunMicrosoftInstaller(cmdlet, msiArgs, workingDirectory, validExitCodes, cancellationToken);
        }

        private static void InstallMsp(
            PSCmdlet cmdlet,
            string filePath,
            string workingDirectory,
            string arguments,
            int[] validExitCodes,
            CancellationToken cancellationToken)
        {
            var mspArgs = $"/update \"{filePath}\" {arguments}";
            RunMicrosoftInstaller(cmdlet, mspArgs, workingDirectory, validExitCodes, cancellationToken);
        }

        private static void RunMicrosoftInstaller(
            PSCmdlet cmdlet,
            string args,
            string workingDirectory,
            int[] validExitCodes,
            CancellationToken cancellationToken)
        {
            var msiExe = PSHelper.CombinePaths(cmdlet, EnvironmentHelper.GetVariable("SystemRoot"), @"System32\msiexec.exe");
            RunInstaller(cmdlet, msiExe, workingDirectory, args, validExitCodes, cancellationToken);
        }

        private const string PathPattern = @"(?:['""])(([a-zA-Z]:|\.)\\[^'""]+)(?:[""'])|(([a-zA-Z]:|\.)\\[\S]+)";

        private static readonly Regex _pathRegex = new Regex(PathPattern, RegexOptions.Compiled);

        private const string InstallDirectoryArgumentPattern = "INSTALLDIR|TARGETDIR|dir=|/D=";

        private static readonly Regex _installDirectoryRegex = new Regex(InstallDirectoryArgumentPattern, RegexOptions.Compiled);

        private static bool IsKnownInstallerType(string type)
        { 
            switch (type.ToLower())
            {
                case "msi":
                case "msu":
                case "exe":
                case "msp":
                    return true;
                default:
                    return false;
            };
        }
    }
}
