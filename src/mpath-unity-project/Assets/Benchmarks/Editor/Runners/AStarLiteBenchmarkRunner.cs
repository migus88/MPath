using System;
using AStar;
using AStar.Options;
using Benchmarks.Editor.Helpers;
using Migs.MPath.Core.Data;
using UnityEngine;

namespace Benchmarks.Editor.Runners
{
    public class AStarLiteBenchmarkRunner : BaseMazeBenchmarkRunner
    {
        private readonly PathFinder _pathFinder;

        public AStarLiteBenchmarkRunner(UnityMaze maze) : base(maze)
        {
            // Convert cells to a grid usable by AStar
            var grid = new short[Maze.Width, Maze.Height];

            for (var x = 0; x < Maze.Width; x++)
            {
                for (var y = 0; y < Maze.Height; y++)
                {
                    var cell = Maze.Cells[x, y];
                    // In AStar, 0 is blocked, positive values are walkable
                    grid[x, y] = cell.IsWalkable && !cell.IsOccupied ? (short)1 : (short)0;
                }
            }

            var worldGrid = new WorldGrid(grid);
            var options = new PathFinderOptions
            {
                SearchLimit = 20000
            };
            _pathFinder = new PathFinder(worldGrid, options);
        }

        public override void FindPath(Vector2Int start, Vector2Int destination)
        {
            var path = _pathFinder.FindPath(
                new Position(start.x, start.y),
                new Position(destination.x, destination.y)
            );

            if (path.Length == 0)
            {
                throw new Exception("No path found for AStar!");
            }
        }
    }
}