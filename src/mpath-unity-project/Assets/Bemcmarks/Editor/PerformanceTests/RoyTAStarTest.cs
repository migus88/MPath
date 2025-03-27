using System;
using UnityEngine;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using Grid = Roy_T.AStar.Grids.Grid;

namespace Bemcmarks.Editor.PerformanceTests
{
    /// <summary>
    /// Performance test for RoyT.AStar pathfinding algorithm
    /// </summary>
    public class RoyTAStarTest : BaseMazeTest
    {
        private PathFinder _pathFinder;
        private Node[,] _nodes;
        private Grid _grid;
        
        protected override string GetTestName() => "RoyT.AStar";
        
        protected override void InitializePathfinder()
        {
            // Create the nodes grid
            _nodes = new Node[_mazeData.Width, _mazeData.Height];
            
            // Initialize each node
            for (int y = 0; y < _mazeData.Height; y++)
            {
                for (int x = 0; x < _mazeData.Width; x++)
                {
                    _nodes[x, y] = new Node(new Position(x, y));
                }
            }
            
            // Connect walkable nodes
            for (int y = 0; y < _mazeData.Height; y++)
            {
                for (int x = 0; x < _mazeData.Width; x++)
                {
                    ConnectNode(x, y);
                }
            }
            
            // Create the grid and pathfinder
            _grid = Grid.CreateGridFrom2DArrayOfNodes(_nodes);
            _pathFinder = new PathFinder();
            
            Debug.Log("RoyT.AStar pathfinder initialized");
        }
        
        /// <summary>
        /// Connect a node to its walkable neighbors
        /// </summary>
        private void ConnectNode(int x, int y)
        {
            var node = _nodes[x, y];
            
            // Check if the current node's cell is walkable
            if (!_mazeData.IsCellWalkable(x, y))
            {
                return; // Don't connect unwalkable nodes
            }
            
            // Define the 8 possible neighbors (4 cardinal + 4 diagonal)
            var neighbors = new (int dx, int dy)[]
            {
                (1, 0),   // Right
                (-1, 0),  // Left
                (0, 1),   // Up
                (0, -1),  // Down
                (1, 1),   // Right-Up
                (-1, -1), // Left-Down
                (1, -1),  // Right-Down
                (-1, 1)   // Left-Up
            };
            
            var velocity = Velocity.FromMetersPerSecond(1);
            
            foreach (var (dx, dy) in neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                
                // Skip out-of-bounds neighbors
                if (nx < 0 || nx >= _mazeData.Width || ny < 0 || ny >= _mazeData.Height)
                {
                    continue;
                }
                
                // Skip unwalkable neighbors
                if (!_mazeData.IsCellWalkable(nx, ny))
                {
                    continue;
                }
                
                // Connect the nodes
                node.Connect(_nodes[nx, ny], velocity);
            }
        }
        
        protected override bool FindPath(Vector2Int start, Vector2Int end)
        {
            try
            {
                // Convert Vector2Int to GridPosition
                var startPos = new GridPosition(start.x, start.y);
                var endPos = new GridPosition(end.x, end.y);
                
                // Find path
                var path = _pathFinder.FindPath(startPos, endPos, _grid);
                
                // Return success
                return path.Edges.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error finding path with RoyT.AStar: {ex.Message}");
                return false;
            }
        }
    }
} 