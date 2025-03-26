using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Internal
{
    internal unsafe class UnsafePriorityQueue
    {
        private const int DefaultCollectionSize = 11;
        
        public int Count;
        private readonly List<IntPtr> _collection;

        public UnsafePriorityQueue(int? bufferSize = null)
        {
            Count = 0;
            _collection = new List<IntPtr>(bufferSize ?? DefaultCollectionSize) 
            {
                // Ensure the first index is unused 
                IntPtr.Zero 
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(Cell* item, float priority)
        {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Cell* item)
        {
            return item->QueueIndex <= Count && _collection[item->QueueIndex] == (IntPtr)item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            for (var i = 1; i <= Count; i++)
            {
                _collection[i] = IntPtr.Zero;
            }
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeUp(Cell* item)
        {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CascadeDown(Cell* item)
        {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasHigherPriority(Cell* higher, Cell* lower) =>
            higher->ScoreF < lower->ScoreF;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasHigherOrEqualPriority(Cell* higher, Cell* lower) =>
            higher->ScoreF <= lower->ScoreF;
    }
}