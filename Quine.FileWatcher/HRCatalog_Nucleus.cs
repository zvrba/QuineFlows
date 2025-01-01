#pragma warning disable 1591
using System;

namespace Quine.HRCatalog
{
    public static partial class QHNucleus
    {
        public static class Filesystem
        {
            static Filesystem() { QHFacilities.ValidateFacility(typeof(Filesystem)); }

            public const int Facility = QHFacilities.Nucleus_Filesystem;

            public static readonly QHMessage E_Watchfolder_SiblingConflict = QHMessage.Error(Facility, 1,
                "Conflicting sibling found.");
            public static readonly QHMessage E_Watchfolder_Sibling_TypeConflict = QHMessage.Error(Facility, 2,
                "Conflicting sibling found (invalid type).");
            public static readonly QHMessage E_Watchfolder_Sibling_PatternConflict = QHMessage.Error(Facility, 3,
                "Conflicting sibling found (different pattern).");
            public static readonly QHMessage E_Watchfolder_Sibling_ValueConflict = QHMessage.Error(Facility, 4,
                "Conflicting sibling found (different value).");
            public static readonly QHMessage E_Watchfolder_Path_ParameterConflict = QHMessage.Error(Facility, 5,
                "Conflicting parameter found on the path.");

            // Up to 15: reserved for watch folders.

            // 16-31 : volume manager

            public static readonly QHMessage E_Volume_UnexpectedError = QHMessage.Error(Facility, 16,
                "Volume manager has encountered an unexpected error.");

            public static readonly QHMessage E_Volume_CannotBeInferred = QHMessage.Error(Facility, 17,
                "The provided path is not contained within a known destination.");

            /// <summary>
            /// 0: found volume id, 1: path, 2: requested volume id.
            /// </summary>
            public static readonly QHMessage E_Volume_InconsistentId = QHMessage.Error(Facility, 18,
                "Inconsistent volume state detected: found volume id {0} at {1} while looking for id {2}.");

            /// <summary>
            /// 0: volume name, 1: volume id
            /// </summary>
            public static readonly QHMessage E_Volume_PathNotConfigured = QHMessage.Error(Facility, 19,
                "No usable path configured for volume {0} ({1}).");

            /// <summary>
            /// 0: volume name, 1: volume id
            /// </summary>
            public static readonly QHMessage E_Volume_Offline = QHMessage.Error(Facility, 20,
                "Volume {0} ({1}) is temporarily inaccessible.");

            /// <summary>
            /// 0: path
            /// </summary>
            public static readonly QHMessage E_Volume_CreateOnInvalidDirectory = QHMessage.Error(Facility, 21,
                "Path `{0}` does not reference an existing directory.");

            /// <summary>
            /// 0: path
            /// </summary>
            public static readonly QHMessage E_Volume_CreateOnDriveRoot = QHMessage.Error(Facility, 22,
                "Cannot initialize a volume on drive root `{0}`.");

            /// <summary>
            /// 0: path, 1: # of entries
            /// </summary>
            public static readonly QHMessage E_Volume_CreateInNonemptyDirectory = QHMessage.Error(Facility, 23,
                "Volume must be created in an empty directory. `{0}` contains `{1}` entries.");

            /// <summary>
            /// 0: path, 1: id
            /// </summary>
            public static readonly QHMessage E_Volume_CreateOnExistingVolume = QHMessage.Error(Facility, 24,
                "Path `{0}` belongs to an already initialized volume {1}.");

            /// <summary>
            /// 0: path.
            /// </summary>
            public static readonly QHMessage W_Volume_CannotHideIdFile = QHMessage.Warning(Facility, 25,
                "Could not set hidden flag on file `{0}`.");

            // 32-47: "magazine" problems.

            /// <summary>
            /// 0: file path.
            /// </summary>
            public static readonly QHMessage I_MagazineFileDiscardedByFilter = QHMessage.Information(Facility, 32,
                "File/directory `{0}` is ignored and will not be ingested.");

            /// <summary>
            /// 0: file path; 1: error code.
            /// </summary>
            public static readonly QHMessage E_MagazineEnumerationError = QHMessage.Error(Facility, 33,
                "File/directory `{0}` will not be ingested because enumeration failed.  Error code was: {1}.");

            /// <summary>
            /// 0: file path
            /// </summary>
            public static readonly QHMessage C_InvalidFileInfoType = QHMessage.Critical(Facility, 34,
                "The path `{0}` is neither a file nor directory.");

            // 48- : General filesystem errors.

            /// <summary>
            /// 0: path
            /// </summary>
            public static readonly QHMessage W_CannotDeleteFile = QHMessage.Warning(Facility, 48,
                "Could not delete file `{0}`.  It must be deleted manually.");
        }
    }
}
