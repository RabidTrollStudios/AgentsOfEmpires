using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameManager.Graph
{
	/// <summary>
	/// Generic undirected weighted graph with A* pathfinding.
	///
	/// Used by <see cref="MapManager"/> with <see cref="GridCell"/> as the node type
	/// to provide grid-based pathfinding on the Unity side. Each GridCell becomes a
	/// node; edges connect the 8 cardinal/diagonal neighbors with Euclidean costs
	/// (1.0 cardinal, ~1.414 diagonal).
	///
	/// The A* implementation uses <see cref="PriorityQueue{T}"/> as its open set
	/// and Euclidean distance as its heuristic. It supports both normal pathfinding
	/// (walkable cells) and unit-avoidance mode (buildable cells only).
	///
	/// Note: This is the Unity-side graph used for the visual game. The shared
	/// deterministic pathfinder lives in <c>AgentSDK.Pathfinder</c>.
	/// </summary>
	internal class Graph<T> where T : IColorable, IBuildable, IPositionable
    {
        /// <summary>When true, all edge weights are equal (enables BFS). Currently unused.</summary>
        public static bool isUniform = false;

        /// <summary>All nodes keyed by their integer ID (typically grid cell index).</summary>
        public Dictionary<int, Node<T>> nodesDict = new Dictionary<int, Node<T>>();

        /// <summary>All edges in the graph (each edge also appears in its endpoint nodes' adjacency lists).</summary>
        public List<Edge<T>> edges = new List<Edge<T>>();

        /// <summary>Reusable priority queue for A* to avoid per-search allocation.</summary>
        PriorityQueue<Node<T>> pq = new PriorityQueue<Node<T>>();

        public Graph()
        {
        }

        /// <summary>Deep-copy constructor: duplicates all nodes and edges from another graph.</summary>
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

        /// <summary>Add a node wrapping <paramref name="startItem"/> with the given numeric key.</summary>
        public void AddNode(int number, T startItem)
        {
            Node<T> node = new Node<T>(number, startItem);
            nodesDict.Add(number, node);
        }

        /// <summary>
        /// Add an undirected edge between two existing nodes.
        /// The edge is stored in the master list and in both endpoints' adjacency lists.
        /// </summary>
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

        /// <summary>
        /// Find the neighbor of <paramref name="endNodeNbr"/> that is closest
        /// to <paramref name="startNodeNbr"/> by Euclidean distance.
        /// Returns -1 if no neighbors exist.
        /// </summary>
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

        /// <summary>
        /// Breadth-first search (stub). Only valid when <see cref="isUniform"/> is true.
        /// Currently throws — all pathfinding uses <see cref="AStarSearch"/> instead.
        /// </summary>
        public List<T> BreadthFirstSearch(int startNodeNbr, int endNodeNbr)
        {
            List<T> path = new List<T>();

            if (!isUniform)
            {
                throw new Exception("Cannot perform breadth-first on a non-uniform edge weight graph");
            }

            return path;
        }

        /// <summary>Clear transient search state on all nodes before a new A* search.</summary>
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

        /// <summary>
        /// A* shortest-path search from <paramref name="startNodeNbr"/> to <paramref name="endNodeNbr"/>.
        ///
        /// Returns a list of node IDs from start (exclusive) to end (inclusive), or
        /// an empty list if no path exists.
        ///
        /// When <paramref name="avoidUnits"/> is true, only nodes where
        /// <see cref="IBuildable.IsBuildable"/> is true are expanded (avoids cells
        /// occupied by mobile units). Otherwise, <see cref="IBuildable.IsWalkable"/>
        /// is used (standard terrain passability).
        ///
        /// The start node is allowed to be impassable — units may be inside a building
        /// and need to pathfind out. The end node must be passable.
        ///
        /// Search is capped at <paramref name="maxExpansions"/> node expansions to
        /// prevent unbounded computation on large or disconnected maps.
        /// Results are recorded in <see cref="LastSearchResult"/> and <see cref="LastSearchExpansions"/>.
        /// </summary>
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
