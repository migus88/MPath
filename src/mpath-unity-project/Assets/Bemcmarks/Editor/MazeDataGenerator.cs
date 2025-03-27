using UnityEngine;
using UnityEditor;
using System.IO;

namespace Bemcmarks.Editor
{
    /// <summary>
    /// Editor utility to generate maze data from image files
    /// </summary>
    public static class MazeDataGenerator
    {
        [MenuItem("Tools/Benchmarks/Generate Maze Data")]
        public static void GenerateMazeData()
        {
            string mazeImagePath = EditorUtility.OpenFilePanel("Select Maze Image", "", "gif,png,jpg");
            if (string.IsNullOrEmpty(mazeImagePath))
                return;
                
            try
            {
                // Load the maze from the image using our Unity-friendly implementation
                var maze = new UnityMaze(mazeImagePath);
                
                // Create a new maze data asset
                var mazeDataAsset = ScriptableObject.CreateInstance<MazeDataAsset>();
                mazeDataAsset.Width = maze.Width;
                mazeDataAsset.Height = maze.Height;
                mazeDataAsset.IsWalkable = new bool[maze.Width * maze.Height];
                
                // Populate the walkable array
                for (int y = 0; y < maze.Height; y++)
                {
                    for (int x = 0; x < maze.Width; x++)
                    {
                        var cell = maze.Cells[x, y];
                        int index = y * maze.Width + x;
                        mazeDataAsset.IsWalkable[index] = cell.IsWalkable && !cell.IsOccupied;
                    }
                }
                
                // Save as a scriptable object
                SaveMazeData(mazeDataAsset, Path.GetFileNameWithoutExtension(mazeImagePath));
                
                Debug.Log($"Successfully generated maze data from {Path.GetFileName(mazeImagePath)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to generate maze data: {ex.Message}");
            }
        }
        
        private static void SaveMazeData(MazeDataAsset mazeDataAsset, string name)
        {
            // Create directory if it doesn't exist
            string directory = "Assets/Bemcmarks/MazeData";
            string fullDirectory = Path.Combine(Application.dataPath, "Bemcmarks", "MazeData");
            if (!Directory.Exists(fullDirectory))
            {
                Directory.CreateDirectory(fullDirectory);
                AssetDatabase.Refresh();
            }
            
            // Save the asset
            string assetPath = $"{directory}/{name}MazeData.asset";
            AssetDatabase.CreateAsset(mazeDataAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select the new asset
            Selection.activeObject = mazeDataAsset;
            EditorGUIUtility.PingObject(mazeDataAsset);
        }
    }
} 