using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Core.Internal;
using Migs.MPath.Core.Caching;
using static Migs.MPath.Core.Internal.DirectionIndexes;

namespace Migs.MPath.Core
{
    /// <summary>
    /// A high-performance, memory-efficient A* pathfinding implementation for grid-based environments.
    /// </summary>
    public sealed unsafe partial class Pathfinder : IDisposable
    {
        private const int MaxNeighbors = 8;
        private const int SingleCellAgentSize = 1;

        private int Width { get; }
        private int Height { get; }
        private int Size { get; }

        private readonly FastPathfinderSettings _settings;

        // The occupancy rules of the running query. The settings' defaults outside an options query.
        private QueryRules _query;
        private readonly UnsafePriorityQueue _openSet;
        
        private readonly InitializationMode _initializationMode;
        private readonly ICellHolder[] _cellHolders;
        private readonly ICellHolder[,] _cellHoldersMatrix;
        private readonly Cell[,] _cellsMatrix;

        // The caller's array in Cell[] mode. Queries only read it.
        private readonly Cell[] _sourceCells;

        // The buffer searches read, laid out by GetLayoutIndex: the caller's Cell[] when it is in row- or column-major
        // order, otherwise a pooled snapshot of the caller's grid.
        private Cell[] _cells;

        // Whether the snapshot in _cells has been taken and not marked stale by Refresh since.
        private bool _isSnapshotCurrent;

        // Cell[] mode: whether the array's layout has been checked (CellLayout.Auto: detected) since construction or
        // the last Refresh, and whether it is unordered, so _cells is a snapshot (_arraySnapshot) of it.
        private bool _isArrayLayoutResolved;
        private bool _isArraySnapshotted;

        // Cell[] mode, unordered arrays only: the pooled snapshot, and the caller's index of each snapshot cell
        // (so the cell-update API finds a cell without a search). Rented on first use, returned by Dispose.
        private Cell[] _arraySnapshot;
        private int[] _arraySourceIndexes;

        // The layout of _cells: the flat index of (x, y) is x * _xStride + y * _yStride.
        private int _xStride;
        private int _yStride;

        // The search state of every cell (same index as _cells). Only nodes stamped with the current generation
        // belong to the running search, so starting a search never walks the grid.
        private PathNode[] _nodes;
        private uint _generation;
        
        private bool _isPathCachingEnabled;
        private IPathCaching _pathCaching;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class over the caller's cell array.
        /// The pathfinder keeps a reference to the array and only reads it: queries never reorder or write it.
        /// </summary>
        /// <param name="cells">
        /// Array of Cells, laid out as <see cref="IPathfinderSettings.CellLayout"/> says: column-major
        /// (<c>X * fieldHeight + Y</c>), row-major (<c>Y * fieldWidth + X</c>) or in any order, each cell carrying its
        /// <see cref="Cell.Coordinate"/>. The default, <see cref="CellLayout.Auto"/>, detects which at the first
        /// query. Only the first <c>fieldWidth * fieldHeight</c> elements are read. Cannot be null.
        /// </param>
        /// <param name="fieldWidth">The width of the field.</param>
        /// <param name="fieldHeight">The height of the field.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="cells"/> is shorter than <c>fieldWidth * fieldHeight</c>.
        /// </exception>
        /// <remarks>
        /// A row- or column-major array is searched in place: a query costs only the cells it searches, and direct
        /// edits to the array are seen by the next query. An unordered array is copied into a pooled buffer, like a
        /// <see cref="Cell"/>[,] matrix, before every query or as <see cref="IPathfinderSettings.GridSnapshotMode"/>
        /// says. Changing the layout of the array itself (moving cells or changing their coordinates) needs a
        /// <see cref="Refresh"/>.
        /// </remarks>
        public Pathfinder(Cell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _sourceCells = cells ?? throw new ArgumentNullException(nameof(cells));

            if (cells.Length < Size)
            {
                throw new ArgumentException(
                    $"The array holds {cells.Length} cells, fewer than the {Size} of a {Width}x{Height} field.",
                    nameof(cells));
            }

            _cells = cells;
            _initializationMode = InitializationMode.CellsArray;

            switch (_settings.CellLayout)
            {
                case CellLayout.RowMajor:
                    SetLayout(CellLayout.RowMajor);
                    break;
                case CellLayout.Unordered:
                    ResolveArrayLayout();
                    break;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with cell holders.
        /// This constructor borrows a cell array from the shared array pool and returns it when disposed.
        /// </summary>
        /// <param name="holders">Array of Cell Holders. Cannot be null.</param>
        /// <param name="fieldWidth">The width of the field.</param>
        /// <param name="fieldHeight">The height of the field.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="holders"/> is null.</exception>
        public Pathfinder(ICellHolder[] holders, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _cellHolders = holders ?? throw new ArgumentNullException(nameof(holders));
            _cells = ArrayPool<Cell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellHoldersArray;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with a cell matrix.
        /// This constructor borrows a cell array from the shared array pool and returns it when disposed.
        /// </summary>
        /// <param name="cellsMatrix">Matrix of Cells. Cannot be null.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cellsMatrix"/> is null.</exception>
        public Pathfinder(Cell[,] cellsMatrix, IPathfinderSettings settings = null)
            : this(cellsMatrix?.GetLength(0) ?? throw new ArgumentNullException(nameof(cellsMatrix)), 
                  cellsMatrix.GetLength(1), 
                  settings)
        {
            _cellsMatrix = cellsMatrix;
            _cells = ArrayPool<Cell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellsMatrix;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with a cell holder matrix.
        /// This constructor borrows a cell array from the shared array pool and returns it when disposed.
        /// </summary>
        /// <param name="cellHoldersMatrix">Matrix of Cell Holders. Cannot be null.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cellHoldersMatrix"/> is null.</exception>
        public Pathfinder(ICellHolder[,] cellHoldersMatrix, IPathfinderSettings settings = null)
            : this(cellHoldersMatrix?.GetLength(0) ?? throw new ArgumentNullException(nameof(cellHoldersMatrix)), 
                  cellHoldersMatrix.GetLength(1), 
                  settings)
        {
            _cellHoldersMatrix = cellHoldersMatrix;
            _cells = ArrayPool<Cell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellHoldersMatrix;
        }

        private Pathfinder(int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
        {
            if (fieldWidth <= 0)
            {
                throw new ArgumentException("Field width must be positive", nameof(fieldWidth));
            }
            
            if (fieldHeight <= 0)
            {
                throw new ArgumentException("Field height must be positive", nameof(fieldHeight));
            }
                
            Width = fieldWidth;
            Height = fieldHeight;
            Size = Width * Height;

            _settings = FastPathfinderSettings.FromSettings(settings ?? new PathfinderSettings());
            SetLayout(CellLayout.ColumnMajor);
            _query = QueryRules.FromSettings(_settings);
            _openSet = new UnsafePriorityQueue(Size);

            // Pooled arrays may hold another owner's data; generation 0 marks every node as untouched.
            _nodes = ArrayPool<PathNode>.Shared.Rent(Size);
            Array.Clear(_nodes, 0, Size);

            if (_settings.IsTieBreaking)
            {
                _keyedOpenSet = new KeyedPriorityQueue(Size);
                _arrivals = ArrayPool<byte>.Shared.Rent(Size);
            }
        }

        /// <summary>
        /// Tells the pathfinder the caller's grid changed outside the cell-update methods: the next query copies the
        /// <see cref="Cell"/>[,] matrix, the cell holders or an unordered <see cref="Cell"/>[] again, a
        /// <see cref="Cell"/>[] array's layout is checked (or, with <see cref="CellLayout.Auto"/>, detected) again,
        /// and the path cache is cleared, as by <see cref="InvalidateCache"/>.
        /// </summary>
        /// <returns>The current pathfinder instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// Cheap: the copy and the layout check happen on the next query, once, however many times this is called
        /// before it. Needed after direct edits to the grid when they must be seen: with
        /// <see cref="GridSnapshotMode.Manual"/> (snapshots are not retaken otherwise), after moving cells or changing
        /// their coordinates in a <see cref="Cell"/>[] array, and whenever path caching is enabled (cached paths do not
        /// see direct edits). Edits through the cell-update methods (<see cref="SetWalkable"/>, ...) are seen at once
        /// and clear the cache themselves.
        /// </remarks>
        public Pathfinder Refresh()
        {
            ThrowIfDisposed();

            _isSnapshotCurrent = false;
            _isArrayLayoutResolved = false;
            InvalidateCache();

            return this;
        }
        
        /// <summary>
        /// Enables path caching for the pathfinder.
        /// </summary>
        /// <param name="pathCachingHandler">Optional custom implementation of IPathCaching. If null, the default implementation will be used.</param>
        /// <returns>The current pathfinder instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public Pathfinder EnablePathCaching(IPathCaching pathCachingHandler = null)
        {
            ThrowIfDisposed();

            if (_isPathCachingEnabled)
            {
                // Dispose any existing path caching handler
                _pathCaching?.Dispose();
            }
            
            _pathCaching = pathCachingHandler ?? new DefaultPathCaching();
            _isPathCachingEnabled = true;
            
            return this;
        }
        
        /// <summary>
        /// Disables path caching for the pathfinder.
        /// </summary>
        /// <returns>The current pathfinder instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public Pathfinder DisablePathCaching()
        {
            ThrowIfDisposed();

            if (!_isPathCachingEnabled)
            {
                return this;
            }
            
            _pathCaching?.Dispose();
            _pathCaching = null;
            _isPathCachingEnabled = false;

            return this;
        }
        
        /// <summary>
        /// Invalidates the current path cache.
        /// </summary>
        /// <returns>The current pathfinder instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public Pathfinder InvalidateCache()
        {
            ThrowIfDisposed();

            if (_isPathCachingEnabled && _pathCaching != null)
            {
                _pathCaching.ClearCache();
            }
            
            return this;
        }

        /// <summary>
        /// Calculates a path from the specified starting position to the destination.
        /// </summary>
        /// <param name="agent">The agent for which to calculate the path. Cannot be null.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <returns>A <see cref="PathResult"/> containing the calculated path or a failure result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin or the destination is outside the valid field.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// A destination the agent cannot enter (not walkable, occupied while
        /// <see cref="IPathfinderSettings.IsCalculatingOccupiedCells"/> is enabled, or without clearance for the
        /// agent's size) fails immediately without searching, unless it is the origin itself.
        /// </remarks>
        public PathResult GetPath(IAgent agent, Coordinate from, Coordinate to)
        {
            ThrowIfDisposed();

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return FindPath(agent, agent.Size, from, to);
        }

        /// <summary>
        /// Calculates a path from the specified starting position to the destination for an agent that occupies an
        /// <paramref name="agentSize"/> x <paramref name="agentSize"/> square of cells, without an <see cref="IAgent"/>.
        /// </summary>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="agentSize">The agent's square size in cells. Defaults to a single-cell agent.</param>
        /// <returns>A <see cref="PathResult"/> containing the calculated path or a failure result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin or the destination is outside the valid field.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// Finds the same path as <see cref="GetPath(IAgent, Coordinate, Coordinate)"/> with an agent of the same
        /// <see cref="IAgent.Size"/>, and shares its path cache entries.
        /// </remarks>
        public PathResult GetPath(Coordinate from, Coordinate to, int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return FindPath(null, agentSize, from, to);
        }

        /// <summary>
        /// The shared body of the <c>GetPath</c> overloads. <paramref name="agent"/> is the caller's agent, or
        /// <c>null</c> for the <c>agentSize</c> overloads; it is only handed to the path cache.
        /// </summary>
        private PathResult FindPath(IAgent agent, int agentSize, Coordinate from, Coordinate to)
        {
            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }
                
            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Destination is outside the valid field range", nameof(to));
            }
            
            if (TryGetCachedPath(agent, agentSize, from, to, out var cachedPath))
            {
                return cachedPath;
            }

            InitializeCellsArray();

            var cells = new Span<Cell>(_cells, 0, Size);

            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            fixed (PathNode* nodes = _nodes)
            fixed (byte* arrivals = _arrivals)
            {
                if (!IsDestinationEnterable(ptr, from, to, agentSize))
                {
                    return PathResult.Failure();
                }

                BeginSearch(nodes);

                PathResult result;

                if (_settings.IsTieBreaking)
                {
                    BeginKeyedSearch(nodes, arrivals);
                    result = CalculatePathKeyed(agentSize, from, to, ptr, nodes);
                }
                else
                {
                    result = CalculatePath(agentSize, from, to, ptr, nodes);
                }
                
                CachePath(agent, agentSize, from, to, result);
                
                return result;
            }
        }

        /// <summary>
        /// Looks the query up in the path cache. A hit is returned as a fresh copy owned by the caller, so the
        /// instance the cache keeps is never handed out and disposing a result never touches the cache.
        /// </summary>
        /// <remarks>
        /// Every cache read goes through here and every write through <see cref="CachePath"/>; a query that must not
        /// share entries with plain <c>(agent, from, to)</c> queries (for example one with non-default per-query
        /// options) skips both.
        /// </remarks>
        private bool TryGetCachedPath(IAgent agent, int agentSize, Coordinate from, Coordinate to,
            out PathResult pathResult)
        {
            if (IsCachingQuery() &&
                _pathCaching.TryGetCachedPath(agent ?? SizedAgent.Of(agentSize), from, to, out var cachedPath))
            {
                pathResult = cachedPath.Copy();
                return true;
            }

            pathResult = null;
            return false;
        }

        /// <summary>
        /// Stores a successful result in the path cache as a private copy that the cache owns, so the caller keeps
        /// sole ownership of <paramref name="result"/>.
        /// </summary>
        private void CachePath(IAgent agent, int agentSize, Coordinate from, Coordinate to, PathResult result)
        {
            if (result.IsSuccess && IsCachingQuery())
            {
                _pathCaching.CachePath(agent ?? SizedAgent.Of(agentSize), from, to, result.Copy());
            }
        }

        /// <summary>
        /// Whether the running query may use the path cache: caching is enabled and the query runs under the settings'
        /// own rules. Cached paths are keyed by <c>(agent, from, to)</c> only, so a query whose
        /// <see cref="QueryOptions"/> change the rules neither reads nor fills the cache.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCachingQuery()
        {
            return _isPathCachingEnabled && _query.IsDefault(_settings);
        }

        /// <summary>
        /// Calculates a path like <see cref="GetPath(IAgent, Coordinate, Coordinate)"/>, with movement rules
        /// overridden for this query only by <paramref name="options"/>. The grid and the settings are not modified.
        /// </summary>
        /// <param name="agent">The agent for which to calculate the path. Cannot be null.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="options">
        /// The per-query rules. <c>default</c> behaves exactly like the overload without options. With
        /// <see cref="QueryOptions.IsDestinationAlwaysEnterable"/>, a path to an occupied destination can succeed.
        /// </param>
        /// <returns>A <see cref="PathResult"/> containing the calculated path or a failure result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="agent"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin or the destination is outside the valid field.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// When the options change the rules, path caching is bypassed: the cache is neither read nor filled.
        /// </remarks>
        public PathResult GetPath(IAgent agent, Coordinate from, Coordinate to, QueryOptions options)
        {
            ThrowIfDisposed();

            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            return FindPath(agent, agent.Size, from, to, options);
        }

        /// <summary>
        /// Calculates a path like <see cref="GetPath(Coordinate, Coordinate, int)"/>, with movement rules
        /// overridden for this query only by <paramref name="options"/>. The grid and the settings are not modified.
        /// </summary>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="options">
        /// The per-query rules. <c>default</c> behaves exactly like the overload without options. With
        /// <see cref="QueryOptions.IsDestinationAlwaysEnterable"/>, a path to an occupied destination can succeed.
        /// </param>
        /// <param name="agentSize">The agent's square size in cells. Defaults to a single-cell agent.</param>
        /// <returns>A <see cref="PathResult"/> containing the calculated path or a failure result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin or the destination is outside the valid field.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// When the options change the rules, path caching is bypassed: the cache is neither read nor filled.
        /// </remarks>
        public PathResult GetPath(Coordinate from, Coordinate to, QueryOptions options,
            int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return FindPath(null, agentSize, from, to, options);
        }

        private PathResult FindPath(IAgent agent, int agentSize, Coordinate from, Coordinate to, QueryOptions options)
        {
            var enterableCells = options.AlwaysEnterable;

            fixed (Coordinate* enterable = enterableCells)
            {
                _query = QueryRules.FromOptions(_settings, options.Occupancy, enterable, enterableCells.Length,
                    options.IsDestinationAlwaysEnterable, to);

                try
                {
                    return FindPath(agent, agentSize, from, to);
                }
                finally
                {
                    _query = QueryRules.FromSettings(_settings);
                }
            }
        }

        /// <summary>
        /// Calculates the path using A* algorithm.
        /// </summary>
        private PathResult CalculatePath(int agentSize, Coordinate from, Coordinate to, Cell* cells, PathNode* nodes)
        {
            OpenNode(nodes, GetLayoutIndex(from.X, from.Y), PathNode.NoParent, 0, 0f,
                GetH(from.X, from.Y, to.X, to.Y));

            var neighbors = stackalloc Cell*[MaxNeighbors];

            // A* algorithm main loop. Each iteration expands one cell; ExpandNextFloat (behind ExpandNext) is the
            // single source of truth for that step, shared with the stepwise (tick-by-tick) search.
            while (_openSet.Count > 0)
            {
                if (ExpandNextFloat(cells, nodes, to, agentSize, neighbors, out var current))
                {
                    // Destination dequeued — search is done.
                    return ReconstructPath(current, cells, nodes, agentSize);
                }
            }

            return PathResult.Failure();
        }

        /// <summary>
        /// Starts a search over <paramref name="nodes"/> (the pinned <see cref="_nodes"/>): a new generation makes
        /// every node untouched without visiting it, and the open set is emptied. On the rare wrap-around of the
        /// generation counter the nodes are cleared once, so no stale stamp can match a later generation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BeginSearch(PathNode* nodes)
        {
            if (++_generation == 0)
            {
                new Span<PathNode>(nodes, Size).Clear();
                _generation = 1;
            }

            _openSet.Reset(nodes);
        }

        /// <summary>
        /// The generation of the latest search. Internal so tests can force the counter near its wrap-around.
        /// </summary>
        internal uint SearchGeneration
        {
            get => _generation;
            set => _generation = value;
        }

        /// <summary>
        /// Touches the node at <paramref name="index"/> for the running search and queues it with priority
        /// <paramref name="scoreG"/> + <paramref name="scoreH"/>. The node must be untouched in this search.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OpenNode(PathNode* nodes, int index, int parentIndex, int depth, float scoreG, float scoreH)
        {
            var node = nodes + index;
            node->Generation = _generation;
            node->ScoreG = scoreG;
            node->ScoreH = scoreH;
            node->ParentIndex = parentIndex;
            node->Depth = depth;

            _openSet.Enqueue(index, scoreG + scoreH);
        }

        /// <summary>
        /// Expands the next cell from the open set: dequeues the current best candidate and — unless it is the
        /// destination — closes it and relaxes its neighbors. This is one iteration of the A* main loop,
        /// factored out so the batch <see cref="GetPath"/> and the stepwise <see cref="StepwiseSearch"/> run the
        /// exact same logic. The caller is responsible for ensuring the open set is non-empty.
        /// </summary>
        /// <param name="current">The index of the dequeued cell.</param>
        /// <returns><c>true</c> when the dequeued cell is the destination and the search should stop;
        /// otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExpandNext(Cell* cells, PathNode* nodes, Coordinate to, int agentSize, Cell** neighbors,
            out int current)
        {
            return _settings.IsTieBreaking
                ? ExpandNextKeyed(cells, nodes, to, agentSize, neighbors, out current)
                : ExpandNextFloat(cells, nodes, to, agentSize, neighbors, out current);
        }

        /// <summary>
        /// One expansion of the float search (<see cref="TieBreaking.None"/>); <see cref="CalculatePath"/> calls it
        /// directly so its loop has no mode check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExpandNextFloat(Cell* cells, PathNode* nodes, Coordinate to, int agentSize, Cell** neighbors,
            out int current)
        {
            current = _openSet.Dequeue();

            var cell = cells + current;

            if (cell->Coordinate == to)
            {
                return true;
            }

            nodes[current].QueueIndex = PathNode.ClosedQueueIndex;

            PopulateNeighbors(cells, cell, agentSize, neighbors);
            ProcessNeighbors(cells, nodes, current, neighbors, to);

            return false;
        }

        /// <summary>
        /// Processes each neighbor of the current cell in the pathfinding algorithm. A node this search has not
        /// touched yet is opened; a touched one is either closed (skipped) or open (decrease-key when cheaper).
        /// </summary>
        private void ProcessNeighbors(Cell* cells, PathNode* nodes, int current, Cell** neighbors, Coordinate to)
        {
            var currentCell = cells + current;
            var currentNode = nodes + current;
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

                if (node->Generation != generation)
                {
                    var neighborCoord = neighbor->Coordinate;
                    OpenNode(nodes, index, current, currentNode->Depth + 1,
                        currentNode->ScoreG + GetStepCost(currentCell, neighbor),
                        GetH(neighborCoord.X, neighborCoord.Y, to.X, to.Y));
                    continue;
                }

                if (node->IsClosed)
                {
                    continue;
                }

                var scoreG = currentNode->ScoreG + GetStepCost(currentCell, neighbor);

                if (scoreG < node->ScoreG)
                {
                    node->ScoreG = scoreG;
                    node->ParentIndex = current;
                    node->Depth = currentNode->Depth + 1;

                    // A cheaper g lowers f, so the cell must move up the heap (a decrease-key), not just change in place.
                    _openSet.UpdatePriority(index, scoreG + node->ScoreH);
                }
            }
        }

        /// <summary>
        /// Reconstructs the final path from the destination cell back to the start.
        /// </summary>
        private PathResult ReconstructPath(int lastIndex, Cell* cells, PathNode* nodes, int agentSize)
        {
            var depth = nodes[lastIndex].Depth;
            var path = ArrayPool<Coordinate>.Shared.Rent(depth);
            Array.Clear(path, 0, path.Length);

            var diagonalSteps = 0;

            var index = lastIndex;
            for (var i = depth - 1; i >= 0; i--)
            {
                var coordinate = cells[index].Coordinate;
                path[i] = coordinate;
                index = nodes[index].ParentIndex;

                var parent = cells[index].Coordinate;
                if (IsDiagonalMovement(parent.X, parent.Y, coordinate.X, coordinate.Y))
                {
                    diagonalSteps++;
                }
            }

            return CreatePathResult(path, depth, diagonalSteps, cells, agentSize);
        }

        /// <summary>
        /// Wraps a reconstructed route (origin excluded) into the result, smoothing it when enabled. The step counts
        /// always describe the route the search found, before smoothing.
        /// </summary>
        private PathResult CreatePathResult(Coordinate[] path, int steps, int diagonalSteps, Cell* cells,
            int agentSize)
        {
            // Apply path smoothing if enabled
            var result = _settings.PathSmoothingMethod != PathSmoothingMethod.None && steps > 2
                ? SmoothPath(path, steps, cells, agentSize)
                : PathResult.Create(path, steps);

            return result.WithStepCounts(steps, diagonalSteps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Cell* GetCell(Cell* cells, int x, int y)
        {
            return cells + GetLayoutIndex(x, y);
        }

        /// <summary>
        /// The flat index of (<paramref name="x"/>, <paramref name="y"/>) in the searched cell buffer. The single
        /// source of the layout: every grid mode and the cell-update API must place cells by it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLayoutIndex(int x, int y)
        {
            return x * _xStride + y * _yStride;
        }

        /// <summary>
        /// Sets the layout of <see cref="_cells"/>: row-major for <see cref="CellLayout.RowMajor"/>, otherwise
        /// column-major (the layout of every snapshot).
        /// </summary>
        private void SetLayout(CellLayout layout)
        {
            if (layout == CellLayout.RowMajor)
            {
                _xStride = 1;
                _yStride = Width;
            }
            else
            {
                _xStride = Height;
                _yStride = 1;
            }
        }

        /// <summary>
        /// Brings <see cref="_cells"/> up to date for a query: resolves the layout of a <see cref="Cell"/>[] once,
        /// then copies the caller's grid into the snapshot when the grid has one and the snapshot mode asks for it.
        /// A row- or column-major <see cref="Cell"/>[] is searched in place and needs nothing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCellsArray()
        {
            if (_initializationMode == InitializationMode.CellsArray)
            {
                if (!_isArrayLayoutResolved)
                {
                    ResolveArrayLayout();
                }

                if (!_isArraySnapshotted)
                {
                    return;
                }
            }

            if (_isSnapshotCurrent && _settings.GridSnapshotMode == GridSnapshotMode.Manual)
            {
                return;
            }

            TakeSnapshot();
            _isSnapshotCurrent = true;
        }

        /// <summary>
        /// Settles where the cells of the caller's <see cref="Cell"/>[] are: detected for <see cref="CellLayout.Auto"/>,
        /// checked for a declared row- or column-major layout. An unordered array switches the search to a pooled,
        /// column-major snapshot. Runs at the first query and at the first one after <see cref="Refresh"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a declared row- or column-major array holds a cell at the wrong index.
        /// </exception>
        private void ResolveArrayLayout()
        {
            var layout = _settings.CellLayout;

            switch (layout)
            {
                case CellLayout.Auto:
                    layout = DetectArrayLayout();
                    break;
                case CellLayout.ColumnMajor:
                case CellLayout.RowMajor:
                    if (!IsArrayInLayout(layout))
                    {
                        throw new InvalidOperationException(
                            $"The cell array is not in {layout} order: a cell's coordinate does not match its index. " +
                            "Fix the array, or set CellLayout to Auto or Unordered.");
                    }

                    break;
            }

            SetLayout(layout);
            _isArraySnapshotted = layout == CellLayout.Unordered;

            if (_isArraySnapshotted)
            {
                RentArraySnapshot();
                _cells = _arraySnapshot;
                _isSnapshotCurrent = false;
            }
            else
            {
                _cells = _sourceCells;
            }

            _isArrayLayoutResolved = true;
        }

        private CellLayout DetectArrayLayout()
        {
            if (IsArrayInLayout(CellLayout.ColumnMajor))
            {
                return CellLayout.ColumnMajor;
            }

            return IsArrayInLayout(CellLayout.RowMajor) ? CellLayout.RowMajor : CellLayout.Unordered;
        }

        private bool IsArrayInLayout(CellLayout layout)
        {
            var isRowMajor = layout == CellLayout.RowMajor;
            var outer = isRowMajor ? Height : Width;
            var inner = isRowMajor ? Width : Height;

            fixed (Cell* cells = _sourceCells)
            {
                var cell = cells;

                for (var major = 0; major < outer; major++)
                {
                    for (var minor = 0; minor < inner; minor++, cell++)
                    {
                        var coordinate = cell->Coordinate;
                        var isInPlace = isRowMajor
                            ? coordinate.X == minor && coordinate.Y == major
                            : coordinate.X == major && coordinate.Y == minor;

                        if (!isInPlace)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Rents the snapshot of an unordered <see cref="Cell"/>[] and its source-index map, once. Both start cleared,
        /// so a grid cell the array does not hold reads as a wall and maps to no source cell.
        /// </summary>
        private void RentArraySnapshot()
        {
            if (_arraySnapshot != null)
            {
                return;
            }

            _arraySnapshot = ArrayPool<Cell>.Shared.Rent(Size);
            Array.Clear(_arraySnapshot, 0, Size);
            _arraySourceIndexes = ArrayPool<int>.Shared.Rent(Size);
            Array.Clear(_arraySourceIndexes, 0, Size);
        }

        /// <summary>
        /// Copies the caller's grid into <see cref="_cells"/> at each cell's own coordinate.
        /// </summary>
        private void TakeSnapshot()
        {
            switch (_initializationMode)
            {
                case InitializationMode.CellsArray:
                    CopyUnorderedArray();
                    break;
                case InitializationMode.CellHoldersArray:
                    CopyCellHolders();
                    break;
                case InitializationMode.CellsMatrix:
                    CopyCellsMatrix();
                    break;
                case InitializationMode.CellHoldersMatrix:
                    CopyCellHoldersMatrix();
                    break;
            }
        }

        private void CopyUnorderedArray()
        {
            var source = _sourceCells;
            var snapshot = _cells;
            var sourceIndexes = _arraySourceIndexes;

            for (var i = 0; i < Size; i++)
            {
                var coordinate = source[i].Coordinate;

                if (!IsPositionValid(coordinate.X, coordinate.Y))
                {
                    throw new InvalidOperationException(
                        $"The cell at index {i} has coordinate {coordinate}, outside the {Width}x{Height} field.");
                }

                var index = GetLayoutIndex(coordinate.X, coordinate.Y);
                snapshot[index] = source[i];
                sourceIndexes[index] = i;
            }
        }

        private void CopyCellHolders()
        {
            if (_cellHolders == null)
            {
                return;
            }

            foreach (var cellHolder in _cellHolders)
            {
                if (cellHolder == null)
                    continue;
                    
                var coordinate = cellHolder.CellData.Coordinate;
                _cells[GetLayoutIndex(coordinate.X, coordinate.Y)] = cellHolder.CellData;
            }
        }
        
        private void CopyCellsMatrix()
        {
            if (_cellsMatrix == null)
            {
                return;
            }
            
            foreach (var cell in _cellsMatrix)
            {
                _cells[GetLayoutIndex(cell.Coordinate.X, cell.Coordinate.Y)] = cell;
            }
        }
        
        private void CopyCellHoldersMatrix()
        {
            if (_cellHoldersMatrix == null)
            {
                return;
            }

            foreach (var cellHolder in _cellHoldersMatrix)
            {
                if (cellHolder == null)
                    continue;
                    
                var coordinate = cellHolder.CellData.Coordinate;
                _cells[GetLayoutIndex(coordinate.X, coordinate.Y)] = cellHolder.CellData;
            }
        }

        /// <summary>
        /// The cost of stepping from <paramref name="current"/> into the adjacent <paramref name="neighbor"/>: the
        /// straight or diagonal travel multiplier plus the entered cell's <see cref="Cell.Weight"/> (when cell
        /// weighting is enabled). The single step-cost function of both <see cref="GetPath"/> and
        /// <see cref="GetReachable"/>, so a route's cost means the same in both. The weight is added, not
        /// multiplied: <see cref="Cell.Weight"/> defaults to 0, and a multiplier would make every step free.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetStepCost(Cell* current, Cell* neighbor)
        {
            var travel = GetNeighborTravelWeightMultiplier(
                current->Coordinate.X, current->Coordinate.Y,
                neighbor->Coordinate.X, neighbor->Coordinate.Y);

            return travel + GetCellWeightMultiplier(neighbor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetNeighborTravelWeightMultiplier(int startX, int startY, int destX, int destY)
        {
            return IsDiagonalMovement(startX, startY, destX, destY)
                ? _settings.DiagonalMovementMultiplier
                : _settings.StraightMovementMultiplier;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetCellWeightMultiplier(Cell* cell)
        {
            if (cell == null)
                return 0;
                
            return _settings.IsCellWeightEnabled ? cell->Weight : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopulateNeighbors(Cell* cells, Cell* current, int agentSize, Cell** neighbors)
        {
            if (current == null || neighbors == null)
                return;
                
            var position = current->Coordinate;

            // Add cardinal directions first
            neighbors[West] = GetWalkableLocation(cells, position.X - 1, position.Y, agentSize, out var west);
            neighbors[East] = GetWalkableLocation(cells, position.X + 1, position.Y, agentSize, out var east);
            neighbors[South] = GetWalkableLocation(cells, position.X, position.Y + 1, agentSize, out var south);
            neighbors[North] = GetWalkableLocation(cells, position.X, position.Y - 1, agentSize, out var north);

            if (!_settings.IsDiagonalMovementEnabled)
            {
                // Clear diagonal directions if not enabled
                for (var i = DiagonalStart; i < MaxNeighbors; i++)
                {
                    neighbors[i] = null;
                }

                return;
            }

            // Add diagonal directions when the corner rule lets the step pass between its two flanking cells
            PopulateNeighbor(cells, position.X - 1, position.Y + 1, agentSize, neighbors, SouthWest,
                _settings.IsCornerPassable(west, south));
            PopulateNeighbor(cells, position.X - 1, position.Y - 1, agentSize, neighbors, NorthWest,
                _settings.IsCornerPassable(west, north));
            PopulateNeighbor(cells, position.X + 1, position.Y + 1, agentSize, neighbors, SouthEast,
                _settings.IsCornerPassable(east, south));
            PopulateNeighbor(cells, position.X + 1, position.Y - 1, agentSize, neighbors, NorthEast,
                _settings.IsCornerPassable(east, north));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopulateNeighbor(Cell* cells, int x, int y, int agentSize, Cell** neighbors,
            int neighborIndex, bool shouldPopulate = true)
        {
            if (neighbors == null || neighborIndex < 0 || neighborIndex >= MaxNeighbors)
                return;
                
            if (!shouldPopulate)
            {
                neighbors[neighborIndex] = null;
                return;
            }

            neighbors[neighborIndex] = GetWalkableLocation(cells, x, y, agentSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Cell* GetWalkableLocation(Cell* cells, int x, int y, int agentSize)
        {
            return GetWalkableLocation(cells, x, y, agentSize, out _);
        }

        /// <summary>
        /// Returns the cell at (<paramref name="x"/>, <paramref name="y"/>) when an agent of
        /// <paramref name="agentSize"/> can stand there, otherwise <c>null</c>, and reports what blocks the
        /// agent's footprint (the input of the corner-cutting rule).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Cell* GetWalkableLocation(Cell* cells, int x, int y, int agentSize, out CellBlocker blocker)
        {
            blocker = GetFootprintBlocker(cells, x, y, agentSize);

            return blocker == CellBlocker.Open ? GetCell(cells, x, y) : null;
        }

        /// <summary>
        /// Classifies the <paramref name="agentSize"/> x <paramref name="agentSize"/> footprint anchored at
        /// (<paramref name="x"/>, <paramref name="y"/>): <see cref="CellBlocker.Wall"/> when any cell is not
        /// walkable or outside the grid, else <see cref="CellBlocker.Occupied"/> when any cell is occupied (and
        /// occupancy is calculated), else <see cref="CellBlocker.Open"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CellBlocker GetFootprintBlocker(Cell* cells, int x, int y, int agentSize)
        {
            var blocker = GetBlocker(cells, x, y);

            if (agentSize == SingleCellAgentSize || blocker == CellBlocker.Wall)
            {
                return blocker;
            }

            // Check for enough clearance for the agent. A wall anywhere in the footprint wins over occupancy.
            for (var nY = 0; nY < agentSize; nY++)
            {
                for (var nX = 0; nX < agentSize; nX++)
                {
                    var cellBlocker = GetBlocker(cells, x + nX, y + nY);

                    if (cellBlocker == CellBlocker.Wall)
                    {
                        return CellBlocker.Wall;
                    }

                    if (cellBlocker == CellBlocker.Occupied)
                    {
                        blocker = CellBlocker.Occupied;
                    }
                }
            }

            return blocker;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CellBlocker GetBlocker(Cell* cells, int x, int y)
        {
            if (!IsPositionValid(x, y))
            {
                return CellBlocker.Wall;
            }

            var cell = GetCell(cells, x, y);

            if (!cell->IsWalkable)
            {
                return CellBlocker.Wall;
            }

            if (!_query.IsCalculatingOccupiedCells || !cell->IsOccupied)
            {
                return CellBlocker.Open;
            }

            return _query.IsAlwaysEnterable(x, y) ? CellBlocker.Open : CellBlocker.Occupied;
        }

        /// <summary>
        /// Checks whether the search can ever end at <paramref name="to"/>: the destination must pass the same
        /// walkability, occupancy and clearance test the neighbor expansion applies, unless the agent already
        /// stands on it (the origin is never tested, so a search from a cell to itself always succeeds).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDestinationEnterable(Cell* cells, Coordinate from, Coordinate to, int agentSize)
        {
            return from == to || GetWalkableLocation(cells, to.X, to.Y, agentSize) != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsPositionValid(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>
        /// The A* heuristic: the cheapest cost of reaching the destination over an empty grid with the configured
        /// step costs (octile distance with diagonals, Manhattan without, scaled by the multipliers; see
        /// <see cref="FastPathfinderSettings"/>). It never overestimates, so <see cref="GetPath"/> returns a
        /// cheapest route.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetH(int startX, int startY, int destX, int destY)
        {
            var dx = Math.Abs(destX - startX);
            var dy = Math.Abs(destY - startY);

            return dx > dy
                ? _settings.HeuristicMajorAxisCost * dx + _settings.HeuristicMinorAxisCost * dy
                : _settings.HeuristicMajorAxisCost * dy + _settings.HeuristicMinorAxisCost * dx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDiagonalMovement(int startX, int startY, int destX, int destY)
        {
            return startX != destX && startY != destY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                ThrowObjectDisposed();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException(nameof(Pathfinder));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfInvalidAgentSize(int agentSize)
        {
            if (agentSize < SingleCellAgentSize)
            {
                ThrowInvalidAgentSize(agentSize);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInvalidAgentSize(int agentSize)
        {
            throw new ArgumentOutOfRangeException(nameof(agentSize), agentSize, "The agent size must be at least 1.");
        }

        /// <summary>
        /// Releases all resources used by this Pathfinder instance: returns the pooled cell buffer (every
        /// constructor except the <see cref="Cell"/>[] one rents it, and that one for an unordered array), the search-state and open-set buffers, disposes
        /// the path cache and ends an active
        /// <see cref="StepwiseSearch"/>.
        /// </summary>
        /// <remarks>
        /// Idempotent: calling it again does nothing, so the pooled buffer is returned exactly once. An active
        /// <see cref="StepwiseSearch"/> is disposed first (its later ticks throw
        /// <see cref="ObjectDisposedException"/>), so the buffer is never returned while still pinned. Every
        /// instance method throws <see cref="ObjectDisposedException"/> afterwards.
        /// </remarks>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _activeSearch?.Dispose();

            if (_initializationMode != InitializationMode.CellsArray && _cells != null)
            {
                ArrayPool<Cell>.Shared.Return(_cells);
            }

            if (_arraySnapshot != null)
            {
                ArrayPool<Cell>.Shared.Return(_arraySnapshot);
                ArrayPool<int>.Shared.Return(_arraySourceIndexes);
                _arraySnapshot = null;
                _arraySourceIndexes = null;
            }

            _cells = null;

            ArrayPool<PathNode>.Shared.Return(_nodes);
            _nodes = null;

            _openSet.Dispose();
            _keyedOpenSet?.Dispose();

            if (_arrivals != null)
            {
                ArrayPool<byte>.Shared.Return(_arrivals);
                _arrivals = null;
            }

            // Dispose the path caching if it exists
            if (_isPathCachingEnabled)
            {
                _pathCaching?.Dispose();
                _pathCaching = null;
            }
        }

        private enum InitializationMode
        {
            CellsArray,
            CellHoldersArray,
            CellsMatrix,
            CellHoldersMatrix
        }
    }
}