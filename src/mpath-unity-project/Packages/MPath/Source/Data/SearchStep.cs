using System;
using System.Collections.Generic;

namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// An immutable snapshot of a stepwise A* search after a single <see cref="Pathfinder.StepwiseSearch.Tick"/>.
    /// It carries the accumulated searched area (every cell discovered so far, open and closed) and, once the
    /// search succeeds, the resulting path — letting callers render the algorithm's progress frame by frame.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="PathResult"/> and <see cref="RangeResult"/>, a <see cref="SearchStep"/> owns plain
    /// arrays rather than pooled buffers, so it does not need to be disposed and stays valid after the search
    /// advances. This trades a little allocation per tick for simplicity — the stepwise API is meant for
    /// visualization and teaching, not the allocation-free hot path.
    /// </remarks>
    public sealed class SearchStep
    {
        /// <summary>
        /// Gets the overall state of the search after this tick.
        /// </summary>
        public SearchState State { get; }

        /// <summary>
        /// Gets a value indicating whether the search has finished (succeeded or failed).
        /// </summary>
        public bool IsComplete => State != SearchState.InProgress;

        /// <summary>
        /// Gets the 1-based index of the tick that produced this snapshot (the number of cells expanded so far).
        /// </summary>
        public int Iteration { get; }

        /// <summary>
        /// Gets the coordinate of the cell expanded on this tick — the "head" of the search. On the final
        /// successful tick this is the destination.
        /// </summary>
        public Coordinate Current { get; }

        /// <summary>
        /// Gets every cell discovered so far, each tagged as <see cref="SearchNodeState.Open"/> (frontier) or
        /// <see cref="SearchNodeState.Closed"/> (expanded), with its current A* scores. This is the accumulated
        /// searched area to highlight when visualizing the search.
        /// </summary>
        public IReadOnlyList<SearchNode> Searched { get; }

        /// <summary>
        /// Gets the number of cells currently on the open frontier.
        /// </summary>
        public int OpenCount { get; }

        /// <summary>
        /// Gets the number of cells currently in the closed (expanded) set.
        /// </summary>
        public int ClosedCount { get; }

        /// <summary>
        /// Gets the path from the origin to the destination once <see cref="State"/> is
        /// <see cref="SearchState.Success"/>; otherwise an empty list. Following the same convention as
        /// <see cref="PathResult"/>, the origin cell itself is not included. This is the raw A* path
        /// (parent-pointer chain); path smoothing is a separate post-process and is not applied here.
        /// </summary>
        public IReadOnlyList<Coordinate> Path { get; }

        internal SearchStep(SearchState state, int iteration, Coordinate current,
            SearchNode[] searched, int openCount, int closedCount, Coordinate[] path)
        {
            State = state;
            Iteration = iteration;
            Current = current;
            Searched = searched ?? Array.Empty<SearchNode>();
            OpenCount = openCount;
            ClosedCount = closedCount;
            Path = path ?? Array.Empty<Coordinate>();
        }
    }
}
