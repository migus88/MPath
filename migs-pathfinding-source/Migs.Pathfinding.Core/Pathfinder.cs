using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;
using Migs.Pathfinding.Core.Internal;
using static Migs.Pathfinding.Core.Internal.DirectionIndexes;

namespace Migs.Pathfinding.Core
{
    public sealed unsafe class Pathfinder : IDisposable
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

        /// <summary>
        /// The less allocating version of the Pathfinder. <br/>
        /// With this constructor no collection of cells will be created internally. <br/>
        /// This is the preferred way to use the Pathfinder.
        /// </summary>
        /// <param name="cells">Array of Cells</param>
        /// <param name="fieldWidth">The width of the field</param>
        /// <param name="fieldHeight">The height of the field</param>
        /// <param name="settings">Pathfinder Settings</param>
        public Pathfinder(Cell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _initializationMode = InitializationMode.CellsArray;
            _cells = cells;
        }

        /// <summary>
        /// With this constructor the Pathfinder will borrow a collection of cells from the pool <br/>
        /// and will return it to the pool when disposed. <br/>
        /// Note that the allocation is possible, but as long as the Pathfinder is disposed <br/>
        /// the GC will not have to collect it. In case you want the GC to collect it, <br/>
        /// do not dispose the Pathfinder.
        /// </summary>
        /// <param name="holders">Array of Cell Holders</param>
        /// <param name="fieldWidth">The width of the field</param>
        /// <param name="fieldHeight">The height of the field</param>
        /// <param name="settings">Pathfinder Settings</param>
        public Pathfinder(ICellHolder[] holders, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
            : this(fieldWidth, fieldHeight, settings)
        {
            _cellHolders = holders;
            _cells = ArrayPool<Cell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellHoldersArray;
        }

        /// <summary>
        /// With this constructor the Pathfinder will borrow a collection of cells from the pool <br/>
        /// and will return it to the pool when disposed. <br/>
        /// Note that the allocation is possible, but as long as the Pathfinder is disposed <br/>
        /// the GC will not have to collect it. In case you want the GC to collect it, <br/>
        /// do not dispose the Pathfinder.
        /// </summary>
        /// <param name="cellsMatrix">Matrix of Cells</param>
        /// <param name="settings">Pathfinder Settings</param>
        public Pathfinder(Cell[,] cellsMatrix, IPathfinderSettings settings = null)
            : this(cellsMatrix.GetLength(0), cellsMatrix.GetLength(1), settings)
        {
            _cellsMatrix = cellsMatrix;
            _cells = ArrayPool<Cell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellsMatrix;
        }


        /// <summary>
        /// With this constructor the Pathfinder will borrow a collection of cells from the pool <br/>
        /// and will return it to the pool when disposed. <br/>
        /// Note that the allocation is possible, but as long as the Pathfinder is disposed <br/>
        /// the GC will not have to collect it. In case you want the GC to collect it, <br/>
        /// do not dispose the Pathfinder.
        /// </summary>
        /// <param name="cellHoldersMatrix">Matrix of Cell Holders</param>
        /// <param name="settings">Pathfinder Settings</param>
        public Pathfinder(ICellHolder[,] cellHoldersMatrix, IPathfinderSettings settings = null)
            : this(cellHoldersMatrix.GetLength(0), cellHoldersMatrix.GetLength(1), settings)
        {
            _cellHoldersMatrix = cellHoldersMatrix;
            _cells = ArrayPool<Cell>.Shared.Rent(Size);
            _initializationMode = InitializationMode.CellHoldersMatrix;
        }

        private Pathfinder(int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
        {
            Width = fieldWidth;
            Height = fieldHeight;
            Size = Width * Height;

            _settings = FastPathfinderSettings.FromSettings(settings ?? new PathfinderSettings());
            _openSet = new UnsafePriorityQueue(settings?.InitialBufferSize);
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
        public PathResult GetPath(IAgent agent, Coordinate from, Coordinate to)
        {
            if (!IsPositionValid(to.X, to.Y))
            {
                throw new Exception("Destination is not valid");
            }

            InitializeCellsArray();

            var cells = new Span<Cell>(_cells, 0, Size);

            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            {
                ResetCells(ptr);

                _openSet.Clear();

                var scoreH = GetH(from.X, from.Y, to.X, to.Y);

                var current = GetCell(ptr, from.X, from.Y);

                _openSet.Enqueue(current, scoreH); //ScoreF set by the queue

                var neighbors = stackalloc Cell*[MaxNeighbors];
                var agentSize = agent.Size;

                while (_openSet.Count > 0)
                {
                    current = _openSet.Dequeue();

                    if (current->Coordinate == to)
                    {
                        break;
                    }

                    current->IsClosed = true;

                    PopulateNeighbors(ptr, current, agentSize, neighbors);

                    //foreach (var neighborPtr in neighbors) 
                    for (var n = 0; n < MaxNeighbors; n++)
                    {
                        var neighbor = neighbors[n];
                        if (neighbor == null || neighbor->IsClosed)
                        {
                            continue;
                        }

                        scoreH = GetH(neighbor->Coordinate.X, neighbor->Coordinate.Y, to.X, to.Y);

                        var neighborWeight = GetCellWeightMultiplier(neighbor);

                        var neighborTravelWeight = GetNeighborTravelWeightMultiplier(
                            current->Coordinate.X, current->Coordinate.Y,
                            neighbor->Coordinate.X, neighbor->Coordinate.Y);

                        var scoreG = current->ScoreG + (neighborTravelWeight * scoreH) + (neighborWeight * scoreH);

                        if (!_openSet.Contains(neighbor))
                        {
                            neighbor->ParentCoordinate = current->Coordinate;
                            neighbor->Depth = current->Depth + 1;
                            neighbor->ScoreG = scoreG;
                            neighbor->ScoreH = scoreH;

                            var scoreF = scoreG + scoreH;

                            _openSet.Enqueue(neighbor, scoreF); //ScoreF set by the queue
                        }
                        else if (scoreG + neighbor->ScoreH < neighbor->ScoreF)
                        {
                            neighbor->ScoreG = scoreG;
                            neighbor->ScoreF = scoreG + neighbor->ScoreH;
                            neighbor->ParentCoordinate = current->Coordinate;
                            neighbor->Depth = current->Depth + 1;
                        }
                    }
                }

                if (current->Coordinate != to)
                {
                    return PathResult.Failure();
                }

                var last = current;
                var depth = last->Depth;

                var path = ArrayPool<Coordinate>.Shared.Rent(depth);
                Array.Clear(path, 0, path.Length);

                for (var i = depth - 1; i >= 0; i--)
                {
                    path[i] = last->Coordinate;
                    var parentCoord = last->ParentCoordinate;
                    last = GetCell(ptr, parentCoord.X, parentCoord.Y);
                }

                return PathResult.Success(path, depth);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCellsArray()
        {
            TryInitializingCellsArrayFromCellHolders();
            TryInitializingCellsArrayFromCellsMatrix();
            TryInitializingCellsArrayFromCellHoldersMatrix();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellHolders()
        {
            if (_initializationMode != InitializationMode.CellHoldersArray)
            {
                return;
            }
            
            for (var i = 0; i < Size; i++)
            {
                _cells[i] = _cellHolders[i].CellData;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellsMatrix()
        {
            if (_initializationMode != InitializationMode.CellsMatrix)
            {
                return;
            }
            
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    _cells[x * Height + y] = _cellsMatrix[x, y];
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryInitializingCellsArrayFromCellHoldersMatrix()
        {
            if (_initializationMode != InitializationMode.CellHoldersMatrix)
            {
                return;
            }
            
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    _cells[x * Height + y] = _cellHoldersMatrix[x, y].CellData;
                }
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
            return _settings.IsCellWeightEnabled ? cell->Weight : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PopulateNeighbors(Cell* cells, Cell* current, int agentSize, Cell** neighbors)
        {
            var position = current->Coordinate;

            PopulateNeighbor(cells, position.X - 1, position.Y, agentSize, neighbors, West);
            PopulateNeighbor(cells, position.X + 1, position.Y, agentSize, neighbors, East);
            PopulateNeighbor(cells, position.X, position.Y + 1, agentSize, neighbors, South);
            PopulateNeighbor(cells, position.X, position.Y - 1, agentSize, neighbors, North);

            if (!_settings.IsDiagonalMovementEnabled)
            {
                for (var i = DiagonalStart; i < MaxNeighbors; i++)
                {
                    neighbors[i] = null;
                }

                return;
            }

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
            var cell = GetCell(cells, x, y);

            return (_settings.IsCalculatingOccupiedCells && cell->IsOccupied) || !cell->IsWalkable
                ? null
                : cell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Cell* GetWalkableLocation(Cell* cells, int x, int y, int agentSize)
        {
            if (!IsPositionValid(x, y))
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

            //Clearance calculation
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

        public void Dispose()
        {
            if(_initializationMode != InitializationMode.CellsArray)
            {
                ArrayPool<Cell>.Shared.Return(_cells);
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