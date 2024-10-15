using Chocolatey.PowerShell;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.IO;
using System.Management.Automation;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Start, "ChocolateyProcess")]
    public class StartChocolateyProcessCommand : ChocolateyCmdlet
    {
        //.SYNOPSIS
        //** NOTE:** Administrative Access Required.
        //
        //Runs a process with administrative privileges.If `-ExeToRun` is not
        //specified, it is run with PowerShell.
        //
        //.NOTES
        //This command will assert UAC/Admin privileges on the machine.
        //
        //Will automatically call Set-PowerShellExitCode to set the package exit
        //code in the following ways:
        //
        //- 4 if the binary turns out to be a text file.
        //- The same exit code returned from the process that is run.If a 3010 is returned, it will set 3010 for the package.
        //
        //Aliases `Start-ChocolateyProcess` and `Invoke-ChocolateyProcess`.
        //
        //.INPUTS
        //        None
        //
        //.OUTPUTS
        //None
        //
        //.PARAMETER Statements
        //Arguments to pass to `ExeToRun` or the PowerShell script block to be
        //run.
        //
        //.PARAMETER ExeToRun
        //The executable/application/installer to run.Defaults to `'powershell'`.
        //
        //.PARAMETER Elevated
        //Indicate whether the process should run elevated.
        //
        //.PARAMETER Minimized
        //Switch indicating if a Windows pops up (if not called with a silent
        //argument) that it should be minimized.
        //
        //.PARAMETER NoSleep
        //Used only when calling PowerShell - indicates the window that is opened
        //should return instantly when it is complete.
        //
        //.PARAMETER ValidExitCodes
        //Array of exit codes indicating success.Defaults to `@(0)`.
        //
        //.PARAMETER WorkingDirectory
        //The working directory for the running process.Defaults to
        //`Get-Location`. If current location is a UNC path, uses
        //`$env:TEMP` for default.
        //
        //.PARAMETER SensitiveStatements
        //Arguments to pass to  `ExeToRun` that are not logged.
        //
        //Note that only licensed versions of Chocolatey provide a way to pass
        //those values completely through without having them in the install
        //script or on the system in some way.
        //
        //.PARAMETER IgnoredArguments
        //Allows splatting with arguments that do not apply. Do not use directly.
        //
        //.EXAMPLE
        //Start-ChocolateyProcessAsAdmin -Statements "$msiArgs" -ExeToRun 'msiexec'
        //
        //.EXAMPLE
        //Start-ChocolateyProcessAsAdmin -Statements "$silentArgs" -ExeToRun $file
        //
        //.EXAMPLE
        //Start-ChocolateyProcessAsAdmin -Statements "$silentArgs" -ExeToRun $file -ValidExitCodes @(0,21)
        //
        //.EXAMPLE
        //>
        //# Run PowerShell statements
        //$psFile = Join-Path "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" 'someInstall.ps1'
        //Start-ChocolateyProcessAsAdmin "& `'$psFile`'"
        //
        //.EXAMPLE
        //# This also works for cmd and is required if you have any spaces in the paths within your command
        //$appPath = "$env:ProgramFiles\myapp"
        //$cmdBatch = "/c `"$appPath\bin\installmyappservice.bat`""
        //Start-ChocolateyProcessAsAdmin $cmdBatch cmd
        //# or more explicitly
        //Start-ChocolateyProcessAsAdmin -Statements $cmdBatch -ExeToRun "cmd.exe"
        //
        //.LINK
        //Install-ChocolateyPackage
        //
        //.LINK
        //Install-ChocolateyInstallPackage

        private StartChocolateyProcessHelper _helper;

        [Parameter]
        [Alias("Statements")]
        public string[] Arguments { get; set; } = Array.Empty<string>();

        [Parameter]
        [Alias("exeToRun")]
        public string ProcessName { get; set; } = "powershell";

        [Parameter]
        public SwitchParameter Elevated { get; set; }

        [Parameter]
        public SwitchParameter Minimized { get; set; }

        [Parameter]
        public SwitchParameter NoSleep { get; set; }

        [Parameter]
        public int[] ValidExitCodes { get; set; } = new int[] { 0 };

        [Parameter]
        public string WorkingDirectory { get; set; } = string.Empty;

        [Parameter]
        public string SensitiveStatements { get; set; }

        protected override void Begin()
        {
            if (MyInvocation.InvocationName == "Start-ChocolateyProcessAsAdmin")
            {
                Elevated = true;
            }
        }

        protected override void End()
        {
            var arguments = Arguments is null
                ? null
                : string.Join(" ", Arguments);

            try
            {
                _helper = new StartChocolateyProcessHelper(this, PipelineStopToken, ProcessName);
                var exitCode = _helper.Start(WorkingDirectory, arguments, SensitiveStatements, Elevated.IsPresent, Minimized.IsPresent, NoSleep.IsPresent, ValidExitCodes);
                WriteObject(exitCode);
            }
            catch (FileNotFoundException notFoundEx)
            {
                ThrowTerminatingError(new ErrorRecord(notFoundEx, ErrorId, ErrorCategory.ObjectNotFound, ProcessName));
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, ErrorId, ErrorCategory.NotSpecified, ProcessName));
            }
            finally
            {
            }
        }

        protected override void Stop()
        {
            _helper?.CancelWait();
        }
    }
}
