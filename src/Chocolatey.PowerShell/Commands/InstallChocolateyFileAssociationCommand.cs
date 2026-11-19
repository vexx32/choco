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
using System.IO;
using System.Management.Automation;
using Chocolatey.PowerShell.Helpers;
using Chocolatey.PowerShell.Shared;

namespace Chocolatey.PowerShell.Commands
{
    [Cmdlet(VerbsLifecycle.Install, "ChocolateyFileAssociation", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(void))]
    public class InstallChocolateyFileAssociationCommand : ChocolateyCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Extension { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        [Alias("Application")]
        public string Executable { get; set; }

        [Parameter(Position = 2)]
        [Alias("Args")]
        public string Arguments { get; set; } = "\"%1\" \"%*\"";

        protected override void End()
        {
            if (string.IsNullOrWhiteSpace(Extension))
            {
                var error = new ArgumentException("The extension cannot be empty or only whitespace.", nameof(Extension));
                ThrowTerminatingError(new ErrorRecord(error, $"{ErrorId}.EmptyExtension", ErrorCategory.InvalidArgument, Extension));
            }

            string executable;
            try
            {
                executable = RegistryHelper.GetFileAssociationExecutable(this, Executable);
            }
            catch (FileNotFoundException error)
            {
                ThrowTerminatingError(new ErrorRecord(error, $"{ErrorId}.FileNotFound", ErrorCategory.ObjectNotFound, Executable));
                return;
            }

            var extension = RegistryHelper.GetFileAssociationExtension(Extension);
            if (!ShouldProcess(extension, $"Associate with '{executable}'"))
            {
                return;
            }

            Elevation.RunElevated(
                this,
                () => RegistryHelper.SetFileAssociation(extension, executable, Arguments),
                $"Install-ChocolateyFileAssociation -Extension '{Escape(extension)}' -Executable '{Escape(executable)}' -Arguments '{Escape(Arguments)}'");

            PSHelper.WriteHost(this, $"'{extension}' has been associated with '{executable}'");
        }

        private static string Escape(string value)
        {
            return PSHelper.EscapeSingleQuotedStringContent(value);
        }
    }
}
