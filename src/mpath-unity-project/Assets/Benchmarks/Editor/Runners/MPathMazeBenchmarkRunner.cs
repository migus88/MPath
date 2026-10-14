using System;
using System.Linq;
using System.Reflection;
using Benchmarks.Editor.Helpers;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using UnityEngine;

namespace Benchmarks.Editor.Runners
{
    public class MPathMazeBenchmarkRunner : BaseMazeBenchmarkRunner
    {
        private readonly Pathfinder _pathfinder;

        public MPathMazeBenchmarkRunner(UnityMaze maze) : base(maze)
        {
            _pathfinder = new Pathfinder(Maze.Cells);
        }

        public override void FindPath(Vector2Int start, Vector2Int destination)
        {
            if (_pathfinder == null)
            {
                return;
            }

            var result = _pathfinder.GetPath(
                new Coordinate(start.x, start.y),
                new Coordinate(destination.x, destination.y)
            );

            if (!result.IsSuccess)
            {
                throw new Exception("Path not found");
            }
        }
        
        public override void RenderPath(string path, int scale, Vector2Int start, Vector2Int destination)
        {
            // Set start and destination points on the maze
            Maze.SetStart(new Coordinate(start.x, start.y));
            Maze.SetDestination(new Coordinate(destination.x, destination.y));
            
            if (_pathfinder == null)
            {
                Debug.LogError("Pathfinder is null");
                return;
            }

            var result = _pathfinder.GetPath(
                new Coordinate(start.x, start.y),
                new Coordinate(destination.x, destination.y)
            );

            if (result.IsSuccess)
            {
                // Add the path to the maze
                Maze.AddPath(result.Path.ToArray());
            }
            
            Maze.SaveImage(path, scale);
        }
    }
}