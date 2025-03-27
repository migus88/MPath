using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Data;
using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor
{
    public class BenchmarkResultsWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<AlgorithmResult> _results = new List<AlgorithmResult>();
        private string[] _mazeNames;
        private string[] _algorithmNames;
        private bool _initialized;
        private float _maxTime;

        private Color[] _algorithmColors =
        {
            new Color(0.34f, 0.73f, 0.56f), // Green
            new Color(0.93f, 0.51f, 0.39f), // Orange
            new Color(0.46f, 0.61f, 0.80f), // Blue
        };

        [MenuItem("Tools/Benchmarks/View Results")]
        public static void ShowWindow()
        {
            var window = GetWindow<BenchmarkResultsWindow>("Benchmark Results");
            window.minSize = new Vector2(600, 400);
            window.maxSize = new Vector2(1000, 800);
            window.Show();
        }

        private void OnGUI()
        {
            if (!_initialized)
            {
                InitializeData();
            }

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox("No benchmark results available. Run benchmarks first.", MessageType.Info);
                if (GUILayout.Button("Run All Benchmarks"))
                {
                    RunBenchmarks();
                }
                return;
            }

            DrawToolbar();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Benchmark Results (Lower is better)", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            DrawLegend();
            EditorGUILayout.Space(20);

            // Draw graph for each maze
            foreach (var mazeName in _mazeNames)
            {
                DrawMazeGraph(mazeName);
                EditorGUILayout.Space(30);
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("Run All Benchmarks", EditorStyles.toolbarButton))
            {
                RunBenchmarks();
            }
            
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                InitializeData();
                Repaint();
            }
            
            if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton))
            {
                BenchmarkResultsUtility.ExportResults();
            }
            
            if (GUILayout.Button("Clear Results", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("Clear Results", "Are you sure you want to clear all benchmark results?", "Yes", "No"))
                {
                    BenchmarkResultsUtility.ClearResults();
                    InitializeData();
                    Repaint();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLegend()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Space(20);
            
            for (int i = 0; i < _algorithmNames.Length; i++)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
                
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(16, 16), _algorithmColors[i % _algorithmColors.Length]);
                
                EditorGUILayout.LabelField(_algorithmNames[i]);
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMazeGraph(string mazeName)
        {
            EditorGUILayout.LabelField(mazeName, EditorStyles.boldLabel);
            
            Rect graphRect = GUILayoutUtility.GetRect(0, 100, GUILayout.ExpandWidth(true));
            
            // Draw background
            EditorGUI.DrawRect(graphRect, new Color(0.2f, 0.2f, 0.2f));
            
            // Draw grid lines
            DrawGridLines(graphRect);
            
            // Draw bars
            DrawBars(graphRect, mazeName);
            
            // Draw scale labels
            DrawScaleLabels(graphRect);
        }

        private void DrawGridLines(Rect graphRect)
        {
            // Horizontal grid lines
            int divisions = 5;
            float y;
            
            for (int i = 0; i <= divisions; i++)
            {
                y = graphRect.y + graphRect.height * i / divisions;
                
                // Draw line
                EditorGUI.DrawRect(new Rect(graphRect.x, y, graphRect.width, 1), new Color(0.4f, 0.4f, 0.4f, 0.5f));
            }
        }

        private void DrawBars(Rect graphRect, string mazeName)
        {
            var mazeResults = _results.Where(r => r.MazeName == mazeName).ToList();
            if (mazeResults.Count == 0)
                return;
                
            float barWidth = 30;
            float groupWidth = (_algorithmNames.Length * (barWidth + 5)) + 20;
            float startX = graphRect.x + (graphRect.width - groupWidth) / 2;
            
            for (int i = 0; i < _algorithmNames.Length; i++)
            {
                var result = mazeResults.FirstOrDefault(r => r.AlgorithmName == _algorithmNames[i]);
                if (result == null)
                    continue;
                    
                float x = startX + (i * (barWidth + 5));
                float normalizedHeight = result.MedianTime / _maxTime;
                float height = normalizedHeight * graphRect.height;
                float y = graphRect.y + graphRect.height - height;
                
                Rect barRect = new Rect(x, y, barWidth, height);
                
                // Draw bar
                EditorGUI.DrawRect(barRect, _algorithmColors[i % _algorithmColors.Length]);
                
                // Draw label
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.normal.textColor = Color.white;
                
                EditorGUI.LabelField(
                    new Rect(x, y - 20, barWidth, 20),
                    $"{result.MedianTime:F2}ms", 
                    labelStyle);
            }
        }

        private void DrawScaleLabels(Rect graphRect)
        {
            int divisions = 5;
            float y;
            float value;
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.alignment = TextAnchor.MiddleRight;
            
            for (int i = 0; i <= divisions; i++)
            {
                y = graphRect.y + graphRect.height * (divisions - i) / divisions;
                value = _maxTime * i / divisions;
                
                EditorGUI.LabelField(
                    new Rect(graphRect.x - 50, y - 8, 45, 16),
                    $"{value:F1}ms", 
                    labelStyle);
            }
        }

        private void InitializeData()
        {
            _results.Clear();
            
            var testResults = LoadTestResults();
            foreach (var result in testResults)
            {
                foreach (var sampleGroup in result.SampleGroups)
                {
                    string[] parts = sampleGroup.Name.Split('_');
                    if (parts.Length != 2)
                        continue;
                        
                    string algorithm = parts[0];
                    string maze = parts[1];
                    
                    var samples = sampleGroup.Samples;
                    if (samples.Count == 0)
                        continue;
                        
                    // Convert to milliseconds
                    float median = (float)(GetMedian(samples) / 1000000f);
                    
                    _results.Add(new AlgorithmResult
                    {
                        AlgorithmName = algorithm,
                        MazeName = maze,
                        MedianTime = median
                    });
                }
            }
            
            // Extract unique maze names
            _mazeNames = _results
                .Select(r => r.MazeName)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();
                
            // Extract unique algorithm names
            _algorithmNames = _results
                .Select(r => r.AlgorithmName)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();
                
            // Find maximum time for scaling
            _maxTime = _results.Count > 0 ? _results.Max(r => r.MedianTime) * 1.1f : 100f;
            
            _initialized = true;
        }

        private float GetMedian(List<double> samples)
        {
            var sortedSamples = samples.OrderBy(d => d).ToList();
            int count = sortedSamples.Count;
            
            if (count == 0)
                return 0;
                
            if (count % 2 == 0)
                return (float)((sortedSamples[count / 2 - 1] + sortedSamples[count / 2]) / 2);
                
            return (float)sortedSamples[count / 2];
        }

        private void RunBenchmarks()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
            ShowNotification(new GUIContent("Test Runner opened. Run 'PerformanceBenchmarks' tests."));
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

        private class AlgorithmResult
        {
            public string AlgorithmName;
            public string MazeName;
            public float MedianTime;
        }
    }
} 