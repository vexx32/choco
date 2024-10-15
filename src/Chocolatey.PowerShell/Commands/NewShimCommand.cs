using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Text;

using static Chocolatey.PowerShell.Helpers.PSHelper;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.New, "Shim")]
    public class NewShimCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
Creates a shim (or batch redirect) for a file that is on the PATH.

.DESCRIPTION
Chocolatey installs have the folder `$($env:ChocolateyInstall)\bin`
included in the PATH environment variable. Chocolatey automatically
shims executables in package folders that are not explicitly ignored,
putting them into the bin folder (and subsequently onto the PATH).

When you have other files you want to shim to add them to the PATH or
if you want to handle the shimming explicitly, use this function.

If you do use this function, ensure you also add `Uninstall-BinFile` to
your `chocolateyUninstall.ps1` script as Chocolatey will not
automatically clean up shims created with this function.

.NOTES
Not normally needed for exe files in the package folder, those are
automatically discovered and added as shims after the install script
completes.

.INPUTS
None

.OUTPUTS
None

.PARAMETER Name
The name of the redirect file, will have .exe appended to it.

.PARAMETER Path
The path to the original file. Can be relative from
`$($env:ChocolateyInstall)\bin` back to your file or a full path to the
file.

.PARAMETER UseStart
This should be passed if the shim should not wait on the action to
complete. This is usually the case with GUI apps, you don't want the
command shell blocked waiting for the GUI app to be shut back down.

.PARAMETER Command
OPTIONAL - This is any additional command arguments you want passed
every time to the command. This is not normally used, but may be
necessary if you are calling something and then your application. For
example if you are calling Java with your JAR, the command would be the
JAR file plus any other options to start Java appropriately.

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.LINK
Uninstall-BinFile

.LINK
Install-ChocolateyShortcut

.LINK
Install-ChocolateyPath
        */

        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; } = string.Empty;

        [Parameter(Mandatory = true, Position = 1)]
        public string Path { get; set; } = string.Empty;

        [Parameter]
        [Alias("IsGui")]
        public SwitchParameter UseStart { get; set; }

        [Parameter]
        public string Command { get; set; } = string.Empty;

        protected override void End()
        {
            var nugetPath = ChocolateyInstallLocation;
            var nugetExePath = CombinePaths(this, nugetPath, "bin");

            var packageBashFileName = CombinePaths(this, nugetExePath, Name);
            var packageBatchFileName = packageBashFileName + ".bat";
            var packageShimFileName = packageBashFileName + ".exe";

            if (ItemExists(this, packageBatchFileName))
            {
                RemoveItem(this, packageBatchFileName);
            }

            if (ItemExists(this, packageBashFileName))
            {
                RemoveItem(this, packageBashFileName);
            }

            var path = Path.ToLower().Replace(nugetPath.ToLower(), @"..\").Replace(@"\\", @"\");

            var shimGenArgs = new StringBuilder().AppendFormat("-o \"{0}\" -p \"{1}\" -i \"{2}\"", packageShimFileName, path, Path);

            if (!string.IsNullOrEmpty(Command))
            {
                shimGenArgs.AppendFormat(" -c {0}", Command);
            }

            if (UseStart)
            {
                shimGenArgs.Append(" -gui");
            }

            if (Debug)
            {
                shimGenArgs.Append(" -debug");
            }

            var shimGenPath = CombinePaths(this, ChocolateyInstallLocation, @"tools\shimgen.exe");
            if (!ItemExists(this, shimGenPath))
            {
                EnvironmentHelper.UpdateSession(this);
                shimGenPath = CombinePaths(this, EnvironmentVariable(EnvironmentVariables.ChocolateyInstall), @"tools\shimgen.exe");
            }

            shimGenPath = GetFullPath(this, shimGenPath);

            var args = shimGenArgs.ToString();

            WriteDebug($"ShimGen found at '{shimGenPath}'");
            WriteDebug($"Calling {shimGenPath} {args}");

            if (ItemExists(this, shimGenPath))
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(shimGenPath, args)
                    {
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    }
                };

                // TODO: use the helper class to do this bit tbh
                process.Start();
                process.WaitForExit();
            }

            if (ItemExists(this, packageShimFileName))
            {
                WriteHost($"Added {packageShimFileName} shim pointed to '{path}'.");
            }
            else
            {
                WriteWarning("An error occurred generating shim, using old method.");
                CreateScriptShims(this, Name, path, packageBatchFileName, packageBashFileName, UseStart);
            }
        }

        private static void CreateScriptShims(
            PSCmdlet cmdlet,
            string name,
            string exePath,
            string batchFileName,
            string bashFileName,
            bool useStart)
        {
            var path = $"%DIR%{exePath}";
            var pathBash = path.Replace(@"%DIR%..\", @"$DIR/../").Replace(@"\", "/");

            PSHelper.WriteHost(cmdlet, $"Adding {batchFileName} and pointing to '{path}'.");
            PSHelper.WriteHost(cmdlet, $"Adding {bashFileName} and pointing to '{path}'.");

            if (useStart)
            {
                PSHelper.WriteHost(cmdlet, $"Setting up {name} as a non-command line application.");
                SetContent(
                    cmdlet,
                    batchFileName,
                    string.Format(BatchFileContentWithStart, path),
                    Encoding.ASCII);

                SetContent(
                    cmdlet,
                    bashFileName,
                    string.Format(BashFileContentWithStart, pathBash));
            }
            else
            {
                SetContent(
                    cmdlet,
                    batchFileName,
                    string.Format(BatchFileContent, path),
                    Encoding.ASCII);

                SetContent(
                    cmdlet,
                    bashFileName,
                    string.Format(BashFileContent, pathBash));
            }
        }

        private const string BatchFileContentWithStart = @"
@echo off
SET DIR=%~dp0%
start """" ""{0}"" %*
";

        private const string BashFileContentWithStart = @"#!/bin/sh`nDIR=`${{0%/*}}`n""{0}"" ""`$@"" &`n";

        private const string BatchFileContent = @"
@echo off
SET DIR=%~dp0%
cmd /c """"{0}"" %*""
exit /b %ERRORLEVEL%
";
        private const string BashFileContent = @"#!/bin/sh`nDIR=`${{0%/*}}`n""{0}"" ""`$@""`nexit `$?`n";
    }
}
