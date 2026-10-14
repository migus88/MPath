using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Core.Internal;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        // The active session, if any: guards against two concurrent stepwise searches sharing this instance's
        // open set and cell buffer, and lets Dispose end the session before returning the buffer it pins.
        private StepwiseSearch _activeSearch;

        /// <summary>
        /// Begins a stepwise (tick-by-tick) A* search that can be advanced one expansion at a time, exposing
        /// the accumulated searched area and — once it succeeds — the resulting path. This is an
        /// <b>educational / visualization</b> facility for showing how the algorithm explores the grid; the
        /// regular <see cref="GetPath"/> remains the way to compute a path in one shot.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="StepwiseSearch"/> drives the very same A* expansion as <see cref="GetPath"/>
        /// (it shares this instance's open set and cell buffer), so it produces an identical path; it simply
        /// pauses between expansions. Because it pins this instance's cell buffer for the lifetime of the
        /// search, the session <b>must be disposed</b> (wrap it in <c>using</c>), and the owning
        /// <see cref="Pathfinder"/> must not be used for any other query — including a second
        /// <see cref="BeginStepwiseSearch"/> — until the session is disposed.
        /// </remarks>
        /// <param name="agent">The agent for which to search. Cannot be null.</param>
        /// <param name="from">The origin coordinate. Must be inside the grid.</param>
        /// <param name="to">The destination coordinate. Must be inside the grid.</param>
        /// <returns>A disposable <see cref="StepwiseSearch"/> positioned before the first expansion.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="from"/> or <paramref name="to"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a stepwise search is already active on this instance.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public StepwiseSearch BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to)
        {
            ThrowIfDisposed();

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return BeginStepwiseSearch(agent.Size, from, to, OccupancyRule.UseSettings, Array.Empty<Coordinate>(),
                false);
        }

        /// <summary>
        /// Begins a stepwise search like <see cref="BeginStepwiseSearch(IAgent, Coordinate, Coordinate)"/> for an
        /// agent that occupies an <paramref name="agentSize"/> x <paramref name="agentSize"/> square of cells,
        /// without an <see cref="IAgent"/>. The session must be disposed.
        /// </summary>
        /// <param name="from">The origin coordinate. Must be inside the grid.</param>
        /// <param name="to">The destination coordinate. Must be inside the grid.</param>
        /// <param name="agentSize">The agent's square size in cells. Defaults to a single-cell agent.</param>
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
        /// Begins a stepwise search like <see cref="BeginStepwiseSearch(IAgent, Coordinate, Coordinate)"/>, with
        /// movement rules overridden by <paramref name="options"/> for the whole search. The options are copied when
        /// the search begins and applied on every tick, so it explores exactly as
        /// <see cref="GetPath(IAgent, Coordinate, Coordinate, QueryOptions)"/> with the same options would.
        /// </summary>
        /// <param name="agent">The agent for which to search. Cannot be null.</param>
        /// <param name="from">The origin coordinate. Must be inside the grid.</param>
        /// <param name="to">The destination coordinate. Must be inside the grid.</param>
        /// <param name="options">The per-query rules. <c>default</c> behaves exactly like the overload without options.</param>
        /// <returns>A disposable <see cref="StepwiseSearch"/> positioned before the first expansion.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="from"/> or <paramref name="to"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a stepwise search is already active on this instance.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public StepwiseSearch BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to, QueryOptions options)
        {
            ThrowIfDisposed();

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return BeginStepwiseSearch(agent.Size, from, to, options.Occupancy, options.AlwaysEnterable.ToArray(),
                options.IsDestinationAlwaysEnterable);
        }

        /// <summary>
        /// Begins a stepwise search like <see cref="BeginStepwiseSearch(Coordinate, Coordinate, int)"/>, with
        /// movement rules overridden by <paramref name="options"/> for the whole search, exactly as
        /// <see cref="GetPath(Coordinate, Coordinate, QueryOptions, int)"/> with the same options would.
        /// </summary>
        /// <param name="from">The origin coordinate. Must be inside the grid.</param>
        /// <param name="to">The destination coordinate. Must be inside the grid.</param>
        /// <param name="options">The per-query rules. <c>default</c> behaves exactly like the overload without options.</param>
        /// <param name="agentSize">The agent's square size in cells. Defaults to a single-cell agent.</param>
        /// <returns>A disposable <see cref="StepwiseSearch"/> positioned before the first expansion.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="from"/> or <paramref name="to"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a stepwise search is already active on this instance.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public StepwiseSearch BeginStepwiseSearch(Coordinate from, Coordinate to, QueryOptions options,
            int agentSize = SingleCellAgentSize)
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
        /// Obtain an instance from <see cref="BeginStepwiseSearch"/>. It pins the pathfinder's cell buffer for
        /// the duration of the search and therefore implements <see cref="IDisposable"/> — always dispose it.
        /// This type favours clarity over the allocation-free design of the main pathfinder: it is not meant
        /// for a real-time hot path.
        /// </remarks>
        public sealed unsafe class StepwiseSearch : IDisposable
        {
            private readonly Pathfinder _pathfinder;
            private readonly Coordinate _to;
            private readonly int _agentSize;

            // The per-query rules, re-applied to the pathfinder around every expansion.
            private readonly OccupancyRule _occupancy;
            private readonly Coordinate[] _enterableCells;
            private readonly bool _isDestinationEnterable;

            // The cell buffer is pinned for the whole search because the open set holds raw pointers into it
            // that must stay valid across ticks.
            private GCHandle _handle;
            private Cell* _cells;
            private bool _pinned;

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
                _to = to;
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
                    _pinned = true;
                    _cells = (Cell*)_handle.AddrOfPinnedObject();

                    pathfinder.ResetCells(_cells);
                    pathfinder._openSet.Reset(_cells);

                    _seen = new bool[pathfinder.Size];

                    // Seed the open set with the origin, mirroring CalculatePath. ScoreH is set purely so the
                    // start node reports a consistent f = g + h when visualized; it does not affect the search.
                    var start = pathfinder.GetCell(_cells, from.X, from.Y);
                    start->ScoreH = pathfinder.GetH(from.X, from.Y, to.X, to.Y);
                    pathfinder._openSet.Enqueue(start, start->ScoreH);
                    MarkDiscovered(start);

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

                if (_pathfinder._openSet.Count == 0)
                {
                    // Frontier exhausted without reaching the destination: no path exists.
                    _state = SearchState.Failure;
                    return _lastStep = BuildStep(Array.Empty<Coordinate>());
                }

                _iteration++;

                var neighbors = stackalloc Cell*[MaxNeighbors];
                var reachedGoal = ExpandNext(neighbors, out var current);
                _current = current->Coordinate;

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

            private bool ExpandNext(Cell** neighbors, out Cell* current)
            {
                fixed (Coordinate* enterable = _enterableCells)
                {
                    ApplyRules(enterable);

                    try
                    {
                        return _pathfinder.ExpandNext(_cells, _to, _agentSize, neighbors, out current);
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
                _discovered.Add(cell->Coordinate);
            }

            /// <summary>
            /// Reconstructs the raw A* path (parent-pointer chain) into a plain array. Mirrors
            /// <see cref="Pathfinder.ReconstructPath"/> but without pooling or smoothing, and — like it — excludes
            /// the origin cell.
            /// </summary>
            private Coordinate[] TracePath(Cell* lastCell)
            {
                var depth = lastCell->Depth;
                var path = new Coordinate[depth];

                var current = lastCell;
                for (var i = depth - 1; i >= 0; i--)
                {
                    path[i] = current->Coordinate;
                    var parent = current->ParentCoordinate;
                    current = _pathfinder.GetCell(_cells, parent.X, parent.Y);
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
                    var cell = _pathfinder.GetCell(_cells, coordinate.X, coordinate.Y);
                    var nodeState = cell->IsClosed ? SearchNodeState.Closed : SearchNodeState.Open;

                    if (nodeState == SearchNodeState.Open)
                    {
                        openCount++;
                    }

                    searched[i] = new SearchNode(coordinate, nodeState,
                        cell->ScoreG, cell->ScoreH, cell->ScoreF);
                }

                return new SearchStep(_state, _iteration, _current, searched, openCount, count - openCount, path);
            }

            private void ReleaseHandle()
            {
                if (!_pinned)
                {
                    return;
                }

                _handle.Free();
                _pinned = false;
                _cells = null;
            }

            /// <summary>
            /// Releases the pinned cell buffer and frees the owning <see cref="Pathfinder"/> for other queries.
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
