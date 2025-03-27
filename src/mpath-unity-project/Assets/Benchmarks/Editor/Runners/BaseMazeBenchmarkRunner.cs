using Benchmarks.Editor.Helpers;
using Migs.MPath.Core.Data;
using UnityEngine;

namespace Benchmarks.Editor.Runners
{
    
    public abstract class BaseMazeBenchmarkRunner
    {
        protected UnityMaze Maze { get;}

        protected BaseMazeBenchmarkRunner(UnityMaze maze)
        {
            Maze = maze;
        }

        public abstract void FindPath(Vector2Int start, Vector2Int destination);

        /// <summary>
        /// Renders the path from start to destination
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="start">Start position</param>
        /// <param name="destination">Destination position</param>
        /// <param name="path"></param>
        public abstract void RenderPath(string path, int scale, Vector2Int start, Vector2Int destination);
        
        /// <summary>
        /// Gets the name of the algorithm for display purposes
        /// </summary>
        public virtual string AlgorithmName => GetType().Name.Replace("MazeBenchmarkRunner", "");
    }
}