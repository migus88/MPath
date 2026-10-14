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
        private readonly PathCell[,] _cellsMatrix;

        // The buffer searches read, laid out by GetLayoutIndex: the caller's array in PathCell[] mode (queries only read
        // it), otherwise a pooled snapshot of the caller's grid. A cell's index is its position.
        private PathCell[] _cells;

        // Whether the snapshot in _cells has been taken and not marked stale by Refresh since.
        private bool _isSnapshotCurrent;

        // The layout of _cells: the flat index of (x, y) is x * _xStride + y * _yStride, row-major or column-major.
        private int _xStride;
        private int _yStride;
        private bool _isRowMajor;

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
        /// (<c>X * fieldHeight + Y</c>, the default) or row-major (<c>Y * fieldWidth + X</c>). A cell's index is its
        /// position. Only the first <c>fieldWidth * fieldHeight</c> elements are read. Cannot be null.
        /// </param>
        /// <param name="fieldWidth">The width of the field.</param>
        /// <param name="fieldHeight">The height of the field.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="cells"/> is shorter than <c>fieldWidth * fieldHeight</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the settings are invalid, for example a <see cref="IPathfinderSettings.CellLayout"/> that is
        /// neither <see cref="CellLayout.ColumnMajor"/> nor <see cref="CellLayout.RowMajor"/>.
        /// </exception>
        /// <remarks>
        /// The array is searched in place, with no copy: a query costs only the cells it searches, and direct edits to
        /// the array are seen by the next query.
        /// </remarks>
        public Pathfinder(PathCell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _cells = cells ?? throw new ArgumentNullException(nameof(cells));
            ThrowIfShorterThanGrid(cells.Length, nameof(cells));
            _initializationMode = InitializationMode.CellsArray;
            SetLayout(_settings.CellLayout);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with cell holders.
        /// This constructor borrows a cell array from the shared array pool and returns it when disposed.
        /// </summary>
        /// <param name="holders">
        /// Array of Cell Holders, laid out as <see cref="IPathfinderSettings.CellLayout"/> says, like the
        /// <see cref="PathCell"/>[] constructor's array: a holder's index is its cell's position. A <c>null</c> holder is a
        /// non-walkable cell. Only the first <c>fieldWidth * fieldHeight</c> elements are read. Cannot be null.
        /// </param>
        /// <param name="fieldWidth">The width of the field.</param>
        /// <param name="fieldHeight">The height of the field.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="holders"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="holders"/> is shorter than <c>fieldWidth * fieldHeight</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the settings are invalid.</exception>
        public Pathfinder(ICellHolder[] holders, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _cellHolders = holders ?? throw new ArgumentNullException(nameof(holders));
            ThrowIfShorterThanGrid(holders.Length, nameof(holders));
            _cells = ArrayPool<PathCell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellHoldersArray;
            SetLayout(_settings.CellLayout);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with a cell matrix.
        /// This constructor borrows a cell array from the shared array pool and returns it when disposed.
        /// </summary>
        /// <param name="cellsMatrix">Matrix of Cells: the cell at (X, Y) is <c>[X, Y]</c>. Cannot be null.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cellsMatrix"/> is null.</exception>
        public Pathfinder(PathCell[,] cellsMatrix, IPathfinderSettings settings = null)
            : this(cellsMatrix?.GetLength(0) ?? throw new ArgumentNullException(nameof(cellsMatrix)), 
                  cellsMatrix.GetLength(1), 
                  settings)
        {
            _cellsMatrix = cellsMatrix;
            _cells = ArrayPool<PathCell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellsMatrix;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with a cell holder matrix.
        /// This constructor borrows a cell array from the shared array pool and returns it when disposed.
        /// </summary>
        /// <param name="cellHoldersMatrix">
        /// Matrix of Cell Holders: the cell at (X, Y) is held by <c>[X, Y]</c>, and a <c>null</c> holder is a
        /// non-walkable cell. Cannot be null.
        /// </param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cellHoldersMatrix"/> is null.</exception>
        public Pathfinder(ICellHolder[,] cellHoldersMatrix, IPathfinderSettings settings = null)
            : this(cellHoldersMatrix?.GetLength(0) ?? throw new ArgumentNullException(nameof(cellHoldersMatrix)), 
                  cellHoldersMatrix.GetLength(1), 
                  settings)
        {
            _cellHoldersMatrix = cellHoldersMatrix;
            _cells = ArrayPool<PathCell>.Shared.Rent(Size);
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
            _settings.Validate(fieldWidth, fieldHeight);
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
        /// <see cref="PathCell"/>[,] matrix or the cell holders again, and the path cache is cleared, as by
        /// <see cref="InvalidateCache"/>.
        /// </summary>
        /// <returns>The current pathfinder instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// Cheap: the copy happens on the next query, once, however many times this is called before it. Needed
        /// after direct edits to the grid when they must be seen: with <see cref="GridSnapshotMode.Manual"/>
        /// (snapshots are not retaken otherwise), and whenever path caching is enabled (cached paths do not see
        /// direct edits). A <see cref="PathCell"/>[] array is searched in place, so without caching its direct edits
        /// need no refresh. Edits through the cell-update methods (<see cref="SetWalkable"/>, ...) are seen at once
        /// and clear the cache themselves.
        /// </remarks>
        public Pathfinder Refresh()
        {
            ThrowIfDisposed();

            _isSnapshotCurrent = false;
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
        /// Calculates a path from the specified starting position to the destination for an agent that occupies an
        /// <paramref name="agentSize"/> x <paramref name="agentSize"/> square of cells.
        /// </summary>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="agentSize">
        /// The agent's square size in cells (anchored at its top-left cell). Defaults to a single-cell agent.
        /// </param>
        /// <returns>A <see cref="PathResult"/> containing the calculated path or a failure result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin or the destination is outside the valid field.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// A destination the agent cannot enter (not walkable, occupied while
        /// <see cref="IPathfinderSettings.AreOccupiedCellsBlocking"/> is enabled, or without clearance for the
        /// agent's size) fails immediately without searching, unless it is the origin itself.
        /// </remarks>
        public PathResult GetPath(Coordinate from, Coordinate to, int agentSize = SingleCellAgentSize)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            return FindPath(agentSize, from, to);
        }

        /// <summary>
        /// Calculates a path like <see cref="GetPath(Coordinate, Coordinate, int)"/>, with movement rules
        /// overridden for this query only by <paramref name="options"/>. The grid and the settings are not modified.
        /// </summary>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="agentSize">The agent's square size in cells; pass 1 for a single-cell agent.</param>
        /// <param name="options">
        /// The per-query rules. <c>default</c> behaves exactly like the overload without options. With
        /// <see cref="QueryOptions.IsDestinationAlwaysEnterable"/>, a path to an occupied destination can succeed.
        /// </param>
        /// <returns>A <see cref="PathResult"/> containing the calculated path or a failure result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="agentSize"/> is less than 1.</exception>
        /// <exception cref="ArgumentException">Thrown when the origin or the destination is outside the valid field.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        /// <remarks>
        /// When the options change the rules, path caching is bypassed: the cache is neither read nor filled.
        /// </remarks>
        public PathResult GetPath(Coordinate from, Coordinate to, int agentSize, QueryOptions options)
        {
            ThrowIfDisposed();
            ThrowIfInvalidAgentSize(agentSize);

            var enterableCells = options.AlwaysEnterable;

            fixed (Coordinate* enterable = enterableCells)
            {
                _query = QueryRules.FromOptions(_settings, options.Occupancy, enterable, enterableCells.Length,
                    options.IsDestinationAlwaysEnterable, to);

                try
                {
                    return FindPath(agentSize, from, to);
                }
                finally
                {
                    _query = QueryRules.FromSettings(_settings);
                }
            }
        }

        /// <summary>
        /// The shared body of the <c>GetPath</c> overloads, run under the rules currently in <see cref="_query"/>.
        /// </summary>
        private PathResult FindPath(int agentSize, Coordinate from, Coordinate to)
        {
            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }
                
            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Destination is outside the valid field range", nameof(to));
            }
            
            if (TryGetCachedPath(agentSize, from, to, out var cachedPath))
            {
                return cachedPath;
            }

            InitializeCellsArray();

            var cells = new Span<PathCell>(_cells, 0, Size);

            fixed (PathCell* ptr = &MemoryMarshal.GetReference(cells))
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
                
                CachePath(agentSize, from, to, result);
                
                return result;
            }
        }

        /// <summary>
        /// Looks the query up in the path cache. A hit is returned as a fresh copy owned by the caller, so the
        /// instance the cache keeps is never handed out and disposing a result never touches the cache.
        /// </summary>
        /// <remarks>
        /// Every cache read goes through here and every write through <see cref="CachePath"/>; a query that must not
        /// share entries with plain <c>(agentSize, from, to)</c> queries (for example one with non-default per-query
        /// options) skips both.
        /// </remarks>
        private bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult)
        {
            if (IsCachingQuery() && _pathCaching.TryGetCachedPath(agentSize, from, to, out var cachedPath))
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
        private void CachePath(int agentSize, Coordinate from, Coordinate to, PathResult result)
        {
            if (result.IsSuccess && IsCachingQuery())
            {
                _pathCaching.CachePath(agentSize, from, to, result.Copy());
            }
        }

        /// <summary>
        /// Whether the running query may use the path cache: caching is enabled and the query runs under the settings'
        /// own rules. Cached paths are keyed by <c>(agentSize, from, to)</c> only, so a query whose
        /// <see cref="QueryOptions"/> change the rules neither reads nor fills the cache.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCachingQuery()
        {
            return _isPathCachingEnabled && _query.IsDefault(_settings);
        }

        /// <summary>
        /// Calculates the path using A* algorithm.
        /// </summary>
        private PathResult CalculatePath(int agentSize, Coordinate from, Coordinate to, PathCell* cells, PathNode* nodes)
        {
            OpenNode(nodes, GetLayoutIndex(from.X, from.Y), PathNode.NoParent, 0, 0,
                GetH(from.X, from.Y, to.X, to.Y));

            var toIndex = GetLayoutIndex(to.X, to.Y);
            var neighbors = stackalloc PathCell*[MaxNeighbors];

            // A* algorithm main loop. Each iteration expands one cell; ExpandNextPlain (behind ExpandNext) is the
            // single source of truth for that step, shared with the stepwise (tick-by-tick) search.
            while (_openSet.Count > 0)
            {
                if (ExpandNextPlain(cells, nodes, to, toIndex, agentSize, neighbors, out var current))
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
        private void OpenNode(PathNode* nodes, int index, int parentIndex, int depth, int scoreG, int scoreH)
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
        /// <param name="to">The destination, for the heuristic.</param>
        /// <param name="toIndex">The destination's index (<see cref="GetLayoutIndex"/> of <paramref name="to"/>).</param>
        /// <param name="current">The index of the dequeued cell.</param>
        /// <returns><c>true</c> when the dequeued cell is the destination and the search should stop;
        /// otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExpandNext(PathCell* cells, PathNode* nodes, Coordinate to, int toIndex, int agentSize,
            PathCell** neighbors, out int current)
        {
            return _settings.IsTieBreaking
                ? ExpandNextKeyed(cells, nodes, to, toIndex, agentSize, neighbors, out current)
                : ExpandNextPlain(cells, nodes, to, toIndex, agentSize, neighbors, out current);
        }

        /// <summary>
        /// One expansion of the plain search (<see cref="TieBreaking.None"/>); <see cref="CalculatePath"/> calls it
        /// directly so its loop has no mode check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExpandNextPlain(PathCell* cells, PathNode* nodes, Coordinate to, int toIndex, int agentSize,
            PathCell** neighbors, out int current)
        {
            current = _openSet.Dequeue();

            if (current == toIndex)
            {
                return true;
            }

            nodes[current].QueueIndex = PathNode.ClosedQueueIndex;

            GetPosition(current, out var x, out var y);
            PopulateNeighbors(cells, x, y, agentSize, neighbors);
            ProcessNeighbors(cells, nodes, current, x, y, neighbors, to);

            return false;
        }

        /// <summary>
        /// Processes each neighbor of the current cell in the pathfinding algorithm. A node this search has not
        /// touched yet is opened; a touched one is either closed (skipped) or open (decrease-key when cheaper).
        /// A step whose route would cost more than <see cref="FastPathfinderSettings.MaxCost"/> is not taken.
        /// (<paramref name="x"/>, <paramref name="y"/>) is the position of <paramref name="current"/>.
        /// </summary>
        private void ProcessNeighbors(PathCell* cells, PathNode* nodes, int current, int x, int y, PathCell** neighbors,
            Coordinate to)
        {
            var currentNode = nodes + current;
            var currentG = currentNode->ScoreG;
            var headroom = FastPathfinderSettings.MaxCost - currentG;
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

                var scoreG = currentG + stepCost;

                if (!isTouched)
                {
                    OpenNode(nodes, index, current, currentNode->Depth + 1, scoreG,
                        GetH(x + GetOffsetX(n), y + GetOffsetY(n), to.X, to.Y));
                    continue;
                }

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
        private PathResult ReconstructPath(int lastIndex, PathCell* cells, PathNode* nodes, int agentSize)
        {
            var depth = nodes[lastIndex].Depth;
            var path = ArrayPool<Coordinate>.Shared.Rent(depth);
            Array.Clear(path, 0, path.Length);

            var diagonalSteps = 0;

            var index = lastIndex;
            var coordinate = GetCoordinate(index);
            for (var i = depth - 1; i >= 0; i--)
            {
                path[i] = coordinate;
                index = nodes[index].ParentIndex;

                var parent = GetCoordinate(index);
                if (IsDiagonalMovement(parent.X, parent.Y, coordinate.X, coordinate.Y))
                {
                    diagonalSteps++;
                }

                coordinate = parent;
            }

            return CreatePathResult(path, depth, diagonalSteps, cells, agentSize);
        }

        /// <summary>
        /// Wraps a reconstructed route (origin excluded) into the result, smoothing it when enabled. The step counts
        /// always describe the route the search found, before smoothing.
        /// </summary>
        private PathResult CreatePathResult(Coordinate[] path, int steps, int diagonalSteps, PathCell* cells,
            int agentSize)
        {
            // Apply path smoothing if enabled
            var result = _settings.PathSmoothingMethod != PathSmoothingMethod.None && steps > 2
                ? SmoothPath(path, steps, cells, agentSize)
                : PathResult.Create(path, steps);

            return result.WithStepCounts(steps, diagonalSteps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PathCell* GetCell(PathCell* cells, int x, int y)
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
        /// The position of the cell at <paramref name="index"/> in <see cref="_cells"/>: the inverse of
        /// <see cref="GetLayoutIndex"/>. One division.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetPosition(int index, out int x, out int y)
        {
            if (_isRowMajor)
            {
                y = index / Width;
                x = index - y * Width;
            }
            else
            {
                x = index / Height;
                y = index - x * Height;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Coordinate GetCoordinate(int index)
        {
            GetPosition(index, out var x, out var y);
            return new Coordinate(x, y);
        }

        /// <summary>
        /// Sets the layout of <see cref="_cells"/>: row-major for <see cref="CellLayout.RowMajor"/>, otherwise
        /// column-major (the layout of matrix snapshots).
        /// </summary>
        private void SetLayout(CellLayout layout)
        {
            _isRowMajor = layout == CellLayout.RowMajor;

            if (_isRowMajor)
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

        private void ThrowIfShorterThanGrid(int length, string paramName)
        {
            if (length < Size)
            {
                throw new ArgumentException(
                    $"The array holds {length} cells, fewer than the {Size} of a {Width}x{Height} field.", paramName);
            }
        }

        /// <summary>
        /// Brings <see cref="_cells"/> up to date for a query: copies the caller's grid into the snapshot when the
        /// snapshot mode asks for it. A <see cref="PathCell"/>[] is searched in place and needs nothing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCellsArray()
        {
            if (_initializationMode == InitializationMode.CellsArray)
            {
                return;
            }

            if (_isSnapshotCurrent && _settings.GridSnapshotMode == GridSnapshotMode.Manual)
            {
                return;
            }

            TakeSnapshot();
            _isSnapshotCurrent = true;
        }

        /// <summary>
        /// Copies the caller's grid into <see cref="_cells"/> slot by slot: every grid cell is written, and a
        /// <c>null</c> holder becomes a non-walkable cell, so nothing a pooled buffer held before survives.
        /// </summary>
        private void TakeSnapshot()
        {
            switch (_initializationMode)
            {
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

        /// <summary>
        /// The holder array and the snapshot share the declared layout, so holder <c>i</c> fills cell <c>i</c>.
        /// </summary>
        private void CopyCellHolders()
        {
            var holders = _cellHolders;
            var cells = _cells;

            for (var i = 0; i < Size; i++)
            {
                var holder = holders[i];
                cells[i] = holder == null ? default : holder.CellData;
            }
        }

        /// <summary>
        /// A <c>T[,]</c> is stored with its last index varying fastest, so <c>[x, y]</c> sits at
        /// <c>x * Height + y</c>: the column-major layout of the snapshot. One block copy.
        /// </summary>
        private void CopyCellsMatrix()
        {
            fixed (PathCell* source = _cellsMatrix)
            {
                new ReadOnlySpan<PathCell>(source, Size).CopyTo(_cells);
            }
        }

        private void CopyCellHoldersMatrix()
        {
            var holders = _cellHoldersMatrix;
            var cells = _cells;
            var index = 0;

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++, index++)
                {
                    var holder = holders[x, y];
                    cells[index] = holder == null ? default : holder.CellData;
                }
            }
        }

        /// <summary>
        /// The cost of a step in <paramref name="direction"/> (a <see cref="DirectionIndexes"/> slot) into
        /// <paramref name="neighbor"/>: the straight or diagonal step cost plus the entered cell's
        /// <see cref="PathCell.Weight"/> (when cell weighting is enabled). The single step-cost function of both searches
        /// and of <see cref="GetPath"/> and <see cref="GetReachable"/>, so a route's cost means the same everywhere.
        /// The weight is added, not multiplied: <see cref="PathCell.Weight"/> defaults to 0, and a multiplier would make
        /// every step free. A negative weight counts as 0 and one above <see cref="FastPathfinderSettings.MaxCost"/>
        /// as that, so the sum never overflows (a step cost is at most half of it after
        /// <see cref="FastPathfinderSettings.Validate"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStepCost(int direction, PathCell* neighbor)
        {
            var travel = direction >= DiagonalStart ? _settings.DiagonalStepCost : _settings.StraightStepCost;

            return _settings.IsCellWeightEnabled
                ? travel + Math.Min(Math.Max(neighbor->Weight, 0), FastPathfinderSettings.MaxCost)
                : travel;
        }

        /// <summary>
        /// Fills <paramref name="neighbors"/> (one slot per <see cref="DirectionIndexes"/> direction) with the cells
        /// an agent standing at (<paramref name="x"/>, <paramref name="y"/>) can step into, <c>null</c> elsewhere.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopulateNeighbors(PathCell* cells, int x, int y, int agentSize, PathCell** neighbors)
        {
            // Add cardinal directions first
            neighbors[West] = GetWalkableLocation(cells, x - 1, y, agentSize, out var west);
            neighbors[East] = GetWalkableLocation(cells, x + 1, y, agentSize, out var east);
            neighbors[South] = GetWalkableLocation(cells, x, y + 1, agentSize, out var south);
            neighbors[North] = GetWalkableLocation(cells, x, y - 1, agentSize, out var north);

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
            PopulateNeighbor(cells, x - 1, y + 1, agentSize, neighbors, SouthWest,
                _settings.IsCornerPassable(west, south));
            PopulateNeighbor(cells, x - 1, y - 1, agentSize, neighbors, NorthWest,
                _settings.IsCornerPassable(west, north));
            PopulateNeighbor(cells, x + 1, y + 1, agentSize, neighbors, SouthEast,
                _settings.IsCornerPassable(east, south));
            PopulateNeighbor(cells, x + 1, y - 1, agentSize, neighbors, NorthEast,
                _settings.IsCornerPassable(east, north));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopulateNeighbor(PathCell* cells, int x, int y, int agentSize, PathCell** neighbors,
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
        private PathCell* GetWalkableLocation(PathCell* cells, int x, int y, int agentSize)
        {
            return GetWalkableLocation(cells, x, y, agentSize, out _);
        }

        /// <summary>
        /// Returns the cell at (<paramref name="x"/>, <paramref name="y"/>) when an agent of
        /// <paramref name="agentSize"/> can stand there, otherwise <c>null</c>, and reports what blocks the
        /// agent's footprint (the input of the corner-cutting rule).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PathCell* GetWalkableLocation(PathCell* cells, int x, int y, int agentSize, out CellBlocker blocker)
        {
            blocker = GetFootprintBlocker(cells, x, y, agentSize);

            return blocker == CellBlocker.Open ? GetCell(cells, x, y) : null;
        }

        /// <summary>
        /// Classifies the <paramref name="agentSize"/> x <paramref name="agentSize"/> footprint anchored at
        /// (<paramref name="x"/>, <paramref name="y"/>): <see cref="CellBlocker.Wall"/> when any cell is not
        /// walkable or outside the grid, else <see cref="CellBlocker.Occupied"/> when any cell is occupied (and
        /// occupied cells block), else <see cref="CellBlocker.Open"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CellBlocker GetFootprintBlocker(PathCell* cells, int x, int y, int agentSize)
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
        private CellBlocker GetBlocker(PathCell* cells, int x, int y)
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

            if (!_query.AreOccupiedCellsBlocking || !cell->IsOccupied)
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
        private bool IsDestinationEnterable(PathCell* cells, Coordinate from, Coordinate to, int agentSize)
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
        /// step costs (octile distance with diagonals, Manhattan without, scaled by the step costs; see
        /// <see cref="FastPathfinderSettings"/>). It never overestimates, so <see cref="GetPath"/> returns a
        /// cheapest route.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetH(int startX, int startY, int destX, int destY)
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
        /// constructor except the <see cref="PathCell"/>[] one rents it), the search-state and open-set buffers, disposes
        /// the path cache and ends an active <see cref="StepwiseSearch"/>.
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
                ArrayPool<PathCell>.Shared.Return(_cells);
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