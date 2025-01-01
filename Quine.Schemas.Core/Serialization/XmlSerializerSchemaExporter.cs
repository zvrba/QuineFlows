using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Quine.Schemas
{
    /// <summary>
    /// Utility class to load all assemblies from the application's directory and export them to XSD.
    /// Classes in <c>Quine.Schemas.Externals</c> will NOT be exported.  Schemas are written to files
    /// named <c>Schema{0:D2}.xsd.</c>
    /// </summary>
    public static class XmlSerializerSchemaExporter
    {
        // Must be static ctor so that assemblies are loaded BEFORE Serializer.ExportedTypes.
        static XmlSerializerSchemaExporter() {
            SchemaLoader.Load();
        }

        public static void Export() {
            var schemas = new XmlSchemas();
            var exporter = new XmlSchemaExporter(schemas);
            var refimp = new XmlReflectionImporter();
            List<XmlTypeMapping> mappings = new List<XmlTypeMapping>();

            foreach (var t in XSerializer.ExportedTypes) {
                var m = refimp.ImportTypeMapping(t);
                exporter.ExportTypeMapping(m);
            }

            int counter = 0;
            foreach (System.Xml.Schema.XmlSchema s in schemas)
                using (var f = File.Open(String.Format("Schema{0:D2}.xsd", counter++), FileMode.Create)) {
                    s.Write(f);
                }
        }
    }
}
