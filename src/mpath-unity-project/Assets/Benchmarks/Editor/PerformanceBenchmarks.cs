using System.Collections.Generic;
using System.IO;
using System.Linq;
using Benchmarks.Editor.Helpers;
using Benchmarks.Editor.Runners;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace Benchmarks.Editor
{
    public class PerformanceBenchmarks
    {
        private const string MazesDirectory = "Assets/Benchmarks/Mazes";
        private static readonly Vector2Int StartPoint = new Vector2Int(10, 10);
        private static readonly Vector2Int Destination = new Vector2Int(502, 374);
        
        [Test, Performance]
        public void RunAllPathfindingBenchmarks()
        {
            foreach (var mazePath in GetMazeImagePaths())
            {
                var maze = LoadMaze(mazePath);
                if (maze == null) continue;
                
                var fileName = Path.GetFileNameWithoutExtension(mazePath);
                RunAllBenchmarksOnMaze(maze, fileName);
            }
        }
        
        [Test, Performance]
        public void MPathBenchmark()
        {
            foreach (var mazePath in GetMazeImagePaths())
            {
                var maze = LoadMaze(mazePath);
                if (maze == null) continue;
                
                var fileName = Path.GetFileNameWithoutExtension(mazePath);
                RunSingleBenchmarkOnMaze<MPathMazeBenchmarkRunner>(maze, fileName);
            }
        }
        
        [Test, Performance]
        public void RoyTAStarBenchmark()
        {
            foreach (var mazePath in GetMazeImagePaths())
            {
                var maze = LoadMaze(mazePath);
                if (maze == null) continue;
                
                var fileName = Path.GetFileNameWithoutExtension(mazePath);
                RunSingleBenchmarkOnMaze<RoyTAStarMazeBenchmarkRunner>(maze, fileName);
            }
        }
        
        [Test, Performance]
        public void AStarLiteBenchmark()
        {
            foreach (var mazePath in GetMazeImagePaths())
            {
                var maze = LoadMaze(mazePath);
                if (maze == null) continue;
                
                var fileName = Path.GetFileNameWithoutExtension(mazePath);
                RunSingleBenchmarkOnMaze<AStarLiteBenchmarkRunner>(maze, fileName);
            }
        }
        
        private void RunAllBenchmarksOnMaze(UnityMaze maze, string mazeName)
        {
            var runners = new BaseMazeBenchmarkRunner[]
            {
                new MPathMazeBenchmarkRunner(maze),
                new RoyTAStarMazeBenchmarkRunner(maze),
                new AStarLiteBenchmarkRunner(maze)
            };
            
            foreach (var runner in runners)
            {
                string runnerName = runner.GetType().Name.Replace("MazeBenchmarkRunner", "");
                string testName = $"{runnerName}_{mazeName}";
                
                Measure.Method(() =>
                {
                    runner.FindPath(StartPoint, Destination);
                })
                .SampleGroup(testName)
                .WarmupCount(3)
                .MeasurementCount(10)
                .Run();
            }
        }
        
        private void RunSingleBenchmarkOnMaze<T>(UnityMaze maze, string mazeName) where T : BaseMazeBenchmarkRunner
        {
            var runner = (BaseMazeBenchmarkRunner)System.Activator.CreateInstance(typeof(T), new object[] { maze });
            string runnerName = runner.GetType().Name.Replace("MazeBenchmarkRunner", "");
            string testName = $"{runnerName}_{mazeName}";
            
            Measure.Method(() =>
            {
                runner.FindPath(StartPoint, Destination);
            })
            .SampleGroup(testName)
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }
        
        private string[] GetMazeImagePaths()
        {
            return Directory.GetFiles(MazesDirectory, "*.png")
                .Concat(Directory.GetFiles(MazesDirectory, "*.gif"))
                .Concat(Directory.GetFiles(MazesDirectory, "*.jpg"))
                .ToArray();
        }
        
        private UnityMaze LoadMaze(string path)
        {
            try
            {
                return new UnityMaze(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load maze at {path}: {ex.Message}");
                return null;
            }
        }
    }
} 