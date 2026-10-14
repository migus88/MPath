using System;
using System.Collections.Generic;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tests.Implementations;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Property tests: on seeded random grids, <see cref="Pathfinder.GetPath"/> returns a legal route whose cost is
    /// the cheapest one, and <see cref="Pathfinder.GetReachable"/> reports the cheapest cost of every cell. Both are
    /// checked against <see cref="ReferenceGrid"/>, an independent, deliberately naive Dijkstra over the documented
    /// movement rules (walls, occupied cells, agent footprint, corner blocking, step cost = travel + weight).
    /// </summary>
    [TestFixture]
    public class PathfinderOptimalityTests
    {
        private const int GridWidth = 20;
        private const int GridHeight = 16;
        private const int GridsPerCase = 40;
        private const int QueriesPerGrid = 4;
        private const float Unbounded = 1_000_000f;

        private static readonly (float Straight, float Diagonal)[] Multipliers =
        {
            (1f, 1.41f),
            (1f, 1f),
            (2f, 3f),
            (1f, 2.5f),
            (1f, 0.6f),
            (0.5f, 0.75f)
        };

        private static readonly CornerBlocking[] CornerBlockingPresets =
        {
            CornerBlocking.None,
            CornerBlocking.BetweenBlockedCells,
            CornerBlocking.BetweenWalls,
            CornerBlocking.AnyWall,
            CornerBlocking.All
        };

        public sealed class Scenario
        {
            public bool IsDiagonalMovementEnabled { get; init; }
            public CornerBlocking CornerBlocking { get; init; }
            public bool IsCellWeightEnabled { get; init; }
            public int AgentSize { get; init; }

            public override string ToString()
            {
                var diagonal = IsDiagonalMovementEnabled ? $"Diagonal{CornerBlocking}" : "NoDiagonal";
                var weights = IsCellWeightEnabled ? "Weighted" : "Unweighted";
                return $"{diagonal}_{weights}_Size{AgentSize}";
            }
        }

        private static IEnumerable<TestCaseData> Scenarios(string testName)
        {
            foreach (var agentSize in new[] { 1, 2 })
            {
                foreach (var isCellWeightEnabled in new[] { false, true })
                {
                    yield return Case(testName, new Scenario
                    {
                        IsDiagonalMovementEnabled = false,
                        CornerBlocking = CornerBlocking.BetweenBlockedCells,
                        IsCellWeightEnabled = isCellWeightEnabled,
                        AgentSize = agentSize
                    });

                    foreach (var cornerBlocking in CornerBlockingPresets)
                    {
                        yield return Case(testName, new Scenario
                        {
                            IsDiagonalMovementEnabled = true,
                            CornerBlocking = cornerBlocking,
                            IsCellWeightEnabled = isCellWeightEnabled,
                            AgentSize = agentSize
                        });
                    }
                }
            }
        }

        private static TestCaseData Case(string testName, Scenario scenario)
        {
            return new TestCaseData(scenario).SetName($"{testName}_{scenario}");
        }

        private static IEnumerable<TestCaseData> GetPathScenarios() =>
            Scenarios("GetPath_OnRandomGrids_ReturnsLegalCheapestRoute");

        private static IEnumerable<TestCaseData> GetReachableScenarios() =>
            Scenarios("GetReachable_OnRandomGrids_ReturnsCheapestCostOfEveryCell");

        [TestCaseSource(nameof(GetPathScenarios))]
        public void GetPath_OnRandomGrids_ReturnsLegalCheapestRoute(Scenario scenario)
        {
            var agent = new Agent { Size = scenario.AgentSize };
            var foundPaths = 0;
            var failedSearches = 0;

            for (var seed = 0; seed < GridsPerCase; seed++)
            {
                var random = new Random(seed);
                var grid = ReferenceGrid.CreateRandom(random, scenario);
                using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, grid.Settings);

                for (var query = 0; query < QueriesPerGrid; query++)
                {
                    var from = grid.RandomOrigin(random);
                    var to = grid.RandomCoordinate(random);

                    if (from == to)
                    {
                        continue;
                    }

                    var expected = grid.GetCheapestCosts(from);
                    var context = $"seed {seed}, {from} -> {to}, {grid}";

                    using var path = pathfinder.GetPath(agent, from, to);

                    if (!expected.TryGetValue(to, out var cheapest))
                    {
                        path.IsSuccess.Should().BeFalse($"the destination is unreachable ({context})");
                        failedSearches++;
                        continue;
                    }

                    path.IsSuccess.Should().BeTrue($"the destination is reachable ({context})");
                    path.Get(path.Length - 1).Should().Be(to, context);
                    grid.GetRouteCost(from, path).Should().BeApproximately(cheapest, Tolerance(cheapest), context);
                    foundPaths++;
                }
            }

            foundPaths.Should().BeGreaterThan(GridsPerCase, "the scenario must exercise successful searches");
            failedSearches.Should().BeGreaterThan(0, "the scenario must exercise failed searches");
        }

        [TestCaseSource(nameof(GetReachableScenarios))]
        public void GetReachable_OnRandomGrids_ReturnsCheapestCostOfEveryCell(Scenario scenario)
        {
            var agent = new Agent { Size = scenario.AgentSize };

            for (var seed = 0; seed < GridsPerCase; seed++)
            {
                var random = new Random(seed);
                var grid = ReferenceGrid.CreateRandom(random, scenario);
                using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, grid.Settings);
                var from = grid.RandomOrigin(random);
                var context = $"seed {seed}, from {from}, {grid}";

                var expected = grid.GetCheapestCosts(from);
                using var reachable = pathfinder.GetReachable(agent, from, Unbounded);

                reachable.Length.Should().Be(expected.Count, context);

                foreach (var cell in reachable.Cells)
                {
                    expected.Should().ContainKey(cell.Coordinate, context);
                    var cheapest = expected[cell.Coordinate];
                    cell.Cost.Should().BeApproximately(cheapest, Tolerance(cheapest), $"{cell.Coordinate}, {context}");
                }
            }
        }

        [TestCase(1f, 1.41f, 9, 4, 4 * 1.41f + 5f, TestName = "GetPath_OnOpenGround_CostsOctileDistance")]
        [TestCase(1f, 2.5f, 6, 3, 9f, TestName = "GetPath_WhenDiagonalCostsMoreThanTwoStraightSteps_CostsManhattanDistance")]
        [TestCase(1f, 0.6f, 6, 0, 6 * 0.6f, TestName = "GetPath_WhenDiagonalCostsLessThanStraightStep_ZigzagsDiagonally")]
        public void GetPath_OnOpenGround_CostsCheapestStepMix(float straight, float diagonal, int toX, int toY,
            float expectedCost)
        {
            var settings = new PathfinderSettings
            {
                StraightMovementMultiplier = straight,
                DiagonalMovementMultiplier = diagonal,
                IsCellWeightEnabled = false
            };
            var grid = new ReferenceGrid(settings, 1);
            using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, settings);
            var from = new Coordinate(2, 4);
            var to = new Coordinate(from.X + toX, from.Y + toY);

            using var path = pathfinder.GetPath(new Agent(), from, to);

            path.IsSuccess.Should().BeTrue();
            grid.GetRouteCost(from, path).Should().BeApproximately(expectedCost, 0.0001f);
        }

        [Test]
        public void GetPath_WithHeavyCellsOnTheStraightLine_DetoursAroundThem()
        {
            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false, IsCellWeightEnabled = true };
            var grid = new ReferenceGrid(settings, 1);
            for (var x = 3; x <= 7; x++)
            {
                grid.Weights[x, 5] = 10f;
            }

            using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, settings);
            var from = new Coordinate(1, 5);
            var to = new Coordinate(9, 5);

            using var path = pathfinder.GetPath(new Agent(), from, to);

            path.IsSuccess.Should().BeTrue();
            grid.GetRouteCost(from, path).Should().BeApproximately(10f, 0.0001f, "two side steps beat five weight-10 cells");
        }

        private static float Tolerance(float cost)
        {
            return 0.001f + cost * 0.0001f;
        }

        /// <summary>
        /// The grid of a property test plus an independent reference implementation of the movement rules.
        /// </summary>
        private sealed class ReferenceGrid
        {
            private const int MaxNeighbors = 8;

            private static readonly (int X, int Y)[] Steps =
            {
                (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)
            };

            private readonly int _agentSize;

            public PathfinderSettings Settings { get; }
            public bool[,] Walls { get; } = new bool[GridWidth, GridHeight];
            public bool[,] Occupied { get; } = new bool[GridWidth, GridHeight];
            public float[,] Weights { get; } = new float[GridWidth, GridHeight];

            public ReferenceGrid(PathfinderSettings settings, int agentSize)
            {
                Settings = settings;
                _agentSize = agentSize;
            }

            public static ReferenceGrid CreateRandom(Random random, Scenario scenario)
            {
                var (straight, diagonal) = Multipliers[random.Next(Multipliers.Length)];
                var settings = new PathfinderSettings
                {
                    IsDiagonalMovementEnabled = scenario.IsDiagonalMovementEnabled,
                    CornerBlocking = scenario.CornerBlocking,
                    IsCalculatingOccupiedCells = true,
                    IsCellWeightEnabled = scenario.IsCellWeightEnabled,
                    StraightMovementMultiplier = straight,
                    DiagonalMovementMultiplier = diagonal,
                    PathSmoothingMethod = PathSmoothingMethod.None
                };

                var grid = new ReferenceGrid(settings, scenario.AgentSize);
                var wallChance = scenario.AgentSize == 1 ? 0.25 : 0.1;

                for (var x = 0; x < GridWidth; x++)
                {
                    for (var y = 0; y < GridHeight; y++)
                    {
                        var roll = random.NextDouble();
                        grid.Walls[x, y] = roll < wallChance;
                        grid.Occupied[x, y] = roll >= wallChance && roll < wallChance + 0.05;
                        grid.Weights[x, y] = (float)Math.Round(random.NextDouble() * 4, 1);
                    }
                }

                return grid;
            }

            public Cell[] ToCells()
            {
                var cells = new Cell[GridWidth * GridHeight];
                for (var x = 0; x < GridWidth; x++)
                {
                    for (var y = 0; y < GridHeight; y++)
                    {
                        cells[x * GridHeight + y] = new Cell
                        {
                            Coordinate = new Coordinate(x, y),
                            IsWalkable = !Walls[x, y],
                            IsOccupied = Occupied[x, y],
                            Weight = Weights[x, y]
                        };
                    }
                }

                return cells;
            }

            public Coordinate RandomCoordinate(Random random)
            {
                return new Coordinate(random.Next(GridWidth), random.Next(GridHeight));
            }

            public Coordinate RandomOrigin(Random random)
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    var origin = RandomCoordinate(random);
                    if (GetFootprint(origin.X, origin.Y) == Blocker.Open)
                    {
                        return origin;
                    }
                }

                return RandomCoordinate(random);
            }

            /// <summary>Naive Dijkstra (linear scan for the minimum) from <paramref name="from"/>.</summary>
            public Dictionary<Coordinate, float> GetCheapestCosts(Coordinate from)
            {
                var costs = new Dictionary<Coordinate, double> { [from] = 0 };
                var settled = new Dictionary<Coordinate, float>();

                while (true)
                {
                    var hasCurrent = false;
                    var current = default(Coordinate);
                    var currentCost = double.MaxValue;

                    foreach (var (coordinate, cost) in costs)
                    {
                        if (!settled.ContainsKey(coordinate) && cost < currentCost)
                        {
                            hasCurrent = true;
                            current = coordinate;
                            currentCost = cost;
                        }
                    }

                    if (!hasCurrent)
                    {
                        return settled;
                    }

                    settled[current] = (float)currentCost;

                    for (var i = 0; i < MaxNeighbors; i++)
                    {
                        var next = new Coordinate(current.X + Steps[i].X, current.Y + Steps[i].Y);
                        if (settled.ContainsKey(next) || !IsStepAllowed(current, next))
                        {
                            continue;
                        }

                        var nextCost = currentCost + GetStepCost(current, next);
                        if (!costs.TryGetValue(next, out var known) || nextCost < known)
                        {
                            costs[next] = nextCost;
                        }
                    }
                }
            }

            /// <summary>Validates every step of the route and returns its total cost.</summary>
            public float GetRouteCost(Coordinate from, PathResult path)
            {
                var cost = 0.0;
                var previous = from;

                for (var i = 0; i < path.Length; i++)
                {
                    var next = path.Get(i);
                    IsStepAllowed(previous, next).Should().BeTrue($"step {i} {previous} -> {next} must be legal");
                    cost += GetStepCost(previous, next);
                    previous = next;
                }

                return (float)cost;
            }

            public override string ToString()
            {
                return $"straight {Settings.StraightMovementMultiplier}, diagonal {Settings.DiagonalMovementMultiplier}";
            }

            private bool IsStepAllowed(Coordinate from, Coordinate to)
            {
                var dx = to.X - from.X;
                var dy = to.Y - from.Y;

                if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0))
                {
                    return false;
                }

                if (GetFootprint(to.X, to.Y) != Blocker.Open)
                {
                    return false;
                }

                if (dx == 0 || dy == 0)
                {
                    return true;
                }

                return Settings.IsDiagonalMovementEnabled
                       && !IsCornerBlocked(GetFootprint(from.X + dx, from.Y), GetFootprint(from.X, from.Y + dy));
            }

            private bool IsCornerBlocked(Blocker first, Blocker second)
            {
                var flag = (Min(first, second), Max(first, second)) switch
                {
                    (Blocker.Wall, Blocker.Wall) => CornerBlocking.WallAndWall,
                    (Blocker.Occupied, Blocker.Wall) => CornerBlocking.WallAndOccupied,
                    (Blocker.Occupied, Blocker.Occupied) => CornerBlocking.OccupiedAndOccupied,
                    (Blocker.Open, Blocker.Wall) => CornerBlocking.WallAndOpen,
                    (Blocker.Open, Blocker.Occupied) => CornerBlocking.OccupiedAndOpen,
                    _ => CornerBlocking.None
                };

                return flag != CornerBlocking.None && (Settings.CornerBlocking & flag) == flag;
            }

            private double GetStepCost(Coordinate from, Coordinate to)
            {
                var isDiagonal = from.X != to.X && from.Y != to.Y;
                double travel = isDiagonal ? Settings.DiagonalMovementMultiplier : Settings.StraightMovementMultiplier;
                return Settings.IsCellWeightEnabled ? travel + Weights[to.X, to.Y] : travel;
            }

            private Blocker GetFootprint(int anchorX, int anchorY)
            {
                var result = Blocker.Open;

                for (var x = anchorX; x < anchorX + _agentSize; x++)
                {
                    for (var y = anchorY; y < anchorY + _agentSize; y++)
                    {
                        if (x < 0 || y < 0 || x >= GridWidth || y >= GridHeight || Walls[x, y])
                        {
                            return Blocker.Wall;
                        }

                        if (Occupied[x, y] && Settings.IsCalculatingOccupiedCells)
                        {
                            result = Blocker.Occupied;
                        }
                    }
                }

                return result;
            }

            private static Blocker Min(Blocker a, Blocker b) => a < b ? a : b;

            private static Blocker Max(Blocker a, Blocker b) => a > b ? a : b;

            private enum Blocker
            {
                Open,
                Occupied,
                Wall
            }
        }
    }
}
