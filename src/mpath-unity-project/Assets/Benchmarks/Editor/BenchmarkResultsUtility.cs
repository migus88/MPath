using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Data;
using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor
{
    public static class BenchmarkResultsUtility
    {
        [MenuItem("Tools/Benchmarks/Export Results")]
        public static void ExportResults()
        {
            var testResults = LoadTestResults();
            if (testResults.Count == 0)
            {
                Debug.LogWarning("No benchmark results found. Please run benchmarks first.");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Save Benchmark Results",
                "",
                "PathfindingBenchmarkResults.csv",
                "csv");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Algorithm,Maze,Samples,Median (ms),Min (ms),Max (ms),Mean (ms),Standard Deviation (ms)");

            foreach (var result in testResults)
            {
                foreach (var sampleGroup in result.SampleGroups)
                {
                    string[] parts = sampleGroup.Name.Split('_');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    string algorithm = parts[0];
                    string maze = parts[1];

                    var samples = sampleGroup.Samples;
                    var sampleCount = samples.Count;
                    if (sampleCount == 0)
                    {
                        continue;
                    }

                    // Convert to milliseconds for better readability
                    var median = GetMedian(samples) / 1000000f;
                    var min = samples.Min() / 1000000f;
                    var max = samples.Max() / 1000000f;
                    var mean = samples.Average() / 1000000f;
                    var stdDev = CalculateStdDev(samples) / 1000000f;

                    csv.AppendLine($"{algorithm},{maze},{sampleCount},{median:F4},{min:F4},{max:F4},{mean:F4},{stdDev:F4}");
                }
            }

            File.WriteAllText(path, csv.ToString());
            Debug.Log($"Benchmark results exported to: {path}");
        }

        private static float GetMedian(List<double> samples)
        {
            var sortedSamples = samples.OrderBy(s => s).ToList();
            int count = sortedSamples.Count;
            
            if (count == 0)
            {
                return 0;
            }
            
            if (count % 2 == 0)
            {
                return (float)((sortedSamples[count / 2 - 1] + sortedSamples[count / 2]) / 2);
            }
            
            return (float)sortedSamples[count / 2];
        }

        private static float CalculateStdDev(List<double> samples)
        {
            int count = samples.Count;
            if (count <= 1)
            {
                return 0;
            }

            double mean = samples.Average();
            double sum = samples.Sum(d => (d - mean) * (d - mean));
            return (float)Math.Sqrt(sum / (count - 1));
        }

        [MenuItem("Tools/Benchmarks/Clear Results")]
        public static void ClearResults()
        {
            // Unity's performance testing API doesn't provide a direct way to clear results
            // We'll need to delete the results file if it exists
            string resultsPath = Path.Combine(Application.persistentDataPath, "PerformanceTestResults.json");
            if (File.Exists(resultsPath))
            {
                File.Delete(resultsPath);
                Debug.Log("All benchmark results have been cleared.");
            }
            else
            {
                Debug.Log("No benchmark results found to clear.");
            }
        }
        
        private static List<PerformanceTestResult> LoadTestResults()
        {
            string resultsPath = Path.Combine(Application.persistentDataPath, "PerformanceTestResults.json");
            
            if (!File.Exists(resultsPath))
            {
                return new List<PerformanceTestResult>();
            }
            
            try
            {
                string json = File.ReadAllText(resultsPath);
                var run = JsonUtility.FromJson<Run>(json);
                return run?.Results ?? new List<PerformanceTestResult>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load performance test results: {ex.Message}");
                return new List<PerformanceTestResult>();
            }
        }
        
        [Serializable]
        private class Run
        {
            public List<PerformanceTestResult> Results;
        }
    }
} 