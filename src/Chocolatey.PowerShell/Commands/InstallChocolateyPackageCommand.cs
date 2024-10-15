using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

using static Chocolatey.PowerShell.Helpers.PSHelper;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Install, "ChocolateyPackage")]
    public class InstallChocolateyPackageCommand : ChocolateyCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string PackageName { get; set; } = string.Empty;

        [Parameter(Position = 1)]
        [Alias("InstallerType", "InstallType")]
        public string FileType { get; set; } = "exe";

        [Parameter(Position = 2)]
        public string[] SilentArgs { get; set; } = new[] { string.Empty };

        [Parameter(Position = 3)]
        public string Url { get; set; } = string.Empty;

        [Parameter(Position = 4)]
        [Alias("Url64")]
        public string Url64 { get; set; } = string.Empty;

        [Parameter]
        public int[] ValidExitCodes { get; set; } = new int[] { 0 };

        [Parameter]
        public string Checksum { get; set; } = string.Empty;

        [Parameter]
        public ChecksumType ChecksumType { get; set; }

        [Parameter]
        public string Checksum64 { get; set; } = string.Empty;

        [Parameter]
        public ChecksumType ChecksumType64 { get; set; }

        [Parameter]
        public Hashtable Options { get; set; } = new Hashtable
        {
            { "Headers", new Hashtable() }
        };

        [Parameter]
        [Alias("FileFullPath", "File")]
        public string Path { get; set; } = string.Empty;

        [Parameter]
        [Alias("FileFullPath64", "File64")]
        public string Path64 { get; set; } = string.Empty;

        [Parameter]
        [Alias("UseOnlyPackageSilentArgs")]
        public SwitchParameter UseOnlyPackageSilentArguments { get; set; }

        [Parameter]
        public SwitchParameter UseOriginalLocation { get; set; }

        [Parameter]
        public ScriptBlock BeforeInstall { get; set; }

        protected override void End()
        {
            var silentArgs = string.Join(" ", SilentArgs);
            var chocoTempDir = EnvironmentVariable("TEMP");
            var chocoPackageName = EnvironmentVariable(EnvironmentVariables.ChocolateyPackageName);

            var tempDir = CombinePaths(this, chocoTempDir, chocoPackageName);

            var version = EnvironmentVariable(EnvironmentVariables.ChocolateyPackageVersion);
            if (!string.IsNullOrEmpty(version))
            {
                tempDir = CombinePaths(this, tempDir, version);
            }

            tempDir = tempDir.Replace("\\chocolatey\\chocolatey\\", "\\chocolatey\\");
            if (!ItemExists(this, tempDir))
            {
                NewDirectory(this, tempDir);
            }

            var downloadFilePath = CombinePaths(this, tempDir, $"{PackageName}Install.{FileType}");

            var url = string.IsNullOrEmpty(Url) ? Path : Url;
            var url64 = string.IsNullOrEmpty(Url64) ? Path64 : Url64;

            var filePath = downloadFilePath;
            if (UseOriginalLocation)
            {
                filePath = url;
                if (ArchitectureWidth.Matches(64))
                {
                    var forceX86 = EnvironmentVariable(EnvironmentVariables.ChocolateyForceX86);
                    if (ConvertTo<bool>(forceX86))
                    {
                        WriteDebug("User specified '-x86' so forcing 32-bit");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(url64))
                        {
                            filePath = url64;
                        }
                    }
                }
            }
            else
            {
                // TODO: finsih this code after web code is done
            }
        }
    }
}
