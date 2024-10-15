using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Management.Automation;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsData.Expand, "ChocolateyArchive", DefaultParameterSetName = "Path")]
    [OutputType(typeof(string))]
    public class ExpandChocolateyArchiveCommand : ChocolateyCmdlet
    {
        /*
         
.SYNOPSIS
Unzips an archive file and returns the location for further processing.

.DESCRIPTION
This unzips files using the 7-zip command line tool 7z.exe.
Supported archive formats are listed at:
https://sevenzip.osdn.jp/chm/general/formats.htm

.INPUTS
None

.OUTPUTS
Returns the passed in $destination.

.NOTES
If extraction fails, an exception is thrown.

If you are embedding files into a package, ensure that you have the
rights to redistribute those files if you are sharing this package
publicly (like on the community feed). Otherwise, please use
Install-ChocolateyZipPackage to download those resources from their
official distribution points.

Will automatically call Set-PowerShellExitCode to set the package exit code
based on 7-zip's exit code.

.PARAMETER FileFullPath
This is the full path to the zip file. If embedding it in the package
next to the install script, the path will be like
`"$(Split-Path -Parent $MyInvocation.MyCommand.Definition)\\file.zip"`

`File` is an alias for FileFullPath.

This can be a 32-bit or 64-bit file. This is mandatory in earlier versions
of Chocolatey, but optional if FileFullPath64 has been provided.

.PARAMETER FileFullPath64
Full file path to a 64-bit native installer to run.
If embedding in the package, you can get it to the path with
`"$(Split-Path -parent $MyInvocation.MyCommand.Definition)\\INSTALLER_FILE"`

Provide this when you want to provide both 32-bit and 64-bit
installers or explicitly only a 64-bit installer (which will cause a package
install failure on 32-bit systems).

.PARAMETER Destination
This is a directory where you would like the unzipped files to end up.
If it does not exist, it will be created.

.PARAMETER SpecificFolder
OPTIONAL - This is a specific directory within zip file to extract. The
folder and its contents will be extracted to the destination.

.PARAMETER PackageName
OPTIONAL - This will facilitate logging unzip activity for subsequent
uninstalls

.PARAMETER DisableLogging
OPTIONAL - This disables logging of the extracted items. It speeds up
extraction of archives with many files.

Usage of this parameter will prevent Uninstall-ChocolateyZipPackage
from working, extracted files will have to be cleaned up with
Remove-Item or a similar command instead.

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
>
# Path to the folder where the script is executing
$toolsDir = (Split-Path -parent $MyInvocation.MyCommand.Definition)
Get-ChocolateyUnzip -FileFullPath "c:\someFile.zip" -Destination $toolsDir

.LINK
Install-ChocolateyZipPackage
        */

        [Alias("File", "FileFullPath")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path")]
        public string Path { get; set; } = string.Empty;

        [Alias("UnzipLocation")]
        [Parameter(Mandatory = true, Position = 1)]
        public string Destination { get; set; } = string.Empty;

        [Parameter(Position = 2)]
        public string SpecificFolder { get; set; }

        [Parameter(Position = 3)]
        public string PackageName { get; set; }

        [Alias("File64", "FileFullPath64")]
        [Parameter(Mandatory = true, ParameterSetName = "Path64")]
        [Parameter(ParameterSetName = "Path")]
        public string Path64 { get; set; }

        [Parameter]
        public SwitchParameter DisableLogging { get; set; }

        protected override void End()
        {
            // This case should be prevented by the parameter set definitions,
            // but it doesn't hurt to make absolutely sure here as well.
            if (!(BoundParameters.ContainsKey(nameof(Path)) || BoundParameters.ContainsKey(nameof(Path64))))
            {
                ThrowTerminatingError(new RuntimeException("Parameters are incorrect; either -Path or -Path64 must be specified.").ErrorRecord);
            }

            var helper = new SevenZipHelper(this, PipelineStopToken);
            helper.Run7zip(Path, Path64, PackageName, Destination, SpecificFolder, DisableLogging);

            WriteObject(Destination);

            base.EndProcessing();
        }
    }
}
