using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaseGraph
{
    public class Graph<T>
    {
        private readonly Dictionary<T, HashSet<T>> adjacency =
            new Dictionary<T, HashSet<T>>();

        public IEnumerable<T> Nodes => adjacency.Keys;

        public void AddNode(T node)
        {
            if (!adjacency.ContainsKey(node))
                adjacency[node] = new HashSet<T>();
        }

        public void AddEdge(T a, T b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException();

            AddNode(a);
            AddNode(b);

            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }
        
        public IReadOnlyCollection<T> GetNeighbors(T node)
        {
            if (!adjacency.TryGetValue(node, out var neighbors))
                throw new KeyNotFoundException();

            return neighbors;
        }

        public bool HasEdge(T a, T b)
        {
            return adjacency.ContainsKey(a) &&
                    adjacency[a].Contains(b);
        }
    }
}
