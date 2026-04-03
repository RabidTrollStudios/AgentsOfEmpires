namespace GameManager.Graph
{
	/// <summary>
	/// An undirected weighted edge connecting two <see cref="Node{V}"/> instances.
	/// Stored in both endpoint nodes' adjacency lists so traversal works
	/// from either side via <see cref="GetNeighbor"/>.
	/// </summary>
	internal class Edge<V> where V : IColorable, IBuildable
    {
        internal Node<V> Start;
        internal Node<V> End;
        internal double Cost;

        internal Edge(Node<V> start, Node<V> end, double cost)
        {
            this.Start = start;
            this.End = end;
            this.Cost = cost;
        }

        /// <summary>Copy constructor.</summary>
        internal Edge(Edge<V> edge)
        {
            this.Start = edge.Start;
            this.End = edge.End;
            this.Cost = edge.Cost;
        }

        /// <summary>
        /// Given one endpoint of this edge, returns the other.
        /// This is how the graph supports undirected traversal — each edge
        /// appears in both nodes' adjacency lists.
        /// </summary>
        internal Node<V> GetNeighbor(Node<V> item)
        {
            if (Start == item)
                return End;
            else
                return Start;
        }
    }
}
