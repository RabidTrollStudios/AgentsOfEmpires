namespace GameManager.Graph
{
	/// <summary>
	/// An undirected weighted edge connecting two <see cref="Node{V}"/> instances.
	/// Stored in both endpoint nodes' adjacency lists so traversal works
	/// from either side via <see cref="GetNeighbor"/>.
	/// </summary>
	internal class Edge<V> where V : IColorable, IBuildable
    {
        internal Node<V> start;
        internal Node<V> end;
        internal double cost;

        internal Edge(Node<V> start, Node<V> end, double cost)
        {
            this.start = start;
            this.end = end;
            this.cost = cost;
        }

        /// <summary>Copy constructor.</summary>
        internal Edge(Edge<V> edge)
        {
            this.start = edge.start;
            this.end = edge.end;
            this.cost = edge.cost;
        }

        /// <summary>
        /// Given one endpoint of this edge, returns the other.
        /// This is how the graph supports undirected traversal — each edge
        /// appears in both nodes' adjacency lists.
        /// </summary>
        internal Node<V> GetNeighbor(Node<V> item)
        {
            if (start == item)
                return end;
            else
                return start;
        }
    }
}
