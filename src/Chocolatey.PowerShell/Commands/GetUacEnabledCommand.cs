using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "UacEnabled")]
    [OutputType(typeof(bool))]
    public class GetUacEnabledCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
Determines if UAC (User Account Control) is turned on or off.

.DESCRIPTION
This is a low level function used by Chocolatey to decide whether
prompting for elevated privileges is necessary or not.

.NOTES
This checks the `EnableLUA` registry value to be determine the state of
a system.

.INPUTS
None

.OUTPUTS
System.Boolean
        */

        private const string UacRegistryPath = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string UacRegistryProperty = "EnableLUA";

        protected override void End()
        {
            var uacEnabled = false;

            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms724832(v=vs.85).aspx
            var osVersion = Environment.OSVersion.Version;
            if (osVersion >= new Version(6, 0))
            {
                try
                {
                    var uacRegistryValue = InvokeProvider.Property.Get(UacRegistryPath, new Collection<string>()).FirstOrDefault()?.Properties[UacRegistryProperty].Value;
                    uacEnabled = (int?)uacRegistryValue == 1;
                }
                catch
                {
                    // Registry key doesn't exist, proceed with false
                }
            }

            WriteObject(uacEnabled);
        }
    }
}
