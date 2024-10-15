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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Threading;
using Chocolatey.PowerShell.Helpers;

namespace Chocolatey.PowerShell.Shared
{
    /// <summary>
    /// Base class for all Chocolatey cmdlets.
    /// Contains a number of helpers and common code that is used by all cmdlets.
    /// </summary>
    public abstract class ChocolateyCmdlet : PSCmdlet
    {
        // Place deprecated command names and their corresponding replacement in this dictionary to have those commands
        // warn users about the deprecation when they are called by those names.
        private readonly Dictionary<string, string> _deprecatedCommandNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Use the following format to provide a deprecation notice. If the new command name is an empty string,
            // the warning will inform the user it is to be removed instead of renamed.
            //
            // { "Deprecated-CommandName", "New-CommandName" },
        };

        // These members are used to coordinate use of StopProcessing()
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _pipelineStopTokenSource = new CancellationTokenSource();

        /// <summary>
        /// A cancellation token that will be triggered when StopProcessing() is called.
        /// Use this cancellation token for any .NET methods called that accept a cancellation token,
        /// and prefer overloads that accept a cancellation token.
        /// This will allow Ctrl+C / StopProcessing to be handled appropriately by commands.
        /// </summary>
        protected CancellationToken PipelineStopToken
        {
            get
            {
                return _pipelineStopTokenSource.Token;
            }
        }

        /// <summary>
        /// Convenience property to access MyInvocation.BoundParameters, the bound parameters for the
        /// cmdlet call.
        /// </summary>
        protected Dictionary<string, object> BoundParameters
        {
            get
            {
                return MyInvocation.BoundParameters;
            }
        }

        /// <summary>
        /// The canonical error ID for the command to assist with traceability.
        /// For more specific error IDs where needed, use <c>"{ErrorId}.EventName"</c>.
        /// </summary>
        protected string ErrorId
        {
            get
            {
                return GetType().Name + "Error";
            }
        }

        /// <summary>
        /// Gets the directory that Chocolatey is installed in.
        /// </summary>
        protected string ChocolateyInstallLocation
        {
            get
            {
                return PSHelper.GetInstallLocation(this);
            }
        }

        protected bool Debug
        {
            get
            {
                return MyInvocation.BoundParameters.ContainsKey("Debug")
                    ? PSHelper.ConvertTo<SwitchParameter>(MyInvocation.BoundParameters["Debug"]).ToBool()
                    : PSHelper.ConvertTo<ActionPreference>(GetVariableValue(PreferenceVariables.Debug)) != ActionPreference.SilentlyContinue;
            }
        }

        /// <summary>
        /// For compatibility reasons, we always add the -IgnoredArguments parameter, so that newly added parameters
        /// won't break things too much if a package is run with an older version of Chocolatey.
        /// </summary>
        [Parameter(ValueFromRemainingArguments = true)]
        public object[] IgnoredArguments { get; set; }

        /// <summary>
        /// Sets whether the cmdlet writes its parameters and name to the debug log when it is called and
        /// when it completes its operation (after End() is called).
        /// This should remain set to true for all commands that are considered part of the public Chocolatey CLI API,
        /// unless there are concerns about potentially sensitive information making it into a log file from the parameters of the command.
        /// </summary>
        protected virtual bool Logging { get; } = true;

        private void WriteCmdletCallDebugMessage()
        {
            if (!Logging)
            {
                return;
            }

            var logMessage = new StringBuilder()
                .Append("Running ")
                .Append(MyInvocation.InvocationName);

            foreach (var param in MyInvocation.BoundParameters)
            {
                var paramNameLower = param.Key.ToLower();

                if (paramNameLower == "ignoredarguments")
                {
                    continue;
                }

                var paramValue = paramNameLower == "sensitivestatements" || paramNameLower == "password"
                    ? "[REDACTED]"
                    : param.Value is IList list
                        ? string.Join(" ", list)
                        : LanguagePrimitives.ConvertTo(param.Value, typeof(string));

                logMessage.Append($" -{param.Key} '{paramValue}'");
            }

            WriteDebug(logMessage.ToString());
        }

        private void WriteCmdletCompletionDebugMessage()
        {
            if (!Logging)
            {
                return;
            }

            WriteDebug($"Finishing '{MyInvocation.InvocationName}'");
        }

        private void WriteWarningForDeprecatedCommands()
        {
            if (_deprecatedCommandNames.TryGetValue(MyInvocation.InvocationName, out var replacement))
            {
                var message = string.IsNullOrEmpty(replacement)
                    ? $"The command '{MyInvocation.InvocationName}' is deprecated and will be removed in a future version"
                    : $"The '{MyInvocation.InvocationName}' alias is deprecated and will be removed in a future version. Use '{replacement}' to ensure compatibility with future versions of Chocolatey.";
                WriteWarning(message);
            }
        }

        protected sealed override void BeginProcessing()
        {
            WriteWarningForDeprecatedCommands();
            WriteCmdletCallDebugMessage();
            Begin();
        }

        /// <summary>
        /// Override this method to define the cmdlet's begin {} block behaviour.
        /// Note that parameters that are defined as ValueFromPipeline or ValueFromPipelineByPropertyName
        /// will not be available for the duration of this method.
        /// </summary>
        protected virtual void Begin()
        {
        }

        protected sealed override void ProcessRecord()
        {
            Process();
        }

        /// <summary>
        /// Override this method to define the cmdlet's process {} block behaviour.
        /// This is called once for every item the cmdlet receives to a pipeline parameter, or only once if the value is supplied directly.
        /// Parameters that are defined as ValueFromPipeline or ValueFromPipelineByPropertyName will be available during this method call.
        /// </summary>
        protected virtual void Process()
        {
        }

        protected sealed override void EndProcessing()
        {
            End();
            WriteCmdletCompletionDebugMessage();
        }

        /// <summary>
        /// Override this method to define the cmdlet's end {} block behaviour.
        /// Note that parameters that are defined as ValueFromPipeline or ValueFromPipelineByPropertyName
        /// may not be available or have complete data during this method call.
        /// </summary>
        protected virtual void End()
        {
        }

        protected sealed override void StopProcessing()
        {
            lock (_lock)
            {
                _pipelineStopTokenSource.Cancel();
                Stop();
            }
        }

        /// <summary>
        /// Override this method to define the cmdlet's behaviour when being asked to stop/cancel processing.
        /// Note that this method will be called by <see cref="StopProcessing"/>, after an exclusive lock is
        /// obtained. Do not call this method manually.
        /// </summary>
        /// <remarks>
        /// The <see cref="PipelineStopToken"/> will be triggered before this method is called. This method
        /// need be called only if the cmdlet overriding it has its own stop or dispose behaviour that also
        /// needs to be managed that are not dependent on the <see cref="PipelineStopToken"/>.
        /// </remarks>
        protected virtual void Stop()
        {
        }


        /// <summary>
        /// Write a message directly to the host console, bypassing any output streams.
        /// </summary>
        /// <param name="message"></param>
        protected void WriteHost(string message)
        {
            PSHelper.WriteHost(this, message);
        }

        /// <summary>
        /// Write an object to the pipeline, enumerating its contents.
        /// Use <see cref="Cmdlet.WriteObject(object, bool)"/> to disable enumerating collections.
        /// </summary>
        /// <param name="value"></param>
        protected new void WriteObject(object value)
        {
            PSHelper.WriteObject(this, value);
        }

        /// <summary>
        /// Get an environment variable from the current process scope by name.
        /// </summary>
        /// <param name="name">The name of the variable to retrieve.</param>
        /// <returns>The value of the environment variable.</returns>
        protected string EnvironmentVariable(string name)
        {
            return EnvironmentHelper.GetVariable(name);
        }

        /// <summary>
        /// Gets an environment variable from the target scope by name, expanding
        /// environment variable tokens present in the value.
        /// </summary>
        /// <param name="name">The name of the variable to retrieve.</param>
        /// <param name="scope">The scope to retrieve the variable from.</param>
        /// <returns>The value of the environment variable.</returns>
        protected string EnvironmentVariable(string name, EnvironmentVariableTarget scope)
        {
            return EnvironmentVariable(name, scope, preserveVariables: false);
        }

        /// <summary>
        /// Gets an environment variable from the target scope by name, expanding
        /// environment variable tokens present in the value only if specified.
        /// </summary>
        /// <param name="name">The name of the variable to retrieve.</param>
        /// <param name="scope">The scope to retrieve the variable from.</param>
        /// <param name="preserveVariables"><c>True</c> if variables should be preserved, <c>False</c> if variables should be expanded.</param>
        /// <returns>The value of the environment variable.</returns>
        protected string EnvironmentVariable(string name, EnvironmentVariableTarget scope, bool preserveVariables)
        {
            return EnvironmentHelper.GetVariable(this, name, scope, preserveVariables);
        }

        protected bool IsEqual(object first, object second)
        {
            return PSHelper.IsEqual(first, second);
        }
    }
}
