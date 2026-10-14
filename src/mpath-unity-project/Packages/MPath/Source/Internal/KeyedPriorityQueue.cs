using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The open set of the keyed search used with a <see cref="Data.TieBreaking"/>: the same fixed-array binary
    /// min-heap as <see cref="UnsafePriorityQueue"/>, ordered lexicographically by an integer priority key, then the
    /// tie key, then the cost key (each entry carries all three, so a sift never touches the nodes it passes).
    /// </summary>
    /// <remarks>
    /// The last key (cost ascending) makes every node on a route leave the queue before the nodes after it whenever
    /// their first two keys tie, which the tie-break relies on. Only a tie-breaking pathfinder creates one, so the
    /// float search keeps its own queue untouched. Rented from <see cref="ArrayPool{T}.Shared"/> and returned by
    /// <see cref="Dispose"/>; after construction no operation allocates.
    /// </remarks>
    internal sealed unsafe class KeyedPriorityQueue : IDisposable
    {
        /// <summary>
        /// Gets the current number of items in the queue.
        /// </summary>
        public int Count;

        // 1-based: slot 0 is unused, so a node whose QueueIndex is 0 is never mistaken for a member.
        private Entry[] _heap;
        private PathNode* _nodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyedPriorityQueue"/> class.
        /// </summary>
        /// <param name="capacity">The number of cells in the grid: the most entries the heap can ever hold.</param>
        public KeyedPriorityQueue(int capacity)
        {
            _heap = ArrayPool<Entry>.Shared.Rent(capacity + 1);
        }

        /// <summary>
        /// Empties the queue and binds it to the pinned node buffer of the next search.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(PathNode* nodes)
        {
            _nodes = nodes;
            Count = 0;
        }

        /// <summary>
        /// Adds a node with its keys. The node must not already be in the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(int index, long priorityKey, int tieKey, long costKey)
        {
            Debug.Assert(!Contains(index), "The node is already queued.");

            fixed (Entry* heap = _heap)
            {
                CascadeUp(heap, ++Count, new Entry(priorityKey, costKey, tieKey, index));
            }
        }

        /// <summary>
        /// Removes and returns the node with the smallest keys.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Dequeue()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Queue is empty.");
            }

            fixed (Entry* heap = _heap)
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
        /// Lowers the keys of a node already in the queue (a decrease-key) and restores the heap property.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePriority(int index, long priorityKey, int tieKey, long costKey)
        {
            Debug.Assert(Contains(index), "The node is not queued.");

            fixed (Entry* heap = _heap)
            {
                var slot = _nodes[index].QueueIndex;
                var entry = new Entry(priorityKey, costKey, tieKey, index);

                if (slot > 1 && entry.IsBefore(heap[slot >> 1]))
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
        /// Determines whether the queue contains the specified node (stale-slot safe, like
        /// <see cref="UnsafePriorityQueue.Contains"/>).
        /// </summary>
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

            ArrayPool<Entry>.Shared.Return(_heap);
            _heap = null;
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeUp(Entry* heap, int index, Entry entry)
        {
            var nodes = _nodes;

            while (index > 1)
            {
                var parentIndex = index >> 1;
                var parent = heap[parentIndex];

                if (!entry.IsBefore(parent))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeDown(Entry* heap, int index, Entry entry)
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

                if (childIndex < count && heap[childIndex + 1].IsBefore(heap[childIndex]))
                {
                    childIndex++;
                }

                var child = heap[childIndex];

                if (!child.IsBefore(entry))
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
        /// One slot of the heap: the three keys it is ordered by and the index of its node (and cell).
        /// </summary>
        private readonly struct Entry
        {
            public readonly long PriorityKey;
            public readonly long CostKey;
            public readonly int TieKey;
            public readonly int Index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Entry(long priorityKey, long costKey, int tieKey, int index)
            {
                PriorityKey = priorityKey;
                CostKey = costKey;
                TieKey = tieKey;
                Index = index;
            }

            /// <summary>
            /// Whether this entry orders strictly before <paramref name="other"/>.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsBefore(in Entry other)
            {
                if (PriorityKey != other.PriorityKey)
                {
                    return PriorityKey < other.PriorityKey;
                }

                if (TieKey != other.TieKey)
                {
                    return TieKey < other.TieKey;
                }

                return CostKey < other.CostKey;
            }
        }
    }
}
