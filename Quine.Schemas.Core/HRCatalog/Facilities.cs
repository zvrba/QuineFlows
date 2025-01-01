using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Quine.HRCatalog;

public static class QHFacilities
{
    public const int Bugs = 0;

    // Up to 7: schemas & infrastructure
    public const int Schemas_Core = 1;
    public const int Graph = 7;

    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    // Declaring new facilities is NOT a breaking change.  However, existing
    // numbering MUST NOT be changed as other code may depend on event ids
    // that have already been serialized to various databases.
    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

    public const int Nucleus_ExternalProgramComponent = 8;
    public const int Nucleus_Filesystem = 14;
    // Up to 64: reserved for nucleus

    /// <summary>
    /// Ensures that all <see cref="QHMessage"/> ids are unique within <paramref name="class"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">A duplicate HResult constant was found.</exception>
    public static void ValidateFacility(Type @class) {
        var ids = new HashSet<QHResult>();
        foreach (var member in @class.GetMembers(BindingFlags.Public | BindingFlags.Static)) {
            if (member.MemberType != MemberTypes.Field)
                continue;
            if (member.Name == "Facility")
                continue;

            var fi = (FieldInfo)member;
            if (fi.FieldType != typeof(QHMessage))
                continue;

            var hr = ((QHMessage)fi.GetValue(null)).HResult;
            if (!ids.Add(hr))
                throw new InvalidDataException($"Field {fi.FieldType.Name} has a duplicate HResult.");
        }
    }
}
