using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Install, "ChocolateyEnvironmentVariable")]
    public class InstallChocolateyEnvironmentVariableCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
**NOTE:** Administrative Access Required when `-VariableType 'Machine'.`

Creates a persistent environment variable.

.DESCRIPTION
Install-ChocolateyEnvironmentVariable creates an environment variable
with the specified name and value. The variable is persistent and
will remain after reboots and across multiple PowerShell and command
line sessions. The variable can be scoped either to the User or to
the Machine. If Machine level scoping is specified, the command is
elevated to an administrative session.

.NOTES
This command will assert UAC/Admin privileges on the machine when
`-VariableType Machine`.

This will add the environment variable to the current session.

.INPUTS
None

.OUTPUTS
None

.PARAMETER VariableName
The name or key of the environment variable

.PARAMETER VariableValue
A string value assigned to the above name.

.PARAMETER VariableType
Specifies whether this variable is to be accessible at either the
individual user level or at the Machine level.

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
>
# Creates a User environment variable "JAVA_HOME" pointing to
# "d:\oracle\jdk\bin".
Install-ChocolateyEnvironmentVariable "JAVA_HOME" "d:\oracle\jdk\bin"

.EXAMPLE
>
# Creates a User environment variable "_NT_SYMBOL_PATH" pointing to
# "symsrv*symsrv.dll*f:\localsymbols*http://msdl.microsoft.com/download/symbols".
# The command will be elevated to admin privileges.
Install-ChocolateyEnvironmentVariable `
  -VariableName "_NT_SYMBOL_PATH" `
  -VariableValue "symsrv*symsrv.dll*f:\localsymbols*http://msdl.microsoft.com/download/symbols" `
  -VariableType Machine

.EXAMPLE
>
# Remove an environment variable
Install-ChocolateyEnvironmentVariable -VariableName 'bob' -VariableValue $null

.LINK
Uninstall-ChocolateyEnvironmentVariable

.LINK
Get-EnvironmentVariable

.LINK
Set-EnvironmentVariable

.LINK
Install-ChocolateyPath
        */
        [Parameter(Position = 0)]
        [Alias("VariableName")]
        public string Name { get; set; } = string.Empty;

        [Parameter(Position = 1)]
        [Alias("VariableValue")]
        public string Value { get; set; } = string.Empty;

        [Parameter(Position = 2)]
        [Alias("Target", "VariableType")]
        public EnvironmentVariableTarget Type { get; set; } = EnvironmentVariableTarget.User;

        protected override void End()
        {
            if (Type == EnvironmentVariableTarget.Machine)
            {
                if (ProcessInformation.IsElevated())
                {
                    EnvironmentHelper.SetVariable(this, Name, Type, Value);
                }
                else
                {
                    var helper = new StartChocolateyProcessHelper(this, PipelineStopToken);
                    var args = $"Install-ChocolateyEnvironmentVariable -Name '{Name}' -Value '{Value}' -Type '{Type}'";
                    helper.Start(workingDirectory: null, args, sensitiveStatements: null, elevated: true, minimized: true, noSleep: true);
                }
            }
            else
            {
                try
                {
                    EnvironmentHelper.SetVariable(this, Name, Type, Value);
                }
                catch (Exception ex)
                {
                    if (ProcessInformation.IsElevated())
                    {
                        // HKCU:\Environment may not exist, which happens sometimes with Server Core.
                        // In this case, set it at machine scope instead.
                        EnvironmentHelper.SetVariable(this, Name, EnvironmentVariableTarget.Machine, Value);
                    }
                    else
                    {
                        ThrowTerminatingError(new RuntimeException(ex.Message, ex).ErrorRecord);
                    }
                }
            }

            EnvironmentHelper.SetVariable(Name, Value);
        }
    }
}
