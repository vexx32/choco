using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "VirusCheckValid")]
    public class GetVirusCheckValidCommand : ChocolateyCmdlet
    {
        /*
        .SYNOPSIS
    Used in Pro/Business editions. Runtime virus check against downloaded
    resources.

    .DESCRIPTION
    Run a runtime malware check against downloaded resources prior to
    allowing Chocolatey to execute a file. This is only available
    in Pro / Business editions.

    .NOTES
    Only licensed editions of Chocolatey provide runtime malware protection.

    .INPUTS
    None

    .OUTPUTS
    None

    .PARAMETER Url
    Not used

    .PARAMETER File
    The full file path to the file to verify against anti-virus scanners.

    .PARAMETER IgnoredArguments
    Allows splatting with arguments that do not apply. Do not use directly.

        */

        [Parameter(Mandatory = false, Position = 0)]
        public string Url { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string File { get; set; } = string.Empty;
        
        protected override void End()
        {
            WriteDebug("No runtime virus checking built into FOSS Chocolatey. Check out Pro/Business - https://chocolatey.org/compare");
        }
    }
}
