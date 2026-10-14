using BenchmarkDotNet.Attributes;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Corner-to-corner <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/> over seeded grids with 20%
/// random walls. Unlike the cavern maze, these grids are mostly open, so the search keeps a wide frontier and the
/// open set (heap) dominates the cost of each expansion.
/// </summary>
[MemoryDiagnoser]
public class RandomGridBenchmarkRunner : IDisposable
{
    private const float WallRatio = 0.2f;
    private const int Seed = 1234;

    private readonly Pathfinder _smallPathfinder;
    private readonly Pathfinder _largePathfinder;

    public RandomGridBenchmarkRunner()
    {
        _smallPathfinder = new Pathfinder(CreateGrid(200, 200));
        _largePathfinder = new Pathfinder(CreateGrid(500, 500));
    }

    [Benchmark]
    public void RandomWalls_200x200()
    {
        using var result = _smallPathfinder.GetPath(new Coordinate(0, 0), new Coordinate(199, 199));

        if (!result.IsSuccess)
        {
            throw new Exception("Path not found");
        }
    }

    [Benchmark]
    public void RandomWalls_500x500()
    {
        using var result = _largePathfinder.GetPath(new Coordinate(0, 0), new Coordinate(499, 499));

        if (!result.IsSuccess)
        {
            throw new Exception("Path not found");
        }
    }

    public void Dispose()
    {
        _smallPathfinder?.Dispose();
        _largePathfinder?.Dispose();
    }

    private static Cell[,] CreateGrid(int width, int height)
    {
        var random = new Random(Seed);
        var cells = new Cell[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var isCorner = (x == 0 && y == 0) || (x == width - 1 && y == height - 1);

                cells[x, y] = new Cell
                {
                    IsWalkable = isCorner || random.NextDouble() >= WallRatio,
                };
            }
        }

        return cells;
    }
}
