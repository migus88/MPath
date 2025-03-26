using System;
using System.Buffers;
using System.Collections.Generic;

namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Represents the result of a pathfinding operation.
    /// Contains the calculated path (if successful) and information about the operation's success or failure.
    /// Implements IDisposable to properly release the path array back to the array pool.
    /// </summary>
    public sealed class PathResult : IDisposable
    {
        private static readonly PathResult FailureResult = new(null, 0, false);
        
        /// <summary>
        /// Gets a value indicating whether the pathfinding operation was successful.
        /// </summary>
        public bool IsSuccess { get; }
        
        /// <summary>
        /// Gets the number of coordinates in the path.
        /// </summary>
        public int Length { get; }

        public IEnumerable<Coordinate> Path
        {
            get
            {
                if (Length <= 0 || _path.Length < Length)
                {
                    yield break;
                }

                for (var i = 0; i < Length; i++)
                {
                    yield return _path[i];
                }
            }
        }

        private readonly Coordinate[] _path;
        private bool _isDisposed;

        private PathResult(Coordinate[] path, int length, bool isSuccess)
        {
            _path = path;
            Length = length;
            IsSuccess = isSuccess;
            _isDisposed = false;
        }

        /// <summary>
        /// Creates a successful path result with the specified path.
        /// </summary>
        /// <param name="path">The array of coordinates representing the path.</param>
        /// <param name="length">The actual number of coordinates in the path.</param>
        /// <returns>A new PathResult representing a successful pathfinding operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative or greater than the path array length.</exception>
        public static PathResult Success(Coordinate[] path, int length)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
                
            if (length < 0 || length > path.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
                
            return new PathResult(path, length, true);
        }

        /// <summary>
        /// Creates a failed path result.
        /// </summary>
        /// <returns>A new PathResult representing a failed pathfinding operation.</returns>
        public static PathResult Failure()
        {
            return FailureResult;
        }

        /// <summary>
        /// Gets the coordinate at the specified index in the path.
        /// </summary>
        /// <param name="index">The zero-based index of the coordinate to get.</param>
        /// <returns>The coordinate at the specified index.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the PathResult has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the path was not found.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public Coordinate Get(int index)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PathResult));
                
            if (!IsSuccess)
                throw new InvalidOperationException("Path was not found");
                
            if (index < 0 || index >= Length)
                throw new ArgumentOutOfRangeException(nameof(index));
                
            return _path[index];
        }

        /// <summary>
        /// Releases the resources used by the PathResult.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed || _path == null)
                return;
                
            ArrayPool<Coordinate>.Shared.Return(_path);
            _isDisposed = true;
        }
    }
}
