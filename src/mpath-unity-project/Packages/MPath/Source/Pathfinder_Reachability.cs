using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Core.Internal;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        /// <summary>
        /// Finds every cell reachable from the specified origin whose cheapest path cost does not exceed
        /// the supplied movement budget. This is a uniform-cost (Dijkstra) flood fill over the grid that
        /// honours the same movement rules as <see cref="GetPath"/> (diagonal movement, corner-cutting,
        /// occupied cells and agent clearance).
        /// </summary>
        /// <param name="agent">The agent for which to evaluate reachability. Cannot be null.</param>
        /// <param name="from">The origin coordinate. Always included in the result (cost 0) when the budget is non-negative.</param>
        /// <param name="budget">
        /// The maximum allowed path cost. Each straight step costs <see cref="IPathfinderSettings.StraightMovementMultiplier"/>
        /// and each diagonal step costs <see cref="IPathfinderSettings.DiagonalMovementMultiplier"/>; when
        /// <see cref="IPathfinderSettings.IsCellWeightEnabled"/> is set, the destination cell's
        /// <see cref="Cell.Weight"/> is added to the step cost.
        /// </param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public RangeResult GetReachable(IAgent agent, Coordinate from, float budget)
        {
            ThrowIfDisposed();

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return FindReachable(agent.Size, from, budget);
        }

        /// <summary>
        /// Finds every reachable cell like <see cref="GetReachable(IAgent, Coordinate, float)"/> for an agent that
        /// occupies an <paramref name="agentSize"/> x <paramref name="agentSize"/> square of cells, without an
        /// <see cref="IAgent"/>.
        /// </summary>
        /// <param name="from">The origin coordinate. Always included in the result (cost 0) when the budget is non-negative.</param>
        /// <param name="budget">The maximum allowed path cost.</param>
        /// <param name="agentSize">The agent's square size in cells. Defaults to a single-cell agent.</param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public RangeResult GetReachable(Coordinate from, float budget, int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return FindReachable(agentSize, from, budget);
        }

        /// <summary>
        /// The shared body of the <c>GetReachable</c> overloads.
        /// </summary>
        private RangeResult FindReachable(int agentSize, Coordinate from, float budget)
        {
            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }

            if (budget < 0)
            {
                return RangeResult.Empty();
            }

            InitializeCellsArray();

            var cells = new Span<Cell>(_cells, 0, Size);

            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            fixed (PathNode* nodes = _nodes)
            {
                BeginSearch(nodes);

                return CalculateReachable(agentSize, from, budget, ptr, nodes);
            }
        }

        /// <summary>
        /// Finds every reachable cell like <see cref="GetReachable(IAgent, Coordinate, float)"/>, with movement rules
        /// overridden for this query only by <paramref name="options"/>. The grid and the settings are not modified.
        /// </summary>
        /// <param name="agent">The agent for which to evaluate reachability. Cannot be null.</param>
        /// <param name="from">The origin coordinate. Always included in the result (cost 0) when the budget is non-negative.</param>
        /// <param name="budget">The maximum allowed path cost.</param>
        /// <param name="options">
        /// The per-query rules. <c>default</c> behaves exactly like the overload without options.
        /// <see cref="QueryOptions.IsDestinationAlwaysEnterable"/> is ignored, since the query has no destination.
        /// </param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public RangeResult GetReachable(IAgent agent, Coordinate from, float budget, QueryOptions options)
        {
            ThrowIfDisposed();

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return FindReachable(agent.Size, from, budget, options);
        }

        /// <summary>
        /// Finds every reachable cell like <see cref="GetReachable(Coordinate, float, int)"/>, with movement rules
        /// overridden for this query only by <paramref name="options"/>. The grid and the settings are not modified.
        /// </summary>
        /// <param name="from">The origin coordinate. Always included in the result (cost 0) when the budget is non-negative.</param>
        /// <param name="budget">The maximum allowed path cost.</param>
        /// <param name="options">
        /// The per-query rules. <c>default</c> behaves exactly like the overload without options.
        /// <see cref="QueryOptions.IsDestinationAlwaysEnterable"/> is ignored, since the query has no destination.
        /// </param>
        /// <param name="agentSize">The agent's square size in cells. Defaults to a single-cell agent.</param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public RangeResult GetReachable(Coordinate from, float budget, QueryOptions options,
            int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return FindReachable(agentSize, from, budget, options);
        }

        private RangeResult FindReachable(int agentSize, Coordinate from, float budget, QueryOptions options)
        {
            var enterableCells = options.AlwaysEnterable;

            fixed (Coordinate* enterable = enterableCells)
            {
                _query = QueryRules.FromOptions(_settings, options.Occupancy, enterable, enterableCells.Length,
                    false, null);

                try
                {
                    return FindReachable(agentSize, from, budget);
                }
                finally
                {
                    _query = QueryRules.FromSettings(_settings);
                }
            }
        }

        /// <summary>
        /// Runs the bounded uniform-cost search that backs <see cref="GetReachable"/>.
        /// </summary>
        private RangeResult CalculateReachable(int agentSize, Coordinate from, float budget, Cell* cells,
            PathNode* nodes)
        {
            // The reachable set can never exceed the number of cells in the grid.
            var reachable = ArrayPool<ReachableCell>.Shared.Rent(Size);
            var count = 0;

            OpenNode(nodes, GetLayoutIndex(from.X, from.Y), PathNode.NoParent, 0, 0f, 0f);

            var neighbors = stackalloc Cell*[MaxNeighbors];
            var generation = _generation;

            while (_openSet.Count > 0)
            {
                var current = _openSet.Dequeue();
                var currentCell = cells + current;
                var currentNode = nodes + current;

                // In Dijkstra's algorithm the cost of a cell is final once it is dequeued.
                currentNode->QueueIndex = PathNode.ClosedQueueIndex;
                reachable[count++] = new ReachableCell(currentCell->Coordinate, currentNode->ScoreG);

                PopulateNeighbors(cells, currentCell, agentSize, neighbors);

                for (var n = 0; n < MaxNeighbors; n++)
                {
                    var neighbor = neighbors[n];
                    if (neighbor == null)
                    {
                        continue;
                    }

                    var index = (int)(neighbor - cells);
                    var node = nodes + index;

                    // A node is touched only when it is queued, so a touched node is either closed or open.
                    var isTouched = node->Generation == generation;
                    if (isTouched && node->IsClosed)
                    {
                        continue;
                    }

                    var tentativeG = currentNode->ScoreG + GetStepCost(currentCell, neighbor);

                    // Cells that cannot be entered within the budget are pruned from the search.
                    if (tentativeG > budget)
                    {
                        continue;
                    }

                    if (!isTouched)
                    {
                        node->Generation = generation;
                        node->ScoreG = tentativeG;
                        _openSet.Enqueue(index, tentativeG);
                    }
                    else if (tentativeG < node->ScoreG)
                    {
                        node->ScoreG = tentativeG;
                        _openSet.UpdatePriority(index, tentativeG);
                    }
                }
            }

            return RangeResult.Create(reachable, count);
        }
    }
}
