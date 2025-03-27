using UnityEngine;

namespace Bemcmarks
{
    /// <summary>
    /// ScriptableObject to hold maze data
    /// </summary>
    [CreateAssetMenu(fileName = "NewMazeData", menuName = "Benchmarks/Maze Data Asset", order = 1)]
    public class MazeDataAsset : ScriptableObject
    {
        /// <summary>
        /// Width of the maze
        /// </summary>
        public int Width;
        
        /// <summary>
        /// Height of the maze
        /// </summary>
        public int Height;
        
        /// <summary>
        /// Flattened 2D array for walkability information
        /// </summary>
        public bool[] IsWalkable;
        
        /// <summary>
        /// Check if a cell at specified coordinates is walkable
        /// </summary>
        public bool IsCellWalkable(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
            
            int index = y * Width + x;
            return index < IsWalkable.Length ? IsWalkable[index] : false;
        }
    }
} 