using System;
using System.Linq;
using UnityEngine;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Bemcmarks.Editor.PerformanceTests
{
    /// <summary>
    /// Performance test for MPath pathfinding algorithm
    /// </summary>
    public class MPathTest : BaseMazeTest
    {
        private Pathfinder _pathfinder;
        private Cell[,] _cells;
        private IAgent _agent;
        private PathResult _pathResult;
        
        protected override string GetTestName() => "MPath";
        
        /// <summary>
        /// Simple agent implementation for pathfinding
        /// </summary>
        private class Agent : IAgent
        {
            public int Size => 1;
        }
        
        protected override void InitializePathfinder()
        {
            // Create a grid of cells from maze data
            _cells = new Cell[_mazeData.Width, _mazeData.Height];
            
            for (short y = 0; y < _mazeData.Height; y++)
            {
                for (short x = 0; x < _mazeData.Width; x++)
                {
                    ref var cell = ref _cells[x, y];
                    cell.Coordinate = new Coordinate(x, y);
                    cell.IsWalkable = _mazeData.IsCellWalkable(x, y);
                }
            }
            
            // Create the agent
            _agent = new Agent();
            
            // Initialize the pathfinder
            _pathfinder = new Pathfinder(_cells);
            
            Debug.Log("MPath pathfinder initialized");
        }
        
        protected override bool FindPath(Vector2Int start, Vector2Int end)
        {
            try
            {
                // Dispose previous result if any
                _pathResult?.Dispose();
                
                // Convert Vector2Int to Coordinate
                var startCoord = new Coordinate(start.x, start.y);
                var endCoord = new Coordinate(end.x, end.y);
                
                // Find path
                _pathResult = _pathfinder.GetPath(_agent, startCoord, endCoord);
                
                // Return success
                return _pathResult.IsSuccess;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error finding path with MPath: {ex.Message}");
                return false;
            }
        }
        
        public override void Setup()
        {
            base.Setup();
            
            // Log additional information
            // TODO: Uncomment when Settings are exposed
            //Debug.Log($"Pathfinder settings: Diagonal={_pathfinder.Settings.IsDiagonalMovementEnabled}, " +
                      //$"BetweenCorners={_pathfinder.Settings.IsMovementBetweenCornersEnabled}");
        }
    }
} 