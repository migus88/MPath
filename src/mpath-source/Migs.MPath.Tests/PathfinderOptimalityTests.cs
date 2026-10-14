using System;
using System.Collections.Generic;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Property tests: on seeded random grids, <see cref="Pathfinder.GetPath"/> returns a legal route whose cost is
    /// the cheapest one, and <see cref="Pathfinder.GetReachable"/> reports the cheapest cost of every cell. Both are
    /// checked against <see cref="ReferenceGrid"/>, an independent, deliberately naive Dijkstra over the documented
    /// movement rules (walls, occupied cells, agent footprint, corner blocking, step cost = travel + weight).
    /// Costs are integers, so every comparison is exact. With a <see cref="TieBreaking"/> the reference also finds
    /// the best tie-break key: a lexicographic Dijkstra for (cost, diagonals, steps), and one over
    /// (cell, arrival direction) states for (cost, turns).
    /// </summary>
    [TestFixture]
    public class PathfinderOptimalityTests
    {
        private const int GridWidth = 20;
        private const int GridHeight = 16;
        private const int GridsPerCase = 40;
        private const int QueriesPerGrid = 4;
        private const int Unbounded = int.MaxValue;

        private static readonly (int Straight, int Diagonal)[] StepCosts =
        {
            (100, 141),
            (100, 100),
            (200, 300),
            (100, 250),
            (100, 60),
            (50, 75)
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
            public TieBreaking TieBreaking { get; init; }

            public override string ToString()
            {
                var diagonal = IsDiagonalMovementEnabled ? $"Diagonal{CornerBlocking}" : "NoDiagonal";
                var weights = IsCellWeightEnabled ? "Weighted" : "Unweighted";
                var tieBreaking = TieBreaking == TieBreaking.None ? "" : $"_{TieBreaking}";
                return $"{diagonal}_{weights}_Size{AgentSize}{tieBreaking}";
            }
        }

        private static IEnumerable<TestCaseData> Scenarios(string testName)
        {
            return Scenarios(testName, TieBreaking.None);
        }

        private static IEnumerable<TestCaseData> TieBreakingScenarios(string testName)
        {
            foreach (var tieBreaking in new[] { TieBreaking.FewerDiagonals, TieBreaking.FewerTurns })
            {
                foreach (var testCase in Scenarios(testName, tieBreaking))
                {
                    yield return testCase;
                }
            }
        }

        private static IEnumerable<TestCaseData> Scenarios(string testName, TieBreaking tieBreaking)
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
                        AgentSize = agentSize,
                        TieBreaking = tieBreaking
                    });

                    foreach (var cornerBlocking in CornerBlockingPresets)
                    {
                        yield return Case(testName, new Scenario
                        {
                            IsDiagonalMovementEnabled = true,
                            CornerBlocking = cornerBlocking,
                            IsCellWeightEnabled = isCellWeightEnabled,
                            AgentSize = agentSize,
                            TieBreaking = tieBreaking
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

        private static IEnumerable<TestCaseData> GetPathTieBreakingScenarios() =>
            TieBreakingScenarios("GetPath_WithTieBreaking_OnRandomGrids_ReturnsBestRouteByCostThenTieKey");

        private static IEnumerable<TestCaseData> GetReachableTieBreakingScenarios() =>
            TieBreakingScenarios("GetReachable_WithTieBreaking_OnRandomGrids_ReturnsExactCostsAndStepCounts");

        [TestCaseSource(nameof(GetPathScenarios))]
        public void GetPath_OnRandomGrids_ReturnsLegalCheapestRoute(Scenario scenario)
        {
            var agentSize = scenario.AgentSize;
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

                    using var path = pathfinder.GetPath(from, to, agentSize);

                    if (!expected.TryGetValue(to, out var cheapest))
                    {
                        path.IsSuccess.Should().BeFalse($"the destination is unreachable ({context})");
                        failedSearches++;
                        continue;
                    }

                    path.IsSuccess.Should().BeTrue($"the destination is reachable ({context})");
                    path.Get(path.Length - 1).Should().Be(to, context);
                    grid.GetRouteCost(from, path).Should().Be(cheapest, context);
                    path.Steps.Should().Be(path.Length, context);
                    path.DiagonalSteps.Should().Be(ReferenceGrid.GetRouteShape(from, path).Diagonals, context);
                    foundPaths++;
                }
            }

            foundPaths.Should().BeGreaterThan(GridsPerCase, "the scenario must exercise successful searches");
            failedSearches.Should().BeGreaterThan(0, "the scenario must exercise failed searches");
        }

        [TestCaseSource(nameof(GetReachableScenarios))]
        public void GetReachable_OnRandomGrids_ReturnsCheapestCostOfEveryCell(Scenario scenario)
        {
            var agentSize = scenario.AgentSize;

            for (var seed = 0; seed < GridsPerCase; seed++)
            {
                var random = new Random(seed);
                var grid = ReferenceGrid.CreateRandom(random, scenario);
                using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, grid.Settings);
                var from = grid.RandomOrigin(random);
                var context = $"seed {seed}, from {from}, {grid}";

                var expected = grid.GetCheapestCosts(from);
                using var reachable = pathfinder.GetReachable(from, Unbounded, agentSize);

                reachable.Length.Should().Be(expected.Count, context);

                foreach (var cell in reachable.Cells)
                {
                    expected.Should().ContainKey(cell.Coordinate, context);
                    var cheapest = expected[cell.Coordinate];
                    ((long)cell.Cost).Should().Be(cheapest, $"{cell.Coordinate}, {context}");

                    if (!scenario.IsCellWeightEnabled)
                    {
                        var settings = grid.Settings;
                        var routeCost = (cell.Steps - cell.DiagonalSteps) * (long)settings.StraightStepCost +
                                        cell.DiagonalSteps * settings.DiagonalStepCost;
                        routeCost.Should().Be(cheapest,
                            $"the step counts describe a cheapest route to {cell.Coordinate}, {context}");
                    }
                }
            }
        }

        [TestCaseSource(nameof(GetPathTieBreakingScenarios))]
        public void GetPath_WithTieBreaking_OnRandomGrids_ReturnsBestRouteByCostThenTieKey(Scenario scenario)
        {
            var agentSize = scenario.AgentSize;
            var foundPaths = 0;

            for (var seed = 0; seed < GridsPerCase; seed++)
            {
                var random = new Random(seed);
                var grid = ReferenceGrid.CreateRandom(random, scenario);
                using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, grid.Settings);
                using var shuffledPathfinder = new Pathfinder(grid.ToShuffledCells(random), GridWidth, GridHeight,
                    grid.Settings);

                for (var query = 0; query < QueriesPerGrid; query++)
                {
                    var from = grid.RandomOrigin(random);
                    var to = grid.RandomCoordinate(random);

                    if (from == to)
                    {
                        continue;
                    }

                    var context = $"seed {seed}, {from} -> {to}, {grid}";
                    var expected = scenario.TieBreaking == TieBreaking.FewerTurns
                        ? grid.GetFewestTurnLabels(from)
                        : grid.GetFewestDiagonalLabels(from);

                    using var path = pathfinder.GetPath(from, to, agentSize);
                    using var shuffledPath = shuffledPathfinder.GetPath(from, to, agentSize);

                    shuffledPath.Path.Should().Equal(path.Path, $"the route depends on the grid only ({context})");

                    if (!expected.TryGetValue(to, out var best))
                    {
                        path.IsSuccess.Should().BeFalse($"the destination is unreachable ({context})");
                        continue;
                    }

                    path.IsSuccess.Should().BeTrue($"the destination is reachable ({context})");
                    path.Get(path.Length - 1).Should().Be(to, context);

                    var routeCost = grid.GetRouteCost(from, path);
                    var shape = ReferenceGrid.GetRouteShape(from, path);
                    routeCost.Should().Be(best.Cost, $"the route is a cheapest one ({context})");
                    path.Steps.Should().Be(shape.Steps, context);
                    path.DiagonalSteps.Should().Be(shape.Diagonals, context);

                    if (scenario.TieBreaking == TieBreaking.FewerTurns)
                    {
                        shape.Turns.Should().Be(best.TieKey, $"no cheapest route turns less ({context})");
                    }
                    else
                    {
                        shape.Diagonals.Should().Be(best.TieKey, $"no cheapest route has fewer diagonals ({context})");
                        shape.Steps.Should().Be(best.Steps, $"no such route has fewer steps ({context})");
                    }

                    foundPaths++;
                }
            }

            foundPaths.Should().BeGreaterThan(GridsPerCase, "the scenario must exercise successful searches");
        }

        [TestCaseSource(nameof(GetReachableTieBreakingScenarios))]
        public void GetReachable_WithTieBreaking_OnRandomGrids_ReturnsExactCostsAndStepCounts(Scenario scenario)
        {
            var agentSize = scenario.AgentSize;

            for (var seed = 0; seed < GridsPerCase; seed++)
            {
                var random = new Random(seed);
                var grid = ReferenceGrid.CreateRandom(random, scenario);
                using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, grid.Settings);
                var from = grid.RandomOrigin(random);
                var context = $"seed {seed}, from {from}, {grid}";

                var expected = grid.GetFewestDiagonalLabels(from);
                using var reachable = pathfinder.GetReachable(from, Unbounded, agentSize);

                reachable.Length.Should().Be(expected.Count, context);

                foreach (var cell in reachable.Cells)
                {
                    expected.Should().ContainKey(cell.Coordinate, context);
                    var best = expected[cell.Coordinate];
                    var cellContext = $"{cell.Coordinate}, {context}";
                    ((long)cell.Cost).Should().Be(best.Cost, cellContext);
                    cell.DiagonalSteps.Should().Be(best.TieKey, cellContext);
                    cell.Steps.Should().Be(best.Steps, cellContext);
                }
            }
        }

        [TestCase(100, 141, 9, 4, 4 * 141 + 5 * 100, TestName = "GetPath_OnOpenGround_CostsOctileDistance")]
        [TestCase(100, 250, 6, 3, 900, TestName = "GetPath_WhenDiagonalCostsMoreThanTwoStraightSteps_CostsManhattanDistance")]
        [TestCase(100, 60, 6, 0, 6 * 60, TestName = "GetPath_WhenDiagonalCostsLessThanStraightStep_ZigzagsDiagonally")]
        public void GetPath_OnOpenGround_CostsCheapestStepMix(int straight, int diagonal, int toX, int toY,
            int expectedCost)
        {
            var settings = new PathfinderSettings
            {
                StraightStepCost = straight,
                DiagonalStepCost = diagonal,
                IsCellWeightEnabled = false
            };
            var grid = new ReferenceGrid(settings, 1);
            using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, settings);
            var from = new Coordinate(2, 4);
            var to = new Coordinate(from.X + toX, from.Y + toY);

            using var path = pathfinder.GetPath(from, to);

            path.IsSuccess.Should().BeTrue();
            grid.GetRouteCost(from, path).Should().Be(expectedCost);
        }

        [Test]
        public void GetPath_WithHeavyCellsOnTheStraightLine_DetoursAroundThem()
        {
            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false, IsCellWeightEnabled = true };
            var grid = new ReferenceGrid(settings, 1);
            for (var x = 3; x <= 7; x++)
            {
                grid.Weights[x, 5] = 1000;
            }

            using var pathfinder = new Pathfinder(grid.ToCells(), GridWidth, GridHeight, settings);
            var from = new Coordinate(1, 5);
            var to = new Coordinate(9, 5);

            using var path = pathfinder.GetPath(from, to);

            path.IsSuccess.Should().BeTrue();
            grid.GetRouteCost(from, path).Should().Be(1000, "two side steps beat five weight-1000 cells");
        }

        /// <summary>
        /// The best label of a cell under a tie-break: the cost, the tie key (diagonals or turns) and, for fewer
        /// diagonals, the steps.
        /// </summary>
        private readonly record struct Label(long Cost, int TieKey, int Steps) : IComparable<Label>
        {
            public int CompareTo(Label other)
            {
                var cost = Cost.CompareTo(other.Cost);
                if (cost != 0)
                {
                    return cost;
                }

                var tie = TieKey.CompareTo(other.TieKey);
                return tie != 0 ? tie : Steps.CompareTo(other.Steps);
            }
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
            public int[,] Weights { get; } = new int[GridWidth, GridHeight];

            public ReferenceGrid(PathfinderSettings settings, int agentSize)
            {
                Settings = settings;
                _agentSize = agentSize;
            }

            public static ReferenceGrid CreateRandom(Random random, Scenario scenario)
            {
                var (straight, diagonal) = StepCosts[random.Next(StepCosts.Length)];
                var settings = new PathfinderSettings
                {
                    IsDiagonalMovementEnabled = scenario.IsDiagonalMovementEnabled,
                    CornerBlocking = scenario.CornerBlocking,
                    IsCalculatingOccupiedCells = true,
                    IsCellWeightEnabled = scenario.IsCellWeightEnabled,
                    StraightStepCost = straight,
                    DiagonalStepCost = diagonal,
                    PathSmoothingMethod = PathSmoothingMethod.None,
                    TieBreaking = scenario.TieBreaking
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
                        grid.Weights[x, y] = (int)Math.Round(random.NextDouble() * 40) * 10;
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

            /// <summary>The cells of <see cref="ToCells"/> in a random order.</summary>
            public Cell[] ToShuffledCells(Random random)
            {
                var cells = ToCells();
                for (var i = cells.Length - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (cells[i], cells[j]) = (cells[j], cells[i]);
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
            public Dictionary<Coordinate, long> GetCheapestCosts(Coordinate from)
            {
                var costs = new Dictionary<Coordinate, long> { [from] = 0 };
                var settled = new Dictionary<Coordinate, long>();

                while (true)
                {
                    var hasCurrent = false;
                    var current = default(Coordinate);
                    var currentCost = long.MaxValue;

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

                    settled[current] = currentCost;

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

            /// <summary>
            /// Lexicographic Dijkstra (lazy-deletion queue): per cell the best
            /// (cost, diagonals, steps).
            /// </summary>
            public Dictionary<Coordinate, Label> GetFewestDiagonalLabels(Coordinate from)
            {
                var labels = new Dictionary<Coordinate, Label> { [from] = new Label(0, 0, 0) };
                var settled = new Dictionary<Coordinate, Label>();
                var queue = new PriorityQueue<Coordinate, Label>();
                queue.Enqueue(from, labels[from]);

                while (queue.TryDequeue(out var current, out var currentLabel))
                {
                    if (settled.ContainsKey(current) || currentLabel.CompareTo(labels[current]) != 0)
                    {
                        continue;
                    }

                    settled[current] = currentLabel;

                    for (var i = 0; i < MaxNeighbors; i++)
                    {
                        var next = new Coordinate(current.X + Steps[i].X, current.Y + Steps[i].Y);
                        if (settled.ContainsKey(next) || !IsStepAllowed(current, next))
                        {
                            continue;
                        }

                        var isDiagonal = Steps[i].X != 0 && Steps[i].Y != 0;
                        var nextLabel = new Label(currentLabel.Cost + GetStepCost(current, next),
                            currentLabel.TieKey + (isDiagonal ? 1 : 0), currentLabel.Steps + 1);

                        if (!labels.TryGetValue(next, out var known) || nextLabel.CompareTo(known) < 0)
                        {
                            labels[next] = nextLabel;
                            queue.Enqueue(next, nextLabel);
                        }
                    }
                }

                return settled;
            }

            /// <summary>
            /// Dijkstra over (cell, arrival direction) states by (cost, turns): per cell the fewest turns of a
            /// cheapest route (steps unused).
            /// </summary>
            public Dictionary<Coordinate, Label> GetFewestTurnLabels(Coordinate from)
            {
                const int origin = -1;
                var labels = new Dictionary<(Coordinate Cell, int Direction), Label> { [(from, origin)] = default };
                var settled = new HashSet<(Coordinate, int)>();
                var best = new Dictionary<Coordinate, Label>();
                var queue = new PriorityQueue<(Coordinate Cell, int Direction), Label>();
                queue.Enqueue((from, origin), default);

                while (queue.TryDequeue(out var current, out var currentLabel))
                {
                    if (settled.Contains(current) || currentLabel.CompareTo(labels[current]) != 0)
                    {
                        continue;
                    }

                    settled.Add(current);

                    if (!best.TryGetValue(current.Cell, out var known) || currentLabel.CompareTo(known) < 0)
                    {
                        best[current.Cell] = currentLabel;
                    }

                    for (var i = 0; i < MaxNeighbors; i++)
                    {
                        var next = new Coordinate(current.Cell.X + Steps[i].X, current.Cell.Y + Steps[i].Y);
                        if (!IsStepAllowed(current.Cell, next) || settled.Contains((next, i)))
                        {
                            continue;
                        }

                        var turn = current.Direction != origin && current.Direction != i ? 1 : 0;
                        var nextLabel = new Label(currentLabel.Cost + GetStepCost(current.Cell, next),
                            currentLabel.TieKey + turn, 0);

                        if (!labels.TryGetValue((next, i), out var knownState) || nextLabel.CompareTo(knownState) < 0)
                        {
                            labels[(next, i)] = nextLabel;
                            queue.Enqueue((next, i), nextLabel);
                        }
                    }
                }

                return best;
            }

            /// <summary>The steps, diagonal steps and direction changes of an unsmoothed route.</summary>
            public static (int Steps, int Diagonals, int Turns) GetRouteShape(Coordinate from, PathResult path)
            {
                var diagonals = 0;
                var turns = 0;
                var previous = from;
                (int X, int Y) previousStep = (0, 0);

                for (var i = 0; i < path.Length; i++)
                {
                    var next = path.Get(i);
                    var step = (next.X - previous.X, next.Y - previous.Y);

                    if (step.Item1 != 0 && step.Item2 != 0)
                    {
                        diagonals++;
                    }

                    if (i > 0 && step != previousStep)
                    {
                        turns++;
                    }

                    previousStep = step;
                    previous = next;
                }

                return (path.Length, diagonals, turns);
            }

            /// <summary>Validates every step of the route and returns its total cost.</summary>
            public long GetRouteCost(Coordinate from, PathResult path)
            {
                var cost = 0L;
                var previous = from;

                for (var i = 0; i < path.Length; i++)
                {
                    var next = path.Get(i);
                    IsStepAllowed(previous, next).Should().BeTrue($"step {i} {previous} -> {next} must be legal");
                    cost += GetStepCost(previous, next);
                    previous = next;
                }

                return cost;
            }

            public override string ToString()
            {
                return $"straight {Settings.StraightStepCost}, diagonal {Settings.DiagonalStepCost}";
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

            private long GetStepCost(Coordinate from, Coordinate to)
            {
                var isDiagonal = from.X != to.X && from.Y != to.Y;
                long travel = isDiagonal ? Settings.DiagonalStepCost : Settings.StraightStepCost;
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
