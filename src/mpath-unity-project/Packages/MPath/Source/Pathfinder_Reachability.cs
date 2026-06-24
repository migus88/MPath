using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

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
        /// <see cref="IPathfinderSettings.IsCellWeightEnabled"/> is set, the step cost is multiplied by the destination
        /// cell's <see cref="Cell.Weight"/>.
        /// </param>
        /// <returns>
        /// A <see cref="RangeResult"/> listing every reachable cell with its cheapest path cost.
        /// The result must be disposed (use <c>using</c>) to return its pooled buffer.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin is outside the valid field range.</exception>
        public RangeResult GetReachable(IAgent agent, Coordinate from, float budget)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

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
            {
                ResetCells(ptr);
                _openSet.Clear();

                return CalculateReachable(agent, from, budget, ptr);
            }
        }

        /// <summary>
        /// Runs the bounded uniform-cost search that backs <see cref="GetReachable"/>.
        /// </summary>
        private RangeResult CalculateReachable(IAgent agent, Coordinate from, float budget, Cell* ptr)
        {
            // The reachable set can never exceed the number of cells in the grid.
            var reachable = ArrayPool<ReachableCell>.Shared.Rent(Size);
            var count = 0;

            var start = GetCell(ptr, from.X, from.Y);
            start->ScoreG = 0;
            _openSet.Enqueue(start, 0);

            var neighbors = stackalloc Cell*[MaxNeighbors];
            var agentSize = agent.Size;

            while (_openSet.Count > 0)
            {
                var current = _openSet.Dequeue();

                // In Dijkstra's algorithm the cost of a cell is final once it is dequeued.
                current->IsClosed = true;
                reachable[count++] = new ReachableCell(current->Coordinate, current->ScoreG);

                PopulateNeighbors(ptr, current, agentSize, neighbors);

                for (var n = 0; n < MaxNeighbors; n++)
                {
                    var neighbor = neighbors[n];
                    if (neighbor == null || neighbor->IsClosed)
                    {
                        continue;
                    }

                    var tentativeG = current->ScoreG + GetReachableStepCost(current, neighbor);

                    // Cells that cannot be entered within the budget are pruned from the search.
                    if (tentativeG > budget)
                    {
                        continue;
                    }

                    if (!_openSet.Contains(neighbor))
                    {
                        neighbor->ScoreG = tentativeG;
                        _openSet.Enqueue(neighbor, tentativeG);
                    }
                    else if (tentativeG < neighbor->ScoreG)
                    {
                        neighbor->ScoreG = tentativeG;
                        _openSet.UpdatePriority(neighbor, tentativeG);
                    }
                }
            }

            return RangeResult.Create(reachable, count);
        }

        /// <summary>
        /// Calculates the cost of moving from <paramref name="current"/> into <paramref name="neighbor"/>.
        /// A straight or diagonal travel multiplier is multiplied by the destination cell's weight when
        /// cell weighting is enabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetReachableStepCost(Cell* current, Cell* neighbor)
        {
            var travel = GetNeighborTravelWeightMultiplier(
                current->Coordinate.X, current->Coordinate.Y,
                neighbor->Coordinate.X, neighbor->Coordinate.Y);

            return _settings.IsCellWeightEnabled ? travel * neighbor->Weight : travel;
        }
    }
}
