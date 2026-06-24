using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        private const float RadToDeg = 57.2958f; // 180/π ≈ 57.2958

        private PathResult SmoothPath(Coordinate[] originalPath, int originalLength, Cell* cells)
        {
            if (originalLength <= 2)
            {
                // Path is already optimal (just start and end points)
                return PathResult.Success(originalPath, originalLength);
            }

            return _settings.PathSmoothingMethod switch
            {
                PathSmoothingMethod.StringPulling => ApplyStringPulling(originalPath, originalLength, cells),
                PathSmoothingMethod.Simple => ApplySimpleSmoothing(originalPath, originalLength),
                PathSmoothingMethod.None => PathResult.Success(originalPath, originalLength),
                _ => PathResult.Success(originalPath, originalLength)
            };
        }

        private PathResult ApplyStringPulling(Coordinate[] originalPath, int originalLength, Cell* cells)
        {
            var smoothedPath = ArrayPool<Coordinate>.Shared.Rent(originalLength);
            Array.Clear(smoothedPath, 0, smoothedPath.Length);

            smoothedPath[0] = originalPath[0];
            var smoothedLength = 1;
            var currentIndex = 0;

            while (currentIndex < originalLength - 1)
            {
                var furthestVisible = currentIndex;

                // Find furthest point with line of sight from current
                for (var i = originalLength - 1; i > currentIndex; i--)
                {
                    // Smoothing may only shortcut across cells an agent can actually walk through.
                    if (!HasLineOfSight(originalPath[currentIndex], originalPath[i], cells,
                            LineOfSightMode.BlockedByUnwalkableCells))
                    {
                        continue;
                    }

                    furthestVisible = i;
                    break;
                }

                // If we can't see any further than the next point, just add the next point
                if (furthestVisible == currentIndex)
                {
                    furthestVisible = currentIndex + 1;
                }

                // Add the furthest visible point to the smoothed path
                smoothedPath[smoothedLength++] = originalPath[furthestVisible];
                currentIndex = furthestVisible;
            }

            ArrayPool<Coordinate>.Shared.Return(originalPath);
            return PathResult.Success(smoothedPath, smoothedLength);
        }

        private PathResult ApplySimpleSmoothing(Coordinate[] originalPath, int originalLength)
        {
            var smoothedPath = ArrayPool<Coordinate>.Shared.Rent(originalLength);
            Array.Clear(smoothedPath, 0, smoothedPath.Length);

            smoothedPath[0] = originalPath[0];
            var smoothedLength = 1;

            var direction = Coordinate.Zero;

            // Process all points except the last one
            for (var i = 1; i < originalLength - 1; i++)
            {
                var newDirection = originalPath[i] - originalPath[i - 1];

                if (direction == newDirection)
                {
                    continue;
                }

                smoothedPath[smoothedLength] = originalPath[i - 1];
                smoothedLength++;
                direction = newDirection;
            }

            smoothedPath[smoothedLength] = originalPath[originalLength - 1];
            smoothedLength++;

            ArrayPool<Coordinate>.Shared.Return(originalPath);
            
            return PathResult.Success(smoothedPath, smoothedLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasLineOfSight(Coordinate from, Coordinate to, Cell* cells, LineOfSightMode mode)
        {
            (int X, int Y) p0 = new Coordinate(from.X, from.Y);
            (int X, int Y) p1 = new Coordinate(to.X, to.Y);

            var steep = Math.Abs(p1.Y - p0.Y) > Math.Abs(p1.X - p0.X);
            
            if (steep)
            {
                // Swap x and y
                (p0.X, p0.Y) = (p0.Y, p0.X);
                (p1.X, p1.Y) = (p1.Y, p1.X);
            }

            if (p0.X > p1.X)
            {
                // Swap start and end
                (p0.X, p1.X) = (p1.X, p0.X);
                (p0.Y, p1.Y) = (p1.Y, p0.Y);
            }

            var dx = p1.X - p0.X;
            var dy = Math.Abs(p1.Y - p0.Y);
            var error = dx / 2;
            var yStep = p0.Y < p1.Y ? 1 : -1;
            var y = p0.Y;

            for (var x = p0.X; x <= p1.X; x++)
            {
                var checkX = steep ? y : x;
                var checkY = steep ? x : y;

                var currentCoord = new Coordinate(checkX, checkY);

                // Skip checking the endpoints
                if (currentCoord != from && currentCoord != to)
                {
                    if (IsLineOfSightBlocked(cells, checkX, checkY, mode))
                    {
                        // Hit an obstacle
                        return false;
                    }
                }

                error -= dy;
                
                if (error >= 0)
                {
                    continue;
                }
                
                y += yStep;
                error += dx;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the cell at (<paramref name="x"/>, <paramref name="y"/>) interrupts line of
        /// sight. Under <see cref="LineOfSightMode.BlockedByUnwalkableCells"/> a non-walkable cell blocks;
        /// under <see cref="LineOfSightMode.IgnoreUnwalkableCells"/> walkability is ignored. In both modes an
        /// occupied cell blocks when <see cref="IPathfinderSettings.IsCalculatingOccupiedCells"/> is enabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLineOfSightBlocked(Cell* cells, int x, int y, LineOfSightMode mode)
        {
            var cell = GetCell(cells, x, y);

            if (mode == LineOfSightMode.BlockedByUnwalkableCells && !cell->IsWalkable)
            {
                return true;
            }

            return _settings.IsCalculatingOccupiedCells && cell->IsOccupied;
        }
    }
}