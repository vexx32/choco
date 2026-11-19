// Copyright © 2017 - 2026 Chocolatey Software, Inc
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Chocolatey.PowerShell.Shared;
using Microsoft.Win32;

namespace Chocolatey.PowerShell.Helpers
{
    /// <summary>
    /// Utility class for handling registry operations like finding software uninstall keys, modifying Explorer
    /// context menus, or associating file extensions.
    /// Used by <see cref="Commands.GetUninstallRegistryKeyCommand"/>,
    /// <see cref="Commands.InstallChocolateyExplorerMenuItemCommand"/>,
    /// <see cref="Commands.InstallChocolateyFileAssociationCommand"/>, and any other commands that need them.
    /// </summary>
    public static class RegistryHelper
    {
        private const int BadKeyWarningThreshold = 10;

        // Searched in this order, which is the order the keys are returned in.
        private static readonly string[] UninstallKeyLocations =
        {
            @"HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        /// <summary>
        /// Finds the uninstall registry keys whose <c>DisplayName</c> matches <paramref name="softwareName"/>.
        /// Keys that cannot be read are skipped.
        /// </summary>
        /// <param name="cmdlet">The cmdlet to read the registry and write messages through.</param>
        /// <param name="softwareName">The display name to match, which may contain wildcards, ignoring case.</param>
        /// <returns>The properties of each matching key.</returns>
        /// <exception cref="WildcardPatternException"><paramref name="softwareName"/> is not a valid wildcard pattern.</exception>
        public static List<PSObject> FindUninstallKeys(PSCmdlet cmdlet, string softwareName)
        {
            return FindUninstallKeys(
                cmdlet,
                softwareName,
                location => PSHelper.GetChildItem(cmdlet, location, recurse: false)
                    .Select(key => (string)key.Properties["PSPath"].Value),
                keyPath => PSHelper.GetItemProperty(cmdlet, keyPath, literalPath: true)
                    .FirstOrDefault());
        }

        /// <summary>
        /// Finds the uninstall keys whose <c>DisplayName</c> matches <paramref name="softwareName"/>, reading the
        /// keys with the functions given.
        /// </summary>
        /// <remarks>
        /// Prefer using <see cref="FindUninstallKeys(PSCmdlet, string)"/> over calling this directly.
        /// This overload is intended as an entry point to allow unit testing of the functionality without
        /// touching the actual PowerShell providers.
        /// </remarks>
        /// <param name="cmdlet">The cmdlet to write messages through.</param>
        /// <param name="softwareName">The display name to match, which may contain wildcards, ignoring case.</param>
        /// <param name="getKeyPaths">A function which gets the provider paths of the keys directly under the provided location.</param>
        /// <param name="getProperties">A function which gets the values of a provided registry key path as properties, or <c>null</c> if it has none.</param>
        /// <returns>The properties of each matching key.</returns>
        internal static List<PSObject> FindUninstallKeys(
            PSCmdlet cmdlet,
            string softwareName,
            Func<string, IEnumerable<string>> getKeyPaths,
            Func<string, PSObject> getProperties)
        {
            var pattern = new WildcardPattern(softwareName, WildcardOptions.IgnoreCase);

            cmdlet.WriteVerbose("Retrieving all uninstall registry keys");

            var keyPaths = GetUninstallKeyPaths(getKeyPaths);

            cmdlet.WriteDebug($"Registry uninstall keys on system: {keyPaths.Count}");

            var foundKeys = GetKeyValues(cmdlet, pattern, keyPaths, getProperties);

            if (foundKeys.Count == 0)
            {
                cmdlet.WriteWarning($"No registry key found based on '{softwareName}'");
            }

            cmdlet.WriteDebug($"Found {foundKeys.Count} uninstall registry key(s) with SoftwareName: '{softwareName}'");

            return foundKeys;
        }

        private static List<string> GetUninstallKeyPaths(Func<string, IEnumerable<string>> getKeyPaths)
        {
            var keyPaths = new List<string>();
            foreach (var location in UninstallKeyLocations)
            {
                try
                {
                    keyPaths.AddRange(getKeyPaths(location));
                }
                catch (Exception error) when (!(error is PipelineStoppedException))
                {
                    // Location may not exist or is inaccessible, so we can skip searching it.
                }
            }

            return keyPaths;
        }

        private static List<PSObject> GetKeyValues(
            PSCmdlet cmdlet,
            WildcardPattern pattern,
            IEnumerable<string> keyPaths,
            Func<string, PSObject> getProperties)
        {
            var foundKeys = new List<PSObject>();
            var badKeys = 0;
            foreach (var keyPath in keyPaths)
            {
                PSObject key;
                try
                {
                    key = getProperties(keyPath);
                }
                catch (Exception error) when (!(error is PipelineStoppedException))
                {
                    cmdlet.WriteVerbose($"Skipping bad key: {keyPath}");
                    badKeys++;
                    continue;
                }

                // A key with no values has no properties, so it never matches.
                if (!(key is null) && pattern.IsMatch(GetDisplayName(key)))
                {
                    foundKeys.Add(key);
                }
            }

            if (badKeys >= BadKeyWarningThreshold)
            {
                cmdlet.WriteWarning("Found 10 or more bad registry keys. Run command again with '--verbose --debug' for more info.");
                cmdlet.WriteDebug("Each key searched should correspond to an installed program. It is very unlikely to have more than a few programs with incorrectly encoded keys, if any at all. This may be indicative of one or more corrupted registry branches.");
            }

            return foundKeys;
        }

        private static string GetDisplayName(PSObject key)
        {
            // A key without a display name matches as an empty name, so it is found by "*".
            return PSHelper.ConvertTo<string>(key.Properties["DisplayName"]?.Value);
        }

        /// <summary>
        /// Gets the path, relative to <c>HKEY_CLASSES_ROOT</c>, of the key for a Windows Explorer context menu item.
        /// </summary>
        /// <param name="key">The name of the menu item's key.</param>
        /// <param name="type">The kind of item the menu item is shown for.</param>
        /// <returns>The path of the menu item's key.</returns>
        public static string GetExplorerMenuItemKeyPath(string key, ExplorerMenuItemType type)
        {
            var itemKey = type == ExplorerMenuItemType.Directory ? "directory" : "*";
            return $@"{itemKey}\shell\{key}";
        }

        /// <summary>
        /// Creates or updates a Windows Explorer context menu item in <c>HKEY_CLASSES_ROOT</c>, which runs
        /// <paramref name="command"/> with the path of the item that was right-clicked.
        /// </summary>
        /// <param name="key">The name of the menu item's key.</param>
        /// <param name="label">The text shown in the context menu.</param>
        /// <param name="command">The command line to run, without the path of the item.</param>
        /// <param name="type">The kind of item the menu item is shown for.</param>
        public static void SetExplorerMenuItem(string key, string label, string command, ExplorerMenuItemType type)
        {
            SetExplorerMenuItem(Registry.ClassesRoot, key, label, command, type);
        }

        /// <summary>
        /// Creates or updates a Windows Explorer context menu item under <paramref name="root"/>.
        /// </summary>
        /// <remarks>
        /// Prefer using <see cref="SetExplorerMenuItem(string, string, string, ExplorerMenuItemType)"/> over calling
        /// this directly. This overload is intended as an entry point to allow unit testing of the functionality
        /// without writing to <c>HKEY_CLASSES_ROOT</c>.
        /// </remarks>
        /// <param name="root">The key to create the menu item under.</param>
        /// <param name="key">The name of the menu item's key.</param>
        /// <param name="label">The text shown in the context menu.</param>
        /// <param name="command">The command line to run, without the path of the item.</param>
        /// <param name="type">The kind of item the menu item is shown for.</param>
        internal static void SetExplorerMenuItem(RegistryKey root, string key, string label, string command, ExplorerMenuItemType type)
        {
            var menuKeyPath = GetExplorerMenuItemKeyPath(key, type);

            SetDefaultValue(root, menuKeyPath, label);
            SetDefaultValue(root, $@"{menuKeyPath}\command", $"{command} \"%1\"");
        }

        /// <summary>
        /// Gets the file extension to associate, trimmed of whitespace and starting with a period.
        /// </summary>
        /// <param name="extension">The extension, with or without its leading period.</param>
        /// <returns>The extension with a leading period, such as <c>.txt</c>.</returns>
        /// <exception cref="ArgumentException">The provided extension is null or whitespace.</exception>
        public static string GetFileAssociationExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentException("The extension cannot be empty or only whitespace.", nameof(extension));
            }

            var trimmed = extension.Trim();
            return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : $".{trimmed}";
        }

        /// <summary>
        /// Gets the executable path to register for a file association. A path that uses environment variables,
        /// such as <c>%ChocolateyInstall%\bin\tool.exe</c>, is kept as given so that the association follows the
        /// variables, as long as it expands to a full path. Any other path is registered as its full path.
        /// The executable must exist.
        /// </summary>
        /// <param name="cmdlet">The cmdlet to resolve a relative path from.</param>
        /// <param name="executable">The executable path as it was given.</param>
        /// <returns>The path to register.</returns>
        /// <exception cref="FileNotFoundException">There is no file at the path, after expanding any environment variables.</exception>
        public static string GetFileAssociationExecutable(PSCmdlet cmdlet, string executable)
        {
            return GetFileAssociationExecutable(executable, path => PSHelper.GetUnresolvedPath(cmdlet, path), File.Exists);
        }

        /// <summary>
        /// Gets the executable path to register for a file association, resolving and checking the path with the
        /// functions given.
        /// </summary>
        /// <remarks>
        /// Prefer using <see cref="GetFileAssociationExecutable(PSCmdlet, string)"/> over calling this directly.
        /// This overload is intended as an entry point to allow unit testing of the functionality without
        /// touching the file system or the PowerShell session state.
        /// </remarks>
        /// <param name="executable">The executable path as it was given.</param>
        /// <param name="resolvePath">A function which gets the full path of a path that may be relative.</param>
        /// <param name="fileExists">A function which checks whether there is a file at a full path.</param>
        /// <returns>The path to register.</returns>
        /// <exception cref="FileNotFoundException">There is no file at the path, after expanding any environment variables.</exception>
        internal static string GetFileAssociationExecutable(string executable, Func<string, string> resolvePath, Func<string, bool> fileExists)
        {
            var expandedExecutable = Environment.ExpandEnvironmentVariables(executable);
            var resolvedExecutable = resolvePath(expandedExecutable);

            if (!fileExists(resolvedExecutable))
            {
                throw new FileNotFoundException($"'{executable}' does not exist, cannot create file association", executable);
            }

            var usesVariables = !string.Equals(expandedExecutable, executable, StringComparison.Ordinal);

            return usesVariables && string.Equals(expandedExecutable, resolvedExecutable, StringComparison.OrdinalIgnoreCase)
                ? executable
                : resolvedExecutable;
        }

        /// <summary>
        /// Gets the name of the file type that an extension is associated with, which is the file name of the
        /// executable with any spaces replaced by underscores.
        /// </summary>
        /// <param name="executable">The path to the executable.</param>
        /// <returns>The file type name, such as <c>my_app.exe</c>.</returns>
        public static string GetFileAssociationType(string executable)
        {
            return Path.GetFileName(executable).Replace(" ", "_");
        }

        /// <summary>
        /// Associates a file extension with an executable.
        /// The extension and the file type's command are written under <c>HKEY_LOCAL_MACHINE\SOFTWARE\Classes</c>,
        /// and the file type's label through <c>HKEY_CLASSES_ROOT</c>.
        /// </summary>
        /// <param name="extension">The extension to associate, with its leading dot.</param>
        /// <param name="executable">The full path to the executable.</param>
        /// <param name="arguments">The arguments passed to the executable, such as <c>"%1"</c>.</param>
        public static void SetFileAssociation(string extension, string executable, string arguments)
        {
            using (var machineClasses = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Classes"))
            {
                SetFileAssociation(machineClasses, Registry.ClassesRoot, extension, executable, arguments);
            }
        }

        /// <summary>
        /// Associates a file extension with an executable, writing under the keys given.
        /// </summary>
        /// <remarks>
        /// Prefer using <see cref="SetFileAssociation(string, string, string)"/> over calling this directly.
        /// This overload is intended as an entry point to allow unit testing of the functionality without
        /// writing to the machine's file associations.
        /// </remarks>
        /// <param name="machineClasses">The key to write the extension and the file type's command under.</param>
        /// <param name="classesRoot">The key to write the file type's label under, after the other values.</param>
        /// <param name="extension">The extension to associate, with its leading dot.</param>
        /// <param name="executable">The full path to the executable.</param>
        /// <param name="arguments">The arguments passed to the executable, such as <c>"%1"</c>.</param>
        internal static void SetFileAssociation(RegistryKey machineClasses, RegistryKey classesRoot, string extension, string executable, string arguments)
        {
            var fileType = GetFileAssociationType(executable);
            var command = string.IsNullOrEmpty(arguments) ? $"\"{executable}\"" : $"\"{executable}\" {arguments}";

            SetDefaultValue(machineClasses, extension, fileType);
            SetDefaultValue(machineClasses, $@"{fileType}\shell\open\command", command, RegistryValueKind.ExpandString);

            // Written last, so that the file type's key already exists under machineClasses.
            SetDefaultValue(classesRoot, fileType, $"{fileType} file");
        }

        private static void SetDefaultValue(RegistryKey root, string keyPath, string value, RegistryValueKind kind = RegistryValueKind.String)
        {
            using (var key = root.CreateSubKey(keyPath))
            {
                key.SetValue(string.Empty, value ?? string.Empty, kind);
            }
        }
    }
}
