using System;
using System.Collections.Generic;
using FluentAssertions;
using Migs.MPath.Core.Internal;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// The open set against a reference <see cref="SortedSet{T}"/>: random interleavings of enqueue, decrease-key and
    /// dequeue must always dequeue a cell with the lowest live priority, and membership must follow every operation.
    /// </summary>
    [TestFixture]
    public unsafe class UnsafePriorityQueueTests
    {
        private const int CellCount = 300;
        private const int OperationsPerRun = 5000;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RandomOperations_DequeueInReferenceOrder(int seed)
        {
            // Arrange
            var random = new Random(seed);
            var nodes = new PathNode[CellCount];
            using var queue = new UnsafePriorityQueue(CellCount);
            var reference = new SortedSet<(float Priority, int Index)>();
            var priorities = new Dictionary<int, float>();

            fixed (PathNode* ptr = nodes)
            {
                queue.Reset(ptr);

                for (var i = 0; i < OperationsPerRun; i++)
                {
                    // Act
                    var index = random.Next(CellCount);
                    var operation = random.Next(3);

                    if (operation == 0 && !priorities.ContainsKey(index))
                    {
                        // Few distinct priorities, so ties are common.
                        var priority = random.Next(50);
                        queue.Enqueue(index, priority);
                        priorities[index] = priority;
                        reference.Add((priority, index));
                    }
                    else if (operation == 1 && priorities.TryGetValue(index, out var current))
                    {
                        var priority = current - random.Next(10);
                        queue.UpdatePriority(index, priority);
                        reference.Remove((current, index));
                        priorities[index] = priority;
                        reference.Add((priority, index));
                    }
                    else if (operation == 2 && reference.Count > 0)
                    {
                        var dequeued = queue.Dequeue();

                        // Assert
                        priorities.Should().ContainKey(dequeued);
                        priorities[dequeued].Should().Be(reference.Min.Priority);
                        reference.Remove((priorities[dequeued], dequeued));
                        priorities.Remove(dequeued);
                        queue.Contains(dequeued).Should().BeFalse();
                    }

                    queue.Count.Should().Be(reference.Count);
                    queue.Contains(index).Should().Be(priorities.ContainsKey(index));
                }
            }
        }

        [Test]
        public void Reset_AfterUse_EmptiesTheQueueAndForgetsMembers()
        {
            // Arrange
            var nodes = new PathNode[CellCount];
            using var queue = new UnsafePriorityQueue(CellCount);

            fixed (PathNode* ptr = nodes)
            {
                queue.Reset(ptr);

                for (var i = 0; i < CellCount; i++)
                {
                    queue.Enqueue(i, CellCount - i);
                }

                // Act
                queue.Reset(ptr);

                // Assert
                queue.Count.Should().Be(0);

                for (var i = 0; i < CellCount; i++)
                {
                    queue.Contains(i).Should().BeFalse();
                }
            }
        }

        [Test]
        public void Enqueue_EveryCell_FitsWithoutGrowing()
        {
            // Arrange
            var nodes = new PathNode[CellCount];
            using var queue = new UnsafePriorityQueue(CellCount);

            fixed (PathNode* ptr = nodes)
            {
                queue.Reset(ptr);

                // Act
                for (var i = 0; i < CellCount; i++)
                {
                    queue.Enqueue(i, CellCount - i);
                }

                // Assert
                for (var i = CellCount - 1; i >= 0; i--)
                {
                    queue.Dequeue().Should().Be(i);
                }

                queue.Count.Should().Be(0);
            }
        }
    }
}
