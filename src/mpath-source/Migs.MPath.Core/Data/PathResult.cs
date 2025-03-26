using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Migs.MPath.Core.Data
{
    public class PathResult : IDisposable
    {
        public bool IsPathFound => Length > 0;
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
        
        private static readonly PathResult _failureResult = new(null);

        private PathResult(Coordinate[] path, int lenght = 0)
        {
            _path = path;
            Length = lenght;
        }

        /// <summary>
        /// Creates a successful path result. <br/>
        /// The array can be larger than the path length, but the path length must be valid.
        /// </summary>
        /// <param name="path">Arran containing the path</param>
        /// <param name="lenght">Lenght of the path</param>
        /// <returns>Successful path result</returns>
        public static PathResult Success(Coordinate[] path, int lenght) => new(path, lenght);
        public static PathResult Failure() => _failureResult;

        public void Dispose()
        {
            ArrayPool<Coordinate>.Shared.Return(_path);
        }
    }
}
