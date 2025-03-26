using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Internal;

internal static class Utils
{
    internal static int CellsComparison(Cell a, Cell b)
    {
        var result = a.Coordinate.Y.CompareTo(b.Coordinate.Y);
        return result == 0 ? a.Coordinate.X.CompareTo(b.Coordinate.X) : result;
    }
}