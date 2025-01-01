using System;
using System.Collections.Generic;
using System.Text;

namespace Quine.HRCatalog
{
    /// <summary>
    /// Exceptions thrown directly from <c>Quine.Schemas.*</c> assemblies.  Facility 1.
    /// </summary>
    public static partial class QHSchemas
    {
        /// <summary>
        /// Exceptions thrown from <c>Quine.Schemas.Core</c>.
        /// </summary>
        public static class Core
        {
            static Core() { QHFacilities.ValidateFacility(typeof(Core)); }

            public const int Facility = QHFacilities.Schemas_Core;

            public static readonly QHMessage PathFormat_ForbiddenCharacter = QHMessage.Error(Facility, 1,
                "Found forbidden character {1} (code 0x{2:X4}) in path component.");

            public static readonly QHMessage PathFormat_EmptyAbsolutePath = QHMessage.Error(Facility, 2,
                "Empty path must not be absolute.");

            public static readonly QHMessage PathFormat_EmptyPathComponent = QHMessage.Error(Facility, 3,
                "Empty component found in the path.");

            public static readonly QHMessage PathFormat_ForbidRoot = QHMessage.Error(Facility, 4,
                "Filesystem root is forbidden due to security precautions.");

            public static readonly QHMessage PathFormat_SmbServerMissing = QHMessage.Error(Facility, 5,
                "Invalid SMB path: empty server part.");

            public static readonly QHMessage PathFormat_SmbShareMissing = QHMessage.Error(Facility, 6,
                "Invalid SMB path: empty share name part.");

            public static readonly QHMessage PathFormat_ForbidDriveRelative = QHMessage.Error(Facility, 7,
                "Drive-relative paths are forbidden.");

            public static readonly QHMessage PathFormat_InvalidDrive = QHMessage.Error(Facility, 8,
                "Invalid drive letter in root part.");

            // TODO: Path can't end with '.', e.g., filename like "asdf." is not allowed.
            // Error codes up to 15 reserved for further extensions to path components.

            public static readonly QHMessage TemplateFormat_DuplicateParameter = QHMessage.Error(Facility, 16,
                "Duplicate parameter found.");

            public static readonly QHMessage TemplateFormat_AmbiguousMatch = QHMessage.Error(Facility, 17,
                "Template matches the input ambiguously.");

            public static readonly QHMessage TemplateFormat_MissingValue = QHMessage.Error(Facility, 18,
                "No value provided for the parameter.");

            public static readonly QHMessage TemplateFormat_InvalidRegex = QHMessage.Error(Facility, 19,
                "Parameter translator returned an invalid regex.");

            // Error codes up to 23 reserved for further extensions to template variable processor

            public static readonly QHMessage RationalNumber_SignFormat = QHMessage.Error(Facility, 24,
                "Unsupported rational number format: the number must be positive or zero.");

            public static readonly QHMessage Timecode_InvalidFormat = QHMessage.Error(Facility, 25,
                "Invalid timecode format.");

            public static readonly QHMessage Timecode_InvalidFields = QHMessage.Error(Facility, 26,
                "Timecode fields are out of their valid range.");

            // Error codes up to 31 reserved various rational and timecode exceptions
        }
    }
}
