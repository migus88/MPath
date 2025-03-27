using System;
using UnityEngine;
using AStar;
using AStar.Options;
using Position = AStar.Position;

namespace Bemcmarks.Editor.PerformanceTests
{
    /// <summary>
    /// Performance test for AStar-Lite pathfinding algorithm
    /// </summary>
    public class AStarLiteTest : BaseMazeTest
    {
        private WorldGrid _worldGrid;
        private PathFinder _pathFinder;
        
        protected override string GetTestName() => "AStar-Lite";
        
        protected override void InitializePathfinder()
        {
            // Create a grid for AStar-Lite
            short[,] grid = new short[_mazeData.Width, _mazeData.Height];
            
            // Initialize the grid
            for (int y = 0; y < _mazeData.Height; y++)
            {
                for (int x = 0; x < _mazeData.Width; x++)
                {
                    // For AStar-Lite, 0 is blocked, any positive value is walkable
                    grid[x, y] = _mazeData.IsCellWalkable(x, y) ? (short)1 : (short)0;
                }
            }
            
            // Create the world grid and pathfinder
            _worldGrid = new WorldGrid(grid);
            
            var options = new PathFinderOptions
            {
                SearchLimit = 500000, // Large limit for complex maze
                UseDiagonals = true
            };
            
            _pathFinder = new PathFinder(_worldGrid, options);
            
            Debug.Log("AStar-Lite pathfinder initialized");
        }
        
        protected override bool FindPath(Vector2Int start, Vector2Int end)
        {
            try
            {
                // Find path
                var path = _pathFinder.FindPath(
                    new Position(start.x, start.y),
                    new Position(end.x, end.y)
                );
                
                // Return success
                return path != null && path.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error finding path with AStar-Lite: {ex.Message}");
                return false;
            }
        }
    }
} 