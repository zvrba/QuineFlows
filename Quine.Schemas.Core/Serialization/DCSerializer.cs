using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace Quine.Schemas
{
    /// <summary>
    /// Simple methods for (de)serializing types with <see cref="System.Runtime.Serialization.DataContractSerializer"/> to XML.
    /// </summary>
    public static class DCSerializer
    {
        private static readonly Dictionary<Type, DataContractSerializer> Serializers =
            new Dictionary<Type, DataContractSerializer>();

        public static readonly XmlWriterSettings WriterSettings = new XmlWriterSettings() {
            ConformanceLevel = ConformanceLevel.Fragment,   // Both this and OmitXmlDeclaration must be
            OmitXmlDeclaration = true,                      // set for the declaration to be omitted
            Indent = true,
            IndentChars = "\t",                             // Default is two spaces, so this reduces size
            Encoding = new System.Text.UTF8Encoding(false)  // Don't emit BOM
        };

        public static readonly Type[] ExportedTypes;

        /// <summary>
        /// Ensures that all schemas are loaded and their static constructors invoked.
        /// </summary>
        static DCSerializer() {
            SchemaLoader.Load();

            ExportedTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("Quine.Schemas."))
                .SelectMany(a => a.ExportedTypes.Where(t => IsXmlExported(t)))
                .ToArray();

            foreach (var t in ExportedTypes)
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
        }

        public static DataContractSerializer Get(Type t) {
            if (!t.CustomAttributes.Any(a => a.AttributeType == typeof(DataContractAttribute)))
                throw new ArgumentException("Only DataContract types can be (de)serialized; trying to get serializer for: " + t.FullName);

            // We don't use concurrent dictionary to avoid the race where two threads try to create serializer for the same type
            lock (Serializers) {
                if (Serializers.TryGetValue(t, out var value))
                    return value;
                value = new DataContractSerializer(t, ExportedTypes);
                Serializers.Add(t, value);
                return value;
            }
        }

        public static string Serialize<T>(T t) {
            var s = Get(typeof(T));
            var sb = new System.Text.StringBuilder(16384);
            using (var xw0 = XmlWriter.Create(sb, WriterSettings))
            using (var xw1 = XmlDictionaryWriter.CreateDictionaryWriter(xw0))
                s.WriteObject(xw1, t);
            return sb.ToString();
        }

        public static void Serialize<T>(Stream output, T t) {
            var s = Get(typeof(T));
            using (var xw0 = XmlWriter.Create(output, WriterSettings))
            using (var xw1 = XmlDictionaryWriter.CreateDictionaryWriter(xw0))
                s.WriteObject(xw1, t);
        }

        public static T Deserialize<T>(string t) {
            var s = Get(typeof(T));
            var r = new StringReader(t);
            using (var xr0 = XmlReader.Create(r))
            using (var xr1 = XmlDictionaryReader.CreateDictionaryReader(xr0))
                return (T)s.ReadObject(xr1);
        }

        public static T Deserialize<T>(Stream input) {
            var s = Get(typeof(T));
            using (var xr0 = XmlReader.Create(input))
            using (var xr1 = XmlDictionaryReader.CreateDictionaryReader(xr0))
                return (T)s.ReadObject(xr1);
        }

        public static XmlNode SerializeToXmlNode<T>(T t) {
            var s = Get(typeof(T));
            var d = new XmlDocument();
            using (var w = d.CreateNavigator().AppendChild())
                s.WriteObject(w, t);
            return d.FirstChild;
        }

        public static T DeserializeFromXmlNode<T>(XmlNode n) {
            var s = Get(typeof(T));
            using (var xr = new XmlNodeReader(n))
                return (T)s.ReadObject(xr);
        }

        /// <summary>
        /// Used to deep-clone any schema object by round-tripping through serialization to XML nodes.
        /// </summary>
        public static T CloneGeneric<T>(T o) where T : class {
            var xdoc = SerializeToXmlNode(o);
            return DeserializeFromXmlNode<T>(xdoc);
        }

        static bool IsXmlExported(Type t) {
            if (!t.IsClass) return false;
            if (!t.IsPublic) return false;
            //if (t.IsGenericType) return false;
            if (!t.CustomAttributes.Any(a => IsXmlExported(a))) return false;
            return true;
        }

        static bool IsXmlExported(CustomAttributeData a) {
            return a.AttributeType == typeof(DataContractAttribute);
        }
    }
}
