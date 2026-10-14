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

        /// <summary>
        /// Gets the number of grid steps of the route the search found: one per cell entered, the origin excluded.
        /// Equals <see cref="Length"/> unless path smoothing removed waypoints; 0 for a failed search.
        /// </summary>
        public int Steps { get; private set; }

        /// <summary>
        /// Gets how many of <see cref="Steps"/> are diagonal. Like <see cref="Steps"/>, it describes the route
        /// before smoothing.
        /// </summary>
        public int DiagonalSteps { get; private set; }

        public IEnumerable<Coordinate> Path
        {
            get
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(PathResult));
                }
                
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
            Steps = length;
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
            {
                throw new ArgumentNullException(nameof(path));
            }
                
            if (length < 0 || length > path.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
                
            return new PathResult(path, length, true);
        }

        /// <summary>
        /// Creates a successful path result with the specified path and the step counts of the route it came from
        /// (for example a smoothed path, whose waypoints are fewer than the steps of the route).
        /// </summary>
        /// <param name="path">The array of coordinates representing the path.</param>
        /// <param name="length">The actual number of coordinates in the path.</param>
        /// <param name="steps">The number of grid steps of the route.</param>
        /// <param name="diagonalSteps">How many of <paramref name="steps"/> are diagonal.</param>
        /// <returns>A new PathResult representing a successful pathfinding operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative or greater than the path
        /// array length, or when the step counts are negative or <paramref name="diagonalSteps"/> exceeds
        /// <paramref name="steps"/>.</exception>
        public static PathResult Success(Coordinate[] path, int length, int steps, int diagonalSteps)
        {
            if (steps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(steps));
            }

            if (diagonalSteps < 0 || diagonalSteps > steps)
            {
                throw new ArgumentOutOfRangeException(nameof(diagonalSteps));
            }

            return Success(path, length).WithStepCounts(steps, diagonalSteps);
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
            {
                throw new ObjectDisposedException(nameof(PathResult));
            }
                
            if (!IsSuccess)
            {
                throw new InvalidOperationException("Path was not found");
            }
                
            if (index < 0 || index >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
                
            return _path[index];
        }

        /// <summary>
        /// Creates an independent result with the same path in a freshly rented array, so the copy and the original
        /// can be disposed separately.
        /// </summary>
        /// <returns>A new successful result, or the shared failure result when this result is a failure.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the PathResult has been disposed.</exception>
        internal PathResult Copy()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PathResult));
            }

            if (!IsSuccess)
            {
                return FailureResult;
            }

            var path = ArrayPool<Coordinate>.Shared.Rent(Length);
            Array.Copy(_path, path, Length);
            return new PathResult(path, Length, true).WithStepCounts(Steps, DiagonalSteps);
        }

        /// <summary>
        /// Sets the step counts of a result the pathfinder just created (never the shared failure result).
        /// </summary>
        internal PathResult WithStepCounts(int steps, int diagonalSteps)
        {
            Steps = steps;
            DiagonalSteps = diagonalSteps;
            return this;
        }

        /// <summary>
        /// Releases the resources used by the PathResult.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed || _path == null)
            {
                return;
            }
                
            ArrayPool<Coordinate>.Shared.Return(_path);
            _isDisposed = true;
        }
    }
}
