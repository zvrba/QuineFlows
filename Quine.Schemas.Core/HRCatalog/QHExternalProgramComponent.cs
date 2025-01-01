namespace Quine.HRCatalog;

public static class QHExternalProgramComponent
{
    static QHExternalProgramComponent() { QHFacilities.ValidateFacility(typeof(QHExternalProgramComponent)); }

    public const int Facility = QHFacilities.Nucleus_ExternalProgramComponent;

    /// <summary>
    /// 0: component name.
    /// </summary>
    public static readonly QHMessage C_PathNotConfigured = QHMessage.Critical(Facility, 0,
        "Path to {0} is not configured.");

    /// <summary>
    /// 0: component name, 1: configured path
    /// </summary>
    public static readonly QHMessage C_PathInvalid = QHMessage.Critical(Facility, 1,
        "Path to {0} is configured to `{1}`, but the file does not exist or is not accessible.");

    /// <summary>
    /// 0: component name, 1: configured path.
    /// </summary>
    public static readonly QHMessage C_WontRun = QHMessage.Critical(Facility, 2,
        "Path to {0} is configured to `{1}`, but the process could not run.");

    /// <summary>
    /// 0: component name, 1: configured path.
    /// </summary>
    public static readonly QHMessage C_PathNotAbsolute = QHMessage.Critical(Facility, 3,
        "Path to {0} is configured to `{1}`, which is not an absolute path.");

    /// <summary>
    /// 0: component name
    /// </summary>
    public static readonly QHMessage E_InternalError = QHMessage.Error(Facility, 4,
        "{0} encountered unexpected error during external program invocation.");

    /// <summary>
    /// 0: component name; 1: full path to the file, 2: cause.
    /// </summary>
    public static readonly QHMessage I_FileCleanupFailed = QHMessage.Information(Facility, 5,
        "{0} could not delete temporary file `{1}`: {2}");

    /// <summary>
    /// 0: component name; 1: full path to the directory, 2: cause.
    /// </summary>
    public static readonly QHMessage I_DirectoryCleanupFailed = QHMessage.Information(Facility, 6,
        "{0} could not delete temporary directory `{1}`: {2}");

    /// <summary>
    /// 0: name of external process; 1: exception type; 2: message.
    /// </summary>
    public static readonly QHMessage I_KillFailed = QHMessage.Information(Facility, 7,
        "Could not cancel external process {0}: {1}: {2}");
}
