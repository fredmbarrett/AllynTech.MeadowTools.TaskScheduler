// Copyright (c) 2025 Allyn Technology Group
// Licensed under the Apache License, Version 2.0.
// See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace AllynTech.MeadowTools.TaskScheduler
{
    /// <summary>
    /// A generic min-heap (priority queue) implementation backed by a <see cref="List{T}"/>.
    /// The smallest element according to the supplied comparer is always at the root (index 0).
    /// </summary>
    /// <typeparam name="T">The type of elements stored in the heap.</typeparam>
    internal sealed class MinHeap<T>
    {
        // Internal storage for heap elements.
        private readonly List<T> _data = [];

        // The comparer used to determine priority (min value is highest priority).
        private readonly IComparer<T> _cmp;

        /// <summary>
        /// Initializes a new instance of <see cref="MinHeap{T}"/> using the specified comparer.
        /// If comparer is null, the default comparer for T is used.
        /// </summary>
        public MinHeap(IComparer<T> comparer) => _cmp = comparer ?? Comparer<T>.Default;

        /// <summary>
        /// Number of elements in the heap.
        /// </summary>
        public int Count => _data.Count;

        /// <summary>
        /// Returns true if the heap contains any elements.
        /// </summary>
        public bool Any => _data.Count > 0;

        /// <summary>
        /// Removes all elements from the heap.
        /// </summary>
        public void Clear() => _data.Clear();

        /// <summary>
        /// Adds an element to the heap and re-establishes heap ordering.
        /// </summary>
        public void Push(T item)
        {
            _data.Add(item);
            SiftUp(_data.Count - 1);
        }

        /// <summary>
        /// Returns the minimum element without removing it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the heap is empty.</exception>
        public T Peek()
        {
            if (_data.Count == 0) throw new InvalidOperationException("Heap is empty");
            return _data[0];
        }

        /// <summary>
        /// Removes and returns the minimum element from the heap.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the heap is empty.</exception>
        public T Pop()
        {
            if (_data.Count == 0) throw new InvalidOperationException("Heap is empty");

            var root = _data[0];
            var last = _data[_data.Count - 1];
            _data.RemoveAt(_data.Count - 1);

            if (_data.Count > 0)
            {
                _data[0] = last;
                SiftDown(0);
            }

            return root;
        }

        /// <summary>
        /// Removes all elements matching the specified predicate.
        /// Re-heapifies if any elements are removed.
        /// </summary>
        /// <param name="match">Predicate to match elements for removal.</param>
        /// <returns>True if any elements were removed; otherwise false.</returns>
        public bool RemoveWhere(Predicate<T> match)
        {
            bool removedAny = false;
            for (int i = _data.Count - 1; i >= 0; i--)
            {
                if (match(_data[i]))
                {
                    removedAny = true;
                    _data.RemoveAt(i);
                }
            }
            if (removedAny) Heapify();
            return removedAny;
        }

        /// <summary>
        /// Restores the heap property for all elements (O(n) operation).
        /// Useful after bulk removals or unordered modifications.
        /// </summary>
        private void Heapify()
        {
            for (int i = _data.Count / 2 - 1; i >= 0; i--)
                SiftDown(i);
        }

        /// <summary>
        /// Moves the element at index i up the heap until the heap property is restored.
        /// </summary>
        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2; // parent index
                if (_cmp.Compare(_data[i], _data[p]) >= 0) break;
                Swap(i, p);
                i = p;
            }
        }

        /// <summary>
        /// Moves the element at index i down the heap until the heap property is restored.
        /// </summary>
        private void SiftDown(int i)
        {
            int n = _data.Count;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, s = i; // left, right, smallest
                if (l < n && _cmp.Compare(_data[l], _data[s]) < 0) s = l;
                if (r < n && _cmp.Compare(_data[r], _data[s]) < 0) s = r;
                if (s == i) break;
                Swap(i, s);
                i = s;
            }
        }

        /// <summary>
        /// Swaps two elements in the underlying list.
        /// </summary>
        private void Swap(int a, int b)
        {
            var t = _data[a];
            _data[a] = _data[b];
            _data[b] = t;
        }
    }
}
