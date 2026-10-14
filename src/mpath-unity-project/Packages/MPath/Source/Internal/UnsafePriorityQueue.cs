using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The open set of the A* and reachability searches: a binary min-heap of <see cref="HeapEntry"/> values
    /// (priority + cell index) in a fixed array sized to the grid. Comparisons read the priority from the entry, so a
    /// sift never touches the cells it passes; each cell keeps its slot in <see cref="Cell.QueueIndex"/> for O(1)
    /// membership tests and O(log n) decrease-key.
    /// </summary>
    /// <remarks>
    /// A cell is in the heap at most once at a time, so the heap never holds more entries than the grid has cells
    /// and never grows: after construction no operation allocates. The array is rented from
    /// <see cref="ArrayPool{T}.Shared"/> and returned by <see cref="Dispose"/>. Every search starts with
    /// <see cref="Reset"/>, which binds the heap to the pinned cell buffer the entries index into.
    /// </remarks>
    internal sealed unsafe class UnsafePriorityQueue : IDisposable
    {
        /// <summary>
        /// Gets the current number of items in the queue.
        /// </summary>
        public int Count;

        // 1-based: slot 0 is unused, so a cell whose QueueIndex is 0 is never mistaken for a member.
        private HeapEntry[] _heap;
        private Cell* _cells;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnsafePriorityQueue"/> class.
        /// </summary>
        /// <param name="capacity">The number of cells in the grid: the most entries the heap can ever hold.</param>
        public UnsafePriorityQueue(int capacity)
        {
            _heap = ArrayPool<HeapEntry>.Shared.Rent(capacity + 1);
        }

        /// <summary>
        /// Empties the queue and binds it to the cell buffer of the next search. Every enqueued cell must lie in
        /// <paramref name="cells"/>, and the buffer must stay pinned until the search ends.
        /// </summary>
        /// <param name="cells">The first cell of the pinned cell buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(Cell* cells)
        {
            _cells = cells;
            Count = 0;
        }

        /// <summary>
        /// Adds a cell to the queue with the specified priority.
        /// </summary>
        /// <param name="item">Pointer to the cell to add. Must not already be in the queue.</param>
        /// <param name="priority">Priority value (lower values have higher priority).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(Cell* item, float priority)
        {
            Debug.Assert(item != null && !Contains(item), "The cell is null or already queued.");

            item->ScoreF = priority;

            fixed (HeapEntry* heap = _heap)
            {
                CascadeUp(heap, ++Count, new HeapEntry(priority, (int)(item - _cells)));
            }
        }

        /// <summary>
        /// Removes and returns the cell with the highest priority (lowest value) from the queue.
        /// </summary>
        /// <returns>A pointer to the highest priority cell.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Cell* Dequeue()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Queue is empty.");
            }

            fixed (HeapEntry* heap = _heap)
            {
                var result = _cells + heap[1].Index;
                var last = heap[Count--];

                if (Count > 0)
                {
                    CascadeDown(heap, 1, last);
                }

                return result;
            }
        }

        /// <summary>
        /// Updates the priority of a cell already contained in the queue and restores the heap property.
        /// Used to perform a decrease-key operation (e.g. when a cheaper route to a cell is discovered).
        /// </summary>
        /// <param name="item">Pointer to the cell whose priority should change. Must already be in the queue.</param>
        /// <param name="priority">The new priority value (lower values have higher priority).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePriority(Cell* item, float priority)
        {
            Debug.Assert(item != null && Contains(item), "The cell is null or not queued.");

            item->ScoreF = priority;

            fixed (HeapEntry* heap = _heap)
            {
                var index = item->QueueIndex;
                var entry = new HeapEntry(priority, heap[index].Index);

                if (index > 1 && priority < heap[index >> 1].Priority)
                {
                    CascadeUp(heap, index, entry);
                }
                else
                {
                    CascadeDown(heap, index, entry);
                }
            }
        }

        /// <summary>
        /// Determines whether the queue contains the specified cell.
        /// </summary>
        /// <param name="item">Pointer to the cell to locate.</param>
        /// <returns>true if the queue contains the cell; otherwise, false.</returns>
        /// <remarks>
        /// <see cref="Cell.QueueIndex"/> is never cleared, so it may be stale; the cell is a member only when the
        /// slot it names is live and holds that very cell.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Cell* item)
        {
            var index = item->QueueIndex;

            return (uint)(index - 1) < (uint)Count && _heap[index].Index == (int)(item - _cells);
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
            var cells = _cells;

            while (index > 1)
            {
                var parentIndex = index >> 1;
                var parent = heap[parentIndex];

                if (parent.Priority <= entry.Priority)
                {
                    break;
                }

                heap[index] = parent;
                cells[parent.Index].QueueIndex = index;
                index = parentIndex;
            }

            heap[index] = entry;
            cells[entry.Index].QueueIndex = index;
        }

        /// <summary>
        /// Moves <paramref name="entry"/> down from the hole at <paramref name="index"/> until no child has a higher
        /// priority, then stores it there.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeDown(HeapEntry* heap, int index, HeapEntry entry)
        {
            var cells = _cells;
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
                cells[child.Index].QueueIndex = index;
                index = childIndex;
            }

            heap[index] = entry;
            cells[entry.Index].QueueIndex = index;
        }

        /// <summary>
        /// One slot of the heap: the priority it is ordered by and the index of its cell in the cell buffer.
        /// </summary>
        private readonly struct HeapEntry
        {
            public readonly float Priority;
            public readonly int Index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HeapEntry(float priority, int index)
            {
                Priority = priority;
                Index = index;
            }
        }
    }
}
