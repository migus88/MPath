using UnityEngine;
using UnityEditor;
using Bemcmarks.Editor.PerformanceTests;

namespace Bemcmarks.Editor
{
    /// <summary>
    /// Menu items for running benchmarks
    /// </summary>
    public static class BenchmarkMenuItems
    {
        [MenuItem("Tools/Benchmarks/Run Comparison Test")]
        public static void RunComparisonTest()
        {
            Debug.Log("Starting benchmark comparison test...");
            
            var test = new ComparisonTest();
            
            // Use reflection to call the OneTimeSetUp method
            var setupMethod = typeof(ComparisonTest).GetMethod("Setup", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic);
            
            setupMethod.Invoke(test, null);
            
            // Run the test
            test.CompareAllAlgorithms();
            
            Debug.Log("Benchmark comparison test completed!");
        }
        
        [MenuItem("Tools/Benchmarks/Open Test Results Folder")]
        public static void OpenResultsFolder()
        {
            string resultDirectory = System.IO.Path.Combine(Application.persistentDataPath, "BemcmarkResults");
            
            if (!System.IO.Directory.Exists(resultDirectory))
            {
                System.IO.Directory.CreateDirectory(resultDirectory);
            }
            
            EditorUtility.RevealInFinder(resultDirectory);
        }
    }
} 