using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Add, "ChocolateyPinnedTaskbarItem")]
    public class AddChocolateyPinnedTaskbarItemCommand : ChocolateyCmdlet
    {
        /*
        .SYNOPSIS
        Creates an item in the task bar linking to the provided path.

        .NOTES
        Does not work with SYSTEM, but does not error. It warns with the error
        message.

        .INPUTS
        None

        .OUTPUTS
        None

        .PARAMETER TargetFilePath
        The path to the application that should be launched when clicking on the
        task bar icon.

        .PARAMETER IgnoredArguments
        Allows splatting with arguments that do not apply. Do not use directly.

        .EXAMPLE
        >
        # This will create a Visual Studio task bar icon.
        Install-ChocolateyPinnedTaskBarItem -TargetFilePath "${env:ProgramFiles(x86)}\Microsoft Visual Studio 11.0\Common7\IDE\devenv.exe"

        .LINK
        Install-ChocolateyShortcut

        .LINK
        Install-ChocolateyExplorerMenuItem
        */

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("TargetFilePath")]
        public string Path { get; set; } = string.Empty;

        protected override void End()
        {
            const string verb = "Pin To Taskbar";
            var targetFolder = PSHelper.GetParentDirectory(this, Path);
            var targetItem = PSHelper.GetFileName(Path);

            try
            {
                if (!PSHelper.ItemExists(this, Path))
                {
                    WriteWarning($"'{Path}' does not exist, not able to pin to task bar");
                    return;
                }

                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                var folder = shell.NameSpace(targetFolder);
                var item = folder.ParseName(targetItem);

                bool verbFound = false;
                foreach (var itemVerb in item.Verbs())
                {
                    var name = (string)itemVerb.Name;
                    if (name.Replace("&", string.Empty) == verb)
                    {
                        verbFound = true;
                        itemVerb.DoIt();
                        break;
                    }
                }

                if (!verbFound)
                {
                    WriteHost($"TaskBar verb not found for {targetItem}. It may have already been pinned");
                }

                WriteHost($"'{Path}' has been pinned to the task bar on your desktop");
            }
            catch (Exception ex)
            {
                WriteWarning($"Unable to create pin. Error captured was {ex.Message}.");
            }

            base.EndProcessing();
        }
    }
}
