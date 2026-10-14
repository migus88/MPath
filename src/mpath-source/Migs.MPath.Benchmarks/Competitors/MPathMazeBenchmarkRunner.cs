using System.Reflection;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Competitors;

public class MPathMazeBenchmarkRunner : BaseMazeBenchmarkRunner
{
    protected override string ResultImageName => nameof(MPathMazeBenchmarkRunner);
        
    private Pathfinder _pathfinder;

    public override void Init(Maze maze)
    {
        base.Init(maze);

        _pathfinder = new Pathfinder(_maze.Cells);
    }

    public override void FindPath((int x, int y) start, (int x, int y) destination)
    {
        if (_pathfinder == null)
        {
            return;
        }
        using var result = _pathfinder.GetPath((Coordinate)start, (Coordinate)destination);

        if (!result.IsSuccess)
        {
            throw new Exception("Path not found");
        }
    }

    public override void RenderPath((int x, int y) start, (int x, int y) destination)
    {
        var result = _pathfinder.GetPath((Coordinate)start, (Coordinate)destination);
            
        if(!result.IsSuccess)
        {
            return;
        }
        
        
        MarkClosedCells();
            
        _maze.AddPath(result.Path.ToArray());
            
        SaveMazeResultAsImage();
    }

    /// <summary>
    /// Marks the cells the last search closed. The search state is internal to <see cref="Pathfinder"/>: one node per
    /// cell at the cell's layout index (<c>x * height + y</c>), part of the last search when its generation matches,
    /// closed when its queue index is -1. It is read by reflection because this is render-only code.
    /// </summary>
    private void MarkClosedCells()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        var nodes = (Array)typeof(Pathfinder).GetField("_nodes", flags).GetValue(_pathfinder);
        var generation = (uint)typeof(Pathfinder).GetField("_generation", flags).GetValue(_pathfinder);
        var nodeType = nodes.GetType().GetElementType();
        var generationField = nodeType.GetField("Generation");
        var queueIndexField = nodeType.GetField("QueueIndex");
        var height = _maze.Cells.GetLength(1);

        for (var x = 0; x < _maze.Cells.GetLength(0); x++)
        {
            for (var y = 0; y < height; y++)
            {
                var node = nodes.GetValue(x * height + y);

                if ((uint)generationField.GetValue(node) == generation && (int)queueIndexField.GetValue(node) == -1)
                {
                    _maze.SetClosed(new Coordinate(x, y));
                }
            }
        }
    }
}
