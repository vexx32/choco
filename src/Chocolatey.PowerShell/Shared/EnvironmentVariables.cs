// Copyright © 2017 - 2024 Chocolatey Software, Inc
// Copyright © 2011 - 2017 RealDimensions Software, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
//
// You may obtain a copy of the License at
//
// 	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.ComponentModel;

namespace Chocolatey.PowerShell.Shared
{
    /// <summary>
    /// Names of available environment variables that will be created or used by provided
    /// PowerShell commands as part of executing Chocolatey CLI.
    /// </summary>
    /// <remarks>
    /// DEV NOTICE: Mark anything that is not meant for public consumption as
    /// internal constants and not browsable, even if used in other projects.
    /// </remarks>
    public static class EnvironmentVariables
    {
        public const string ComputerName = "COMPUTERNAME";
        public const string Path = "PATH";
        public const string ProcessorArchitecture = "PROCESSOR_ARCHITECTURE";
        public const string PSModulePath = "PSModulePath";
        public const string System = "SYSTEM";
        public const string SystemRoot = "SystemRoot";
        public const string Username = "USERNAME";

        /// <summary>
        /// The location of the current Chocolatey installation; typically defaults to <c>C:\ProgramData\chocolatey</c>.
        /// </summary>
        public const string ChocolateyInstall = nameof(ChocolateyInstall);

        /// <summary>
        /// When this environment variable is set to 'true', we are running under Chocolatey's built-in PowerShell host.
        /// Typically set when running under Chocolatey with the <c>powershellHost</c> feature enabled, if the <c>--use-system-powershell</c> flag is not provided.
        /// </summary>
        public const string ChocolateyPowerShellHost = nameof(ChocolateyPowerShellHost);

        /// The date and time that the system environment variables (for example, PATH) were last updated by Chocolatey
        /// </summary>
        /// <remarks>
        /// Will be set during package installations if the system environment variables are updated / refreshed.
        /// Not otherwise used by anything in Chocolatey itself.
        /// </remarks>
        public const string ChocolateyLastPathUpdate = "ChocolateyLastPathUpdate";

        /// <summary>
        /// The version of the package that is being handled as it is defined in the embedded
        /// nuspec file.
        /// </summary>
        /// <remarks>
        /// Will be sets during package installs, upgrades and uninstalls.
        /// Environment variable is only for internal uses.
        /// </remarks>
        /// <seealso cref="PackageNuspecVersion" />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageNuspecVersion = nameof(ChocolateyPackageNuspecVersion);

        /// <summary>
        /// The version of the package that is being handled as it is defined in the embedded
        /// nuspec file.
        /// </summary>
        /// <remarks>
        /// Will be sets during package installs, upgrades and uninstalls.
        /// Environment variable is only for internal uses.
        /// </remarks>
        /// <seealso cref="ChocolateyPackageNuspecVersion" />
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string PackageNuspecVersion = nameof(PackageNuspecVersion);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyRequestTimeout = nameof(ChocolateyRequestTimeout);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyResponseTimeout = nameof(ChocolateyResponseTimeout);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyUrlOverride = nameof(ChocolateyUrlOverride);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyUrl64BitOverride = nameof(ChocolateyUrl64BitOverride);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyForceX86 = nameof(ChocolateyForceX86);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyChecksum32 = nameof(ChocolateyChecksum32);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyChecksumType32 = nameof(ChocolateyChecksumType32);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyChecksum64 = nameof(ChocolateyChecksum64);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyChecksumType64 = nameof(ChocolateyChecksumType64);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageName = nameof(ChocolateyPackageName);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageFolder = nameof(ChocolateyPackageFolder);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string PackageFolder = nameof(PackageFolder);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyInstallDirectoryPackage = nameof(ChocolateyInstallDirectoryPackage);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageExitCode = nameof(ChocolateyPackageExitCode);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageInstallLocation = nameof(ChocolateyPackageInstallLocation);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageParameters = nameof(ChocolateyPackageParameters);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageParametersSensitive = nameof(ChocolateyPackageParametersSensitive);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyToolsLocation = nameof(ChocolateyToolsLocation);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyBinRoot = nameof(ChocolateyBinRoot);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyInstallerType = nameof(ChocolateyInstallerType);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyInstallArguments = nameof(ChocolateyInstallArguments);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyInstallOverride = nameof(ChocolateyInstallOverride);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyExitCode = nameof(ChocolateyExitCode);

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyPackageVersion = nameof(ChocolateyPackageVersion);
        
        /// <summary>
        /// When this environment variable is set to 'true', checksum validation is skipped.
        /// Typically set when running under Chocolatey if <c>--ignore-checksums</c> is passed or the feature <c>checksumFiles</c> is turned off.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyIgnoreChecksums = nameof(ChocolateyIgnoreChecksums);

        /// <summary>
        /// When this environment variable is set to 'true', an empty checksum is treated as valid.
        /// Typically set when running under Chocolatey if <c>--allow-empty-checksums</c> is passed or the feature <c>allowEmptyChecksums</c> is turned on.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyAllowEmptyChecksums = nameof(ChocolateyAllowEmptyChecksums);

        /// <summary>
        /// When this environment variable is set to 'true', an empty checksum is treated as valid for files downloaded from HTTPS URLs.
        /// Typically set when running under Chocolatey if <c>--allow-empty-checksums-secure</c> is passed or the feature <c>allowEmptyChecksumsSecure</c> is turned on.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public const string ChocolateyAllowEmptyChecksumsSecure = nameof(ChocolateyAllowEmptyChecksumsSecure);
    }
}
