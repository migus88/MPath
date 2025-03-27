using System.IO;
using UnityEditor;
using UnityEngine;

namespace Benchmarks.Editor.Helpers
{
    public static class MazeTestUtility
    {
        [MenuItem("Tools/Benchmarks/Test Maze Image")]
        public static void TestMazeImage()
        {
            // Prompt user to select a PNG file
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Maze PNG Image",
                "",
                new[] { "PNG files", "png" });
                
            if (string.IsNullOrEmpty(sourcePath))
            {
                // User canceled
                return;
            }
            
            // Verify file extension is PNG
            if (!sourcePath.ToLower().EndsWith(".png"))
            {
                EditorUtility.DisplayDialog("Invalid File Type", "Please select a PNG file.", "OK");
                return;
            }
            
            try
            {
                // Create a maze from the image
                var maze = new UnityMaze(sourcePath);
                
                // Prompt user for save location
                string destinationPath = EditorUtility.SaveFilePanel(
                    "Save Scaled Maze Image",
                    Path.GetDirectoryName(sourcePath),
                    $"{Path.GetFileNameWithoutExtension(sourcePath)}_2x.png",
                    "png");
                    
                if (string.IsNullOrEmpty(destinationPath))
                {
                    // User canceled
                    return;
                }
                
                // Save the maze as a 2x scaled image
                maze.SaveImage(destinationPath, sizeMultiplier: 1);
                
                // Open the output file in the file explorer
                EditorUtility.RevealInFinder(destinationPath);
                
                Debug.Log($"Maze image processed and saved as {destinationPath}");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to process the maze image: {ex.Message}", "OK");
                Debug.LogException(ex);
            }
        }
    }
} 