using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// A high-performance, unsafe implementation of a priority queue specifically optimized
    /// for the A* pathfinding algorithm. This queue manages pointers to Cell structures
    /// and efficiently orders them by priority (F-score).
    /// </summary>
    internal unsafe class UnsafePriorityQueue
    {
        private const int DefaultCollectionSize = 11;
        
        /// <summary>
        /// Gets the current number of items in the queue.
        /// </summary>
        public int Count;
        
        private readonly List<IntPtr> _collection;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnsafePriorityQueue"/> class.
        /// </summary>
        /// <param name="bufferSize">Optional initial buffer size. If null, a default size will be used.</param>
        public UnsafePriorityQueue(int? bufferSize = null)
        {
            Count = 0;
            _collection = new List<IntPtr>(bufferSize ?? DefaultCollectionSize) 
            {
                // Ensure the first index is unused 
                IntPtr.Zero 
            };
        }

        /// <summary>
        /// Adds a cell to the queue with the specified priority.
        /// </summary>
        /// <param name="item">Pointer to the cell to add.</param>
        /// <param name="priority">Priority value (lower values have higher priority).</param>
        /// <exception cref="ArgumentNullException">Thrown when item is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(Cell* item, float priority)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
                
            item->ScoreF = priority;
            Count++;

            // Ensure the list can hold the item
            if (Count >= _collection.Count)
            {
                _collection.Add((IntPtr)item);
            }
            else
            {
                _collection[Count] = (IntPtr)item;
            }

            item->QueueIndex = Count;
            CascadeUp(item);
        }

        /// <summary>
        /// Removes and returns the cell with the highest priority from the queue.
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

            var result = (Cell*)_collection[1];

            if (Count == 1)
            {
                _collection[1] = IntPtr.Zero;
                Count = 0;
                return result;
            }

            var formerLastNode = (Cell*)_collection[Count];
            _collection[1] = (IntPtr)formerLastNode;
            formerLastNode->QueueIndex = 1;

            _collection[Count] = IntPtr.Zero;
            Count--;

            CascadeDown(formerLastNode);
            return result;
        }

        /// <summary>
        /// Determines whether the queue contains the specified cell.
        /// </summary>
        /// <param name="item">Pointer to the cell to locate.</param>
        /// <returns>true if the queue contains the cell; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Cell* item)
        {
            if (item == null)
                return false;
                
            return item->QueueIndex <= Count && _collection[item->QueueIndex] == (IntPtr)item;
        }

        /// <summary>
        /// Removes all items from the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            for (var i = 1; i <= Count; i++)
            {
                _collection[i] = IntPtr.Zero;
            }
            Count = 0;
        }

        /// <summary>
        /// Moves a cell up the binary heap to restore heap property after insertion.
        /// </summary>
        /// <param name="item">Pointer to the cell to move.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeUp(Cell* item)
        {
            if (item == null)
                return;
                
            while (item->QueueIndex > 1)
            {
                var parentIndex = item->QueueIndex >> 1;
                var parentNode = (Cell*)_collection[parentIndex];

                if (HasHigherOrEqualPriority(parentNode, item))
                    break;

                _collection[item->QueueIndex] = (IntPtr)parentNode;
                parentNode->QueueIndex = item->QueueIndex;

                item->QueueIndex = parentIndex;
            }

            _collection[item->QueueIndex] = (IntPtr)item;
        }

        /// <summary>
        /// Moves a cell down the binary heap to restore heap property after removal.
        /// </summary>
        /// <param name="item">Pointer to the cell to move.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeDown(Cell* item)
        {
            if (item == null)
                return;
                
            var finalQueueIndex = item->QueueIndex;

            while (true)
            {
                var childLeftIndex = 2 * finalQueueIndex;

                if (childLeftIndex > Count)
                {
                    break;
                }

                var childRightIndex = childLeftIndex + 1;
                var childLeft = (Cell*)_collection[childLeftIndex];

                // Determine the higher-priority child
                var higherPriorityChild = childLeft;
                var higherPriorityChildIndex = childLeftIndex;

                if (childRightIndex <= Count)
                {
                    var childRight = (Cell*)_collection[childRightIndex];
                    if (HasHigherPriority(childRight, childLeft))
                    {
                        higherPriorityChild = childRight;
                        higherPriorityChildIndex = childRightIndex;
                    }
                }

                if (HasHigherOrEqualPriority(item, higherPriorityChild))
                {
                    break;
                }

                _collection[finalQueueIndex] = (IntPtr)higherPriorityChild;
                higherPriorityChild->QueueIndex = finalQueueIndex;

                finalQueueIndex = higherPriorityChildIndex;
            }

            item->QueueIndex = finalQueueIndex;
            _collection[finalQueueIndex] = (IntPtr)item;
        }

        /// <summary>
        /// Determines whether the first cell has higher priority than the second cell.
        /// </summary>
        /// <param name="higher">Pointer to the first cell.</param>
        /// <param name="lower">Pointer to the second cell.</param>
        /// <returns>true if the first cell has higher priority; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasHigherPriority(Cell* higher, Cell* lower)
        {
            if (higher == null)
                return false;
            if (lower == null)
                return true;
                
            return higher->ScoreF < lower->ScoreF;
        }

        /// <summary>
        /// Determines whether the first cell has higher or equal priority to the second cell.
        /// </summary>
        /// <param name="higher">Pointer to the first cell.</param>
        /// <param name="lower">Pointer to the second cell.</param>
        /// <returns>true if the first cell has higher or equal priority; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasHigherOrEqualPriority(Cell* higher, Cell* lower)
        {
            if (higher == null)
                return false;
            if (lower == null)
                return true;
                
            return higher->ScoreF <= lower->ScoreF;
        }
    }
}