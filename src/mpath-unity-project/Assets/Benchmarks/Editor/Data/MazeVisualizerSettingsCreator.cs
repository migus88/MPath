using System.IO;
using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor.Data
{
    public static class MazeVisualizerSettingsCreator
    {
        [MenuItem("Tools/Benchmarks/Create Maze Visualizer Settings")]
        public static void CreateMazeVisualizerSettings()
        {
            // Create the settings asset
            var settings = ScriptableObject.CreateInstance<MazeVisualizerSettings>();
            
            // Create directory if it doesn't exist
            string directory = "Assets/Benchmarks/Settings";
            if (!AssetDatabase.IsValidFolder(directory))
            {
                string parentFolder = Path.GetDirectoryName(directory);
                string newFolderName = Path.GetFileName(directory);
                AssetDatabase.CreateFolder(parentFolder, newFolderName);
                AssetDatabase.Refresh();
            }
            
            // Create the asset
            string assetPath = $"{directory}/MazeVisualizerSettings.asset";
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