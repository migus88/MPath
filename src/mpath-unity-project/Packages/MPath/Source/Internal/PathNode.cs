using System.Runtime.InteropServices;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The search state of one cell, kept by the pathfinder in an array parallel to the cell buffer (same index).
    /// The grid itself is only read by a search.
    /// </summary>
    /// <remarks>
    /// A node is part of the running search only when its <see cref="Generation"/> equals the pathfinder's current
    /// one; any other node is untouched and its other fields are stale. A search therefore never resets the array:
    /// it bumps the generation and initialises each node on first touch, so its cost is O(cells searched), not
    /// O(grid).<br/>
    /// The layout is explicit so the two searches share the same 24 bytes: both keep the cost in
    /// <see cref="ScoreG"/>; the plain search (<see cref="Data.TieBreaking.None"/>) also uses <see cref="ScoreH"/>
    /// and <see cref="ParentIndex"/>, the keyed search (any tie-break) <see cref="TieKey"/> over the parent's bytes.
    /// A pathfinder only ever runs one of them.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    internal struct PathNode
    {
        /// <summary>
        /// The <see cref="QueueIndex"/> of a closed node: never a live heap slot.
        /// </summary>
        public const int ClosedQueueIndex = -1;

        /// <summary>
        /// The parent index of the origin.
        /// </summary>
        public const int NoParent = -1;

        /// <summary>
        /// The cheapest known cost from the origin, in both searches.
        /// </summary>
        [FieldOffset(0)] public int ScoreG;

        /// <summary>
        /// The heuristic estimate to the destination (0 in the reachability search).
        /// </summary>
        [FieldOffset(4)] public int ScoreH;

        /// <summary>
        /// The cell index of the node this one is reached from, or <see cref="NoParent"/>.
        /// </summary>
        [FieldOffset(8)] public int ParentIndex;

        /// <summary>
        /// The number of steps from the origin.
        /// </summary>
        [FieldOffset(12)] public int Depth;

        /// <summary>
        /// The node's slot in the open set heap while it is queued, <see cref="ClosedQueueIndex"/> once it is closed.
        /// May be stale for a node that has left the heap; see <see cref="UnsafePriorityQueue.Contains"/>.
        /// </summary>
        [FieldOffset(16)] public int QueueIndex;

        /// <summary>
        /// The search this node was last touched by. 0 means never.
        /// </summary>
        [FieldOffset(20)] public uint Generation;

        /// <summary>
        /// The number of diagonal steps of the route the plain reachability search keeps for the node. Overlays
        /// <see cref="ScoreH"/>, which that search never uses.
        /// </summary>
        [FieldOffset(4)] public int DiagonalSteps;

        /// <summary>
        /// Keyed search: the tie-break key of the node's label (diagonal steps or turns). Overlays
        /// <see cref="ParentIndex"/>: the keyed search reads routes from arrival directions, not parents.
        /// </summary>
        [FieldOffset(8)] public int TieKey;

        /// <summary>
        /// Whether the node has been expanded (A*) or settled (reachability) in its search.
        /// </summary>
        public bool IsClosed => QueueIndex == ClosedQueueIndex;
    }
}
