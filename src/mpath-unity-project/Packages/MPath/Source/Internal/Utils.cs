using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Internal
{
    internal static class Utils
    {
        // Orders cells to match the flat-array layout used by Pathfinder.GetCell (index = X * Height + Y),
        // i.e. X-major then Y-minor. This must stay consistent with the matrix/holder init modes, which
        // place each cell at "Coordinate.X * Height + Coordinate.Y".
        internal static int CellsComparison(Cell a, Cell b)
        {
            var result = a.Coordinate.X.CompareTo(b.Coordinate.X);
            return result == 0 ? a.Coordinate.Y.CompareTo(b.Coordinate.Y) : result;
        }
    }
}