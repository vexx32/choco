using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Install, "ChocolateyExplorerMenuItem")]
    public class InstallChocolateyExplorerMenuItemCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
**NOTE:** Administrative Access Required.

Creates a windows explorer context menu item that can be associated with
a command

.DESCRIPTION
Install-ChocolateyExplorerMenuItem can add an entry in the context menu
of Windows Explorer. The menu item is given a text label and a command.
The command can be any command accepted on the windows command line. The
menu item can be applied to either folder items or file items.

Because this command accesses and edits the root class registry node, it
will be elevated to admin.

.NOTES
This command will assert UAC/Admin privileges on the machine.

Chocolatey will automatically add the path of the file or folder clicked
to the command. This is done simply by appending a %1 to the end of the
command.

.INPUTS
None

.OUTPUTS
None

.PARAMETER MenuKey
A unique string to identify this menu item in the registry

.PARAMETER MenuLabel
The string that will be displayed in the context menu

.PARAMETER Command
A command line command that will be invoked when the menu item is
selected

.PARAMETER Type
Specifies if the menu item should be applied to a folder or a file

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
>
# This will create a context menu item in Windows Explorer when any file
# is right clicked. The menu item will appear with the text "Open with
# Sublime Text 2" and will invoke sublime text 2 when selected.
$sublimeDir = (Get-ChildItem $env:ALLUSERSPROFILE\chocolatey\lib\sublimetext* | select $_.last)
$sublimeExe = "$sublimeDir\tools\sublime_text.exe"
Install-ChocolateyExplorerMenuItem "sublime" "Open with Sublime Text 2" $sublimeExe

.EXAMPLE
>
# This will create a context menu item in Windows Explorer when any
# folder is right clicked. The menu item will appear with the text
# "Open with Sublime Text 2" and will invoke sublime text 2 when selected.
$sublimeDir = (Get-ChildItem $env:ALLUSERSPROFILE\chocolatey\lib\sublimetext* | select $_.last)
$sublimeExe = "$sublimeDir\tools\sublime_text.exe"
Install-ChocolateyExplorerMenuItem "sublime" "Open with Sublime Text 2" $sublimeExe "directory"

.LINK
Install-ChocolateyShortcut
        */

        [Parameter(Mandatory = true, Position = 0)]
        public string MenuKey { get; set; } = string.Empty;

        [Parameter(Position = 1)]
        public string MenuLabel { get; set; }

        [Parameter(Position = 2)]
        public string Command { get; set; }

        [Parameter(Position = 3)]
        public ExplorerMenuItemType Type { get; set; } = ExplorerMenuItemType.File;

        protected override void End()
        {
            try
            {
                var key = Type == ExplorerMenuItemType.File ? "*" : "directory";

                var elevatedCommand = $@"
if( -not (Test-Path -Path HKCR:) ) {{New-PSDrive -Name HKCR -PSProvider registry -Root Hkey_Classes_Root}};`
if(!(Test-Path -LiteralPath 'HKCR:\{key}\shell\{MenuKey}')) {{ New-Item -Path 'HKCR:\{key}\shell\{MenuKey}' }};`
Set-ItemProperty -LiteralPath 'HKCR:\{key}\shell\{MenuKey}' -Name '(Default)' -Value '{MenuLabel}';`
if(!(Test-Path -LiteralPath 'HKCR:\{key}\shell\{MenuKey}\command')) {{ New-Item -Path 'HKCR:\{key}\shell\{MenuKey}\command' }};`
Set-ItemProperty -LiteralPath 'HKCR:\{key}\shell\{MenuKey}\command' -Name '(Default)' -Value '{Command} \`""%1\`""';`
return 0;";

                var helper = new StartChocolateyProcessHelper(this, PipelineStopToken);
                helper.Start(workingDirectory: null, arguments: elevatedCommand, sensitiveStatements: null, elevated: true, minimized: true, noSleep: true);

                WriteHost($"'{MenuKey}' explorer menu item has been created");
            }
            catch (Exception ex)
            {
                WriteWarning($"'{MenuKey}' explorer menu item was not created - {ex.Message}");
            }
        }
    }

    public enum ExplorerMenuItemType
    {
        File,
        Directory,
    }
}
