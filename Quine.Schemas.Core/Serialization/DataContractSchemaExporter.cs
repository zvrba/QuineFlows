using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Quine.Schemas
{
    // NOTE: works only with NET472!
    public static class DataContractSchemaExporter
    {
        // Must be static ctor so that assemblies are loaded BEFORE Serializer.ExportedTypes.
        static DataContractSchemaExporter() {
            SchemaLoader.Load();
        }

        public static void Export() {
            var catalog = new Dictionary<string, string>(); // NS -> filename
            var qns = new HashSet<(string, string)>();

            var exporter = new XsdDataContractExporter();
            foreach (var t in DCSerializer.ExportedTypes) {
                if (!exporter.CanExport(t)) {
                    Console.WriteLine("Cannot export type: " + t.FullName);
                    continue;
                }

                var st = exporter.GetSchemaTypeName(t);
                if (!qns.Add((st.Namespace, st.Name)))
                    throw new InvalidOperationException($"Duplicate data contract name {st}.");
                
                exporter.Export(t);
            }

            var schemas = exporter.Schemas.Schemas();
            foreach (XmlSchema s in schemas) {
                var filename = GetFilenameForNamespace(s.TargetNamespace);
                catalog.Add(s.TargetNamespace, filename);
                using (var f = File.Open(filename, FileMode.Create)) {
                    s.Write(f);
                }
            }

            WriteCatalog("CATALOG.XML", catalog);
        }

        //const string CatNs = "urn:oasis:names:tc:entity:xmlns:xml:catalog";

        static readonly XNamespace CatNs = "urn:oasis:names:tc:entity:xmlns:xml:catalog";

        static void WriteCatalog(string filename, Dictionary<string, string> catalog) {
            var root = new XElement(CatNs + "catalog");
            var xdoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XDocumentType(
                    "catalog",
                    "-//OASIS//DTD XML Catalogs V1.1//EN",
                    "http://www.oasis-open.org/committees/entity/release/1.1/catalog.dtd",
                    null),
                root);

            foreach (var e in catalog) {
                var entry = new XElement(CatNs + "public",
                    new XAttribute("publicId", e.Key),
                    new XAttribute("uri", e.Value));
                root.Add(entry);
            }

            using (var tw = new StreamWriter(filename, false, new System.Text.UTF8Encoding(false)))
                xdoc.Save(tw);
        }

        static string GetFilenameForNamespace(string ns) {
            var u = new Uri(ns);
            var p = u.Authority + u.AbsolutePath;
            p = p.TrimEnd('/').Replace('.', '_').Replace('/', '_');
            return p + ".xsd";
        }
    }
}
