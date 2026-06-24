using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        // Guards against two concurrent stepwise searches sharing this instance's open set and cell buffer.
        private bool _searchSessionActive;

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
        public StepwiseSearch BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }

            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Destination is outside the valid field range", nameof(to));
            }

            if (_searchSessionActive)
            {
                throw new InvalidOperationException(
                    "A stepwise search is already in progress on this Pathfinder. " +
                    "Dispose the previous StepwiseSearch before starting a new one.");
            }

            _searchSessionActive = true;

            try
            {
                return new StepwiseSearch(this, agent, from, to);
            }
            catch
            {
                _searchSessionActive = false;
                throw;
            }
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

            internal StepwiseSearch(Pathfinder pathfinder, IAgent agent, Coordinate from, Coordinate to)
            {
                _pathfinder = pathfinder;
                _to = to;
                _agentSize = agent.Size;

                // Flatten the caller's grid representation into _cells before we take a pointer to it,
                // exactly as GetPath does.
                pathfinder.InitializeCellsArray();

                try
                {
                    _handle = GCHandle.Alloc(pathfinder._cells, GCHandleType.Pinned);
                    _pinned = true;
                    _cells = (Cell*)_handle.AddrOfPinnedObject();

                    pathfinder.ResetCells(_cells);
                    pathfinder._openSet.Clear();

                    _seen = new bool[pathfinder.Size];

                    // Seed the open set with the origin, mirroring CalculatePath. ScoreH is set purely so the
                    // start node reports a consistent f = g + h when visualized; it does not affect the search.
                    var start = pathfinder.GetCell(_cells, from.X, from.Y);
                    start->ScoreH = pathfinder.GetH(from.X, from.Y, to.X, to.Y);
                    pathfinder._openSet.Enqueue(start, start->ScoreH);
                    MarkDiscovered(start);

                    _state = SearchState.InProgress;
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
            /// Gets a value indicating whether the search has finished (succeeded or failed).
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
                var reachedGoal = _pathfinder.ExpandNext(_cells, _to, _agentSize, neighbors, out var current);
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
                _pathfinder._searchSessionActive = false;
            }
        }
    }
}
