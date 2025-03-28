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
        private readonly UnsafePriorityQueue _openSet;
        
        private readonly InitializationMode _initializationMode;
        private readonly ICellHolder[] _cellHolders;
        private readonly ICellHolder[,] _cellHoldersMatrix;
        private readonly Cell[] _cells;
        private readonly Cell[,] _cellsMatrix;
        
        private bool _isPathCachingEnabled;
        private IPathCaching _pathCaching;

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
            _openSet = new UnsafePriorityQueue(settings?.InitialBufferSize);
        }
        
        /// <summary>
        /// Enables path caching for the pathfinder.
        /// </summary>
        /// <param name="pathCachingHandler">Optional custom implementation of IPathCaching. If null, the default implementation will be used.</param>
        /// <returns>The current pathfinder instance.</returns>
        public Pathfinder EnablePathCaching(IPathCaching pathCachingHandler = null)
        {
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
        public Pathfinder DisablePathCaching()
        {
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
        public Pathfinder InvalidateCache()
        {
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
        /// <exception cref="ArgumentException">Thrown when the destination is outside the valid field.</exception>
        public PathResult GetPath(IAgent agent, Coordinate from, Coordinate to)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }
                
            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Destination is outside the valid field range", nameof(to));
            }
            
            // Try to get the path from cache if caching is enabled
            if (_isPathCachingEnabled && _pathCaching.TryGetCachedPath(agent, from, to, out var cachedPath))
            {
                return cachedPath;
            }

            InitializeCellsArray();

            var cells = new Span<Cell>(_cells, 0, Size);

            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            {
                ResetCells(ptr);
                _openSet.Clear();

                var result = CalculatePath(agent, from, to, ptr);
                
                // Cache the path if caching is enabled and path was found
                if (_isPathCachingEnabled && result.IsSuccess)
                {
                    _pathCaching.CachePath(agent, from, to, result);
                }
                
                return result;
            }
        }

        /// <summary>
        /// Calculates the path using A* algorithm.
        /// </summary>
        private PathResult CalculatePath(IAgent agent, Coordinate from, Coordinate to, Cell* ptr)
        {
            var scoreH = GetH(from.X, from.Y, to.X, to.Y);
            var current = GetCell(ptr, from.X, from.Y);
            
            _openSet.Enqueue(current, scoreH);

            var neighbors = stackalloc Cell*[MaxNeighbors];
            var agentSize = agent.Size;

            // A* algorithm main loop
            while (_openSet.Count > 0)
            {
                current = _openSet.Dequeue();

                if (current->Coordinate == to)
                {
                    break;
                }

                current->IsClosed = true;

                PopulateNeighbors(ptr, current, agentSize, neighbors);
                ProcessNeighbors(current, neighbors, to);
            }

            // Path reconstruction
            if (current->Coordinate != to)
            {
                return PathResult.Failure();
            }

            return ReconstructPath(current, ptr);
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

                var neighborCoord = neighbor->Coordinate;
                var scoreH = GetH(neighborCoord.X, neighborCoord.Y, to.X, to.Y);
                var neighborWeight = GetCellWeightMultiplier(neighbor);
                
                var neighborTravelWeight = GetNeighborTravelWeightMultiplier(
                    current->Coordinate.X, current->Coordinate.Y,
                    neighborCoord.X, neighborCoord.Y);

                var scoreG = current->ScoreG + (neighborTravelWeight * scoreH) + (neighborWeight * scoreH);

                if (!_openSet.Contains(neighbor))
                {
                    AddNeighborToOpenSet(current, neighbor, scoreG, scoreH);
                }
                else if (scoreG + neighbor->ScoreH < neighbor->ScoreF)
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
            neighbor->ScoreF = scoreG + neighbor->ScoreH;
            neighbor->ParentCoordinate = current->Coordinate;
            neighbor->Depth = current->Depth + 1;
        }

        /// <summary>
        /// Reconstructs the final path from the destination cell back to the start.
        /// </summary>
        private PathResult ReconstructPath(Cell* lastCell, Cell* cells)
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
                return SmoothPath(path, depth, cells);
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
            return cells + (x * Height + y);
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
            PopulateNeighbor(cells, position.X - 1, position.Y, agentSize, neighbors, West);
            PopulateNeighbor(cells, position.X + 1, position.Y, agentSize, neighbors, East);
            PopulateNeighbor(cells, position.X, position.Y + 1, agentSize, neighbors, South);
            PopulateNeighbor(cells, position.X, position.Y - 1, agentSize, neighbors, North);

            if (!_settings.IsDiagonalMovementEnabled)
            {
                // Clear diagonal directions if not enabled
                for (var i = DiagonalStart; i < MaxNeighbors; i++)
                {
                    neighbors[i] = null;
                }

                return;
            }

            // Add diagonal directions depending on settings and walkability
            var canGoWest = neighbors[West] != null;
            var canGoEast = neighbors[East] != null;
            var canGoSouth = neighbors[South] != null;
            var canGoNorth = neighbors[North] != null;
            var isCornersCutAllowed = _settings.IsMovementBetweenCornersEnabled;

            PopulateNeighbor(cells, position.X - 1, position.Y + 1, agentSize, neighbors, SouthWest,
                canGoWest || canGoSouth || isCornersCutAllowed);
            PopulateNeighbor(cells, position.X - 1, position.Y - 1, agentSize, neighbors, NorthWest,
                canGoWest || canGoNorth || isCornersCutAllowed);
            PopulateNeighbor(cells, position.X + 1, position.Y + 1, agentSize, neighbors, SouthEast,
                canGoEast || canGoSouth || isCornersCutAllowed);
            PopulateNeighbor(cells, position.X + 1, position.Y - 1, agentSize, neighbors, NorthEast,
                canGoEast || canGoNorth || isCornersCutAllowed);
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
        private Cell* GetWalkableLocation(Cell* cells, int x, int y)
        {
            if (!IsPositionValid(x, y) || cells == null)
                return null;
                
            var cell = GetCell(cells, x, y);

            return (_settings.IsCalculatingOccupiedCells && cell->IsOccupied) || !cell->IsWalkable
                ? null
                : cell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Cell* GetWalkableLocation(Cell* cells, int x, int y, int agentSize)
        {
            if (!IsPositionValid(x, y) || cells == null)
                return null;

            var location = GetWalkableLocation(cells, x, y);

            if (location == null)
            {
                return null;
            }

            if (agentSize == SingleCellAgentSize)
            {
                return location;
            }

            // Check for enough clearance for the agent
            for (var nY = 0; nY < agentSize; nY++)
            {
                for (var nX = 0; nX < agentSize; nX++)
                {
                    if (!IsPositionValid(x + nX, y + nY))
                        return null;

                    var neighbor = GetWalkableLocation(cells, x + nX, y + nY);

                    if (neighbor == null)
                    {
                        return null;
                    }
                }
            }

            return location;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsPositionValid(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetH(int startX, int startY, int destX, int destY)
        {
            return Math.Abs(destX - startX) + Math.Abs(destY - startY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDiagonalMovement(int startX, int startY, int destX, int destY)
        {
            return startX != destX && startY != destY;
        }

        /// <summary>
        /// Releases all resources used by this Pathfinder instance.
        /// </summary>
        public void Dispose()
        {
            if (_initializationMode != InitializationMode.CellsArray && _cells != null)
            {
                ArrayPool<Cell>.Shared.Return(_cells);
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