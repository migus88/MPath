using System;
using System.IO;
using Migs.MPath.Core.Data;
using UnityEngine;

namespace Benchmarks.Editor.Helpers
{
    /// <summary>
    /// Unity-friendly implementation of the Maze class for editor use
    /// </summary>
    public class UnityMaze
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Coordinate Start { get; private set; }
        public Coordinate Destination { get; private set; }
        public Cell[,] Cells { get; private set; }

        private Texture2D _texture;
        private Color32[] _pixelData;

        // Color constants to match the original implementation
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 Blue = new Color32(0, 0, 255, 255);
        private static readonly Color32 Green = new Color32(0, 255, 0, 255);
        private static readonly Color32 Gray = new Color32(128, 128, 128, 255);

        /// <summary>
        /// Create a new maze from an image file
        /// </summary>
        public UnityMaze(string path, bool createCells = true)
        {
            // Check if the file exists
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Maze image not found at path: {path}");
            }

            // Read the file bytes
            byte[] fileData = File.ReadAllBytes(path);
            
            // Create a new texture and load the image
            _texture = new Texture2D(2, 2);
            if (!_texture.LoadImage(fileData))
            {
                throw new Exception($"Failed to load image at path: {path}");
            }

            Width = _texture.width;
            Height = _texture.height;
            
            // Read all pixel data
            _pixelData = _texture.GetPixels32();
            
            if (createCells)
            {
                CreateCells();
            }
        }

        /// <summary>
        /// Create a maze from existing cells
        /// </summary>
        public UnityMaze(Cell[,] cells, int width, int height, Coordinate start = default, Coordinate destination = default)
        {
            Cells = cells;
            Width = width;
            Height = height;
            Start = start;
            Destination = destination;

            CreateTexture();
        }

        /// <summary>
        /// Create cells from the loaded texture
        /// </summary>
        private void CreateCells()
        {
            Cells = new Cell[Width, Height];
            
            for (short y = 0; y < Height; y++)
            {
                for (short x = 0; x < Width; x++)
                {
                    // Get the pixel color
                    var pixel = GetPixel(x, y);
                    
                    // Create a new cell
                    ref var cell = ref Cells[x, y];
                    cell.IsWalkable = IsWalkable(pixel);
                    cell.Coordinate = new Coordinate(x, y);
                    
                    // Set start/destination if this pixel is red/blue
                    if (IsStart(pixel))
                    {
                        Start = new Coordinate(x, y);
                    }
                    else if (IsDestination(pixel))
                    {
                        Destination = new Coordinate(x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Create a texture from cells
        /// </summary>
        private void CreateTexture()
        {
            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            _pixelData = new Color32[Width * Height];
            
            for (short y = 0; y < Height; y++)
            {
                for (short x = 0; x < Width; x++)
                {
                    var cell = Cells[x, y];
                    Color32 pixel = White;
                    
                    if (!cell.IsWalkable || cell.IsOccupied)
                    {
                        pixel = Black;
                    }
                    
                    var coordinates = new Coordinate(x, y);
                    
                    if (Start == coordinates)
                    {
                        pixel = Red;
                    }
                    else if (Destination == coordinates)
                    {
                        pixel = Blue;
                    }
                    
                    SetPixel(x, y, pixel);
                }
            }
            
            ApplyPixelChanges();
        }

        /// <summary>
        /// Set the start point of the maze
        /// </summary>
        public void SetStart(Coordinate coordinate)
        {
            Start = coordinate;
            SetPixel((int)coordinate.X, (int)coordinate.Y, Red);
            ApplyPixelChanges();
        }

        /// <summary>
        /// Set the destination point of the maze
        /// </summary>
        public void SetDestination(Coordinate coordinate)
        {
            Destination = coordinate;
            SetPixel((int)coordinate.X, (int)coordinate.Y, Blue);
            ApplyPixelChanges();
        }

        /// <summary>
        /// Mark a cell as closed (visited)
        /// </summary>
        public void SetClosed(Coordinate coordinate)
        {
            SetPixel((int)coordinate.X, (int)coordinate.Y, Gray);
            ApplyPixelChanges();
        }

        /// <summary>
        /// Add a path to the maze visualization
        /// </summary>
        public void AddPath(Coordinate[] coordinates)
        {
            foreach (var coordinate in coordinates)
            {
                if (coordinate == Start || coordinate == Destination)
                {
                    continue;
                }
                
                SetPixel((int)coordinate.X, (int)coordinate.Y, Green);
            }
            
            ApplyPixelChanges();
        }

        /// <summary>
        /// Save the maze as an image file
        /// </summary>
        public void SaveImage(string path, int sizeMultiplier = 1)
        {
            Texture2D outputTexture;
            
            // If no size multiplier, just save the current texture
            if (sizeMultiplier == 1)
            {
                outputTexture = _texture;
            }
            else
            {
                // Create a larger texture for better visualization
                outputTexture = new Texture2D(Width * sizeMultiplier, Height * sizeMultiplier, TextureFormat.RGBA32, false);
                
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Color32 color = GetPixel(x, y);
                        
                        // Fill in a sizeMultiplier x sizeMultiplier block with this color
                        for (int mY = 0; mY < sizeMultiplier; mY++)
                        {
                            for (int mX = 0; mX < sizeMultiplier; mX++)
                            {
                                int outX = x * sizeMultiplier + mX;
                                int outY = y * sizeMultiplier + mY;
                                int outIndex = outY * (Width * sizeMultiplier) + outX;
                                outputTexture.SetPixel(outX, outY, color);
                            }
                        }
                    }
                }
                
                outputTexture.Apply();
            }
            
            // Encode to PNG and save
            byte[] bytes = outputTexture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            
            Debug.Log($"Saved maze visualization to: {path}");
            
            // Clean up the texture if we created a new one
            if (sizeMultiplier != 1)
            {
                UnityEngine.Object.DestroyImmediate(outputTexture);
            }
        }

        /// <summary>
        /// Apply pixel changes to the texture
        /// </summary>
        private void ApplyPixelChanges()
        {
            _texture.SetPixels32(_pixelData);
            _texture.Apply();
        }

        /// <summary>
        /// Get the color of a pixel at the specified coordinates
        /// </summary>
        private Color32 GetPixel(int x, int y)
        {
            int index = y * Width + x;
            return _pixelData[index];
        }

        /// <summary>
        /// Set the color of a pixel at the specified coordinates
        /// </summary>
        private void SetPixel(int x, int y, Color32 color)
        {
            int index = y * Width + x;
            _pixelData[index] = color;
        }

        public bool IsWalkable(int x, int y) => IsWalkable(GetPixel(x, y));
        public bool IsBlocked(int x, int y) => IsBlocked(GetPixel(x, y));
        public bool IsStart(int x, int y) => IsStart(GetPixel(x, y));
        public bool IsDestination(int x, int y) => IsDestination(GetPixel(x, y));
        public bool IsPath(int x, int y) => IsPath(GetPixel(x, y));

        private bool IsWalkable(Color32 color) => !IsBlocked(color);
        private bool IsBlocked(Color32 color) => ColorEquals(color, Black);
        private bool IsStart(Color32 color) => ColorEquals(color, Red);
        private bool IsDestination(Color32 color) => ColorEquals(color, Blue);
        private bool IsPath(Color32 color) => ColorEquals(color, Green);

        /// <summary>
        /// Compare two colors with some tolerance
        /// </summary>
        private bool ColorEquals(Color32 a, Color32 b, byte tolerance = 2)
        {
            return Math.Abs(a.r - b.r) <= tolerance &&
                   Math.Abs(a.g - b.g) <= tolerance &&
                   Math.Abs(a.b - b.b) <= tolerance;
        }
    }
}