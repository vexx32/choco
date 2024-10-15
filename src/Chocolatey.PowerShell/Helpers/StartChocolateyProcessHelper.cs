using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chocolatey.PowerShell.Helpers
{
    internal class StartChocolateyProcessHelper : ProcessHandler
    {
        const string ErrorId = "StartChocolateyProcessError";

        private static readonly int[] _successExitCodes = { 0, 1605, 1614, 1641, 3010 };

        private string _processName;

        internal StartChocolateyProcessHelper(PSCmdlet cmdlet, CancellationToken pipelineStopToken, string processName = "powershell")
            : base(cmdlet, pipelineStopToken)
        {
            _processName = processName;
        }

        internal int Start(string arguments)
        {
            return Start(arguments, validExitCodes: null);
        }

        internal int Start(string arguments, int[] validExitCodes)
        {
            return Start(arguments, workingDirectory: null, validExitCodes);
        }

        internal int Start(string arguments, string workingDirectory, int[] validExitCodes)
        {
            return Start(workingDirectory, arguments, sensitiveStatements: null, elevated: true, minimized: false, noSleep: false, validExitCodes);
        }

        internal int Start(string workingDirectory, string arguments, string sensitiveStatements, bool elevated, bool minimized, bool noSleep, int[] validExitCodes = null)
        {
            if (validExitCodes is null)
            {
                validExitCodes = new[] { 0 };
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = PSHelper.GetCurrentDirectory(Cmdlet);
                if (string.IsNullOrEmpty(workingDirectory))
                {
                    PSHelper.WriteDebug(Cmdlet, "Unable to use current location for Working Directory. Using Cache Location instead.");
                    workingDirectory = Environment.GetEnvironmentVariable("TEMP");
                }
            }

            var alreadyElevated = ProcessInformation.IsElevated();

            var debugMessagePrefix = elevated ? "Elevating permissions and running" : "Running";

            var processName = NormalizeProcessName(_processName);
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                arguments = arguments.Replace("\0", "");
            }

            if (PSHelper.IsEqual(processName, "powershell"))
            {
                processName = PSHelper.GetPowerShellLocation();
                var installerModulePath = PSHelper.CombinePaths(
                    Cmdlet,
                    PSHelper.GetInstallLocation(Cmdlet),
                    "helpers",
                    "chocolateyInstaller.psm1");
                var importChocolateyHelpers = $"Import-Module -Name '{installerModulePath}' -Verbose:$false | Out-Null";
                var statements = string.Format(PowerShellScriptWrapper, noSleep, importChocolateyHelpers, arguments);

                var encodedStatements = Convert.ToBase64String(Encoding.Unicode.GetBytes(statements));

                arguments = string.Format("-NoLogo -NonInteractive -NoProfile -ExecutionPolicy Bypass -InputFormat Text -OutputFormat Text -EncodedCommand {0}", encodedStatements);
                PSHelper.WriteDebug(Cmdlet, $@"
{debugMessagePrefix} powershell block:
{statements}
This may take a while, depending on the statements.");
            }
            else
            {
                PSHelper.WriteDebug(Cmdlet, $"{debugMessagePrefix} [\"$exeToRun\" {arguments}]. This may take a while, depending on the statements.");
            }

            var exeIsTextFile = PSHelper.GetUnresolvedPath(Cmdlet, processName) + ".istext";
            if (PSHelper.ItemExists(Cmdlet, exeIsTextFile))
            {
                PSHelper.SetExitCode(Cmdlet, 4);
                Cmdlet.ThrowTerminatingError(new ErrorRecord(
                    new InvalidOperationException($"The file was a text file but is attempting to be run as an executable - '{processName}'"),
                    ErrorId,
                    ErrorCategory.InvalidOperation,
                    processName));
            }

            if (PSHelper.IsEqual(processName, "msiexec") || PSHelper.IsEqual(processName, "msiexec.exe"))
            {
                processName = PSHelper.CombinePaths(Cmdlet, Environment.GetEnvironmentVariable("SystemRoot"), "System32\\msiexec.exe");
            }
            else if (!PSHelper.ItemExists(Cmdlet, processName))
            {
                Cmdlet.WriteWarning($"May not be able to find '{processName}'. Please use full path for executables.");
            }


            var windowStyle = minimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
            var exitCode = StartProcess(processName, workingDirectory, arguments, sensitiveStatements, elevated, windowStyle, noNewWindow: false);
            var reason = GetExitCodeReason(exitCode);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                PSHelper.WriteWarning(Cmdlet, reason);
                reason = $"Exit code indicates the following: {reason}";
            }
            else
            {
                reason = "See log for possible error messages.";
            }

            if (!validExitCodes.Contains(exitCode))
            {
                PSHelper.SetExitCode(Cmdlet, exitCode);
                // TODO: Replace RuntimeException with custom exception type
                Cmdlet.ThrowTerminatingError(new RuntimeException($"Running [\"{processName}\" {arguments}] not successful. Exit code was {exitCode}. {reason}").ErrorRecord);
            }
            else if (!_successExitCodes.Contains(exitCode))
            {
                PSHelper.WriteWarning(Cmdlet, $"Exit code '{exitCode}' was considered valid by script, but not as a normal Chocolatey success code. Returning '0'.");
                exitCode = 0;
            }

            return exitCode;
        }

        private const string PowerShellScriptWrapper = @"
$noSleep = ${0}
{1}
try {
    $progressPreference = ""SilentlyContinue""
    {2}

    if (-not $noSleep) {
        Start-Sleep 6
    }
}
catch {
    if (-not $noSleep) {
        Start-Sleep 8
    }

    throw $_
}";

        private string GetExitCodeReason(int exitCode)
        {
            var errorMessageAddendum = $" This is most likely an issue with the '{Environment.GetEnvironmentVariable(EnvironmentVariables.ChocolateyPackageName)}' package and not with Chocolatey itself. Please follow up with the package maintainer(s) directly.";

            switch (exitCode)
            {
                case 0:
                case 1:
                case 3010:
                    return string.Empty;
                // NSIS - http://nsis.sourceforge.net/Docs/AppendixD.html
                // InnoSetup - http://www.jrsoftware.org/ishelp/index.php?topic=setupexitcodes
                case 2:
                    return "Setup was cancelled.";
                case 3:
                    return "A fatal error occurred when preparing or moving to next install phase. Check to be sure you have enough memory to perform an installation and try again.";
                case 4:
                    return "A fatal error occurred during installation process." + errorMessageAddendum;
                case 5:
                    return "User (you) cancelled the installation.'";
                case 6:
                    return "Setup process was forcefully terminated by the debugger.";
                case 7:
                    return "While preparing to install, it was determined setup cannot proceed with the installation. Please be sure the software can be installed on your system.";
                case 8:
                    return "While preparing to install, it was determined setup cannot proceed with the installation until you restart the system. Please reboot and try again.'";
                // MSI - https://msdn.microsoft.com/en-us/library/windows/desktop/aa376931.aspx
                case 1602:
                    return "User (you) cancelled the installation.";
                case 1603:
                    return "Generic MSI Error. This is a local environment error, not an issue with a package or the MSI itself - it could mean a pending reboot is necessary prior to install or something else (like the same version is already installed). Please see MSI log if available. If not, try again adding '--install-arguments=\"'/l*v c:\\$($env:chocolateyPackageName)_msi_install.log'\"'. Then search the MSI Log for \"Return Value 3\" and look above that for the error.";
                case 1618:
                    return "Another installation currently in progress. Try again later.";
                case 1619:
                    return "MSI could not be found - it is possibly corrupt or not an MSI at all. If it was downloaded and the MSI is less than 30K, try opening it in an editor like Notepad++ as it is likely HTML."
                        + errorMessageAddendum;
                case 1620:
                    return "MSI could not be opened - it is possibly corrupt or not an MSI at all. If it was downloaded and the MSI is less than 30K, try opening it in an editor like Notepad++ as it is likely HTML."
                        + errorMessageAddendum;
                case 1622:
                    return "Something is wrong with the install log location specified. Please fix this in the package silent arguments (or in install arguments you specified). The directory specified as part of the log file path must exist for an MSI to be able to log to that directory."
                        + errorMessageAddendum;
                case 1623:
                    return "This MSI has a language that is not supported by your system. Contact package maintainer(s) if there is an install available in your language and you would like it added to the packaging.";
                case 1625:
                    return "Installation of this MSI is forbidden by system policy. Please contact your system administrators.";
                case 1632:
                case 1633:
                    return "Installation of this MSI is not supported on this platform. Contact package maintainer(s) if you feel this is in error or if you need an architecture that is not available with the current packaging.";
                case 1638:
                    return "This MSI requires uninstall prior to installing a different version. Please ask the package maintainer(s) to add a check in the chocolateyInstall.ps1 script and uninstall if the software is installed."
                        + errorMessageAddendum;
                case 1639:
                    return "The command line arguments passed to the MSI are incorrect. If you passed in additional arguments, please adjust. Otherwise followup with the package maintainer(s) to get this fixed."
                        + errorMessageAddendum;
                case 1640:
                case 1645:
                    return "Cannot install MSI when running from remote desktop (terminal services). This should automatically be handled in licensed editions. For open source editions, you may need to run change.exe prior to running Chocolatey or not use terminal services.";
            }

            return string.Empty;
        }

        private string NormalizeProcessName(string processName)
        {
            if (!string.IsNullOrWhiteSpace(processName))
            {
                processName = processName.Replace("\0", string.Empty);

                if (!string.IsNullOrWhiteSpace(processName))
                {
                    processName = processName.Trim().Trim('"', '\'');
                }
            }

            return processName;
        }
    }
}
