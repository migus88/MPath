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
    /// O(grid).
    /// </remarks>
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
        /// The cheapest known cost from the origin.
        /// </summary>
        public float ScoreG;

        /// <summary>
        /// The heuristic estimate to the destination (0 in the reachability search).
        /// </summary>
        public float ScoreH;

        /// <summary>
        /// The cell index of the node this one is reached from, or <see cref="NoParent"/>.
        /// </summary>
        public int ParentIndex;

        /// <summary>
        /// The number of steps from the origin.
        /// </summary>
        public int Depth;

        /// <summary>
        /// The node's slot in the open set heap while it is queued, <see cref="ClosedQueueIndex"/> once it is closed.
        /// May be stale for a node that has left the heap; see <see cref="UnsafePriorityQueue.Contains"/>.
        /// </summary>
        public int QueueIndex;

        /// <summary>
        /// The search this node was last touched by. 0 means never.
        /// </summary>
        public uint Generation;

        /// <summary>
        /// Whether the node has been expanded (A*) or settled (reachability) in its search.
        /// </summary>
        public bool IsClosed => QueueIndex == ClosedQueueIndex;
    }
}
