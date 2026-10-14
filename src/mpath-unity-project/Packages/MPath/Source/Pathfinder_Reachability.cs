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
        /// the supplied movement budget, for an agent that occupies an <paramref name="agentSize"/> x
        /// <paramref name="agentSize"/> square of cells. This is a uniform-cost (Dijkstra) flood fill over the grid
        /// that honours the same movement rules as <see cref="GetPath(Coordinate, Coordinate, int)"/> (diagonal
        /// movement, corner-cutting, occupied cells and agent clearance).
        /// </summary>
        /// <param name="from">The origin coordinate. Always included in the result (cost 0) when the budget is non-negative.</param>
        /// <param name="budget">
        /// The maximum allowed path cost, inclusive: a cell whose cheapest route costs exactly the budget is reached.
        /// Each straight step costs <see cref="IPathfinderSettings.StraightStepCost"/> and each diagonal step costs
        /// <see cref="IPathfinderSettings.DiagonalStepCost"/>; when <see cref="IPathfinderSettings.IsCellWeightEnabled"/>
        /// is set, the destination cell's <see cref="PathCell.Weight"/> is added to the step cost. A negative budget
        /// reaches nothing; routes costing more than 1,073,741,823 (<c>int.MaxValue / 2</c>) are never reached, so
        /// <c>int.MaxValue</c> is an unbounded budget.
        /// </param>
        /// <param name="agentSize">
        /// The agent's square size in cells (anchored at its top-left cell). Defaults to a single-cell agent.
        /// </param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public RangeResult GetReachable(Coordinate from, int budget, int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return FindReachable(agentSize, from, budget);
        }

        /// <summary>
        /// Finds every reachable cell like <see cref="GetReachable(Coordinate, int, int)"/>, with movement rules
        /// overridden for this query only by <paramref name="options"/>. The grid and the settings are not modified.
        /// </summary>
        /// <param name="from">The origin coordinate. Always included in the result (cost 0) when the budget is non-negative.</param>
        /// <param name="budget">The maximum allowed path cost.</param>
        /// <param name="agentSize">The agent's square size in cells; pass 1 for a single-cell agent.</param>
        /// <param name="options">
        /// The per-query rules. <c>default</c> behaves exactly like the overload without options.
        /// <see cref="QueryOptions.IsDestinationAlwaysEnterable"/> is ignored, since the query has no destination.
        /// </param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public RangeResult GetReachable(Coordinate from, int budget, int agentSize, QueryOptions options)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

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
        /// The shared body of the <c>GetReachable</c> overloads, run under the rules currently in <see cref="_query"/>.
        /// </summary>
        private RangeResult FindReachable(int agentSize, Coordinate from, int budget)
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

            var cells = new Span<PathCell>(_cells, 0, Size);

            fixed (PathCell* ptr = &MemoryMarshal.GetReference(cells))
            fixed (PathNode* nodes = _nodes)
            {
                BeginSearch(nodes);

                if (_settings.IsTieBreaking)
                {
                    _keyedOpenSet.Reset(nodes);
                    return CalculateReachableKeyed(agentSize, from, budget, ptr, nodes);
                }

                return CalculateReachable(agentSize, from, budget, ptr, nodes);
            }
        }

        /// <summary>
        /// Runs the bounded uniform-cost search that backs <see cref="GetReachable"/>.
        /// </summary>
        private RangeResult CalculateReachable(int agentSize, Coordinate from, int budget, PathCell* cells,
            PathNode* nodes)
        {
            // The reachable set can never exceed the number of cells in the grid.
            var reachable = ArrayPool<ReachableCell>.Shared.Rent(Size);
            var count = 0;
            var maxCost = Math.Min(budget, FastPathfinderSettings.MaxCost);

            OpenNode(nodes, GetLayoutIndex(from.X, from.Y), PathNode.NoParent, 0, 0, 0);

            var neighbors = stackalloc PathCell*[MaxNeighbors];
            var generation = _generation;

            while (_openSet.Count > 0)
            {
                var current = _openSet.Dequeue();
                var currentNode = nodes + current;

                // In Dijkstra's algorithm the cost of a cell is final once it is dequeued.
                currentNode->QueueIndex = PathNode.ClosedQueueIndex;
                GetPosition(current, out var x, out var y);
                reachable[count++] = new ReachableCell(new Coordinate(x, y), currentNode->ScoreG,
                    currentNode->Depth, currentNode->DiagonalSteps);

                PopulateNeighbors(cells, x, y, agentSize, neighbors);

                // Cells that cannot be entered within the budget (and the overflow headroom) are pruned.
                var headroom = maxCost - currentNode->ScoreG;

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

                    var stepCost = GetStepCost(n, neighbor);

                    if (stepCost > headroom)
                    {
                        continue;
                    }

                    var tentativeG = currentNode->ScoreG + stepCost;

                    if (!isTouched)
                    {
                        node->Generation = generation;
                        SetRoute(currentNode, node, n, tentativeG);
                        _openSet.Enqueue(index, tentativeG);
                    }
                    else if (tentativeG < node->ScoreG)
                    {
                        SetRoute(currentNode, node, n, tentativeG);
                        _openSet.UpdatePriority(index, tentativeG);
                    }
                }
            }

            return CreateRangeResult(reachable, count);
        }

        /// <summary>
        /// Wraps the reached cells in a result that knows the grid's layout, for <see cref="RangeResult.CopyCostsTo"/>.
        /// </summary>
        private RangeResult CreateRangeResult(ReachableCell[] reachable, int count)
        {
            var origin = GetLayoutIndex(0, 0);
            return RangeResult.Create(reachable, count, Size, GetLayoutIndex(1, 0) - origin,
                GetLayoutIndex(0, 1) - origin);
        }

        /// <summary>
        /// Records that the cheapest known route to <paramref name="node"/> is the one to
        /// <paramref name="current"/> plus a step in <paramref name="direction"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetRoute(PathNode* current, PathNode* node, int direction, int scoreG)
        {
            node->ScoreG = scoreG;
            node->Depth = current->Depth + 1;
            node->DiagonalSteps = current->DiagonalSteps + (direction >= DirectionIndexes.DiagonalStart ? 1 : 0);
        }
    }
}
