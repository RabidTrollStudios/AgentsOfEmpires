using System.Collections.Generic;

namespace GameManager.Graph
{
	internal class Node<V> where V : IColorable, IBuildable
    {
        public List<Edge<V>> edges = new List<Edge<V>>();
        public V item;
        public int number;

        // Searching variables
        public double cost = double.MaxValue;
		public PriorityNode<Node<V>> backPtr = null;
		public PriorityNode<Node<V>> priorityNode = null;

        public Node(int number, V item)
        {
            this.number = number;
            this.item = item;
        }
        public Node(Node<V> node)
        {
            this.number = node.number;
            this.item = node.item;
        }

        public void ResetSearchVariables()
        {
            cost = double.MaxValue;
            backPtr = null;
            priorityNode = null;
        }
    }

}
