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
    public sealed unsafe class Pathfinder
    {
        private const int MaxNeighbors = 8;
        private const int SingleCellAgentSize = 1;

        private readonly FastPathfinderSettings _settings;
        private readonly UnsafePriorityQueue _openSet;
        private readonly ICellHolder[] _cellHolders;
        private readonly Cell[] _cells;
        private readonly Cell[,] _cellMatrix;
        private readonly int _width;
        private readonly int _height;
        private readonly int _size;

        public Pathfinder(Cell[,] cells, IPathfinderSettings settings = null) 
            : this(default(ICellHolder[]), cells.GetLength(0), cells.GetLength(1), settings)
        {
            _cellMatrix = cells;
        }
        
        public Pathfinder(Cell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null) 
            : this(default(ICellHolder[]), fieldWidth, fieldHeight, settings)
        {
            _cells = cells;
        }
        
        public Pathfinder(ICellHolder[] cellHolders, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
        {
            if(cellHolders != null)
            {
                _cellHolders = cellHolders;
                Array.Sort(_cellHolders, Utils.FieldCellComparison);
            }
            
            _width = fieldWidth;
            _height = fieldHeight;
            _size = _width * _height;
            
            if(cellHolders != null && cellHolders.Length != _size)
            {
                throw new Exception("Invalid cell holders length");
            }
            
            _settings = FastPathfinderSettings.FromSettings(settings ?? new PathfinderSettings());
            
            _openSet = new UnsafePriorityQueue(settings?.InitialBufferSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetCells(Cell* cells)
        {
            for (var i = 0; i < _size; i++)
            {
                (cells + i)->Reset();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<Cell> GetCells()
        {
            if(_cells != null)
            {
                return _cells;
            }
            if(_cellMatrix != null)
            {
                return _cellMatrix.ToSpan();
            }
            
            Span<Cell> cells = stackalloc Cell[_size];
            for (var i = 0; i < _cellHolders.Length; i++)
            {
                cells[i] = _cellHolders[i].Cell;
            }

            return cells;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Cell* GetCell(Cell* cells, int x, int y)
        {
            return cells + (x * _height + y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PathResult GetPath(IAgent agent, Coordinate from, Coordinate to)
        {
            if (!IsPositionValid(to.X, to.Y))
                throw new Exception("Destination is not valid");
            
            var cells = GetCells();
            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            {
                ResetCells(ptr);

                _openSet.Clear();

                var scoreH = GetH(from.X, from.Y, to.X, to.Y);

                var current = GetCell(ptr, from.X, from.Y);
                
                _openSet.Enqueue(current, scoreH); //ScoreF set by the queue

                var neighbors = new Cell*[MaxNeighbors];
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

                    foreach (var neighborPtr in neighbors)
                    {
                        var neighbor = neighborPtr;
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

                var result = new PathResult();

                if (current->Coordinate != to)
                {
                    return result;
                }

                var last = current;
                var depth = last->Depth;

                // Rent an array from the pool
                var pathArray = ArrayPool<Coordinate>.Shared.Rent(depth);
                Span<Coordinate> path = pathArray;

                for (var i = depth - 1; i >= 0; i--)
                {
                    path[i] = last->Coordinate;
                    var parentCoord = last->ParentCoordinate;
                    last = GetCell(ptr, parentCoord.X, parentCoord.Y);
                }

                // Construct the result using the span
                result.Path = path[..depth];

                // Return the array to the pool
                ArrayPool<Coordinate>.Shared.Return(pathArray);

                return result;
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
        private void PopulateNeighbors(Cell* cells, Cell* current, int agentSize, Cell*[] neighbors)
        {
            var position = current->Coordinate;

            PopulateNeighbor(cells, position.X - 1, position.Y, agentSize, neighbors, West);
            PopulateNeighbor(cells, position.X + 1, position.Y, agentSize, neighbors, East);
            PopulateNeighbor(cells, position.X, position.Y + 1, agentSize, neighbors, South);
            PopulateNeighbor(cells, position.X, position.Y - 1, agentSize, neighbors, North);

            if (!_settings.IsDiagonalMovementEnabled)
            {
                for (var i = DiagonalStart; i < neighbors.Length; i++)
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
        private void PopulateNeighbor(Cell* cells, int x, int y, int agentSize, Cell*[] neighbors,
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
            return x >= 0 && x < _width && y >= 0 && y < _height;
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
    }
}