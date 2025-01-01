using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Quine.Schemas
{
    /// <summary>
    /// Simple methods for (de)serializing types with <see cref="XmlSerializer"/>.
    /// </summary>
    public static class XSerializer
    {
        private static readonly Dictionary<Type, XmlSerializer> Serializers =
            new Dictionary<Type, XmlSerializer>();

        // XSerializer is used for external documents that aren't put into SQL, so XML declaration is present.
        // (With SQL server, the XML declaration specifies UTF-8, but this doesn't match the SQL servers Unicode charset,
        // resulting in an error.)
        public static readonly XmlWriterSettings WriterSettings = new XmlWriterSettings() {
            ConformanceLevel = ConformanceLevel.Auto,
            OmitXmlDeclaration = false,
            Indent = true,
            IndentChars = "\t",
            Encoding = System.Text.Encoding.UTF8
        };

        public static readonly Type[] ExportedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName.StartsWith("Quine.Schemas."))
            .SelectMany(a => a.ExportedTypes.Where(t => IsXmlExported(t)))
            .ToArray();


        public static XmlSerializer Get(Type t) {
            if (!t.CustomAttributes.Any(a => a.AttributeType == typeof(XmlRootAttribute)))
                throw new ArgumentException("Only root types can be (de)serialized; trying to get serializer for: " + t.FullName);

            // We don't use concurrent dictionary to avoid the race where two threads try to create serializer for the same type
            lock (Serializers) {
                if (Serializers.TryGetValue(t, out XmlSerializer value))
                    return value;
                value = new XmlSerializer(t, ExportedTypes);
                Serializers.Add(t, value);
                return value;
            }
        }

        public static string Serialize<T>(T t) {
            var s = Get(typeof(T));
            var w = new StringWriter();
            s.Serialize(w, t);
            return w.ToString();
        }

        public static void Serialize<T>(Stream output, T t) {
            var s = Get(typeof(T));
            using (var xm = XmlWriter.Create(output, WriterSettings))
                s.Serialize(xm, t);
        }

        public static T Deserialize<T>(string t) {
            var s = Get(typeof(T));
            var r = new StringReader(t);
            return (T)s.Deserialize(r);
        }

        public static T Deserialize<T>(Stream input) {
            var s = Get(typeof(T));
            using (var xm = XmlReader.Create(input))
                return (T)s.Deserialize(xm);
        }

        static bool IsXmlExported(Type t) {
            if (!t.IsClass) return false;
            if (!t.IsPublic) return false;
            if (t.IsGenericType) return false;
            if (!t.CustomAttributes.Any(a => IsXmlExported(a))) return false;
            return true;
        }

        static bool IsXmlExported(CustomAttributeData a) {
            return a.AttributeType == typeof(XmlTypeAttribute)
            || a.AttributeType == typeof(XmlRootAttribute);
        }
    }
}
