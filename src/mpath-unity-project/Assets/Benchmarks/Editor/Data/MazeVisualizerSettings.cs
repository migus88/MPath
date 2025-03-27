using UnityEngine;

namespace Benchmarks.Editor.Data
{
    [CreateAssetMenu(fileName = "MazeVisualizerSettings", menuName = "Benchmarks/Maze Visualizer Settings", order = 1)]
    public class MazeVisualizerSettings : ScriptableObject
    {
        [Header("Maze Settings")]
        [Tooltip("The texture containing the maze image")]
        public Texture2D MazeTexture;
        
        [Tooltip("Start point for pathfinding (in texture coordinates)")]
        public Vector2Int StartPoint = new Vector2Int(10, 10);
        
        [Tooltip("Destination point for pathfinding (in texture coordinates)")]
        public Vector2Int Destination = new Vector2Int(502, 374);
        
        [Header("Output Settings")]
        [Tooltip("Directory where output images will be saved")]
        public string OutputDirectory = "Assets/Benchmarks/Output";
        
        [Range(1, 5)]
        [Tooltip("Scale factor for the output image")]
        public int ScaleFactor = 2;
    }
} 