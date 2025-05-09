// Copyright © 2017 - 2025 Chocolatey Software, Inc
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

using Chocolatey.PowerShell.Helpers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Chocolatey.PowerShell.Shared
{
    /// <summary>
    /// Base class for handling console applications that need to be called from PowerShell,
    /// ensuring that their output is redirected correctly.
    /// 
    /// Each instance of this class may be used to handle operations for a single process.
    /// </summary>
    public abstract class ProcessHandler : IDisposable
    {
        private readonly CancellationToken _pipelineStopToken;
        private bool _disposedValue;
        private bool _started;

        /// <summary>
        /// The underlying <see cref="System.Diagnostics.Process" /> object.
        /// </summary>
        protected Process Process { get; private set; }

        /// <summary>
        /// The blocking collection used to handle output messages from the process.
        /// In order to customise how these messages are handled, override the <see cref="HandleProcessMessages"/> virtual method.
        /// </summary>
        protected BlockingCollection<ProcessOutput> ProcessMessages;

        /// <summary>
        /// The original cmdlet used to instantiate and invoke the process.
        /// </summary>
        protected readonly PSCmdlet Cmdlet;

        /// <summary>
        /// Instantiates a new <see cref="ProcessHandler"/> for a given <paramref name="cmdlet"/> using its <paramref name="pipelineStopToken"/>.
        /// </summary>
        /// <param name="cmdlet">The cmdlet invoking the process.</param>
        /// <param name="pipelineStopToken">The cmdlet's <see cref="ChocolateyCmdlet.PipelineStopToken"/>.</param>
        public ProcessHandler(PSCmdlet cmdlet, CancellationToken pipelineStopToken)
        {
            Cmdlet = cmdlet;
            _pipelineStopToken = pipelineStopToken;
        }

        protected int StartProcess(string processName, string workingDirectory, string arguments, bool elevated, ProcessWindowStyle windowStyle, bool noNewWindow)
        {
            return StartProcess(processName, workingDirectory, arguments, sensitiveStatements: null, elevated, windowStyle, noNewWindow);
        }

        protected int StartProcess(string processName, string workingDirectory, string arguments, string sensitiveStatements, ProcessWindowStyle windowStyle, bool noNewWindow)
        {
            return StartProcess(processName, workingDirectory, arguments, sensitiveStatements, elevated: false, windowStyle, noNewWindow);
        }

        protected int StartProcess(string processName, string workingDirectory, string arguments, ProcessWindowStyle windowStyle, bool noNewWindow)
        {
            return StartProcess(processName, workingDirectory, arguments, sensitiveStatements: null, windowStyle, noNewWindow);
        }

        protected int StartProcess(string processName, string workingDirectory, string arguments, string sensitiveStatements)
        {
            return StartProcess(processName, workingDirectory, arguments, sensitiveStatements, ProcessWindowStyle.Hidden, noNewWindow: true);
        }

        protected int StartProcess(string processName, string workingDirectory, string arguments)
        {
            return StartProcess(processName, workingDirectory, arguments, sensitiveStatements: null);
        }

        /// <summary>
        /// Starts the given process by name or path, with the provided arguments.
        /// </summary>
        /// <param name="processName">The name or path of the process to start.</param>
        /// <param name="workingDirectory">The working directory to start the process in.</param>
        /// <param name="arguments">Arguments to pass to the process. These will be logged.</param>
        /// <param name="sensitiveStatements">Sensitive arguments to pass to the process. These will not be logged.</param>
        /// <param name="elevated">Whether to attempt elevation. This currently cannot elevate processes from a non-elevated context.</param>
        /// <param name="windowStyle">Whether to show windows for the process.</param>
        /// <param name="noNewWindow">Whether to run in the current window.</param>
        /// <returns>The exit code from the process.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the process has already been started.</exception>
        protected int StartProcess(string processName, string workingDirectory, string arguments, string sensitiveStatements, bool elevated, ProcessWindowStyle windowStyle, bool noNewWindow)
        {
            if (_started)
            {
                throw new InvalidOperationException("A process has already been started.");
            }

            if (string.IsNullOrWhiteSpace(processName))
            {
                throw new ArgumentNullException(nameof(processName), "No process name was provided.");
            }

            _started = true;

            var alreadyElevated = ProcessInformation.IsElevated();

            Process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo
                {
                    FileName = processName,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory,
                    WindowStyle = windowStyle,
                    CreateNoWindow = noNewWindow,
                },
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                Process.StartInfo.Arguments = arguments;
            }

            if (!string.IsNullOrWhiteSpace(sensitiveStatements))
            {
                PSHelper.WriteHost(Cmdlet, "Sensitive arguments have been passed. Adding to arguments.");
                Process.StartInfo.Arguments += " " + sensitiveStatements;
            }

            if (elevated && !alreadyElevated && Environment.OSVersion.Version > new Version(6, 0))
            {
                // SELF-ELEVATION: This currently doesn't work as we're not using ShellExecute
                Cmdlet.WriteDebug("Setting RunAs for elevation");
                Process.StartInfo.Verb = "RunAs";
            }

            Process.OutputDataReceived += ProcessOutputHandler;
            Process.ErrorDataReceived += ProcessErrorHandler;

            // process.WaitForExit() is a bit unreliable, we use the Exiting event handler to register when
            // the process exits.
            Process.Exited += ProcessExitingHandler;

            var exitCode = 0;

            try
            {
                ProcessMessages = new BlockingCollection<ProcessOutput>();
                Process.Start();
                Process.BeginOutputReadLine();
                Process.BeginErrorReadLine();

                Cmdlet.WriteDebug("Waiting for process to exit");

                // This will handle dispatching output/error messages until either the process has exited or the pipeline
                // has been cancelled.
                HandleProcessMessages();
            }
            catch (Win32Exception error)
            {
                throw new IOException($"There was an error starting the target process '{processName}': {error.Message}", error);
            }
            catch (ObjectDisposedException error)
            {
                // This means that something has disposed the process object before we could start it.
                // This would typically mean that Ctrl+C / StopProcessing() has been called on the
                // cmdlet before we got here, but after we created the Process object.
                throw new OperationCanceledException($"The current operation was cancelled before the process could be started.", error);
            }
            finally
            {
                Process.OutputDataReceived -= ProcessOutputHandler;
                Process.ErrorDataReceived -= ProcessErrorHandler;
                Process.Exited -= ProcessExitingHandler;

                exitCode = Process.ExitCode;
                Process.Dispose();
            }

            Cmdlet.WriteDebug($"Command [\"{Process}\" {arguments}] exited with '{exitCode}'.");

            return exitCode;
        }

        protected virtual void HandleProcessMessages()
        {
            if (ProcessMessages is null)
            {
                return;
            }

            // Use of the _pipelineStopToken allows us to respect calls for StopProcessing() correctly.
            foreach (var item in ProcessMessages.GetConsumingEnumerable(_pipelineStopToken))
            {
                if (item.StdErr)
                {
                    Cmdlet.WriteError(new RuntimeException(item.Message).ErrorRecord);
                }
                else
                {
                    Cmdlet.WriteVerbose(item.Message);
                }
            }
        }

        /// <summary>
        /// Raised when the underlying <see cref="Process"> exits.
        /// </summary>
        protected event EventHandler ProcessExited;

        /// <summary>
        /// Raised when the underlying <see cref="Process"> emits non-null/empty data to stdout.
        /// </summary>
        protected event EventHandler<ProcessOutput> ProcessOutputReceived;

        /// <summary>
        /// Raised when the underlying <see cref="Process"> emits non-null/empty data to stderr.
        /// </summary>
        protected event EventHandler<ProcessOutput> ProcessErrorDataReceived;

        private void ProcessExitingHandler(object sender, EventArgs e)
        {
            ProcessMessages?.CompleteAdding();
            ProcessExited?.Invoke(sender, e);
        }

        private void ProcessOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var message = new ProcessOutput(e.Data, isStdErr: false);
                ProcessMessages?.Add(message);
                ProcessOutputReceived?.Invoke(sender, message);
            }
        }

        private void ProcessErrorHandler(object sender, DataReceivedEventArgs e)
        {
            if (!(e.Data is null))
            {
                var message = new ProcessOutput(e.Data, isStdErr: true);
                ProcessMessages?.Add(message);
                ProcessErrorDataReceived?.Invoke(sender, message);
            }
        }

        /// <summary>
        /// Kill the process while it is running. Has no effect if the process has already
        /// stopped. This operation may fail, but should never raise an exception.
        /// 
        /// This can be overridden to apply additional logic or cleanup operations to
        /// ensure a process terminates cleanly.
        /// </summary>
        public virtual void Terminate()
        {
            try
            {
                if (!(Process is null))
                {
                    // We don't need to handle disposing the process here, as the Process.Exiting
                    // event is leveraged to halt message processing, which will then continue to
                    // dispose the process shortly thereafter.
                    Process.Kill();
                }
            }
            catch
            {
                // Suppress exceptions if the process cannot be stopped or has already stopped.
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // End the process being run.
                    Terminate();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The event args passed to <see cref="ProcessOutputReceived"/> and <see cref="ProcessErrorDataReceived"/>.
        /// </summary>
        protected class ProcessOutput : EventArgs
        {
            public ProcessOutput(string message, bool isStdErr)
            {
                Message = message;
                StdErr = isStdErr;
            }

            /// <summary>
            /// The message string passed to stdout or stderr.
            /// </summary>
            public string Message { get; private set; }

            /// <summary>
            /// True if the message came from stderr.
            /// </summary>
            public bool StdErr { get; private set; }
        }
    }
}
