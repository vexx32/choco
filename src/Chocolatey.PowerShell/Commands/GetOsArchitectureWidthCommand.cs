using System;
using System.Management.Automation;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "OSArchitectureWidth")]
    public class GetOsArchitectureWidthCommand : ChocolateyCmdlet
    {
        /*
         
.SYNOPSIS
Get the operating system architecture address width.

.DESCRIPTION
This will return the system architecture address width (probably 32 or
64 bit). If you pass a comparison, it will return true or false instead
of {`32`|`64`}.

.NOTES
When your installation script has to know what architecture it is run
on, this simple function comes in handy.

ARM64 architecture will automatically select 32bit width as
there is an emulator for 32 bit and there are no current plans by Microsoft to
ship 64 bit x86 emulation for ARM64. For more details, see
https://github.com/chocolatey/choco/issues/1800#issuecomment-484293844.

.INPUTS
None

.OUTPUTS
None

.PARAMETER Compare
This optional parameter causes the function to return $true or $false,
depending on whether or not the bit width matches.
        */
        [Parameter]
        [Alias("Compare")]
        [BoolStringSwitchTransform]
        public int CompareTo { get; set; }

        protected override void End()
        {
            var bits = Environment.Is64BitProcess ? 64 : 32;

            if (BoundParameters.ContainsKey(nameof(CompareTo)))
            {
                WriteObject(ArchitectureWidth.Matches(CompareTo));
            }
            else
            {
                WriteObject(ArchitectureWidth.Get());
            }

            base.EndProcessing();
        }
    }
}
