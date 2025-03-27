using System;
using System.IO;
using UnityEngine;
using NUnit.Framework;
using UnityEditor;

namespace Bemcmarks.Editor.PerformanceTests
{
    /// <summary>
    /// Test for comparing all pathfinding algorithms
    /// </summary>
    public class ComparisonTest
    {
        private MazeDataAsset _mazeData;
        private Vector2Int _startPoint = new Vector2Int(10, 10);
        private Vector2Int _endPoint = new Vector2Int(502, 374);
        private string _resultPath;
        
        private MPathTest _mpathTest;
        private RoyTAStarTest _roytTest;
        private AStarLiteTest _astarLiteTest;
        
        [OneTimeSetUp]
        public void Setup()
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
            
            // Create directory for results
            string resultDirectory = Path.Combine(Application.persistentDataPath, "BemcmarkResults");
            if (!Directory.Exists(resultDirectory))
            {
                Directory.CreateDirectory(resultDirectory);
            }
            
            _resultPath = Path.Combine(resultDirectory, "Comparison.txt");
            
            // Create test instances but don't initialize them yet
            _mpathTest = new MPathTest();
            _roytTest = new RoyTAStarTest();
            _astarLiteTest = new AStarLiteTest();
            
            Debug.Log($"Comparison test setup complete. Using maze data: {assetPath}");
            Debug.Log($"Start: {_startPoint}, End: {_endPoint}");
        }
        
        [Test]
        public void CompareAllAlgorithms()
        {
            int iterations = 5;
            int algorithmCount = 3;
            
            // Arrays to store results
            string[] names = new string[algorithmCount];
            double[] averages = new double[algorithmCount];
            double[] mins = new double[algorithmCount];
            double[] maxes = new double[algorithmCount];
            bool[] successes = new bool[algorithmCount];
            
            // Test MPath
            Debug.Log("Testing MPath...");
            RunTest(_mpathTest, iterations, out names[0], out averages[0], out mins[0], out maxes[0], out successes[0]);
            
            // Test RoyT.AStar
            Debug.Log("Testing RoyT.AStar...");
            RunTest(_roytTest, iterations, out names[1], out averages[1], out mins[1], out maxes[1], out successes[1]);
            
            // Test AStar-Lite
            Debug.Log("Testing AStar-Lite...");
            RunTest(_astarLiteTest, iterations, out names[2], out averages[2], out mins[2], out maxes[2], out successes[2]);
            
            // Save comparative results
            using (var writer = new StreamWriter(_resultPath, true))
            {
                writer.WriteLine($"=== Pathfinding Algorithm Comparison ===");
                writer.WriteLine($"Date: {DateTime.Now}");
                writer.WriteLine($"Maze: {_mazeData.Width}x{_mazeData.Height}");
                writer.WriteLine($"Start: {_startPoint}, End: {_endPoint}");
                writer.WriteLine($"Iterations: {iterations}");
                writer.WriteLine();
                writer.WriteLine($"{"Algorithm",-15} | {"Success",-8} | {"Avg (ms)",-10} | {"Min (ms)",-10} | {"Max (ms)",-10}");
                writer.WriteLine(new string('-', 65));
                
                for (int i = 0; i < algorithmCount; i++)
                {
                    writer.WriteLine($"{names[i],-15} | {successes[i],-8} | {averages[i],-10:F2} | {mins[i],-10:F2} | {maxes[i],-10:F2}");
                }
                
                writer.WriteLine();
                writer.WriteLine("----------------------------");
            }
            
            // Log comparative results
            Debug.Log($"Comparison completed. Results saved to: {_resultPath}");
            
            // Also log to console
            Debug.Log($"{"Algorithm",-15} | {"Success",-8} | {"Avg (ms)",-10} | {"Min (ms)",-10} | {"Max (ms)",-10}");
            Debug.Log(new string('-', 65));
            
            for (int i = 0; i < algorithmCount; i++)
            {
                Debug.Log($"{names[i],-15} | {successes[i],-8} | {averages[i],-10:F2} | {mins[i],-10:F2} | {maxes[i],-10:F2}");
            }
        }
        
        private void RunTest(BaseMazeTest test, int iterations, out string name, out double average, out double min, out double max, out bool success)
        {
            // Override OneTimeSetUp by calling methods directly
            typeof(BaseMazeTest).GetMethod("Setup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Invoke(test, null);
            
            // Get test name
            name = (string)typeof(BaseMazeTest).GetMethod("GetTestName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(test, null);
            
            // Initialize results
            double[] timeResults = new double[iterations];
            success = false;
            
            // Run the test multiple times
            for (int i = 0; i < iterations; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                success = (bool)typeof(BaseMazeTest).GetMethod("FindPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(test, new object[] { _startPoint, _endPoint });
                sw.Stop();
                
                timeResults[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            // Calculate statistics
            average = 0;
            min = double.MaxValue;
            max = double.MinValue;
            
            foreach (var time in timeResults)
            {
                average += time;
                min = Math.Min(min, time);
                max = Math.Max(max, time);
            }
            
            average /= iterations;
        }
    }
} 