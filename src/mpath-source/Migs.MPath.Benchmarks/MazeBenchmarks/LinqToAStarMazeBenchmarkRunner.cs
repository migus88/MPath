using System.Drawing;
using Heuristic.Linq;
using Migs.MPath.Core.Data;
using Migs.MPath.Tools;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace Migs.MPath.Benchmarks.MazeBenchmarks;

public class LinqToAStarMazeBenchmarkRunner : BaseMazeBenchmarkRunner
{
    protected override string ResultImageName => nameof(LinqToAStarMazeBenchmarkRunner);
    
    private Rectangle _boundary;
    private Point[] _path;
    private HashSet<Point> _obstacles;
    private readonly Func<Point, int, IEnumerable<Point>> _stepGenerator;
    
    public LinqToAStarMazeBenchmarkRunner()
    {
        // Cache the step generator function that gets possible steps from current step
        // Including both cardinal and diagonal directions
        _stepGenerator = (step, level) => new[]
        {
            // Cardinal directions
            new Point(step.X + 1, step.Y),
            new Point(step.X - 1, step.Y),
            new Point(step.X, step.Y + 1),
            new Point(step.X, step.Y - 1),
            // Diagonal directions
            new Point(step.X + 1, step.Y + 1),
            new Point(step.X - 1, step.Y - 1),
            new Point(step.X + 1, step.Y - 1),
            new Point(step.X - 1, step.Y + 1)
        };
    }
    
    public override void Init(Maze maze)
    {
        base.Init(maze);
        
        // Create boundary rectangle using the maze dimensions
        _boundary = new Rectangle(0, 0, _maze.Width, _maze.Height);
        
        // Initialize obstacles collection
        _obstacles = new HashSet<Point>();
        
        // Populate obstacles with unwalkable cells
        for (var x = 0; x < _maze.Width; x++)
        {
            for (var y = 0; y < _maze.Height; y++)
            {
                var cell = _maze.Cells[x, y];
                if (!cell.IsWalkable || cell.IsOccupied)
                {
                    _obstacles.Add(new Point(x, y));
                }
            }
        }
    }

    public override void FindPath((int x, int y) start, (int x, int y) destination)
    {
        var startPoint = new Point(start.x, start.y);
        var goalPoint = new Point(destination.x, destination.y);
        
        // Initialize A* algorithm
        var queryable = Heuristic.Linq.HeuristicSearch.AStar(startPoint, goalPoint, _stepGenerator);
        
        // Build the LINQ query for A* search
        var solution = from step in queryable.Except(_obstacles)
                       where _boundary.Contains(step)
                       orderby step.GetManhattanDistance(goalPoint) 
                       select step;
        
        // Execute query and get the solution path
        _path = solution.ToArray();
        
        if (_path.Length == 0)
        {
            throw new Exception("Path not found");
        }
    }

    public override void RenderPath((int x, int y) start, (int x, int y) destination)
    {
        var startPoint = new Point(start.x, start.y);
        var goalPoint = new Point(destination.x, destination.y);
        
        // Initialize A* algorithm
        var queryable = Heuristic.Linq.HeuristicSearch.AStar(startPoint, goalPoint, _stepGenerator);
        
        // Build the LINQ query for A* search
        var solution = from step in queryable.Except(_obstacles)
                       where _boundary.Contains(step)
                       orderby step.GetManhattanDistance(goalPoint)
                       select step;
        
        // Execute query and get the solution path
        var result = solution.ToArray();
        
        if (result.Length == 0)
        {
            Console.WriteLine("No path found with LinqToAStar!");
            return;
        }
        
        // Convert Point array to Coordinate array for rendering
        var coordinates = new Coordinate[result.Length];
        for (var i = 0; i < result.Length; i++)
        {
            coordinates[i] = new Coordinate(result[i].X, result[i].Y);
        }
        
        _maze.AddPath(coordinates);
        
        SaveMazeResultAsImage();
    }
} 