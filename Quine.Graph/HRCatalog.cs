using System;

namespace Quine.HRCatalog;

public static class QHGraph
{
    static QHGraph() { QHFacilities.ValidateFacility(typeof(QHGraph)); }

    public const int Facility = QHFacilities.Graph;

    /// <summary>
    /// 0: graph id
    /// </summary>
    public static readonly QHMessage I_GraphStarting = QHMessage.Information(Facility, 1, "Graph {0} starting.");

    /// <summary>
    /// 0: graph id
    /// </summary>
    public static readonly QHMessage I_GraphStopped = QHMessage.Information(Facility, 2, "Graph {0} stopped.");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage I_NodeStarting = QHMessage.Information(Facility, 3, "Node {0} starting.");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage I_NodeStopped = QHMessage.Information(Facility, 4, "Node {0} stopped.");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage I_Node_Canceled = QHMessage.Information(Facility, 5, "Node {0} canceled.");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage I_Node_Interruption = QHMessage.Information(Facility, 6, "Node {0} canceled (interruption).");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage I_NodeCompleted = QHMessage.Information(Facility, 7, "Node {0} completed (inputs drained).");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage E_UnhandledError = QHMessage.Error(Facility, 8, "Node {0} exited due to unhandled error.");

    /// <summary>
    /// 0: node id.
    /// </summary>
    public static readonly QHMessage C_UnhandledException = QHMessage.Critical(Facility, 9, "Unhandled exception escaped from {0}.");

    /// <summary>
    /// 0: item description.
    /// </summary>
    public static readonly QHMessage W_Retry = QHMessage.Warning(Facility, 10, "Processing of item {0} failed, will retry in the next round.");

}