using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "EnvironmentVariableName")]
    [OutputType(typeof(string))]
    public class GetEnvironmentVariableNameCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
Gets all environment variable names.

.DESCRIPTION
Provides a list of environment variable names based on the scope. This
can be used to loop through the list and generate names.

.NOTES
Process dumps the current environment variable names in memory /
session. The other scopes refer to the registry values.

.INPUTS
None

.OUTPUTS
A list of environment variables names.

.PARAMETER Scope
The environment variable target scope. This is `Process`, `User`, or
`Machine`.

.EXAMPLE
Get-EnvironmentVariableNames -Scope Machine

.LINK
Get-EnvironmentVariable

.LINK
Set-EnvironmentVariable
        */

        [Parameter(Position = 0)]
        public EnvironmentVariableTarget Scope { get; set; }

        [Parameter]
        public SwitchParameter PreserveVariables { get; set; }


        protected override void Begin()
        {
            // Avoid calling base.BeginProcessing() to log function call
        }

        protected override void End()
        {
            foreach (var item in EnvironmentHelper.GetVariableNames(Scope))
            {
                WriteObject(item);
            }
        }
    }
}
