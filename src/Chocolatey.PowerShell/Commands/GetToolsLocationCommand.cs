using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;

using static Chocolatey.PowerShell.Helpers.PSHelper;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "ToolsLocation")]
    [OutputType(typeof(string))]
    public class GetToolsLocationCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
Gets the top level location for tools/software installed outside of
package folders.

.DESCRIPTION
Creates or uses an environment variable that a user can control to
communicate with packages about where they would like software that is
not installed through native installers, but doesn't make much sense
to be kept in package folders. Most software coming in packages stays
with the package itself, but there are some things that seem to fall
out of this category, like things that have plugins that are installed
into the same directory as the tool. Having that all combined in the
same package directory could get tricky.

.NOTES
Sets an environment variable called `ChocolateyToolsLocation`. If the
older `ChocolateyBinRoot` is set, it uses the value from that and
removes the older variable.

.INPUTS
None

.OUTPUTS
None
        */

        private const string DriveLetterPattern = @"^\w:";
        private static readonly Regex _driveLetterRegex = new Regex(DriveLetterPattern, RegexOptions.Compiled);

        protected override void End()
        {
            var envToolsLocation = EnvironmentVariable(EnvironmentVariables.ChocolateyToolsLocation);
            var toolsLocation = envToolsLocation;

            if (string.IsNullOrEmpty(toolsLocation))
            {
                var binRoot = EnvironmentVariable(EnvironmentVariables.ChocolateyBinRoot);
                
                if (string.IsNullOrEmpty(binRoot))
                {
                    toolsLocation = CombinePaths(this, EnvironmentVariable("SYSTEMDRIVE"), "tools");
                }
                else
                {
                    toolsLocation = binRoot;
                    EnvironmentHelper.SetVariable(this, EnvironmentVariables.ChocolateyBinRoot, EnvironmentVariableTarget.User, string.Empty);
                }
            }

            if (!_driveLetterRegex.IsMatch(toolsLocation))
            {
                toolsLocation = CombinePaths(this, EnvironmentVariable("SYSTEMDRIVE"), toolsLocation);
            }

            if (envToolsLocation != toolsLocation)
            {
                try
                {
                    EnvironmentHelper.SetVariable(this, EnvironmentVariables.ChocolateyToolsLocation, EnvironmentVariableTarget.User, toolsLocation);
                }
                catch (Exception e)
                {
                    if (ProcessInformation.IsElevated())
                    {
                        // sometimes User scope may not exist (such as with core)
                        EnvironmentHelper.SetVariable(this, EnvironmentVariables.ChocolateyToolsLocation, EnvironmentVariableTarget.Machine, toolsLocation);
                    }
                    else
                    {
                        ThrowTerminatingError(new RuntimeException(e.Message, e).ErrorRecord);
                    }
                }
            }

            WriteObject(toolsLocation);
        }
    }
}
