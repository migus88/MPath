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
        private Cell[] _cells;
        private readonly Cell[,] _cellsMatrix;
        
        private bool _isPathCachingEnabled;
        private IPathCaching _pathCaching;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pathfinder"/> class with a pre-existing cell array.
        /// This constructor minimizes allocations by using the provided cell array directly.
        /// </summary>
        /// <param name="cells">Array of Cells. Cannot be null.</param>
        /// <param name="fieldWidth">The width of the field.</param>
        /// <param name="fieldHeight">The height of the field.</param>
        /// <param name="settings">Optional pathfinder settings. If null, default settings will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        public Pathfinder(Cell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _cells = cells ?? throw new ArgumentNullException(nameof(cells));
            _initializationMode = InitializationMode.CellsArray;
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
            _query = QueryRules.FromSettings(_settings);
            _openSet = new UnsafePriorityQueue(settings?.InitialBufferSize);
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

            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }
                
            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Destination is outside the valid field range", nameof(to));
            }
            
            if (TryGetCachedPath(agent, from, to, out var cachedPath))
            {
                return cachedPath;
            }

            InitializeCellsArray();

            var cells = new Span<Cell>(_cells, 0, Size);

            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            {
                if (!IsDestinationEnterable(ptr, from, to, agent.Size))
                {
                    return PathResult.Failure();
                }

                ResetCells(ptr);
                _openSet.Clear();

                var result = CalculatePath(agent, from, to, ptr);
                
                CachePath(agent, from, to, result);
                
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
        private bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult)
        {
            if (IsCachingQuery() && _pathCaching.TryGetCachedPath(agent, from, to, out var cachedPath))
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
        private void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult result)
        {
            if (result.IsSuccess && IsCachingQuery())
            {
                _pathCaching.CachePath(agent, from, to, result.Copy());
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
            var enterableCells = options.AlwaysEnterable;

            fixed (Coordinate* enterable = enterableCells)
            {
                _query = QueryRules.FromOptions(_settings, options.Occupancy, enterable, enterableCells.Length,
                    options.IsDestinationAlwaysEnterable, to);

                try
                {
                    return GetPath(agent, from, to);
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
        private PathResult CalculatePath(IAgent agent, Coordinate from, Coordinate to, Cell* ptr)
        {
            var scoreH = GetH(from.X, from.Y, to.X, to.Y);
            var start = GetCell(ptr, from.X, from.Y);

            _openSet.Enqueue(start, scoreH);

            var neighbors = stackalloc Cell*[MaxNeighbors];
            var agentSize = agent.Size;

            // A* algorithm main loop. Each iteration expands one cell; ExpandNext is the single
            // source of truth for that step, shared with the stepwise (tick-by-tick) search.
            Cell* current = null;
            while (_openSet.Count > 0)
            {
                if (ExpandNext(ptr, to, agentSize, neighbors, out current))
                {
                    // Destination dequeued — search is done.
                    break;
                }
            }

            // Path reconstruction
            if (current == null || current->Coordinate != to)
            {
                return PathResult.Failure();
            }

            return ReconstructPath(current, ptr, agentSize);
        }

        /// <summary>
        /// Expands the next cell from the open set: dequeues the current best candidate and — unless it is the
        /// destination — closes it and relaxes its neighbors. This is one iteration of the A* main loop,
        /// factored out so the batch <see cref="GetPath"/> and the stepwise <see cref="StepwiseSearch"/> run the
        /// exact same logic. The caller is responsible for ensuring the open set is non-empty.
        /// </summary>
        /// <returns><c>true</c> when the dequeued cell is the destination and the search should stop;
        /// otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ExpandNext(Cell* ptr, Coordinate to, int agentSize, Cell** neighbors, out Cell* current)
        {
            current = _openSet.Dequeue();

            if (current->Coordinate == to)
            {
                return true;
            }

            current->IsClosed = true;

            PopulateNeighbors(ptr, current, agentSize, neighbors);
            ProcessNeighbors(current, neighbors, to);

            return false;
        }

        /// <summary>
        /// Processes each neighbor of the current cell in the pathfinding algorithm.
        /// </summary>
        private void ProcessNeighbors(Cell* current, Cell** neighbors, Coordinate to)
        {
            for (var n = 0; n < MaxNeighbors; n++)
            {
                var neighbor = neighbors[n];
                if (neighbor == null || neighbor->IsClosed)
                {
                    continue;
                }

                var scoreG = current->ScoreG + GetStepCost(current, neighbor);

                if (!_openSet.Contains(neighbor))
                {
                    var neighborCoord = neighbor->Coordinate;
                    var scoreH = GetH(neighborCoord.X, neighborCoord.Y, to.X, to.Y);
                    AddNeighborToOpenSet(current, neighbor, scoreG, scoreH);
                }
                else if (scoreG < neighbor->ScoreG)
                {
                    UpdateNeighborInOpenSet(current, neighbor, scoreG);
                }
            }
        }

        /// <summary>
        /// Adds a neighbor to the open set for consideration in the pathfinding algorithm.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNeighborToOpenSet(Cell* current, Cell* neighbor, float scoreG, float scoreH)
        {
            neighbor->ParentCoordinate = current->Coordinate;
            neighbor->Depth = current->Depth + 1;
            neighbor->ScoreG = scoreG;
            neighbor->ScoreH = scoreH;

            var scoreF = scoreG + scoreH;
            _openSet.Enqueue(neighbor, scoreF);
        }

        /// <summary>
        /// Updates a neighbor that is already in the open set if a better path is found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateNeighborInOpenSet(Cell* current, Cell* neighbor, float scoreG)
        {
            neighbor->ScoreG = scoreG;
            neighbor->ParentCoordinate = current->Coordinate;
            neighbor->Depth = current->Depth + 1;

            // A cheaper g lowers f, so the cell must move up the heap (a decrease-key), not just change in place.
            _openSet.UpdatePriority(neighbor, scoreG + neighbor->ScoreH);
        }

        /// <summary>
        /// Reconstructs the final path from the destination cell back to the start.
        /// </summary>
        private PathResult ReconstructPath(Cell* lastCell, Cell* cells, int agentSize)
        {
            var depth = lastCell->Depth;
            var path = ArrayPool<Coordinate>.Shared.Rent(depth);
            Array.Clear(path, 0, path.Length);

            var current = lastCell;
            for (var i = depth - 1; i >= 0; i--)
            {
                path[i] = current->Coordinate;
                var parentCoord = current->ParentCoordinate;
                current = GetCell(cells, parentCoord.X, parentCoord.Y);
            }

            // Apply path smoothing if enabled
            if (_settings.PathSmoothingMethod != PathSmoothingMethod.None && depth > 2)
            {
                return SmoothPath(path, depth, cells, agentSize);
            }

            return PathResult.Success(path, depth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetCells(Cell* cells)
        {
            for (var i = 0; i < Size; i++)
            {
                (cells + i)->Reset();
            }
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
            return x * Height + y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCellsArray()
        {
            TryInitializingCellsArrayFromCellsArray();
            TryInitializingCellsArrayFromCellHolders();
            TryInitializingCellsArrayFromCellsMatrix();
            TryInitializingCellsArrayFromCellHoldersMatrix();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellsArray()
        {
            if (_initializationMode != InitializationMode.CellsArray)
            {
                return;
            }
            
            Array.Sort(_cells, Utils.CellsComparison);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellHolders()
        {
            if (_initializationMode != InitializationMode.CellHoldersArray || _cellHolders == null)
            {
                return;
            }

            foreach (var cellHolder in _cellHolders)
            {
                if (cellHolder == null)
                    continue;
                    
                var coordinate = cellHolder.CellData.Coordinate;
                _cells[coordinate.X * Height + coordinate.Y] = cellHolder.CellData;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellsMatrix()
        {
            if (_initializationMode != InitializationMode.CellsMatrix || _cellsMatrix == null)
            {
                return;
            }
            
            foreach (var cell in _cellsMatrix)
            {
                _cells[cell.Coordinate.X * Height + cell.Coordinate.Y] = cell;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellHoldersMatrix()
        {
            if (_initializationMode != InitializationMode.CellHoldersMatrix || _cellHoldersMatrix == null)
            {
                return;
            }

            foreach (var cellHolder in _cellHoldersMatrix)
            {
                if (cellHolder == null)
                    continue;
                    
                var coordinate = cellHolder.CellData.Coordinate;
                _cells[coordinate.X * Height + coordinate.Y] = cellHolder.CellData;
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

        /// <summary>
        /// Releases all resources used by this Pathfinder instance: returns the pooled cell buffer (every
        /// constructor except the <see cref="Cell"/>[] one rents it), disposes the path cache and ends an active
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

            _cells = null;

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