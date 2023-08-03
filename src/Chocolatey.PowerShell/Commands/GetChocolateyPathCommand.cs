using Chocolatey.PowerShell;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

using static Chocolatey.PowerShell.Helpers.PSHelper;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "ChocolateyPath")]
    public class GetChocolateyPathCommand : ChocolateyCmdlet
    {
        /*
.SYNOPSIS
Retrieve the paths available to be used by maintainers of packages.

.DESCRIPTION
This function will attempt to retrieve the path according to the specified Path Type
to a valid location that can be used by maintainers in certain scenarios.

.NOTES
Available in 1.2.0+.

.INPUTS
None

.OUTPUTS
This function outputs the full path stored accordingly with specified path type.
If no path could be found, there is no output.

.PARAMETER pathType
The type of path that should be looked up.
Available values are:
- `PackagePath` - The path to the the package that is being installed. Typically `C:\ProgramData\chocolatey\lib\<PackageName>`
- `InstallPath` - The path to where Chocolatey is installed. Typically `C:\ProgramData\chocolatey`

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
>
$path = Get-ChocolateyPath -PathType 'PackagePath'
         */
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("Type")]
        public ChocolateyPathType PathType { get; set; }

        protected override void End()
        {
            try
            {
                var path = Paths.GetChocolateyPathType(this, PathType);

                if (ContainerExists(this, path))
                {
                    WriteObject(path);
                }
            }
            catch (NotImplementedException error)
            {
                ThrowTerminatingError(new ErrorRecord(error, $"{ErrorId}.NotImplemented", ErrorCategory.NotImplemented, PathType));
            }
            catch (Exception error)
            {
                ThrowTerminatingError(new ErrorRecord(error, $"{ErrorId}.Unknown", ErrorCategory.NotSpecified, PathType));
            }
        }
    }
}
