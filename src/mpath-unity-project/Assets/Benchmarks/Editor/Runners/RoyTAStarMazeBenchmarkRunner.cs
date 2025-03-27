using System;
using Benchmarks.Editor.Helpers;
using Migs.MPath.Core.Data;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using UnityEngine;
using Grid = Roy_T.AStar.Grids.Grid;

namespace Benchmarks.Editor.Runners
{
    public class RoyTAStarMazeBenchmarkRunner : BaseMazeBenchmarkRunner
    {
        private readonly PathFinder _pathFinder = new PathFinder();
        private readonly Node[,] _nodes;
        private readonly Grid _grid;

        public RoyTAStarMazeBenchmarkRunner(UnityMaze maze) : base(maze)
        {
            _nodes = new Node[Maze.Width, Maze.Height];
            PopulateNodes();

            _grid = Grid.CreateGridFrom2DArrayOfNodes(_nodes);
        }

        public override void FindPath(Vector2Int start, Vector2Int destination)
        {
            var startPoint = new GridPosition(start.x, start.y);
            var endPoint = new GridPosition(destination.x, destination.y);

            var path = _pathFinder.FindPath(startPoint, endPoint, _grid);

            if (path.Edges.Count == 0)
            {
                throw new Exception("Path not found");
            }
        }

        private void PopulateNodes()
        {
            var cells = Maze.Cells;

            var height = Maze.Height;
            var width = Maze.Width;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    _nodes[x, y] = new Node(new Position(x, y));
                }
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Connect(x, y, cells);
                }
            }
        }

        private void Connect(int x, int y, Cell[,] cells)
        {
            var node = _nodes[x, y];

            var neighbors = new (int, int)[]
            {
                (x + 1, y),
                (x - 1, y),
                (x, y + 1),
                (x, y - 1),
                (x + 1, y + 1),
                (x - 1, y - 1),
                (x + 1, y - 1),
                (x - 1, y + 1)
            };

            var velocity = Velocity.FromMetersPerSecond(1);

            var height = Maze.Height;
            var width = Maze.Width;

            foreach (var neighbor in neighbors)
            {
                if (neighbor.Item1 < 0 || neighbor.Item1 >= width || neighbor.Item2 < 0 ||
                    neighbor.Item2 >= height)
                {
                    continue;
                }

                var cell = cells[neighbor.Item1, neighbor.Item2];
                if (cell.IsOccupied || !cell.IsWalkable)
                {
                    continue;
                }

                node.Connect(_nodes[neighbor.Item1, neighbor.Item2], velocity);
            }
        }
    }
}