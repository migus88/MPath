using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The open set of the A* and reachability searches: a binary min-heap of <see cref="HeapEntry"/> values
    /// (priority + cell index) in a fixed array sized to the grid. Comparisons read the priority from the entry, so a
    /// sift never touches the nodes it passes; each node keeps its slot in <see cref="PathNode.QueueIndex"/> for O(1)
    /// membership tests and O(log n) decrease-key.
    /// </summary>
    /// <remarks>
    /// A cell is in the heap at most once at a time, so the heap never holds more entries than the grid has cells
    /// and never grows: after construction no operation allocates. The array is rented from
    /// <see cref="ArrayPool{T}.Shared"/> and returned by <see cref="Dispose"/>. Every search starts with
    /// <see cref="Reset"/>, which binds the heap to the pinned node buffer whose slots it maintains.
    /// </remarks>
    internal sealed unsafe class UnsafePriorityQueue : IDisposable
    {
        /// <summary>
        /// Gets the current number of items in the queue.
        /// </summary>
        public int Count;

        // 1-based: slot 0 is unused, so a node whose QueueIndex is 0 is never mistaken for a member.
        private HeapEntry[] _heap;
        private PathNode* _nodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnsafePriorityQueue"/> class.
        /// </summary>
        /// <param name="capacity">The number of cells in the grid: the most entries the heap can ever hold.</param>
        public UnsafePriorityQueue(int capacity)
        {
            _heap = ArrayPool<HeapEntry>.Shared.Rent(capacity + 1);
        }

        /// <summary>
        /// Empties the queue and binds it to the node buffer of the next search. Every enqueued index must index
        /// <paramref name="nodes"/>, and the buffer must stay pinned until the search ends.
        /// </summary>
        /// <param name="nodes">The first node of the pinned node buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(PathNode* nodes)
        {
            _nodes = nodes;
            Count = 0;
        }

        /// <summary>
        /// Adds a node to the queue with the specified priority.
        /// </summary>
        /// <param name="index">The node's index in the node buffer. Must not already be in the queue.</param>
        /// <param name="priority">Priority value (lower values have higher priority).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(int index, int priority)
        {
            Debug.Assert(!Contains(index), "The node is already queued.");

            fixed (HeapEntry* heap = _heap)
            {
                CascadeUp(heap, ++Count, new HeapEntry(priority, index));
            }
        }

        /// <summary>
        /// Removes and returns the node with the highest priority (lowest value) from the queue.
        /// </summary>
        /// <returns>The index of the highest priority node.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Dequeue()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Queue is empty.");
            }

            fixed (HeapEntry* heap = _heap)
            {
                var result = heap[1].Index;
                var last = heap[Count--];

                if (Count > 0)
                {
                    CascadeDown(heap, 1, last);
                }

                return result;
            }
        }

        /// <summary>
        /// Updates the priority of a node already contained in the queue and restores the heap property.
        /// Used to perform a decrease-key operation (e.g. when a cheaper route to a cell is discovered).
        /// </summary>
        /// <param name="index">The node's index in the node buffer. Must already be in the queue.</param>
        /// <param name="priority">The new priority value (lower values have higher priority).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePriority(int index, int priority)
        {
            Debug.Assert(Contains(index), "The node is not queued.");

            fixed (HeapEntry* heap = _heap)
            {
                var slot = _nodes[index].QueueIndex;
                var entry = new HeapEntry(priority, index);

                if (slot > 1 && priority < heap[slot >> 1].Priority)
                {
                    CascadeUp(heap, slot, entry);
                }
                else
                {
                    CascadeDown(heap, slot, entry);
                }
            }
        }

        /// <summary>
        /// Determines whether the queue contains the specified node.
        /// </summary>
        /// <param name="index">The node's index in the node buffer.</param>
        /// <returns>true if the queue contains the node; otherwise, false.</returns>
        /// <remarks>
        /// <see cref="PathNode.QueueIndex"/> is not cleared when a node leaves the heap or a new search starts, so it
        /// may be stale; the node is a member only when the slot it names is live and holds that very node.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int index)
        {
            var slot = _nodes[index].QueueIndex;

            return (uint)(slot - 1) < (uint)Count && _heap[slot].Index == index;
        }

        /// <summary>
        /// Returns the heap array to the pool. The queue must not be used afterwards.
        /// </summary>
        public void Dispose()
        {
            if (_heap == null)
            {
                return;
            }

            ArrayPool<HeapEntry>.Shared.Return(_heap);
            _heap = null;
            Count = 0;
        }

        /// <summary>
        /// Moves <paramref name="entry"/> up from the hole at <paramref name="index"/> until its parent has an equal or
        /// higher priority, then stores it there.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeUp(HeapEntry* heap, int index, HeapEntry entry)
        {
            var nodes = _nodes;

            while (index > 1)
            {
                var parentIndex = index >> 1;
                var parent = heap[parentIndex];

                if (parent.Priority <= entry.Priority)
                {
                    break;
                }

                heap[index] = parent;
                nodes[parent.Index].QueueIndex = index;
                index = parentIndex;
            }

            heap[index] = entry;
            nodes[entry.Index].QueueIndex = index;
        }

        /// <summary>
        /// Moves <paramref name="entry"/> down from the hole at <paramref name="index"/> until no child has a higher
        /// priority, then stores it there.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeDown(HeapEntry* heap, int index, HeapEntry entry)
        {
            var nodes = _nodes;
            var count = Count;

            while (true)
            {
                var childIndex = index << 1;

                if (childIndex > count)
                {
                    break;
                }

                if (childIndex < count && heap[childIndex + 1].Priority < heap[childIndex].Priority)
                {
                    childIndex++;
                }

                var child = heap[childIndex];

                if (entry.Priority <= child.Priority)
                {
                    break;
                }

                heap[index] = child;
                nodes[child.Index].QueueIndex = index;
                index = childIndex;
            }

            heap[index] = entry;
            nodes[entry.Index].QueueIndex = index;
        }

        /// <summary>
        /// One slot of the heap: the priority it is ordered by and the index of its node (and cell).
        /// </summary>
        private readonly struct HeapEntry
        {
            public readonly int Priority;
            public readonly int Index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeapEntry(int priority, int index)
            {
                Priority = priority;
                Index = index;
            }
        }
    }
}
