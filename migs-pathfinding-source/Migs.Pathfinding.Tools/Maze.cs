using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;
using SkiaSharp;

namespace Migs.Pathfinding.Tools
{
    public unsafe class Maze
    {
        public int Width { get; }

        public int Height { get; }

        public Coordinate Start { get; private set; }
        public Coordinate Destination { get; private set; }
        public Cell[,] Cells => _cells;

        private SKBitmap _bitmap;
        private Cell[,] _cells;
        private Cell* _cellsPtr;
        
        private readonly int _size;

        public Maze(string path, bool createCells = true)
        {
            var file = File.ReadAllBytes(path);

            var bitmap = SKBitmap.Decode(file);
            Width = bitmap.Width;
            Height = bitmap.Height;
            _size = Width * Height;

            _bitmap = new SKBitmap(Width, Height);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _bitmap.SetPixel(x, y, bitmap.GetPixel(x, y));
                }
            }

            if (createCells)
            {
                CreateCells();
            }
        }

        public Maze(Cell[,] cells, int width, int height, Coordinate start = default, Coordinate destination = default)
        {
            _cells = cells;
            Width = width;
            Height = height;
            Start = start;
            Destination = destination;
            _size = Width * Height;

            CreateBitmap();
        }

        private void CreateBitmap()
        {
            _bitmap = new SKBitmap(Width, Height);

            for (short x = 0; x < Width; x++)
            {
                for (short y = 0; y < Height; y++)
                {
                    var cell = _cells[x,y];

                    var pixel = SKColors.White;

                    if (!cell.IsWalkable || cell.IsOccupied)
                    {
                        pixel = SKColors.Black;
                    }

                    var coordinates = new Coordinate(x, y);

                    if (Start == coordinates)
                    {
                        pixel = SKColors.Red;
                    }
                    else if (Destination == coordinates)
                    {
                        pixel = SKColors.Blue;
                    }

                    _bitmap.SetPixel(x, y, pixel);
                }
            }
        }

        public void SetStart(Coordinate coordinate)
        {
            Start = coordinate;
            _bitmap.SetPixel((int) coordinate.X, (int) coordinate.Y, SKColors.Red);
        }

        public void SetDestination(Coordinate coordinate)
        {
            Start = coordinate;
            _bitmap.SetPixel((int) coordinate.X, (int) coordinate.Y, SKColors.Blue);
        }

        public void SetClosed(Coordinate coordinate)
        {
            _bitmap.SetPixel((int) coordinate.X, (int) coordinate.Y, SKColors.Gray);
        }

        public void AddPath(Coordinate[] coordinates)
        {
            foreach (var coordinate in coordinates)
            {
                if (coordinate == Start || coordinate == Destination)
                {
                    continue;
                }

                _bitmap.SetPixel((int) coordinate.X, (int) coordinate.Y, SKColors.Green);
            }
        }

        public void SaveImage(string path, int sizeMultiplier = 1)
        {
            if (sizeMultiplier == 1)
            {
                SaveToFile(_bitmap, path);
                return;
            }

            var bitmap = new SKBitmap(Width * sizeMultiplier, Height * sizeMultiplier);

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var color = _bitmap.GetPixel(x, y);

                    for (var mX = 0; mX < sizeMultiplier; mX++)
                    {
                        for (var mY = 0; mY < sizeMultiplier; mY++)
                        {
                            bitmap.SetPixel(x * sizeMultiplier + mX, y * sizeMultiplier + mY, color);
                        }
                    }
                }
            }

            SaveToFile(bitmap, path);
        }

        private void SaveToFile(SKBitmap image, string path)
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
                
            data.SaveTo(stream);
        }

        public void CreateCells()
        {
            _cells = new Cell[Width, Height];
            
            fixed(Cell* ptr = _cells)
            {
                _cellsPtr = ptr;
            }
            
            for (short y = 0; y < Height; y++)
            {
                for (short x = 0; x < Width; x++)
                {
                    var pixel = _bitmap.GetPixel(x, y);

                    ref var cell = ref _cells[x,y];
                    cell.IsWalkable = IsWalkable(pixel);
                    cell.Coordinate = new Coordinate(x, y);

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

        public bool IsWalkable(int x, int y) => IsWalkable(_bitmap.GetPixel(x, y));
        public bool IsBlocked(int x, int y) => IsBlocked(_bitmap.GetPixel(x, y));
        public bool IsStart(int x, int y) => IsStart(_bitmap.GetPixel(x, y));
        public bool IsDestination(int x, int y) => IsDestination(_bitmap.GetPixel(x, y));
        public bool IsPath(int x, int y) => IsPath(_bitmap.GetPixel(x, y));

        private bool IsWalkable(SKColor pixel) => !IsBlocked(pixel);
        private bool IsBlocked(SKColor pixel) => pixel == SKColors.Black;
        private bool IsStart(SKColor pixel) => pixel == SKColors.Red;
        private bool IsDestination(SKColor pixel) => pixel == SKColors.Blue;
        private bool IsPath(SKColor pixel) => pixel == SKColors.Green;
    }
}