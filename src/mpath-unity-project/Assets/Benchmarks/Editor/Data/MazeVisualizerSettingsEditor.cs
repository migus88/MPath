using System.IO;
using Benchmarks.Editor.Helpers;
using Benchmarks.Editor.Runners;
using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor.Data
{
    [CustomEditor(typeof(MazeVisualizerSettings))]
    public class MazeVisualizerSettingsEditor : UnityEditor.Editor
    {
        private MazeVisualizerSettings _settings;
        private UnityMaze _maze;
        
        private void OnEnable()
        {
            _settings = (MazeVisualizerSettings)target;
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Path Visualization", EditorStyles.boldLabel);
            
            // Check if texture is assigned
            if (_settings.MazeTexture == null)
            {
                EditorGUILayout.HelpBox("Please assign a maze texture before rendering paths.", MessageType.Warning);
                return;
            }
            
            // Ensure output directory exists
            if (!string.IsNullOrEmpty(_settings.OutputDirectory) && !Directory.Exists(_settings.OutputDirectory))
            {
                if (GUILayout.Button("Create Output Directory"))
                {
                    Directory.CreateDirectory(_settings.OutputDirectory);
                    AssetDatabase.Refresh();
                }
                EditorGUILayout.HelpBox("Output directory does not exist. Click button above to create it.", MessageType.Warning);
                return;
            }
            
            // Create a copy of the texture in memory
            string texturePath = AssetDatabase.GetAssetPath(_settings.MazeTexture);
            if (string.IsNullOrEmpty(texturePath))
            {
                EditorGUILayout.HelpBox("Cannot find path for texture. Make sure it's saved in the project.", MessageType.Error);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Render All Paths"))
            {
                RenderAllPaths();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Buttons for individual pathfinders
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Render MPath"))
            {
                RenderSinglePath(new MPathMazeBenchmarkRunner(LoadMaze()));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Render RoyT A*"))
            {
                RenderSinglePath(new RoyTAStarMazeBenchmarkRunner(LoadMaze()));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Render AStar.Lite"))
            {
                RenderSinglePath(new AStarLiteBenchmarkRunner(LoadMaze()));
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void RenderAllPaths()
        {
            // Load maze
            var maze = LoadMaze();
            if (maze == null) return;
            
            // Create runners
            var runners = new BaseMazeBenchmarkRunner[]
            {
                new MPathMazeBenchmarkRunner(LoadMaze()),
                new RoyTAStarMazeBenchmarkRunner(LoadMaze()),
                new AStarLiteBenchmarkRunner(LoadMaze())
            };
            
            // Render path for each runner
            foreach (var runner in runners)
            {
                RenderSinglePath(runner);
            }
        }
        
        private void RenderSinglePath(BaseMazeBenchmarkRunner runner)
        {
            try
            {
                // Create filename
                var baseName = Path.GetFileNameWithoutExtension(_settings.MazeTexture.name);
                var outputFileName = $"{baseName}_{runner.AlgorithmName}.png";
                var outputPath = Path.Combine(_settings.OutputDirectory, outputFileName);
                
                // Render path
                runner.RenderPath(outputPath, _settings.ScaleFactor, _settings.StartPoint, _settings.Destination);
                
                // Refresh asset database
                AssetDatabase.Refresh();
                
                // Log success
                Debug.Log($"Path rendered for {runner.AlgorithmName} and saved to {outputPath}");
                
                // Select the file in the project
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to render path: {ex.Message}", "OK");
                Debug.LogException(ex);
            }
        }
        
        private UnityMaze LoadMaze()
        {
            try
            {
                // Get texture path
                var texturePath = AssetDatabase.GetAssetPath(_settings.MazeTexture);
                if (string.IsNullOrEmpty(texturePath))
                {
                    throw new System.Exception("Cannot find path for texture");
                }
                
                // Convert project path to absolute path
                var absolutePath = Path.Combine(Application.dataPath, texturePath.Substring("Assets/".Length));
                
                // Create maze from image
                return new UnityMaze(absolutePath);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to load maze: {ex.Message}", "OK");
                Debug.LogException(ex);
                return null;
            }
        }
    }
} 