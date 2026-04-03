using System.Collections.Generic;

namespace GameManager.Graph
{
	/// <summary>
	/// A node in the <see cref="Graph{T}"/>. Wraps a domain item (e.g., <see cref="GridCell"/>)
	/// and maintains its adjacency list plus transient A* search state.
	/// </summary>
	internal class Node<V> where V : IColorable, IBuildable
    {
        /// <summary>Adjacent edges (undirected — each edge appears in both endpoint nodes).</summary>
        public List<Edge<V>> edges = new List<Edge<V>>();

        /// <summary>The domain object this node represents (e.g., a GridCell).</summary>
        public V item;

        /// <summary>Unique identifier matching the node's key in <see cref="Graph{T}.nodesDict"/>.</summary>
        public int number;

        // --- A* transient search state (reset between searches) ---

        /// <summary>Best-known g-cost from the start node. Reset to MaxValue before each search.</summary>
        public double cost = double.MaxValue;

        /// <summary>Back-pointer to the predecessor in the current search path.</summary>
		public PriorityNode<Node<V>> backPtr = null;

        /// <summary>This node's entry in the priority queue (null if not yet enqueued).</summary>
		public PriorityNode<Node<V>> priorityNode = null;

        public Node(int number, V item)
        {
            this.number = number;
            this.item = item;
        }

        /// <summary>Copy constructor (copies identity, not search state).</summary>
        public Node(Node<V> node)
        {
            this.number = node.number;
            this.item = node.item;
        }

        /// <summary>Clear transient A* state so this node can participate in a new search.</summary>
        public void ResetSearchVariables()
        {
            cost = double.MaxValue;
            backPtr = null;
            priorityNode = null;
        }
    }
}
