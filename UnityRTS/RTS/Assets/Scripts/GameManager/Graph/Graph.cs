using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameManager.Graph
{
	internal class Graph<T> where T : IColorable, IBuildable, IPositionable
    {
        public static bool isUniform = false;

        public Dictionary<int, Node<T>> nodesDict = new Dictionary<int, Node<T>>();
        public List<Edge<T>> edges = new List<Edge<T>>();
        PriorityQueue<Node<T>> pq = new PriorityQueue<Node<T>>();

        public Graph()
        {
        }

        public Graph(Graph<T> graph)
        {
            foreach(Node<T> node in graph.nodesDict.Values)
            {
                AddNode(node.number, node.item);
            }
            foreach (Edge<T> edge in graph.edges)
            {
                AddEdge(edge.start.number, edge.end.number, edge.cost);
            }
        }

        public void AddNode(int number, T startItem)
        {
            Node<T> node = new Node<T>(number, startItem);
            nodesDict.Add(number, node);
        }

        public void AddEdge(int startNodeNbr, int endNodeNbr, double cost)
        {
            Node<T> start = nodesDict[startNodeNbr];
            Node<T> end = nodesDict[endNodeNbr];
            Edge<T> edge = new Edge<T>(start, end, cost);
            edges.Add(edge);
            start.edges.Add(edge);
            end.edges.Add(edge);
        }

        /// <summary>
        /// Compute the heuristic (Euclidean distance) between two nodes on the fly
        /// </summary>
        private double EstimateCost(int nodeA, int nodeB)
        {
            return Vector3.Distance(nodesDict[nodeA].item.GetPosition(),
                                    nodesDict[nodeB].item.GetPosition());
        }

        public int FindClosestNeighborToTarget(int startNodeNbr, int endNodeNbr)
        {
            int closestNodeNbr = -1;
            double closestDistance = double.MaxValue;
            foreach (Edge<T> edge in nodesDict[endNodeNbr].edges)
            {
                Node<T> neighbor = edge.GetNeighbor(nodesDict[startNodeNbr]);
                double dist = EstimateCost(startNodeNbr, neighbor.number);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestNodeNbr = neighbor.number;
                }
            }
            return closestNodeNbr;
        }

        public List<T> BreadthFirstSearch(int startNodeNbr, int endNodeNbr)
        {
            List<T> path = new List<T>();

            if (!isUniform)
            {
                throw new Exception("Cannot perform breadth-first on a non-uniform edge weight graph");
            }

            return path;
        }

        // Reset for AStarSearch
        public void ResetSearch()
        {
            foreach (Node<T> node in nodesDict.Values)
            {
                node.ResetSearchVariables();
            }
        }
        /// <summary>Number of node expansions used in the last AStarSearch call</summary>
        public int LastSearchExpansions { get; private set; }

        /// <summary>Why the last search ended: "found", "cap", "exhausted", "same_node", "end_blocked"</summary>
        public string LastSearchResult { get; private set; }

        public List<int> AStarSearch(int startNodeNbr, int endNodeNbr, int maxExpansions = 2000, bool avoidUnits = false)
        {
			List<int> path = new List<int>();
            LastSearchExpansions = 0;

            // Prepare for the search
            ResetSearch();
            path.Clear();
            pq.Clear();

            if (startNodeNbr == endNodeNbr)
            {
                LastSearchResult = "same_node";
                return path;
            }

            // Early-exit: if the end node is blocked, no path can reach it
            if (avoidUnits ? !nodesDict[endNodeNbr].item.IsBuildable() : !nodesDict[endNodeNbr].item.IsWalkable())
            {
                LastSearchResult = "end_blocked";
                return path;
            }

            // NOTE: We intentionally do NOT check start-node walkability here.
            // Units may legitimately be on unwalkable cells (e.g., inside their
            // own building after construction/training) and need to pathfind out.
            // The expansion loop at line 175 handles this: only walkable neighbors
            // are enqueued, so the path naturally exits the unwalkable area.

            // Add the first node to the priorityQueue
            nodesDict[startNodeNbr].cost = 0.0f;
            PriorityNode<Node<T>> currPNode = new PriorityNode<Node<T>>(nodesDict[startNodeNbr],
                         nodesDict[startNodeNbr].cost + EstimateCost(startNodeNbr, endNodeNbr));
            nodesDict[startNodeNbr].priorityNode = currPNode;
            pq.Enqueue(nodesDict[startNodeNbr].priorityNode);

            // While there are still items in the priorityQueue
            int expansions = 0;
            while (pq.Count > 0)
            {
                // Abort if we've exceeded the expansion budget
                if (++expansions > maxExpansions)
                {
                    LastSearchExpansions = expansions;
                    LastSearchResult = "cap";
                    return path;
                }
                // Pop off the first item in the queue
                currPNode = pq.Dequeue();

                // If this is the end node, success!
	            if (currPNode.item.number == endNodeNbr)
	            {
		            // Reverse-engineer the path
		            while (currPNode != null)
		            {
			            path.Add(currPNode.item.number);
			            currPNode = currPNode.item.backPtr;
		            }

		            // Reverse the path
		            path.Reverse();
					path.RemoveAt(0);
                    LastSearchExpansions = expansions;
                    LastSearchResult = "found";
		            return path;
				}

				// For each edge attached to this node, expand it
				foreach (Edge<T> edge in currPNode.item.edges)
                {
                    // Get the neighbor of this node via the edge
                    Node<T> neighbor = edge.GetNeighbor(currPNode.item);

                    // If the node can be traversed (avoidUnits: only truly empty cells; normal: passable terrain)
                    if (avoidUnits ? neighbor.item.IsBuildable() : neighbor.item.IsWalkable())
                    {
                        // Calculate the new cost through this node to this neighbor
                        double newCost = currPNode.item.cost + edge.cost + EstimateCost(neighbor.number, endNodeNbr);

                        // If the item is already in the queue, update its priority if necessary
                        if (neighbor.priorityNode != null && newCost < neighbor.priorityNode.priority)
                        {
                            neighbor.cost = currPNode.item.cost + edge.cost;
                            neighbor.backPtr = currPNode;
                            pq.ChangePriority(neighbor.priorityNode, newCost);
                        }
                        // If the item has not yet been seen, start tracking it
                        else if (neighbor.priorityNode == null)
                        {
                            neighbor.priorityNode = new PriorityNode<Node<T>>(neighbor, newCost);
                            neighbor.backPtr = currPNode;
                            neighbor.cost = currPNode.item.cost + edge.cost;
                            pq.Enqueue(neighbor.priorityNode);
                        }
                    }
                }
            }

			LastSearchExpansions = expansions;
			LastSearchResult = "exhausted";
			return path;
        }
    }
}
