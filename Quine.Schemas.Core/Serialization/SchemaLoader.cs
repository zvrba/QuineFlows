using System;
using System.IO;
using System.Linq;

namespace Quine.Schemas
{
    /// <summary>
    /// Helper class for loading schemas.  Schemas must be loaded prior to using methods in <see cref="DCSerializer"/> 
    /// because all types must be registered with <see cref="System.Runtime.Serialization.DataContractSerializer"/>
    /// upfront.
    /// </summary>
    public static class SchemaLoader
    {
        /// <summary>
        /// Loads all assemblies matching the pattern <code>Quine.Schemas.*.dll</code> from the application's directory.
        /// </summary>
        public static void Load() {
            var quineSchemas = Directory.GetFiles(".", "Quine.Schemas.*.dll")
                .Where(x => !x.StartsWith(".\\Quine.Schemas.Externals"));
            foreach (var f in quineSchemas)
                System.Reflection.Assembly.LoadFrom(f);
        }
    }
}
