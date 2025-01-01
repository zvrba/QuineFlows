using System;
using Quine.Schemas.Graph;

namespace Quine.Graph
{
    /// <summary>
    /// Used internally to signal that the channel is 1) empty and 2) in closed state, i.e.,
    /// that no more messages will be produced.
    /// </summary>
    public sealed class ChannelClosedException : Exception
    {
        internal ChannelClosedException() : base() { }
    }
}
