using System;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;

namespace Migs.Pathfinding.Core.Internal;

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
}