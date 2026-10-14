using System.Runtime.CompilerServices;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The neighbour slots of an expansion, which are also the movement directions (the slot a neighbour sits in is
    /// the direction of the step into it). North is -Y. The index order is the fixed direction order of the
    /// tie-break.
    /// </summary>
    internal static class DirectionIndexes
    {
        public const int West = 0;
        public const int East = 1;
        public const int North = 2;
        public const int South = 3;
        public const int SouthWest = 4;
        public const int NorthWest = 5;
        public const int SouthEast = 6;
        public const int NorthEast = 7;

        public const int DiagonalStart = SouthWest;

        /// <summary>
        /// A direction set with every direction in it.
        /// </summary>
        public const byte AllDirections = 0xFF;

        /// <summary>
        /// The X offset of a step in <paramref name="direction"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetOffsetX(int direction)
        {
            switch (direction)
            {
                case West:
                case SouthWest:
                case NorthWest:
                    return -1;
                case East:
                case SouthEast:
                case NorthEast:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The Y offset of a step in <paramref name="direction"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetOffsetY(int direction)
        {
            switch (direction)
            {
                case North:
                case NorthWest:
                case NorthEast:
                    return -1;
                case South:
                case SouthWest:
                case SouthEast:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The first direction (in index order) of a non-empty direction set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFirstDirection(byte directions)
        {
            var direction = 0;

            while ((directions & (1 << direction)) == 0)
            {
                direction++;
            }

            return direction;
        }
    }
}
