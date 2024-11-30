using System.Diagnostics;
using System.Reflection;
using Migs.Pathfinding.Core;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;
using Migs.Pathfinding.Tests.Implementations;
using Migs.Pathfinding.Tools;

namespace Migs.Pathfinding.Tests
{
    [TestFixture]
    public unsafe class TerrainPathfinderTests
    {
        [Test]
        public void SingleAgent_AgentSize_Success_Test()
        {
            var maze = new Maze("Maze/Conditions/001.png");
            
            var start = maze.Start;
            var destination = maze.Destination;
            var agent = new Agent { Size = 2 };

            var aStar = new Pathfinder(maze.Cells);
            

            var result = aStar.GetPath(agent, start, destination);
            maze.AddPath(result.Path.ToArray());

            if (!Directory.Exists("Results/"))
            {
                Directory.CreateDirectory("Results/");
            }
            
            maze.SaveImage("Results/001.png", 100);
        }
        
        [Test]
        public void Cavern_Test()
        {
            var maze = new Maze("Maze/Conditions/000.gif");
            var start = new Coordinate(10, 10);
            var destination = new Coordinate(502, 374);
            
            maze.SetStart(start);
            maze.SetDestination(destination);
            var agent = new Agent { Size = 1 };
            
            var aStar = new Pathfinder(maze.Cells);
            
            //Jitting for more accurate stopwatch result
            aStar.GetPath(agent, start, destination);

            var sw = new Stopwatch();
            sw.Start();
            var result = aStar.GetPath(agent, start, destination);
            sw.Stop();

            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
            
            var closedCount = 0;
            
            var propertyInfo = typeof(Cell).GetProperty("IsClosed", BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var cell in maze.Cells)
            {
                if ((bool)(propertyInfo?.GetValue(cell) ?? false))
                {
                    closedCount++;
                    maze.SetClosed(cell.Coordinate);
                }
            }

            Console.WriteLine($"Closed count: {closedCount}");
            
            if(result.IsPathFound)
            {
                maze.AddPath(result.Path.ToArray());
            }

            if (!Directory.Exists("Results/"))
            {
                Directory.CreateDirectory("Results/");
            }
            
            maze.SaveImage("Results/000.png", 4);
            
            Assert.IsTrue(result.IsPathFound);
        }
    }
}