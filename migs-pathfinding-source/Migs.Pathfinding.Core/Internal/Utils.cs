using System;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;

namespace Migs.Pathfinding.Core.Internal;

internal static class Utils
{
    internal static int FieldCellComparison(ICellHolder a, ICellHolder b)
    {
        var result = a.Cell.Coordinate.X.CompareTo(b.Cell.Coordinate.X);
        return result == 0 ? a.Cell.Coordinate.Y.CompareTo(b.Cell.Coordinate.Y) : result;
    }
    
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
}