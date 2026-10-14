using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Core.Internal;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        private const float RadToDeg = 57.2958f; // 180/π ≈ 57.2958

        private PathResult SmoothPath(Coordinate[] originalPath, int originalLength, Cell* cells, int agentSize)
        {
            if (originalLength <= 2)
            {
                // Path is already optimal (just start and end points)
                return PathResult.Create(originalPath, originalLength);
            }

            return _settings.PathSmoothingMethod switch
            {
                PathSmoothingMethod.StringPulling => ApplyStringPulling(originalPath, originalLength, cells, agentSize),
                PathSmoothingMethod.Simple => ApplySimpleSmoothing(originalPath, originalLength),
                PathSmoothingMethod.None => PathResult.Create(originalPath, originalLength),
                _ => PathResult.Create(originalPath, originalLength)
            };
        }

        private PathResult ApplyStringPulling(Coordinate[] originalPath, int originalLength, Cell* cells, int agentSize)
        {
            var smoothedPath = ArrayPool<Coordinate>.Shared.Rent(originalLength);
            Array.Clear(smoothedPath, 0, smoothedPath.Length);

            smoothedPath[0] = originalPath[0];
            var smoothedLength = 1;
            var currentIndex = 0;

            while (currentIndex < originalLength - 1)
            {
                var furthestVisible = currentIndex;

                // Find the furthest point the agent can walk to in a straight line from the current one
                for (var i = originalLength - 1; i > currentIndex; i--)
                {
                    // A shortcut must stay inside the space the search itself could traverse:
                    // agent footprint at every cell and the corner rule at every diagonal step.
                    if (!IsSegmentTraversable(originalPath[currentIndex], originalPath[i], cells, agentSize))
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
            return PathResult.Create(smoothedPath, smoothedLength);
        }

        private PathResult ApplySimpleSmoothing(Coordinate[] originalPath, int originalLength)
        {
            var smoothedPath = ArrayPool<Coordinate>.Shared.Rent(originalLength);
            Array.Clear(smoothedPath, 0, smoothedPath.Length);

            smoothedPath[0] = originalPath[0];
            var smoothedLength = 1;

            // Start from the first segment's direction so the first waypoint is never written twice
            var direction = originalPath[1] - originalPath[0];

            // Keep every point where the direction changes (the turn points)
            for (var i = 2; i < originalLength; i++)
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
            
            return PathResult.Create(smoothedPath, smoothedLength);
        }

        /// <summary>
        /// Determines whether an agent of <paramref name="agentSize"/> can walk the straight segment from
        /// <paramref name="from"/> to <paramref name="to"/>. The segment is traced as an 8-connected Bresenham walk
        /// and every step is validated with the same rules the A* search applies to a move: the agent's
        /// footprint must fit at the entered cell, and a diagonal step must obey the corner rule.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSegmentTraversable(Coordinate from, Coordinate to, Cell* cells, int agentSize)
        {
            var x = from.X;
            var y = from.Y;
            var dx = Math.Abs(to.X - x);
            var dy = -Math.Abs(to.Y - y);
            var stepX = x < to.X ? 1 : -1;
            var stepY = y < to.Y ? 1 : -1;
            var error = dx + dy;

            while (x != to.X || y != to.Y)
            {
                var doubledError = 2 * error;
                var nextX = x;
                var nextY = y;

                if (doubledError >= dy)
                {
                    error += dy;
                    nextX += stepX;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    nextY += stepY;
                }

                if (nextX != x && nextY != y && !IsDiagonalStepAllowed(cells, x, y, nextX, nextY, agentSize))
                {
                    return false;
                }

                if (GetWalkableLocation(cells, nextX, nextY, agentSize) == null)
                {
                    return false;
                }

                x = nextX;
                y = nextY;
            }

            return true;
        }

        /// <summary>
        /// Mirrors the diagonal rule of <see cref="PopulateNeighbors"/>: the step must pass the
        /// <see cref="IPathfinderSettings.CornerBlocking"/> rule for its two flanking footprints.
        /// When diagonal movement is disabled the step always needs a cardinal detour, since the search
        /// could only have made it as two cardinal moves.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDiagonalStepAllowed(Cell* cells, int x, int y, int nextX, int nextY, int agentSize)
        {
            var first = GetFootprintBlocker(cells, nextX, y, agentSize);
            var second = GetFootprintBlocker(cells, x, nextY, agentSize);

            if (!_settings.IsDiagonalMovementEnabled)
            {
                return first == CellBlocker.Open || second == CellBlocker.Open;
            }

            return _settings.IsCornerPassable(first, second);
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
        /// occupied cell blocks when <see cref="IPathfinderSettings.AreOccupiedCellsBlocking"/> is enabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLineOfSightBlocked(Cell* cells, int x, int y, LineOfSightMode mode)
        {
            var cell = GetCell(cells, x, y);

            if (mode == LineOfSightMode.BlockedByUnwalkableCells && !cell->IsWalkable)
            {
                return true;
            }

            return _settings.AreOccupiedCellsBlocking && cell->IsOccupied;
        }
    }
}