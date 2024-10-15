using System.Collections.Generic;
using System;
using System.Management.Automation;
using System.Security.Principal;
using Chocolatey.PowerShell;
using Chocolatey.PowerShell.Shared;
using Chocolatey.PowerShell.Helpers;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsDiagnostic.Test, "ProcessRunningAsAdmin")]
    [OutputType(typeof(bool))]
    public class TestProcessRunningAsAdminCommand : ChocolateyCmdlet
    {
        // .SYNOPSIS
        // Tests whether the current process is running with administrative rights.
        // 
        // .DESCRIPTION
        // This function checks whether the current process has administrative
        // rights by checking if the current user identity is a member of the
        // Administrators group. It returns `$true` if the current process is
        // running with administrative rights, `$false` otherwise.
        // 
        // On Windows Vista and later, with UAC enabled, the returned value
        // represents the actual rights available to the process, e.g. if it
        // returns `$true`, the process is running elevated.
        // 
        // .INPUTS
        // None
        // 
        // .OUTPUTS
        // System.Boolean
        protected override void End()
        {
            var result = ProcessInformation.IsElevated();

            WriteDebug($"Test-ProcessRunningAsAdmin: returning {result}");

            WriteObject(result);
        }
    }
}
