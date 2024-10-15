using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;

using static Chocolatey.PowerShell.Helpers.PSHelper;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsCommon.Get, "UninstallRegistryKey")]
    public class GetUninstallRegistryKeyCommand : ChocolateyCmdlet
    {
        /*
        
.SYNOPSIS
Retrieve registry key(s) for system-installed applications from an
exact or wildcard search.

.DESCRIPTION
This function will attempt to retrieve a matching registry key for an
already installed application, usually to be used with a
chocolateyUninstall.ps1 automation script.

The function also prevents `Get-ItemProperty` from failing when
handling wrongly encoded registry keys.

.INPUTS
String

.OUTPUTS
This function searches registry objects and returns an array
of PSCustomObject with the matched key's properties.

Retrieve properties with dot notation, for example:
`$key.UninstallString`


.PARAMETER SoftwareName
Part or all of the Display Name as you see it in Programs and Features.
It should be enough to be unique.
The syntax follows the rules of the PowerShell `-like` operator, so the
`*` character is interpreted as a wildcard, which matches any (zero or
more) characters.

If the display name contains a version number, such as "Launchy (2.5)",
it is recommended you use a fuzzy search `"Launchy (*)"` (the wildcard
`*`) so if Launchy auto-updates or is updated outside of Chocolatey, the
uninstall script will not fail.

Take care not to abuse fuzzy/glob pattern searches. Be conscious of
programs that may have shared or common root words to prevent
overmatching. For example, "SketchUp*" would match two keys with
software names "SketchUp 2016" and "SketchUp Viewer" that are different
programs released by the same company.

.PARAMETER IgnoredArguments
Allows splatting with arguments that do not apply. Do not use directly.

.EXAMPLE
>
# Version match: Software name is "Gpg4Win (2.3.0)"
[array]$key = Get-UninstallRegistryKey -SoftwareName "Gpg4win (*)"
$key.UninstallString

.EXAMPLE
>
# Fuzzy match: Software name is "Launchy 2.5"
[array]$key = Get-UninstallRegistryKey -SoftwareName "Launchy*"
$key.UninstallString

.EXAMPLE
>
# Exact match: Software name in Programs and Features is "VLC media player"
[array]$key = Get-UninstallRegistryKey -SoftwareName "VLC media player"
$key.UninstallString

.EXAMPLE
>
#  Version match: Software name is "SketchUp 2016"
# Note that the similar software name "SketchUp Viewer" would not be matched.
[array]$key = Get-UninstallRegistryKey -SoftwareName "SketchUp [0-9]*"
$key.UninstallString

.LINK
Install-ChocolateyPackage

.LINK
Install-ChocolateyInstallPackage

.LINK
Uninstall-ChocolateyPackage
        */
        private const string LocalUninstallKey = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*";
        private const string MachineUninstallKey = @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*";
        private const string MachineUninstallKey6432 = @"HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*";


        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public string SoftwareName { get; set; } = string.Empty;

        protected override void End()
        {
            if (!string.IsNullOrEmpty(SoftwareName))
            {
                // TODO: Replace RuntimeException
                ThrowTerminatingError(new RuntimeException($"{SoftwareName} cannot be empty for Get-UninstallRegistryKey").ErrorRecord);
            }

            WriteVerbose("Retrieving all uninstall registry keys");
            var keys = GetChildItem(this, new[] { MachineUninstallKey6432, MachineUninstallKey, LocalUninstallKey }, recurse: false, force: false, literalPath: true);
            WriteDebug($"Registry uninstall keys on system: {keys.Count}");

            // Error handling check: `'Get-ItemProperty`' fails if a registry key is encoded incorrectly.
            List<PSObject> foundKey = null;
            bool warnForBadKeys = false;
            for (var attempt = 1; attempt <= keys.Count;attempt++)
            {
                bool success = false;
                var keyPaths = ConvertTo<string[]>(keys.Select(k => k.Properties["PSPath"].Value));

                try
                {
                    foundKey = InvokeProvider.Property.Get(keyPaths, new Collection<string>(), literalPath: true)
                        .Where(k => IsLike(ConvertTo<string>(k.Properties), SoftwareName))
                        .ToList();
                    success = true;
                }
                catch
                {
                    WriteDebug("Found bad key");
                    var badKey = new List<PSObject>();
                    foreach (var key in keys)
                    {
                        var psPath = ConvertTo<string>(key.Properties["PSPath"].Value);
                        try
                        {
                            InvokeProvider.Property.Get(psPath, new Collection<string>());
                        }
                        catch
                        {
                            badKey.Add(key);
                            WriteVerbose($"Skipping bad key: {psPath}");
                        }
                    }

                    foreach (var bad in badKey)
                    {
                        keys.Remove(bad);
                    }
                }

                if (success)
                {
                    break;
                }

                if (attempt >= 10 && !warnForBadKeys)
                {
                    warnForBadKeys = true;
                }
            }

            if (warnForBadKeys)
            {
                WriteWarning("Found 10 or more bad registry keys. Run command again with `'--verbose --debug`' for more info.");
                WriteDebug("Each key searched should correspond to an installed program. It is very unlikely to have more than a few programs with incorrectly encoded keys, if any at all. This may be indicative of one or more corrupted registry branches.");
            }

            if (!(foundKey?.Count > 0))
            {
                WriteWarning($"No registry key found based on '{SoftwareName}'");
            }
            else
            {
                WriteDebug($"Found {foundKey.Count} uninstall registry key(s) with SoftwareName: '{SoftwareName}'");
                WriteObject(foundKey);
            }
        }
    }
}
