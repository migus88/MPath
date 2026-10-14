using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Internal;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        // The active session, if any: guards against two concurrent stepwise searches sharing this instance's
        // open set and buffers, and lets Dispose end the session before returning the buffers it pins.
        private StepwiseSearch _activeSearch;

        /// <summary>
        /// Begins a stepwise (tick-by-tick) A* search for an agent that occupies an <paramref name="agentSize"/> x
        /// <paramref name="agentSize"/> square of cells, which can be advanced one expansion at a time, exposing
        /// the accumulated searched area and — once it succeeds — the resulting path. This is an
        /// <b>educational / visualization</b> facility for showing how the algorithm explores the grid; the
        /// regular <see cref="GetPath(Coordinate, Coordinate, int)"/> remains the way to compute a path in one shot.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="StepwiseSearch"/> drives the very same A* expansion as
        /// <see cref="GetPath(Coordinate, Coordinate, int)"/> (it shares this instance's open set and buffers), so it
        /// produces an identical path; it simply pauses between expansions. Because it pins this instance's cell and
        /// search-state buffers for the lifetime of the search, the session <b>must be disposed</b> (wrap it in
        /// <c>using</c>), and the owning <see cref="Pathfinder"/> must not be used for any other query — including a
        /// second <c>BeginStepwiseSearch</c> — until the session is disposed.
        /// </remarks>
        /// <param name="from">The origin coordinate. Must be inside the grid.</param>
        /// <param name="to">The destination coordinate. Must be inside the grid.</param>
        /// <param name="agentSize">
        /// The agent's square size in cells (anchored at its top-left cell). Defaults to a single-cell agent.
        /// </param>
        /// <returns>A disposable <see cref="StepwiseSearch"/> positioned before the first expansion.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="from"/> or <paramref name="to"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a stepwise search is already active on this instance.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public StepwiseSearch BeginStepwiseSearch(Coordinate from, Coordinate to, int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return BeginStepwiseSearch(agentSize, from, to, OccupancyRule.UseSettings, Array.Empty<Coordinate>(),
                false);
        }

        /// <summary>
        /// Begins a stepwise search like <see cref="BeginStepwiseSearch(Coordinate, Coordinate, int)"/>, with
        /// movement rules overridden by <paramref name="options"/> for the whole search. The options are copied when
        /// the search begins and applied on every tick, so it explores exactly as
        /// <see cref="GetPath(Coordinate, Coordinate, int, QueryOptions)"/> with the same options would.
        /// </summary>
        /// <param name="from">The origin coordinate. Must be inside the grid.</param>
        /// <param name="to">The destination coordinate. Must be inside the grid.</param>
        /// <param name="agentSize">The agent's square size in cells; pass 1 for a single-cell agent.</param>
        /// <param name="options">The per-query rules. <c>default</c> behaves exactly like the overload without options.</param>
        /// <returns>A disposable <see cref="StepwiseSearch"/> positioned before the first expansion.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="from"/> or <paramref name="to"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a stepwise search is already active on this instance.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public StepwiseSearch BeginStepwiseSearch(Coordinate from, Coordinate to, int agentSize, QueryOptions options)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return BeginStepwiseSearch(agentSize, from, to, options.Occupancy, options.AlwaysEnterable.ToArray(),
                options.IsDestinationAlwaysEnterable);
        }

        private StepwiseSearch BeginStepwiseSearch(int agentSize, Coordinate from, Coordinate to,
            OccupancyRule occupancy, Coordinate[] enterableCells, bool isDestinationEnterable)
        {
            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }

            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Destination is outside the valid field range", nameof(to));
            }

            if (_activeSearch != null)
            {
                throw new InvalidOperationException(
                    "A stepwise search is already in progress on this Pathfinder. " +
                    "Dispose the previous StepwiseSearch before starting a new one.");
            }

            _activeSearch = new StepwiseSearch(this, agentSize, from, to, occupancy, enterableCells,
                isDestinationEnterable);
            return _activeSearch;
        }

        /// <summary>
        /// A resumable, tick-by-tick driver over the A* search, intended for visualizing or teaching how the
        /// pathfinder explores the grid. Each <see cref="Tick"/> performs a single expansion using the owning
        /// <see cref="Pathfinder"/>'s own machinery (so the result matches <see cref="GetPath"/>) and returns a
        /// <see cref="SearchStep"/> with the accumulated searched area and, on success, the path.
        /// </summary>
        /// <remarks>
        /// Obtain an instance from <see cref="BeginStepwiseSearch"/>. It pins the pathfinder's cell and
        /// search-state buffers for the duration of the search and therefore implements
        /// <see cref="IDisposable"/> — always dispose it. This type favours clarity over the allocation-free
        /// design of the main pathfinder: it is not meant for a real-time hot path.
        /// </remarks>
        public sealed unsafe class StepwiseSearch : IDisposable
        {
            private readonly Pathfinder _pathfinder;
            private readonly Coordinate _from;
            private readonly Coordinate _to;
            private readonly int _toIndex;
            private readonly int _agentSize;

            // The per-query rules, re-applied to the pathfinder around every expansion.
            private readonly OccupancyRule _occupancy;
            private readonly Coordinate[] _enterableCells;
            private readonly bool _isDestinationEnterable;

            // The cell and node buffers are pinned for the whole search because the search holds raw pointers into
            // them (the open set into the nodes) that must stay valid across ticks.
            private GCHandle _handle;
            private GCHandle _nodesHandle;
            private GCHandle _arrivalsHandle;
            private Cell* _cells;
            private PathNode* _nodes;
            private byte* _arrivals;

            // Every cell ever discovered (in grid order of discovery). _seen[index] guards against duplicates.
            private readonly List<Coordinate> _discovered = new List<Coordinate>();
            private readonly bool[] _seen;

            private SearchState _state;
            private int _iteration;
            private Coordinate _current;
            private SearchStep _lastStep;
            private bool _disposed;

            internal StepwiseSearch(Pathfinder pathfinder, int agentSize, Coordinate from, Coordinate to,
                OccupancyRule occupancy, Coordinate[] enterableCells, bool isDestinationEnterable)
            {
                _pathfinder = pathfinder;
                _from = from;
                _to = to;
                _toIndex = pathfinder.GetLayoutIndex(to.X, to.Y);
                _agentSize = agentSize;
                _occupancy = occupancy;
                _enterableCells = enterableCells;
                _isDestinationEnterable = isDestinationEnterable;

                // Flatten the caller's grid representation into _cells before we take a pointer to it,
                // exactly as GetPath does.
                pathfinder.InitializeCellsArray();

                try
                {
                    _handle = GCHandle.Alloc(pathfinder._cells, GCHandleType.Pinned);
                    _nodesHandle = GCHandle.Alloc(pathfinder._nodes, GCHandleType.Pinned);
                    _cells = (Cell*)_handle.AddrOfPinnedObject();
                    _nodes = (PathNode*)_nodesHandle.AddrOfPinnedObject();

                    pathfinder.BeginSearch(_nodes);

                    _seen = new bool[pathfinder.Size];

                    // Seed the open set with the origin, exactly as CalculatePath (or its keyed counterpart) does.
                    var startIndex = pathfinder.GetLayoutIndex(from.X, from.Y);

                    if (pathfinder._settings.IsTieBreaking)
                    {
                        _arrivalsHandle = GCHandle.Alloc(pathfinder._arrivals, GCHandleType.Pinned);
                        _arrivals = (byte*)_arrivalsHandle.AddrOfPinnedObject();
                        pathfinder.BeginKeyedSearch(_nodes, _arrivals);
                        pathfinder.OpenOriginKeyed(_nodes, from, pathfinder.GetH(from.X, from.Y, to.X, to.Y));
                    }
                    else
                    {
                        pathfinder.OpenNode(_nodes, startIndex, PathNode.NoParent, 0, 0,
                            pathfinder.GetH(from.X, from.Y, to.X, to.Y));
                    }

                    MarkDiscovered(_cells + startIndex);

                    // Like GetPath, a destination the agent cannot enter fails before the first expansion.
                    _state = IsDestinationEnterable(from)
                        ? SearchState.InProgress
                        : SearchState.Failure;
                    _current = from;
                    _lastStep = BuildStep(Array.Empty<Coordinate>());
                }
                catch
                {
                    ReleaseHandle();
                    throw;
                }
            }

            /// <summary>
            /// Gets the overall state of the search.
            /// </summary>
            public SearchState State => _state;

            /// <summary>
            /// Gets a value indicating whether the search has finished (succeeded or failed). A search whose
            /// destination the agent cannot enter is complete (failed) before the first tick.
            /// </summary>
            public bool IsComplete => _state != SearchState.InProgress;

            /// <summary>
            /// Gets the number of expansions performed so far.
            /// </summary>
            public int Iteration => _iteration;

            /// <summary>
            /// Advances the search by expanding a single cell and returns a snapshot of the search afterwards.
            /// Once the search is complete this is a no-op that returns the final snapshot again.
            /// </summary>
            /// <returns>A <see cref="SearchStep"/> describing the accumulated searched area and, on success, the path.</returns>
            /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
            public SearchStep Tick()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(StepwiseSearch));
                }

                if (_state != SearchState.InProgress)
                {
                    return _lastStep;
                }

                var openCount = _pathfinder._settings.IsTieBreaking
                    ? _pathfinder._keyedOpenSet.Count
                    : _pathfinder._openSet.Count;

                if (openCount == 0)
                {
                    // Frontier exhausted without reaching the destination: no path exists.
                    _state = SearchState.Failure;
                    return _lastStep = BuildStep(Array.Empty<Coordinate>());
                }

                _iteration++;

                var neighbors = stackalloc Cell*[MaxNeighbors];
                var reachedGoal = ExpandNext(neighbors, out var current);
                _current = _pathfinder.GetCoordinate(current);

                if (reachedGoal)
                {
                    _state = SearchState.Success;
                    return _lastStep = BuildStep(TracePath(current));
                }

                // The expansion may have discovered new frontier cells; record them.
                for (var n = 0; n < MaxNeighbors; n++)
                {
                    var neighbor = neighbors[n];
                    if (neighbor != null)
                    {
                        MarkDiscovered(neighbor);
                    }
                }

                return _lastStep = BuildStep(Array.Empty<Coordinate>());
            }

            /// <summary>
            /// Advances the search until it completes and returns the final snapshot. Equivalent to calling
            /// <see cref="Tick"/> in a loop until <see cref="IsComplete"/> is true.
            /// </summary>
            /// <returns>The final <see cref="SearchStep"/> (a success or failure snapshot).</returns>
            /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
            public SearchStep RunToCompletion()
            {
                var step = _lastStep;
                while (!IsComplete)
                {
                    step = Tick();
                }

                return step;
            }

            private bool IsDestinationEnterable(Coordinate from)
            {
                fixed (Coordinate* enterable = _enterableCells)
                {
                    ApplyRules(enterable);

                    try
                    {
                        return _pathfinder.IsDestinationEnterable(_cells, from, _to, _agentSize);
                    }
                    finally
                    {
                        RestoreRules();
                    }
                }
            }

            private bool ExpandNext(Cell** neighbors, out int current)
            {
                fixed (Coordinate* enterable = _enterableCells)
                {
                    ApplyRules(enterable);

                    try
                    {
                        return _pathfinder.ExpandNext(_cells, _nodes, _to, _toIndex, _agentSize, neighbors,
                            out current);
                    }
                    finally
                    {
                        RestoreRules();
                    }
                }
            }

            private void ApplyRules(Coordinate* enterable)
            {
                _pathfinder._query = QueryRules.FromOptions(_pathfinder._settings, _occupancy, enterable,
                    _enterableCells.Length, _isDestinationEnterable, _to);
                _pathfinder._arrivalsPointer = _arrivals;
            }

            private void RestoreRules()
            {
                _pathfinder._query = QueryRules.FromSettings(_pathfinder._settings);
            }

            private void MarkDiscovered(Cell* cell)
            {
                var index = (int)(cell - _cells);
                if (_seen[index])
                {
                    return;
                }

                _seen[index] = true;
                _discovered.Add(_pathfinder.GetCoordinate(index));
            }

            /// <summary>
            /// Reconstructs the raw A* path (parent-index chain, or the tie-break walk) into a plain array. Mirrors
            /// <see cref="Pathfinder.ReconstructPath"/> but without pooling or smoothing, and — like it — excludes
            /// the origin cell.
            /// </summary>
            private Coordinate[] TracePath(int lastIndex)
            {
                if (_pathfinder._settings.IsTieBreaking)
                {
                    _pathfinder._arrivalsPointer = _arrivals;
                    var steps = _pathfinder.WalkRoute(lastIndex, _cells, _from, null, 0, out _);
                    var route = new Coordinate[steps];
                    _pathfinder.WalkRoute(lastIndex, _cells, _from, route, steps, out _);
                    return route;
                }

                var depth = _nodes[lastIndex].Depth;
                var path = new Coordinate[depth];

                var index = lastIndex;
                for (var i = depth - 1; i >= 0; i--)
                {
                    path[i] = _pathfinder.GetCoordinate(index);
                    index = _nodes[index].ParentIndex;
                }

                return path;
            }

            private SearchStep BuildStep(Coordinate[] path)
            {
                var count = _discovered.Count;
                var searched = new SearchNode[count];
                var openCount = 0;

                for (var i = 0; i < count; i++)
                {
                    var coordinate = _discovered[i];
                    var node = _nodes + _pathfinder.GetLayoutIndex(coordinate.X, coordinate.Y);
                    var nodeState = node->IsClosed ? SearchNodeState.Closed : SearchNodeState.Open;

                    if (nodeState == SearchNodeState.Open)
                    {
                        openCount++;
                    }

                    _pathfinder.GetScores(node, coordinate, _to, out var scoreG, out var scoreH, out var scoreF);
                    searched[i] = new SearchNode(coordinate, nodeState, scoreG, scoreH, scoreF);
                }

                return new SearchStep(_state, _iteration, _current, searched, openCount, count - openCount, path);
            }

            private void ReleaseHandle()
            {
                if (_handle.IsAllocated)
                {
                    _handle.Free();
                }

                if (_nodesHandle.IsAllocated)
                {
                    _nodesHandle.Free();
                }

                if (_arrivalsHandle.IsAllocated)
                {
                    _arrivalsHandle.Free();
                }

                _cells = null;
                _nodes = null;
                _arrivals = null;
            }

            /// <summary>
            /// Releases the pinned buffers and frees the owning <see cref="Pathfinder"/> for other queries.
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                ReleaseHandle();
                _pathfinder._activeSearch = null;
            }
        }
    }
}
