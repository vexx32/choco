using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

using static Chocolatey.PowerShell.Helpers.PSHelper;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Install, "ChocolateyFileAssociation")]
    public class InstallChocolateyFileAssociationCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
**NOTE:** Administrative Access Required.

Creates an association between a file extension and a executable.

.DESCRIPTION
Install-ChocolateyFileAssociation can associate a file extension
with a downloaded application. Once this command has created an
association, all invocations of files with the specified extension
will be opened via the executable specified.

.NOTES
This command will assert UAC/Admin privileges on the machine.

.INPUTS
None

.OUTPUTS
None

.PARAMETER Extension
The file extension to be associated.

.PARAMETER Executable
The path to the application's executable to be associated.

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
>
# This will create an association between Sublime Text 2 and all .txt
# files. Any .txt file opened will by default open with Sublime Text 2.
$sublimeDir = (Get-ChildItem $env:ALLUSERSPROFILE\chocolatey\lib\sublimetext* | select $_.last)
$sublimeExe = "$sublimeDir\tools\sublime_text.exe"
Install-ChocolateyFileAssociation ".txt" $sublimeExe
*/

        [Parameter(Mandatory = true, Position = 0)]
        public string Extension { get; set; } = string.Empty;

        [Parameter(Mandatory = true, Position = 1)]
        public string Executable { get; set; } = string.Empty;

        protected override void End()
        {
            if (!ItemExists(this, Executable))
            {
                // TODO: Replace RuntimeException with a proper exception
                ThrowTerminatingError(new RuntimeException($"'{Executable}' does not exist, can't create file association").ErrorRecord);
            }

            var extension = Extension.Trim();
            if (!extension.StartsWith("."))
            {
                extension = $".{extension}";
            }

            var fileType = GetFileName(Executable).Replace(" ", "_");
            var elevatedCommand = $@"
cmd /c ""assoc {extension}={fileType}""
cmd /c 'ftype {fileType}=""{Executable}"" ""%1"" ""%*""'
New-PSDrive -Name HKCR -PSProvider Registry -Root HKEY_CLASSES_ROOT
Set-ItemProperty -Path ""HKCR:\{fileType}"" -Name ""(Default)"" -Value ""{fileType} file"" -ErrorAction Stop
";

            var helper = new StartChocolateyProcessHelper(this, PipelineStopToken);
            helper.Start(workingDirectory: null, elevatedCommand, sensitiveStatements: null, elevated: true, minimized: true, noSleep: true);

            WriteHost($"'{extension}' has been associated with '{Executable}'");
        }
    }
}
