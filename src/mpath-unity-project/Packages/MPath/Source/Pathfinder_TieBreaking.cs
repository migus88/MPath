using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Internal;
using static Migs.MPath.Core.Internal.DirectionIndexes;

namespace Migs.MPath.Core
{
    /// <summary>
    /// The keyed search used with a <see cref="TieBreaking"/>: costs are the same integers as the plain search
    /// (<see cref="PathNode.ScoreG"/>), every node keeps a lexicographic label (cost, tie key[, steps]) and the set of
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
        private void OpenOriginKeyed(PathNode* nodes, Coordinate from, int heuristic)
        {
            var index = GetLayoutIndex(from.X, from.Y);
            nodes[index].Generation = _generation;
            SetLabel(nodes + index, 0, 0, 0);
            _arrivalsPointer[index] = AllDirections;
            _keyedOpenSet.Enqueue(index, heuristic, 0, 0);
        }

        /// <summary>
        /// The keyed counterpart of <see cref="CalculatePath"/>.
        /// </summary>
        private PathResult CalculatePathKeyed(int agentSize, Coordinate from, Coordinate to, PathCell* cells,
            PathNode* nodes)
        {
            OpenOriginKeyed(nodes, from, GetH(from.X, from.Y, to.X, to.Y));

            var toIndex = GetLayoutIndex(to.X, to.Y);
            var neighbors = stackalloc PathCell*[MaxNeighbors];

            while (_keyedOpenSet.Count > 0)
            {
                if (ExpandNextKeyed(cells, nodes, to, toIndex, agentSize, neighbors, out var current))
                {
                    return ReconstructKeyedPath(current, cells, from, agentSize);
                }
            }

            return PathResult.Failure();
        }

        /// <summary>
        /// The keyed counterpart of <see cref="ExpandNext"/>: one expansion of the keyed search.
        /// </summary>
        private bool ExpandNextKeyed(PathCell* cells, PathNode* nodes, Coordinate to, int toIndex, int agentSize,
            PathCell** neighbors, out int current)
        {
            current = _keyedOpenSet.Dequeue();

            if (current == toIndex)
            {
                return true;
            }

            nodes[current].QueueIndex = PathNode.ClosedQueueIndex;

            GetPosition(current, out var x, out var y);
            PopulateNeighbors(cells, x, y, agentSize, neighbors);
            ProcessNeighborsKeyed(cells, nodes, current, x, y, neighbors, to);

            return false;
        }

        /// <summary>
        /// Relaxes the neighbours of <paramref name="current"/> in the keyed search: the tie-break counterpart of
        /// <see cref="ProcessNeighbors"/>, with the same <see cref="FastPathfinderSettings.MaxCost"/> headroom.
        /// (<paramref name="x"/>, <paramref name="y"/>) is the position of <paramref name="current"/>.
        /// </summary>
        private void ProcessNeighborsKeyed(PathCell* cells, PathNode* nodes, int current, int x, int y,
            PathCell** neighbors, Coordinate to)
        {
            var isCountingTurns = _settings.TieBreaking == TieBreaking.FewerTurns;
            var currentNode = nodes + current;
            var headroom = FastPathfinderSettings.MaxCost - currentNode->ScoreG;
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

                var stepCost = GetStepCost(n, neighbor);

                if (stepCost > headroom)
                {
                    continue;
                }

                var cost = currentNode->ScoreG + stepCost;
                var tieKey = isCountingTurns
                    ? currentNode->TieKey + (((currentArrivals >> n) & 1) ^ 1)
                    : currentNode->TieKey + (n >= DiagonalStart ? 1 : 0);
                var direction = (byte)(1 << n);

                if (!isTouched)
                {
                    node->Generation = generation;
                    SetLabel(node, cost, tieKey, depth);
                    _arrivalsPointer[index] = direction;

                    _keyedOpenSet.Enqueue(index, cost + GetH(x + GetOffsetX(n), y + GetOffsetY(n), to.X, to.Y),
                        tieKey, cost);
                    continue;
                }

                var comparison = CompareLabel(node, cost, tieKey, isCountingTurns ? node->Depth : depth);

                if (comparison < 0)
                {
                    SetLabel(node, cost, tieKey, depth);
                    _arrivalsPointer[index] = direction;

                    _keyedOpenSet.UpdatePriority(index,
                        cost + GetH(x + GetOffsetX(n), y + GetOffsetY(n), to.X, to.Y), tieKey, cost);
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
        private PathResult ReconstructKeyedPath(int lastIndex, PathCell* cells, Coordinate from, int agentSize)
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
        private int WalkRoute(int lastIndex, PathCell* cells, Coordinate from, Coordinate[] path, int length,
            out int diagonalSteps)
        {
            var steps = 0;
            var nextDirection = -1;
            var index = lastIndex;
            var coordinate = GetCoordinate(lastIndex);
            diagonalSteps = 0;

            while (coordinate != from)
            {
                var directions = _arrivalsPointer[index];
                var direction = nextDirection >= 0 && (directions & (1 << nextDirection)) != 0
                    ? nextDirection
                    : GetFirstDirection(directions);

                steps++;

                if (path != null)
                {
                    path[length - steps] = coordinate;
                }

                if (direction >= DiagonalStart)
                {
                    diagonalSteps++;
                }

                coordinate = new Coordinate(coordinate.X - GetOffsetX(direction), coordinate.Y - GetOffsetY(direction));
                index = GetLayoutIndex(coordinate.X, coordinate.Y);
                nextDirection = direction;
            }

            return steps;
        }

        /// <summary>
        /// Runs the bounded uniform-cost search behind <see cref="GetReachable"/> in the keyed mode. Every tie-break
        /// ranks equal-cost routes here by (fewer diagonals, fewer steps), so the counts are exact and match the route
        /// <see cref="TieBreaking.FewerDiagonals"/> returns.
        /// </summary>
        private RangeResult CalculateReachableKeyed(int agentSize, Coordinate from, int budget, PathCell* cells,
            PathNode* nodes)
        {
            var reachable = ArrayPool<ReachableCell>.Shared.Rent(Size);
            var count = 0;
            var maxCost = Math.Min(budget, FastPathfinderSettings.MaxCost);
            var generation = _generation;

            var start = GetLayoutIndex(from.X, from.Y);
            nodes[start].Generation = generation;
            SetLabel(nodes + start, 0, 0, 0);
            _keyedOpenSet.Enqueue(start, 0, 0, 0);

            var neighbors = stackalloc PathCell*[MaxNeighbors];

            while (_keyedOpenSet.Count > 0)
            {
                var current = _keyedOpenSet.Dequeue();
                var currentNode = nodes + current;

                currentNode->QueueIndex = PathNode.ClosedQueueIndex;
                GetPosition(current, out var x, out var y);
                reachable[count++] = new ReachableCell(new Coordinate(x, y), currentNode->ScoreG,
                    currentNode->Depth, currentNode->TieKey);

                PopulateNeighbors(cells, x, y, agentSize, neighbors);

                var depth = currentNode->Depth + 1;
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

                    var cost = currentNode->ScoreG + stepCost;
                    var tieKey = currentNode->TieKey + (n >= DiagonalStart ? 1 : 0);

                    if (!isTouched)
                    {
                        node->Generation = generation;
                        SetLabel(node, cost, tieKey, depth);
                        _keyedOpenSet.Enqueue(index, cost, tieKey, cost);
                    }
                    else if (CompareLabel(node, cost, tieKey, depth) < 0)
                    {
                        SetLabel(node, cost, tieKey, depth);
                        _keyedOpenSet.UpdatePriority(index, cost, tieKey, cost);
                    }
                }
            }

            return CreateRangeResult(reachable, count);
        }

        /// <summary>
        /// The search scores of a node in either search (for <see cref="SearchNode"/>): f is g + h, the node's
        /// open-set priority.
        /// </summary>
        private void GetScores(PathNode* node, Coordinate coordinate, Coordinate to, out int scoreG,
            out int scoreH, out int scoreF)
        {
            scoreG = node->ScoreG;
            scoreH = _settings.IsTieBreaking ? GetH(coordinate.X, coordinate.Y, to.X, to.Y) : node->ScoreH;
            scoreF = scoreG + scoreH;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetLabel(PathNode* node, int cost, int tieKey, int depth)
        {
            node->ScoreG = cost;
            node->TieKey = tieKey;
            node->Depth = depth;
        }

        /// <summary>
        /// Compares the label (<paramref name="cost"/>, <paramref name="tieKey"/>, <paramref name="depth"/>) with
        /// the one <paramref name="node"/> holds: negative when it is better.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareLabel(PathNode* node, int cost, int tieKey, int depth)
        {
            if (cost != node->ScoreG)
            {
                return cost < node->ScoreG ? -1 : 1;
            }

            if (tieKey != node->TieKey)
            {
                return tieKey < node->TieKey ? -1 : 1;
            }

            return depth.CompareTo(node->Depth);
        }
    }
}
