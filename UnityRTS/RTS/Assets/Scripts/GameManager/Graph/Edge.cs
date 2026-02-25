namespace GameManager.Graph
{
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
        internal Edge(Edge<V> edge)
        {
            this.start = edge.start;
            this.end = edge.end;
            this.cost = edge.cost;
        }

        internal Node<V> GetNeighbor(Node<V> item)
        {
            if (start == item)
                return end;
            else
                return start;
        }
    }
}
