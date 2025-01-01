using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Quine.HRCatalog;

namespace Quine.Schemas.Core;

/// <summary>
/// Exception that wraps any errors occuring in methods of <see cref="ExternalProgramComponent"/>.
/// </summary>
public class ExternalProgramComponentException : Exception, IQHException
{
    internal ExternalProgramComponentException(QHMessage hMessage, Exception inner, params object[] args)
        : base(hMessage.Format(args), inner)
    {
        HResult = hMessage.HResult;
    }
}

/// <summary>
/// Utility class for ensuring that an external program component exists, and, when executable,
/// for running it as external process.
/// </summary>
public class ExternalProgramComponent
{
    /// <summary>
    /// The name of the external program component.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Information about the program component's file or directory, including its full path.
    /// </summary>
    public FileSystemInfo ComponentInfo { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="componentName">Name of the component.  Should be recognizable to the user.</param>
    /// <param name="path">Absolute (fully qualified) path to the component; this may be a file or a directory.</param>
    /// <exception cref="ExternalProgramComponentException">
    /// When the required file or directory cannot be found for any reason.
    /// </exception>
    public ExternalProgramComponent(string componentName, string path) {
        ComponentName = QHEnsure.NotEmpty(componentName);
        
        if (string.IsNullOrWhiteSpace(path))
            throw new ExternalProgramComponentException(QHExternalProgramComponent.C_PathNotConfigured, null, componentName);
        if (!Path.IsPathFullyQualified(path))
            throw new ExternalProgramComponentException(QHExternalProgramComponent.C_PathNotAbsolute, null, componentName, path);

        Exception exn = null;
        try {
            if (File.Exists(path)) ComponentInfo = new FileInfo(path);
            else if (Directory.Exists(path)) ComponentInfo = new DirectoryInfo(path);

            if (ComponentInfo == null || !ComponentInfo.Exists)
                exn = new ExternalProgramComponentException(QHExternalProgramComponent.C_PathInvalid, null, componentName, path);
        }
        catch (Exception e) {
            exn = new ExternalProgramComponentException(QHExternalProgramComponent.C_PathInvalid, e, componentName, path);
        }

        if (exn != null)
            throw exn;
    }

    /// <summary>
    /// Attempts to start the file described by <see cref="ComponentInfo"/> as an executable process.
    /// Optionally sets up cancellation.
    /// </summary>
    /// <param name="psi">
    /// Startup info for the process.
    /// The file path is ignored and overwritten by that in <see cref="ComponentInfo"/>.
    /// <c>UseShellExecute</c> is unconditionally set to false.
    /// </param>
    /// <param name="configure">
    /// If not null, the action invoked to further configure the process instance before it's started.
    /// (For example, setting up stdio events.)
    /// The argument passed to the action is the created process, which is the same instance as the
    /// returned value. The action must not throw.  
    /// </param>
    /// <param name="ct">
    /// Cancellation token that triggers <paramref name="cancel"/>,
    /// which must also be provided together with the token.
    /// </param>
    /// <param name="cancel">
    /// Action to shut down the started process when <paramref name="cancel"/> is signalled.
    /// The action must not throw.
    /// </param>
    /// <returns>A started process instance.</returns>
    /// <exception cref="ExternalProgramComponentException">
    /// Wraps any exception that occurred during process startup.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation was detected immediately before or after the process has started.
    /// In the latter case, <paramref name="cancel"/> action is run before the exception is thrown.
    /// </exception>
    public CancellableProcess StartProcess
        (
        ProcessStartInfo psi,
        Action<Process> configure,
        CancellationToken ct = default,
        Action<Process> cancel = null
        )
    {
        QHEnsure.NotNull(psi);

        psi.FileName = ComponentInfo.FullName;
        psi.UseShellExecute = false;

        // Must be declared here because of captures used in registration.
        var cancelled = 0;

        // Attempting to execute a "directory" will fail.  No need for a separate check.        
        var process = new Process() { StartInfo = psi, };
        CancellationTokenRegistration registration = default;
        CancellableProcess ret;
        try {
            if (configure != null)
                QHEnsure.NoThrow(() => configure(process));

            if (ct.CanBeCanceled) {
                QHEnsure.NotNull(cancel);
                registration = ct.Register(RunCancel);
            }

            ret = new(process, registration);
        }
        catch {
            process.Dispose();
            registration.Dispose();
            throw;
        }

        var started = false;
        try {
            // Do not start process if cancellation is requested at this point.
            ct.ThrowIfCancellationRequested();
            process.Start();
            started = true;
            ct.ThrowIfCancellationRequested();
            return ret;
        }
        catch (OperationCanceledException) {
            ret.Dispose();  // Waits until the registered callback has executed.
            if (started)
                RunCancel();
            throw;
        }
        catch (Exception e) when (!started) {   // Internal bug in try{} if started == true.  Let it propagate.
            throw new ExternalProgramComponentException(QHExternalProgramComponent.C_WontRun, e, ComponentName, ComponentInfo.FullName);
        }

        // Ensures that cancel callback is executed exactly once on a started process.
        void RunCancel() {
            try {
                if (process.Id != 0 && Interlocked.Exchange(ref cancelled, 1) == 0)
                    QHEnsure.NoThrow(() => cancel(process));
            }
            catch (InvalidOperationException) {
                // Thrown by process.Id when the process has not started yet.
            }
        }
    }

    /// <summary>
    /// Represents a process that can be cancelled.
    /// </summary>
    /// <remarks>
    /// This type exists to ensure disposal of the underlying <see cref="System.Diagnostics.Process"/>.
    /// To cancel the process, use the cancellation token passed to <see cref="ExternalProgramComponent.StartProcess(ProcessStartInfo, Action{Process}, CancellationToken, Action{Process})"/>.
    /// </remarks>
    public readonly struct CancellableProcess : IDisposable
    {
        /// <summary>
        /// An instance of a successfully started process.
        /// </summary>
        public readonly Process Process;

        /// <summary>
        /// Handle for the registered cancellation action.
        /// </summary>
        public readonly CancellationTokenRegistration Registration;

        internal CancellableProcess(Process process, CancellationTokenRegistration registration) {
            Process = process;
            Registration = registration;
        }

        /// <summary>
        /// Disposes of the process and registration.  The process is NOT cancelled if this is invoked while the process is running.
        /// </summary>
        public void Dispose() {
            Process.Dispose();
            Registration.Dispose();
        }

        /// <summary>
        /// True if the process can be cancelled.
        /// </summary>
        public bool IsCancellable => Registration.Token.CanBeCanceled;
    }
}
