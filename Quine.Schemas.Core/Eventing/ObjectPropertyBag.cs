using System;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core.Eventing
{
    /// <remarks>
    /// Publicly usable concrete implementation of <see cref="PropertyValueBag"/>.
    /// </remarks>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    public sealed class ObjectPropertyBag : PropertyValueBag
    {

        /// <summary>
        /// Constructor.  If <paramref name="o"/> is not null, automatically adds members by calling <see cref="Add(object, int)"/>.
        /// </summary>
        /// <param name="o">Object to populate the bag from.  May be null.</param>
        /// <param name="include">Determines whether to include properties (default), fields or both.</param>
        public ObjectPropertyBag(
            object o = null,
            int include = IncludeProperties)
        {
            if (o != null)
                AddMembers(o, include);
        }
    }
}
