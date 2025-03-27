using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor
{
    public static class BenchmarkRunner
    {
        [MenuItem("Tools/Benchmarks/Run All Benchmarks")]
        public static void RunAllBenchmarks()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            Debug.Log("Test Runner opened. Select 'Benchmarks.Editor.PerformanceBenchmarks.RunAllPathfindingBenchmarks' and click 'Run Selected'.");
        }
        
        [MenuItem("Tools/Benchmarks/Run MPath Benchmarks")]
        public static void RunMPathBenchmarks()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            Debug.Log("Test Runner opened. Select 'Benchmarks.Editor.PerformanceBenchmarks.MPathBenchmark' and click 'Run Selected'.");
        }
        
        [MenuItem("Tools/Benchmarks/Run RoyT A* Benchmarks")]
        public static void RunRoyTAStarBenchmarks()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            Debug.Log("Test Runner opened. Select 'Benchmarks.Editor.PerformanceBenchmarks.RoyTAStarBenchmark' and click 'Run Selected'.");
        }
        
        [MenuItem("Tools/Benchmarks/Run AStar.Lite Benchmarks")]
        public static void RunAStarLiteBenchmarks()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            Debug.Log("Test Runner opened. Select 'Benchmarks.Editor.PerformanceBenchmarks.AStarLiteBenchmark' and click 'Run Selected'.");
        }
    }
} 