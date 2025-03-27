using Benchmarks.Editor.Helpers;
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
    }
}