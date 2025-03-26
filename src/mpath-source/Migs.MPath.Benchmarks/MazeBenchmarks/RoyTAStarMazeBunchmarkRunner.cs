using Migs.MPath.Core.Data;
using Migs.MPath.Tools;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using Path = Roy_T.AStar.Paths.Path;

namespace Migs.MPath.Benchmarks.MazeBenchmarks;

public class RoyTAStarMazeBenchmarkRunner : BaseMazeBenchmarkRunner
{
    protected override string ResultImageName => nameof(RoyTAStarMazeBenchmarkRunner);
        
    private readonly PathFinder _pathFinder = new PathFinder();
    private Node[,] _nodes;
    private Grid _grid;
    private Path _path;
        
    public override void Init(Maze maze)
    {
        base.Init(maze);
            
        _nodes = new Node[_maze.Width, _maze.Height];
        PopulateNodes();

        _grid = Grid.CreateGridFrom2DArrayOfNodes(_nodes);
    }

    public override void FindPath((int x, int y) start, (int x, int y) destination)
    {
        _path = GetPath(start, destination);
    }

    public override void RenderPath((int x, int y) start, (int x, int y) destination)
    {
        var result = GetPath(start, destination);
        
        // Create coordinates array to include start point 
        var coordinates = new Coordinate[result.Edges.Count + 1];
        coordinates[0] = new Coordinate(start.x, start.y);
        
        for (var i = 0; i < result.Edges.Count; i++)
        {
            var coordinate = result.Edges[i].End.Position;
            coordinates[i + 1] = new Coordinate((int)coordinate.X, (int)coordinate.Y);
        }
            
        _maze.AddPath(coordinates);
            
        SaveMazeResultAsImage();
    }

    private Path GetPath((int x, int y) start, (int x, int y) destination)
    {
        var startPoint = new GridPosition(start.x, start.y);
        var endPoint = new GridPosition(destination.x, destination.y);
            
        var path = _pathFinder.FindPath(startPoint, endPoint, _grid);

        if (path.Edges.Count == 0)
        {
            throw new Exception("Path not found");
        }

        return path;
    }
        
    private void PopulateNodes()
    {
        var cells = _maze.Cells;
        
        var height = _maze.Height;
        var width = _maze.Width;
            
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _nodes[x, y] = new Node(new Position(x, y));
            }
        }
            
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Connect(x, y, cells);
            }
        }
    }

    private void Connect(int x, int y, Cell[,] cells)
    {
        var node = _nodes[x, y];

        var neighbors = new (int, int)[]
        {
            (x + 1, y),
            (x - 1, y),
            (x, y + 1),
            (x, y - 1),
            (x + 1, y + 1),
            (x - 1, y - 1),
            (x + 1, y - 1),
            (x - 1, y + 1)
        };

        var velocity = Velocity.FromMetersPerSecond(1);
        
        var height = _maze.Height;
        var width = _maze.Width;

        foreach (var neighbor in neighbors)
        {
            if (neighbor.Item1 < 0 || neighbor.Item1 >= width || neighbor.Item2 < 0 ||
                neighbor.Item2 >= height)
            {
                continue;
            }

            var cell = cells[neighbor.Item1, neighbor.Item2];
            if (cell.IsOccupied || !cell.IsWalkable)
            {
                continue;
            }
                
            node.Connect(_nodes[neighbor.Item1, neighbor.Item2], velocity);
        }
    }
} 