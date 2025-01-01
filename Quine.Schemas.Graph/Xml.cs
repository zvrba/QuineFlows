using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Quine.Graph")]

namespace Quine.Schemas.Graph
{
    /// <summary>
    /// Provides constants for XML namespaces.
    /// </summary>
    public static class XmlNamespaces
    {
        public const string Graph = "http://schemas.quine.no/graph/v8_0.xsd";
    }
}
