using System.Drawing;
using Migs.MPath.Core.Data;
using Migs.MPath.Tools;
using AStar;
using AStar.Options;
using Position = AStar.Position;

namespace Migs.MPath.Benchmarks.MazeBenchmarks;

public class AStarLiteBenchmarkRunner : BaseMazeBenchmarkRunner
{
    protected override string ResultImageName => nameof(AStarLiteBenchmarkRunner);
    
    private WorldGrid _worldGrid;
    private PathFinder _pathFinder;
    private Position[] _path;
    
    public override void Init(Maze maze)
    {
        base.Init(maze);
        
        // Convert cells to a grid usable by AStar
        var grid = new short[_maze.Width, _maze.Height];
        
        for (var x = 0; x < _maze.Width; x++)
        {
            for (var y = 0; y < _maze.Height; y++)
            {
                var cell = _maze.Cells[x, y];
                // In AStar, 0 is blocked, positive values are walkable
                grid[x, y] = cell.IsWalkable && !cell.IsOccupied ? (short)1 : (short)0;
            }
        }
        
        _worldGrid = new WorldGrid(grid);
        var options = new PathFinderOptions
        {
            SearchLimit = 20000
        };
        _pathFinder = new PathFinder(_worldGrid, options);
    }

    public override void FindPath((int x, int y) start, (int x, int y) destination)
    {
        _path = GetPath(start, destination);
        
        if (_path.Length == 0)
        {
            throw new Exception("No path found for AStar!");
        }
    }

    public override void RenderPath((int x, int y) start, (int x, int y) destination)
    {
        var result = GetPath(start, destination);
        
        if (result.Length == 0)
        {
            Console.WriteLine("No path found for AStar!");
            return;
        }
        
        // Convert AStar positions to coordinates for rendering
        var coordinates = new Coordinate[result.Length];
        for (var i = 0; i < result.Length; i++)
        {
            coordinates[i] = new Coordinate(result[i].Row, result[i].Column);
        }
        
        _maze.AddPath(coordinates);
        
        SaveMazeResultAsImage();
    }
    
    private Position[] GetPath((int x, int y) start, (int x, int y) destination)
    {
        try
        {
            // The library can return either Position[] or Point[] 
            // We need to use the correct method for the correct return type
            var path = _pathFinder.FindPath(
                new Position(start.x, start.y),
                new Position(destination.x, destination.y)
            );
            
            return path.Length == 0 ? Array.Empty<Position>() : path;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding path with AStar: {ex.Message}");
            return Array.Empty<Position>();
        }
    }
} 

