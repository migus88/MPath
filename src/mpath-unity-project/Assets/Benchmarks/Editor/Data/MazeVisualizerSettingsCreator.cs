using System.IO;
using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor.Data
{
    public static class MazeVisualizerSettingsCreator
    {
        [MenuItem("Tools/Benchmarks/Create Visualizer Settings")]
        public static void CreateMazeVisualizerSettings()
        {
            // Create the settings asset
            var settings = ScriptableObject.CreateInstance<MazeVisualizerSettings>();
            
            // Create directory if it doesn't exist
            var directory = "Assets/Benchmarks/Settings";
            if (!AssetDatabase.IsValidFolder(directory))
            {
                var parentFolder = Path.GetDirectoryName(directory);
                var newFolderName = Path.GetFileName(directory);
                AssetDatabase.CreateFolder(parentFolder, newFolderName);
                AssetDatabase.Refresh();
            }
            
            // Create the asset
            var assetPath = $"{directory}/MazeVisualizerSettings.asset";
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select the created asset
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            
            Debug.Log($"Maze Visualizer Settings created at {assetPath}");
        }
    }
} 