using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        /// <summary>
        /// Calculates the Manhattan (taxicab) distance between two coordinates: the number of cardinal
        /// (horizontal/vertical) steps separating them, <c>|dx| + |dy|</c>. This is the metric used by the
        /// A* heuristic and matches the cost model when diagonal movement is disabled. It is a pure grid
        /// measurement and ignores walls, weights and movement settings.
        /// </summary>
        /// <param name="from">The first coordinate.</param>
        /// <param name="to">The second coordinate.</param>
        /// <returns>The Manhattan distance between <paramref name="from"/> and <paramref name="to"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetManhattanDistance(Coordinate from, Coordinate to)
        {
            return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        }

        /// <summary>
        /// Calculates the Chebyshev (chessboard) distance between two coordinates: the number of steps
        /// separating them when diagonal moves are allowed and cost the same as cardinal moves,
        /// <c>max(|dx|, |dy|)</c>. It is a pure grid measurement and ignores walls, weights and movement
        /// settings.
        /// </summary>
        /// <param name="from">The first coordinate.</param>
        /// <param name="to">The second coordinate.</param>
        /// <returns>The Chebyshev distance between <paramref name="from"/> and <paramref name="to"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetChebyshevDistance(Coordinate from, Coordinate to)
        {
            return Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        }

        /// <summary>
        /// Determines whether there is an unobstructed straight line between two cells, i.e. whether every
        /// cell the line passes through (excluding the two endpoints) is transparent. The line is traced
        /// with a Bresenham walk, so this is an O(distance) query that allocates nothing.
        /// By default a cell blocks line of sight when it is not walkable; pass
        /// <see cref="LineOfSightMode.IgnoreUnwalkableCells"/> to see through obstacles that block movement
        /// but not vision. Independently of the mode, an occupied cell blocks sight when
        /// <see cref="IPathfinderSettings.IsCalculatingOccupiedCells"/> is enabled.
        /// </summary>
        /// <remarks>
        /// The endpoints themselves are never tested, so a target standing on a blocked or occupied cell can
        /// still be "seen". Agent size is not considered — the check traces a single-cell ray. A coordinate
        /// always has line of sight to itself.
        /// </remarks>
        /// <param name="from">The observer coordinate. Must be inside the grid.</param>
        /// <param name="to">The target coordinate. Must be inside the grid.</param>
        /// <param name="mode">
        /// Whether cells that are not walkable block line of sight. Defaults to
        /// <see cref="LineOfSightMode.BlockedByUnwalkableCells"/>.
        /// </param>
        /// <returns><c>true</c> if no obstacle lies between the two coordinates; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="from"/> or <paramref name="to"/> is outside the valid field range.
        /// </exception>
        public bool HasLineOfSight(Coordinate from, Coordinate to,
            LineOfSightMode mode = LineOfSightMode.BlockedByUnwalkableCells)
        {
            if (!IsPositionValid(from.X, from.Y))
            {
                throw new ArgumentException("Origin is outside the valid field range", nameof(from));
            }

            if (!IsPositionValid(to.X, to.Y))
            {
                throw new ArgumentException("Target is outside the valid field range", nameof(to));
            }

            InitializeCellsArray();

            var cells = new Span<Cell>(_cells, 0, Size);

            fixed (Cell* ptr = &MemoryMarshal.GetReference(cells))
            {
                return HasLineOfSight(from, to, ptr, mode);
            }
        }
    }
}
