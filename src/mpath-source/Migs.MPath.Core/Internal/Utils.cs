using System;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Internal;

internal static class Utils
{
    internal static unsafe Span<T> ToSpan<T>(this T[,] matrix) where T : unmanaged
    {
        if (matrix == null)
            throw new ArgumentNullException(nameof(matrix));

        // Pin the matrix and get a pointer to the first element
        fixed (T* ptr = &matrix[0, 0])
        {
            // Create a span over the memory of the matrix
            return new Span<T>(ptr, matrix.Length);
        }
    }
    
    internal static int CellsComparison(Cell a, Cell b)
    {
        var result = a.Coordinate.Y.CompareTo(b.Coordinate.Y);
        return result == 0 ? a.Coordinate.X.CompareTo(b.Coordinate.X) : result;
    }
}