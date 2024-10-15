using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Chocolatey.PowerShell.Helpers;
using System.Diagnostics;
using System.Linq;

namespace Chocolatey.PowerShell.Shared
{
    public abstract class ProcessHandler
    {
        private TaskCompletionSource<bool> _eventHandled;
        private readonly CancellationToken _pipelineStopToken;

        protected BlockingCollection<(string message, bool isError)> ProcessMessages;
        protected readonly PSCmdlet Cmdlet;

        internal ProcessHandler(PSCmdlet cmdlet, CancellationToken pipelineStopToken)
        {
            Cmdlet = cmdlet;
            _pipelineStopToken = pipelineStopToken;
        }

        internal void CancelWait()
        {
            _eventHandled?.SetCanceled();
        }

        protected int StartProcess(string processName, string workingDirectory, string arguments, string sensitiveStatements, bool elevated, ProcessWindowStyle windowStyle, bool noNewWindow)
        {
            var alreadyElevated = ProcessInformation.IsElevated();

            var process = new Process
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
                process.StartInfo.Arguments = arguments;
            }

            if (elevated && !alreadyElevated && Environment.OSVersion.Version > new Version(6, 0))
            {
                // This currently doesn't work as we're not using ShellExecute
                PSHelper.WriteDebug(Cmdlet, "Setting RunAs for elevation");
                process.StartInfo.Verb = "RunAs";
            }

            process.OutputDataReceived += ProcessOutputHandler;
            process.ErrorDataReceived += ProcessErrorHandler;

            // process.WaitForExit() is a bit unreliable, we use the Exiting event handler to register when
            // the process exits.
            process.Exited += ProcessExitingHandler;


            var exitCode = 0;

            try
            {
                _eventHandled = new TaskCompletionSource<bool>();
                ProcessMessages = new BlockingCollection<(string message, bool isError)>();
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                PSHelper.WriteDebug(Cmdlet, "Waiting for process to exit");

                // This will handle dispatching output/error messages until either the process has exited or the pipeline
                // has been cancelled.
                HandleProcessMessages();
            }
            finally
            {
                process.OutputDataReceived -= ProcessOutputHandler;
                process.ErrorDataReceived -= ProcessErrorHandler;
                process.Exited -= ProcessExitingHandler;

                exitCode = process.ExitCode;
                process.Dispose();
            }

            PSHelper.WriteDebug(Cmdlet, $"Command [\"{process}\" {arguments}] exited with '{exitCode}'.");

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
                if (item.isError)
                {
                    Cmdlet.WriteError(new RuntimeException(item.message).ErrorRecord);
                }
                else
                {
                    PSHelper.WriteVerbose(Cmdlet, item.message);
                }
            }
        }

        protected virtual void ProcessExitingHandler(object sender, EventArgs e)
        {
            _eventHandled?.TrySetResult(true);
            ProcessMessages?.CompleteAdding();
        }

        protected virtual void ProcessOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!(e.Data is null))
            {
                ProcessMessages?.Add((e.Data, false));
            }
        }

        protected virtual void ProcessErrorHandler(object sender, DataReceivedEventArgs e)
        {
            if (!(e.Data is null))
            {
                ProcessMessages?.Add((e.Data, true));
            }
        }
    }
}
