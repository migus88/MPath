using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.MazeBenchmarks;

public abstract class BaseMazeBenchmarkRunner : IMazeBenchmarkRunner
{
    protected const string ResultsPath = "Results/";
        
    protected abstract string ResultImageName { get; }
        
    protected Maze _maze;

    public virtual void Init(Maze maze)
    {
        _maze = maze ?? new Maze("Mazes/cavern.png");
    }

    public abstract void FindPath((int x, int y) start, (int x, int y) destination);

    public abstract void RenderPath((int x, int y) start, (int x, int y) destination);

    protected void SaveMazeResultAsImage()
    {
        if (!Directory.Exists(ResultsPath))
        {
            Directory.CreateDirectory(ResultsPath);
        }

        var imagePath = $"{ResultsPath}{ResultImageName}.png";
            
        _maze.SaveImage(imagePath, 4);
    }
}