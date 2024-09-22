using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;

namespace DELTation.AAAARP.Core
{
    public class AAAAIndexAllocator : IDisposable
    {
        public const int InvalidAllocationIndex = -1;

        private readonly bool _autoGrow;

        private int _headIndex;
        private NativeArray<Node> _nodes;

        public AAAAIndexAllocator(int capacity, bool autoGrow = false)
        {
            _autoGrow = autoGrow;
            Assert.IsTrue(capacity > 0);

            _nodes = new NativeArray<Node>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _headIndex = 0;
            InitNodesFrom(_headIndex);
        }

        public int Capacity => _nodes.Length;

        public bool IsFull => _headIndex == InvalidAllocationIndex;

        public void Dispose()
        {
            _nodes.Dispose();
        }

        private unsafe void InitNodesFrom(int startingNodeIndex)
        {
            var pNodes = (Node*) _nodes.GetUnsafePtr();

            for (int i = startingNodeIndex; i < _nodes.Length - 1; i++)
            {
                pNodes[i] = new Node
                {
                    Generation = 0,
                    NextIndex = i + 1,
                };
            }

            pNodes[_nodes.Length - 1] = new Node
            {
                Generation = 0,
                NextIndex = InvalidAllocationIndex,
            };
        }

        public IndexAllocation Allocate()
        {
            if (IsFull)
            {
                if (_autoGrow)
                {
                    ForceGrow(_nodes.Length * 2);
                }

                if (IsFull)
                {
                    return IndexAllocation.Invalid;
                }
            }

            ref Node node = ref _nodes.ElementAtRef(_headIndex);

            IndexAllocation allocation = new()
            {
                Generation = node.Generation,
                Index = _headIndex,
            };

            _headIndex = node.NextIndex;
            node.NextIndex = InvalidAllocationIndex;
            return allocation;
        }

        public void ForceGrow(int ensuredCapacity)
        {
            if (ensuredCapacity <= Capacity)
            {
                return;
            }

            int oldCapacity = Capacity;
            int newCapacity = AAAAMathUtils.AlignUp(ensuredCapacity, oldCapacity);
            _nodes.ResizeArray(newCapacity);
            _headIndex = oldCapacity;
            InitNodesFrom(_headIndex);
        }

        public int GetNodeGeneration(int index)
        {
            Assert.IsTrue(index != InvalidAllocationIndex);
            return _nodes[index].Generation;
        }

        public void Free(IndexAllocation allocation)
        {
            Assert.IsTrue(allocation.Index >= 0);
            Assert.IsTrue(allocation.Index < _nodes.Length);

            ref Node node = ref _nodes.ElementAtRef(allocation.Index);
            Assert.IsTrue(node.Generation == allocation.Generation);

            node.NextIndex = _headIndex;
            ++node.Generation;
            _headIndex = allocation.Index;
        }

        private struct Node
        {
            public int NextIndex;
            public int Generation;
        }

        public struct IndexAllocation : IEquatable<IndexAllocation>
        {
            public int Index;
            public int Generation;

            public static readonly IndexAllocation Invalid = new()
            {
                Index = InvalidAllocationIndex,
                Generation = 0,
            };

            public bool Equals(IndexAllocation other) => Index == other.Index && Generation == other.Generation;

            public override bool Equals(object obj) => obj is IndexAllocation other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Index, Generation);
        }
    }
}