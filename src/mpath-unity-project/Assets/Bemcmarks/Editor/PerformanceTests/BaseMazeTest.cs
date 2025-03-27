using System;
using System.IO;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Bemcmarks.Editor.PerformanceTests
{
    /// <summary>
    /// Base class for maze performance tests
    /// </summary>
    public abstract class BaseMazeTest
    {
        protected MazeDataAsset _mazeData;
        protected Vector2Int _startPoint = new Vector2Int(10, 10);
        protected Vector2Int _endPoint = new Vector2Int(502, 374);
        protected string _resultPath;
        
        /// <summary>
        /// Initialize the test with maze data
        /// </summary>
        [OneTimeSetUp]
        public virtual void Setup()
        {
            // Find a maze data asset
            string[] mazeDataPaths = AssetDatabase.FindAssets("t:MazeDataAsset");
            
            Assert.IsTrue(mazeDataPaths.Length > 0, "No maze data assets found. " +
                "Please create at least one using Tools > Benchmarks > Generate Maze Data");
            
            string assetPath = AssetDatabase.GUIDToAssetPath(mazeDataPaths[0]);
            _mazeData = AssetDatabase.LoadAssetAtPath<MazeDataAsset>(assetPath);
            
            Assert.IsNotNull(_mazeData, "Failed to load maze data asset");
            
            // Set up reasonable end point if needed
            if (_endPoint.x >= _mazeData.Width || _endPoint.y >= _mazeData.Height)
            {
                _endPoint = new Vector2Int(_mazeData.Width - 10, _mazeData.Height - 10);
            }
            
            // Find a walkable start point if needed
            if (!_mazeData.IsCellWalkable(_startPoint.x, _startPoint.y))
            {
                _startPoint = FindWalkableCell(_startPoint);
            }
            
            // Find a walkable end point if needed
            if (!_mazeData.IsCellWalkable(_endPoint.x, _endPoint.y))
            {
                _endPoint = FindWalkableCell(_endPoint);
            }
            
            // Ensure endpoints are not the same
            Assert.AreNotEqual(_startPoint, _endPoint, "Start and end points cannot be the same");
            
            // Set up result path
            string resultDirectory = Path.Combine(Application.persistentDataPath, "BemcmarkResults");
            if (!Directory.Exists(resultDirectory))
            {
                Directory.CreateDirectory(resultDirectory);
            }
            
            _resultPath = Path.Combine(resultDirectory, GetTestName() + ".txt");
            
            // Initialize the pathfinder
            InitializePathfinder();
            
            Debug.Log($"Test setup complete. Using maze data: {assetPath}");
            Debug.Log($"Start: {_startPoint}, End: {_endPoint}");
        }
        
        /// <summary>
        /// Find a walkable cell near the specified point
        /// </summary>
        protected Vector2Int FindWalkableCell(Vector2Int point)
        {
            int maxRadius = 20;
            
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) != radius) continue;
                        
                        int nx = point.x + dx;
                        int ny = point.y + dy;
                        
                        if (nx < 0 || nx >= _mazeData.Width || ny < 0 || ny >= _mazeData.Height)
                            continue;
                            
                        if (_mazeData.IsCellWalkable(nx, ny))
                        {
                            return new Vector2Int(nx, ny);
                        }
                    }
                }
            }
            
            Assert.Fail("Could not find any walkable cell nearby!");
            return point; // Will not reach here due to Assert.Fail
        }
        
        /// <summary>
        /// Get the name of the test for result output
        /// </summary>
        protected abstract string GetTestName();
        
        /// <summary>
        /// Initialize the pathfinder
        /// </summary>
        protected abstract void InitializePathfinder();
        
        /// <summary>
        /// Find a path from start to end
        /// </summary>
        protected abstract bool FindPath(Vector2Int start, Vector2Int end);
        
        /// <summary>
        /// Run the benchmark
        /// </summary>
        [Test, Performance]
        public void RunBenchmark()
        {
            int iterations = 5;
            double[] timeResults = new double[iterations];
            bool success = false;
            
            // Run the test multiple times
            for (int i = 0; i < iterations; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                success = FindPath(_startPoint, _endPoint);
                sw.Stop();
                
                timeResults[i] = sw.Elapsed.TotalMilliseconds;
                Debug.Log($"Run {i+1}: {timeResults[i]:F2}ms");
            }
            
            // Calculate statistics
            double average = 0;
            double min = double.MaxValue;
            double max = double.MinValue;
            
            foreach (var time in timeResults)
            {
                average += time;
                min = Math.Min(min, time);
                max = Math.Max(max, time);
            }
            
            average /= iterations;
            
            // Save results
            using (var writer = new StreamWriter(_resultPath, true))
            {
                writer.WriteLine($"=== {GetTestName()} ===");
                writer.WriteLine($"Date: {DateTime.Now}");
                writer.WriteLine($"Maze: {_mazeData.Width}x{_mazeData.Height}");
                writer.WriteLine($"Start: {_startPoint}, End: {_endPoint}");
                writer.WriteLine($"Success: {success}");
                writer.WriteLine($"Iterations: {iterations}");
                writer.WriteLine($"Average Time: {average:F2}ms");
                writer.WriteLine($"Min Time: {min:F2}ms");
                writer.WriteLine($"Max Time: {max:F2}ms");
                writer.WriteLine("----------------------------");
            }
            
            Debug.Log($"Benchmark completed. Results saved to: {_resultPath}");
        }
    }
} 