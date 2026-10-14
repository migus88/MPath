using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Internal;
using static Migs.MPath.Core.Internal.DirectionIndexes;

namespace Migs.MPath.Core
{
    /// <summary>
    /// The keyed search used with a <see cref="TieBreaking"/>: costs are exact fixed-point integers
    /// (<see cref="PathNode.CostKey"/>), every node keeps a lexicographic label (cost, tie key[, steps]) and the set of
    /// directions it can be entered from on a route with that label (<see cref="_arrivals"/>), and the route is read
    /// back from those sets by a fixed rule instead of from parent indexes.
    /// </summary>
    /// <remarks>
    /// The open set (<see cref="KeyedPriorityQueue"/>) orders by (f, tie key, g). Step costs are positive, so g grows
    /// along every route and each node leaves the queue after every node that can precede it on a route with an
    /// equal label: by then its arrival set is complete, which makes the route a function of the grid alone,
    /// independent of heap order.<br/>
    /// <see cref="TieBreaking.FewerDiagonals"/>: the tie key is the number of diagonal steps and the label also
    /// counts steps. Both add up along a route, so the label is an ordinary lexicographic shortest path.<br/>
    /// <see cref="TieBreaking.FewerTurns"/>: the tie key is the number of direction changes. A turn depends on the
    /// direction a cell was entered from, but (fewest turns, every direction achieving it) is all a later step
    /// needs: entering by any other direction costs at least one more turn, which a turn at the cell costs too.
    /// So one label per node stays exact.
    /// </remarks>
    public sealed unsafe partial class Pathfinder
    {
        // Keyed search only (null otherwise): its open set and one arrival-direction set per cell (same index as
        // _nodes). An arrival set is written whenever its node is touched, so it needs no reset.
        private readonly KeyedPriorityQueue _keyedOpenSet;
        private byte[] _arrivals;

        // The pinned _arrivals of the running keyed search.
        private byte* _arrivalsPointer;

        /// <summary>
        /// Starts a keyed search over the pinned node and arrival buffers (after <see cref="BeginSearch"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BeginKeyedSearch(PathNode* nodes, byte* arrivals)
        {
            _keyedOpenSet.Reset(nodes);
            _arrivalsPointer = arrivals;
        }

        /// <summary>
        /// Touches the origin for the keyed search and queues it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OpenOriginKeyed(PathNode* nodes, Coordinate from, long heuristicKey)
        {
            var index = GetLayoutIndex(from.X, from.Y);
            nodes[index].Generation = _generation;
            SetLabel(nodes + index, 0L, 0, 0);
            _arrivalsPointer[index] = AllDirections;
            _keyedOpenSet.Enqueue(index, heuristicKey, 0, 0L);
        }

        /// <summary>
        /// The keyed counterpart of <see cref="CalculatePath"/>.
        /// </summary>
        private PathResult CalculatePathKeyed(int agentSize, Coordinate from, Coordinate to, Cell* cells,
            PathNode* nodes)
        {
            OpenOriginKeyed(nodes, from, GetHKey(from.X, from.Y, to.X, to.Y));

            var neighbors = stackalloc Cell*[MaxNeighbors];

            while (_keyedOpenSet.Count > 0)
            {
                if (ExpandNextKeyed(cells, nodes, to, agentSize, neighbors, out var current))
                {
                    return ReconstructKeyedPath(current, cells, from, agentSize);
                }
            }

            return PathResult.Failure();
        }

        /// <summary>
        /// The keyed counterpart of <see cref="ExpandNext"/>: one expansion of the keyed search.
        /// </summary>
        private bool ExpandNextKeyed(Cell* cells, PathNode* nodes, Coordinate to, int agentSize, Cell** neighbors,
            out int current)
        {
            current = _keyedOpenSet.Dequeue();

            var cell = cells + current;

            if (cell->Coordinate == to)
            {
                return true;
            }

            nodes[current].QueueIndex = PathNode.ClosedQueueIndex;

            PopulateNeighbors(cells, cell, agentSize, neighbors);
            ProcessNeighborsKeyed(cells, nodes, current, neighbors, to);

            return false;
        }

        /// <summary>
        /// Relaxes the neighbours of <paramref name="current"/> in the keyed search: the tie-break counterpart of
        /// <see cref="ProcessNeighbors"/>.
        /// </summary>
        private void ProcessNeighborsKeyed(Cell* cells, PathNode* nodes, int current, Cell** neighbors, Coordinate to)
        {
            var isCountingTurns = _settings.TieBreaking == TieBreaking.FewerTurns;
            var currentNode = nodes + current;
            var currentArrivals = _arrivalsPointer[current];
            var depth = currentNode->Depth + 1;
            var generation = _generation;

            for (var n = 0; n < MaxNeighbors; n++)
            {
                var neighbor = neighbors[n];
                if (neighbor == null)
                {
                    continue;
                }

                var index = (int)(neighbor - cells);
                var node = nodes + index;
                var isTouched = node->Generation == generation;

                if (isTouched && node->IsClosed)
                {
                    continue;
                }

                var costKey = currentNode->CostKey + GetStepCostKey(n, neighbor);
                var tieKey = isCountingTurns
                    ? currentNode->TieKey + (((currentArrivals >> n) & 1) ^ 1)
                    : currentNode->TieKey + (n >= DiagonalStart ? 1 : 0);
                var direction = (byte)(1 << n);

                if (!isTouched)
                {
                    node->Generation = generation;
                    SetLabel(node, costKey, tieKey, depth);
                    _arrivalsPointer[index] = direction;

                    var neighborCoord = neighbor->Coordinate;
                    _keyedOpenSet.Enqueue(index, costKey + GetHKey(neighborCoord.X, neighborCoord.Y, to.X, to.Y),
                        tieKey, costKey);
                    continue;
                }

                var comparison = CompareLabel(node, costKey, tieKey, isCountingTurns ? node->Depth : depth);

                if (comparison < 0)
                {
                    SetLabel(node, costKey, tieKey, depth);
                    _arrivalsPointer[index] = direction;

                    var neighborCoord = neighbor->Coordinate;
                    _keyedOpenSet.UpdatePriority(index,
                        costKey + GetHKey(neighborCoord.X, neighborCoord.Y, to.X, to.Y), tieKey, costKey);
                }
                else if (comparison == 0)
                {
                    _arrivalsPointer[index] |= direction;
                }
            }
        }

        /// <summary>
        /// Builds the result of a successful keyed search by walking back from <paramref name="lastIndex"/>.
        /// </summary>
        private PathResult ReconstructKeyedPath(int lastIndex, Cell* cells, Coordinate from, int agentSize)
        {
            var steps = WalkRoute(lastIndex, cells, from, null, 0, out var diagonalSteps);
            var path = ArrayPool<Coordinate>.Shared.Rent(steps);
            Array.Clear(path, 0, path.Length);
            WalkRoute(lastIndex, cells, from, path, steps, out _);

            return CreatePathResult(path, steps, diagonalSteps, cells, agentSize);
        }

        /// <summary>
        /// Walks the route from <paramref name="lastIndex"/> back to <paramref name="from"/> through the arrival
        /// sets: at every cell it keeps the direction of the step after it when that direction is in the set,
        /// otherwise it takes the first direction of the set in index order. Writes the route (origin excluded)
        /// into <paramref name="path"/> when it is not null.
        /// </summary>
        /// <returns>The number of steps of the route.</returns>
        private int WalkRoute(int lastIndex, Cell* cells, Coordinate from, Coordinate[] path, int length,
            out int diagonalSteps)
        {
            var steps = 0;
            var nextDirection = -1;
            var index = lastIndex;
            diagonalSteps = 0;

            while (cells[index].Coordinate != from)
            {
                var directions = _arrivalsPointer[index];
                var direction = nextDirection >= 0 && (directions & (1 << nextDirection)) != 0
                    ? nextDirection
                    : GetFirstDirection(directions);

                var coordinate = cells[index].Coordinate;
                steps++;

                if (path != null)
                {
                    path[length - steps] = coordinate;
                }

                if (direction >= DiagonalStart)
                {
                    diagonalSteps++;
                }

                index = GetLayoutIndex(coordinate.X - GetOffsetX(direction), coordinate.Y - GetOffsetY(direction));
                nextDirection = direction;
            }

            return steps;
        }

        /// <summary>
        /// Runs the bounded uniform-cost search behind <see cref="GetReachable"/> in the keyed mode. Every tie-break
        /// ranks equal-cost routes here by (fewer diagonals, fewer steps), so the counts are exact and match the route
        /// <see cref="TieBreaking.FewerDiagonals"/> returns.
        /// </summary>
        private RangeResult CalculateReachableKeyed(int agentSize, Coordinate from, float budget, Cell* cells,
            PathNode* nodes)
        {
            var reachable = ArrayPool<ReachableCell>.Shared.Rent(Size);
            var count = 0;
            var budgetKey = FastPathfinderSettings.ToBudgetKey(budget);
            var generation = _generation;

            var start = GetLayoutIndex(from.X, from.Y);
            nodes[start].Generation = generation;
            SetLabel(nodes + start, 0L, 0, 0);
            _keyedOpenSet.Enqueue(start, 0L, 0, 0L);

            var neighbors = stackalloc Cell*[MaxNeighbors];

            while (_keyedOpenSet.Count > 0)
            {
                var current = _keyedOpenSet.Dequeue();
                var currentCell = cells + current;
                var currentNode = nodes + current;

                currentNode->QueueIndex = PathNode.ClosedQueueIndex;
                reachable[count++] = new ReachableCell(currentCell->Coordinate,
                    FastPathfinderSettings.ToCost(currentNode->CostKey), currentNode->Depth, currentNode->TieKey);

                PopulateNeighbors(cells, currentCell, agentSize, neighbors);

                var depth = currentNode->Depth + 1;

                for (var n = 0; n < MaxNeighbors; n++)
                {
                    var neighbor = neighbors[n];
                    if (neighbor == null)
                    {
                        continue;
                    }

                    var index = (int)(neighbor - cells);
                    var node = nodes + index;
                    var isTouched = node->Generation == generation;

                    if (isTouched && node->IsClosed)
                    {
                        continue;
                    }

                    var costKey = currentNode->CostKey + GetStepCostKey(n, neighbor);

                    if (costKey > budgetKey)
                    {
                        continue;
                    }

                    var tieKey = currentNode->TieKey + (n >= DiagonalStart ? 1 : 0);

                    if (!isTouched)
                    {
                        node->Generation = generation;
                        SetLabel(node, costKey, tieKey, depth);
                        _keyedOpenSet.Enqueue(index, costKey, tieKey, costKey);
                    }
                    else if (CompareLabel(node, costKey, tieKey, depth) < 0)
                    {
                        SetLabel(node, costKey, tieKey, depth);
                        _keyedOpenSet.UpdatePriority(index, costKey, tieKey, costKey);
                    }
                }
            }

            return RangeResult.Create(reachable, count);
        }

        /// <summary>
        /// The search scores of a node as floats, in either cost mode (for <see cref="SearchNode"/>).
        /// </summary>
        private void GetScores(PathNode* node, Coordinate coordinate, Coordinate to, out float scoreG,
            out float scoreH, out float scoreF)
        {
            if (!_settings.IsTieBreaking)
            {
                // f is the node's open-set priority, g + h, recomputed with the same float arithmetic.
                scoreG = node->ScoreG;
                scoreH = node->ScoreH;
                scoreF = node->ScoreG + node->ScoreH;
                return;
            }

            var heuristicKey = GetHKey(coordinate.X, coordinate.Y, to.X, to.Y);
            scoreG = FastPathfinderSettings.ToCost(node->CostKey);
            scoreH = FastPathfinderSettings.ToCost(heuristicKey);
            scoreF = FastPathfinderSettings.ToCost(node->CostKey + heuristicKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetLabel(PathNode* node, long costKey, int tieKey, int depth)
        {
            node->CostKey = costKey;
            node->TieKey = tieKey;
            node->Depth = depth;
        }

        /// <summary>
        /// Compares the label (<paramref name="costKey"/>, <paramref name="tieKey"/>, <paramref name="depth"/>) with
        /// the one <paramref name="node"/> holds: negative when it is better.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareLabel(PathNode* node, long costKey, int tieKey, int depth)
        {
            if (costKey != node->CostKey)
            {
                return costKey < node->CostKey ? -1 : 1;
            }

            if (tieKey != node->TieKey)
            {
                return tieKey < node->TieKey ? -1 : 1;
            }

            return depth.CompareTo(node->Depth);
        }

        /// <summary>
        /// The cost key of a step in <paramref name="direction"/> into <paramref name="neighbor"/>: the keyed
        /// counterpart of <see cref="GetStepCost"/> (travel key plus the entered cell's weight key).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetStepCostKey(int direction, Cell* neighbor)
        {
            var travel = direction >= DiagonalStart ? _settings.DiagonalCostKey : _settings.StraightCostKey;

            return _settings.IsCellWeightEnabled
                ? travel + FastPathfinderSettings.ToCostKey(neighbor->Weight)
                : travel;
        }

        /// <summary>
        /// The keyed counterpart of <see cref="GetH"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetHKey(int startX, int startY, int destX, int destY)
        {
            long dx = Math.Abs(destX - startX);
            long dy = Math.Abs(destY - startY);

            return dx > dy
                ? _settings.HeuristicMajorAxisKey * dx + _settings.HeuristicMinorAxisKey * dy
                : _settings.HeuristicMajorAxisKey * dy + _settings.HeuristicMinorAxisKey * dx;
        }
    }
}
