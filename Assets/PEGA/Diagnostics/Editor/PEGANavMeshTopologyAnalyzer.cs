using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace PEGA.Diagnostics
{
    internal static class PEGANavMeshTopologyAnalyzer
    {
        private const int MaxGridSamples = 150000;

        public static bool TryAnalyze(
            NavMeshSurface surface,
            float spacing,
            out Snapshot snapshot,
            out string diagnostic)
        {
            snapshot = null;
            diagnostic = null;

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length < 3 ||
                triangulation.indices == null || triangulation.indices.Length < 3)
            {
                diagnostic = "A cena não forneceu uma triangulação NavMesh utilizável. Confira os dados assados no Inspector da NavMeshSurface.";
                return false;
            }

            Bounds bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
            for (int index = 1; index < triangulation.vertices.Length; index++)
            {
                bounds.Encapsulate(triangulation.vertices[index]);
            }

            int width = Mathf.CeilToInt(bounds.size.x / spacing);
            int height = Mathf.CeilToInt(bounds.size.z / spacing);
            long gridSize = (long)width * height;
            if (width < 1 || height < 1 || gridSize > MaxGridSamples)
            {
                diagnostic = $"A grade teria {gridSize:N0} células. Aumente a resolução até ficar abaixo de {MaxGridSamples:N0}.";
                return false;
            }

            int cellCount = (int)gridSize;
            bool[] walkable = new bool[cellCount];
            Vector3[] positions = new Vector3[cellCount];
            if (!TryRasterizeTriangles(
                    triangulation.vertices,
                    triangulation.indices,
                    bounds.min,
                    spacing,
                    width,
                    height,
                    walkable,
                    positions,
                    out int walkableCount,
                    out diagnostic))
            {
                return false;
            }

            bool[] skeleton = (bool[])walkable.Clone();
            Thin(skeleton, width, height);
            snapshot = BuildSnapshot(
                skeleton,
                walkable,
                positions,
                width,
                height,
                cellCount,
                walkableCount,
                triangulation.indices.Length / 3,
                surface.agentTypeID);
            if (snapshot.Nodes.Count == 0)
            {
                diagnostic = "A amostragem encontrou área transitável, mas não conseguiu extrair o esqueleto.";
                snapshot = null;
                return false;
            }

            return true;
        }

        private static bool TryRasterizeTriangles(
            Vector3[] vertices,
            int[] indices,
            Vector3 boundsMin,
            float spacing,
            int width,
            int height,
            bool[] walkable,
            Vector3[] positions,
            out int walkableCount,
            out string diagnostic)
        {
            walkableCount = 0;
            diagnostic = null;
            if (indices.Length % 3 != 0)
            {
                diagnostic = "A triangulação NavMesh contém um índice incompleto de triângulos.";
                return false;
            }

            const float minimumProjectedArea = 0.0000001f;
            Vector2[] polygonA = new Vector2[12];
            Vector2[] polygonB = new Vector2[12];
            for (int triangle = 0; triangle < indices.Length; triangle += 3)
            {
                int indexA = indices[triangle];
                int indexB = indices[triangle + 1];
                int indexC = indices[triangle + 2];
                if ((uint)indexA >= (uint)vertices.Length ||
                    (uint)indexB >= (uint)vertices.Length ||
                    (uint)indexC >= (uint)vertices.Length)
                {
                    diagnostic = "A triangulação NavMesh contém uma referência a vértice inválida.";
                    return false;
                }

                Vector3 a = vertices[indexA];
                Vector3 b = vertices[indexB];
                Vector3 c = vertices[indexC];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) <= minimumProjectedArea)
                {
                    continue;
                }

                float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
                float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
                float maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - boundsMin.x) / spacing) - 1, 0, width - 1);
                int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - boundsMin.x) / spacing) + 1, 0, width - 1);
                int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - boundsMin.z) / spacing) - 1, 0, height - 1);
                int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - boundsMin.z) / spacing) + 1, 0, height - 1);

                for (int z = minCellZ; z <= maxCellZ; z++)
                {
                    float cellMinZ = boundsMin.z + z * spacing;
                    float cellMaxZ = cellMinZ + spacing;
                    for (int x = minCellX; x <= maxCellX; x++)
                    {
                        int cell = z * width + x;
                        if (walkable[cell])
                        {
                            continue;
                        }

                        float cellMinX = boundsMin.x + x * spacing;
                        float cellMaxX = cellMinX + spacing;
                        polygonA[0] = new Vector2(a.x, a.z);
                        polygonA[1] = new Vector2(b.x, b.z);
                        polygonA[2] = new Vector2(c.x, c.z);
                        int polygonCount = ClipPolygon(polygonA, 3, polygonB, 0, cellMinX, true);
                        polygonCount = ClipPolygon(polygonB, polygonCount, polygonA, 0, cellMaxX, false);
                        polygonCount = ClipPolygon(polygonA, polygonCount, polygonB, 1, cellMinZ, true);
                        polygonCount = ClipPolygon(polygonB, polygonCount, polygonA, 1, cellMaxZ, false);
                        if (polygonCount < 3 || !TryGetPolygonCentroid(polygonA, polygonCount, out Vector2 centroid))
                        {
                            continue;
                        }

                        float weightA = ((b.z - c.z) * (centroid.x - c.x) + (c.x - b.x) * (centroid.y - c.z)) / denominator;
                        float weightB = ((c.z - a.z) * (centroid.x - c.x) + (a.x - c.x) * (centroid.y - c.z)) / denominator;
                        float weightC = 1f - weightA - weightB;
                        walkable[cell] = true;
                        walkableCount++;

                        positions[cell] = new Vector3(
                            centroid.x,
                            weightA * a.y + weightB * b.y + weightC * c.y,
                            centroid.y);
                    }
                }
            }

            if (walkableCount == 0)
            {
                diagnostic = "Nenhuma célula interceptou os triângulos da NavMesh. Reduza a resolução da amostra.";
                return false;
            }

            return true;
        }

        private static int ClipPolygon(
            Vector2[] input,
            int inputCount,
            Vector2[] output,
            int axis,
            float boundary,
            bool keepGreater)
        {
            if (inputCount == 0) return 0;

            int outputCount = 0;
            Vector2 previous = input[inputCount - 1];
            bool previousInside = IsInside(previous, axis, boundary, keepGreater);
            for (int index = 0; index < inputCount; index++)
            {
                Vector2 current = input[index];
                bool currentInside = IsInside(current, axis, boundary, keepGreater);
                if (currentInside != previousInside)
                {
                    float previousValue = axis == 0 ? previous.x : previous.y;
                    float currentValue = axis == 0 ? current.x : current.y;
                    float denominator = currentValue - previousValue;
                    if (Mathf.Abs(denominator) > Mathf.Epsilon)
                    {
                        float t = (boundary - previousValue) / denominator;
                        output[outputCount++] = Vector2.LerpUnclamped(previous, current, t);
                    }
                }

                if (currentInside) output[outputCount++] = current;
                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static bool IsInside(Vector2 point, int axis, float boundary, bool keepGreater)
        {
            float value = axis == 0 ? point.x : point.y;
            return keepGreater ? value >= boundary : value <= boundary;
        }

        private static bool TryGetPolygonCentroid(Vector2[] polygon, int count, out Vector2 centroid)
        {
            centroid = Vector2.zero;
            float twiceArea = 0f;
            for (int index = 0; index < count; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % count];
                float cross = current.x * next.y - next.x * current.y;
                twiceArea += cross;
                centroid += (current + next) * cross;
            }

            if (Mathf.Abs(twiceArea) <= 0.0000001f) return false;
            centroid /= 3f * twiceArea;
            return true;
        }

        private static void Thin(bool[] grid, int width, int height)
        {
            int[] removals = new int[grid.Length];
            int iterationLimit = Mathf.Max(width, height);
            bool changed = true;
            int iterations = 0;
            while (changed && iterations++ < iterationLimit)
            {
                changed = ThinPass(grid, width, height, removals, true);
                changed |= ThinPass(grid, width, height, removals, false);
            }
        }

        private static bool ThinPass(bool[] grid, int width, int height, int[] removals, bool firstPass)
        {
            int removalCount = 0;
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int cell = y * width + x;
                    if (!grid[cell])
                    {
                        continue;
                    }

                    bool north = grid[cell - width];
                    bool northEast = grid[cell - width + 1];
                    bool east = grid[cell + 1];
                    bool southEast = grid[cell + width + 1];
                    bool south = grid[cell + width];
                    bool southWest = grid[cell + width - 1];
                    bool west = grid[cell - 1];
                    bool northWest = grid[cell - width - 1];
                    int neighborCount = CountNeighbors(
                        north, northEast, east, southEast, south, southWest, west, northWest);
                    if (neighborCount < 2 || neighborCount > 6 ||
                        CountTransitions(north, northEast, east, southEast, south, southWest, west, northWest) != 1)
                    {
                        continue;
                    }

                    bool preservesTopology = firstPass
                        ? (north && east && south) || (east && south && west)
                        : (north && east && west) || (north && south && west);
                    if (!preservesTopology)
                    {
                        removals[removalCount++] = cell;
                    }
                }
            }

            for (int index = 0; index < removalCount; index++)
            {
                grid[removals[index]] = false;
            }

            return removalCount > 0;
        }

        private static int CountNeighbors(
            bool p1, bool p2, bool p3, bool p4,
            bool p5, bool p6, bool p7, bool p8)
        {
            int count = 0;
            if (p1) count++;
            if (p2) count++;
            if (p3) count++;
            if (p4) count++;
            if (p5) count++;
            if (p6) count++;
            if (p7) count++;
            if (p8) count++;
            return count;
        }

        private static int CountTransitions(
            bool p1, bool p2, bool p3, bool p4,
            bool p5, bool p6, bool p7, bool p8)
        {
            int count = 0;
            if (!p1 && p2) count++;
            if (!p2 && p3) count++;
            if (!p3 && p4) count++;
            if (!p4 && p5) count++;
            if (!p5 && p6) count++;
            if (!p6 && p7) count++;
            if (!p7 && p8) count++;
            if (!p8 && p1) count++;
            return count;
        }

        private static Snapshot BuildSnapshot(
            bool[] skeleton,
            bool[] walkableCells,
            Vector3[] sampledPositions,
            int width,
            int height,
            int gridCellCount,
            int walkableCount,
            int triangleCount,
            int agentTypeId)
        {
            List<Vector3> walkableCellPositions = new List<Vector3>(walkableCount);
            for (int cell = 0; cell < walkableCells.Length; cell++)
            {
                if (walkableCells[cell]) walkableCellPositions.Add(sampledPositions[cell]);
            }

            List<Node> nodes = new List<Node>();
            int[] nodeByCell = new int[skeleton.Length];
            for (int cell = 0; cell < nodeByCell.Length; cell++)
            {
                nodeByCell[cell] = -1;
                if (skeleton[cell])
                {
                    nodeByCell[cell] = nodes.Count;
                    nodes.Add(new Node(sampledPositions[cell]));
                }
            }

            List<int>[] adjacency = new List<int>[nodes.Count];
            for (int index = 0; index < adjacency.Length; index++)
            {
                adjacency[index] = new List<int>(4);
            }

            List<Edge> edges = new List<Edge>();
            for (int cell = 0; cell < skeleton.Length; cell++)
            {
                int node = nodeByCell[cell];
                if (node < 0)
                {
                    continue;
                }

                int x = cell % width;
                int y = cell / width;
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        int neighborX = x + offsetX;
                        int neighborY = y + offsetY;
                        if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                        {
                            continue;
                        }

                        int neighborCell = neighborY * width + neighborX;
                        int neighbor = nodeByCell[neighborCell];
                        if (neighbor <= node)
                        {
                            continue;
                        }

                        bool diagonal = offsetX != 0 && offsetY != 0;
                        if (diagonal &&
                            (skeleton[y * width + neighborX] || skeleton[neighborY * width + x]))
                        {
                            continue;
                        }

                        adjacency[node].Add(neighbor);
                        adjacency[neighbor].Add(node);
                        edges.Add(new Edge(node, neighbor));
                    }
                }
            }

            HashSet<long> bridges = FindBridges(adjacency);
            bool[] deadEndNodes = MarkDeadEndBranches(adjacency);
            List<Vector3> junctionCenters = FindJunctionCenters(nodes, adjacency);
            for (int index = 0; index < nodes.Count; index++)
            {
                nodes[index].Degree = adjacency[index].Count;
                nodes[index].IsDeadEnd = deadEndNodes[index];
            }

            int alternativeEdgeCount = 0;
            for (int index = 0; index < edges.Count; index++)
            {
                Edge edge = edges[index];
                edge.IsAlternative = !bridges.Contains(EdgeKey(edge.From, edge.To));
                if (edge.IsAlternative) alternativeEdgeCount++;
                edges[index] = edge;
            }

            int connectedComponents = CountConnectedComponents(adjacency);
            string agentTypeName = NavMesh.GetSettingsNameFromID(agentTypeId);
            if (string.IsNullOrEmpty(agentTypeName)) agentTypeName = $"ID {agentTypeId}";

            int deadEndEndpoints = 0;
            for (int index = 0; index < adjacency.Length; index++)
            {
                if (adjacency[index].Count == 1) deadEndEndpoints++;
            }

            int cycleRank = Math.Max(0, edges.Count - nodes.Count + connectedComponents);
            return new Snapshot(
                nodes, adjacency, edges, junctionCenters, deadEndNodes, walkableCellPositions, gridCellCount,
                walkableCount, triangleCount,
                deadEndEndpoints, cycleRank, alternativeEdgeCount, connectedComponents, agentTypeName);
        }

        private static HashSet<long> FindBridges(List<int>[] adjacency)
        {
            int count = adjacency.Length;
            int[] discovery = new int[count];
            int[] low = new int[count];
            int[] parent = new int[count];
            int[] nextNeighbor = new int[count];
            for (int index = 0; index < count; index++) parent[index] = -1;

            HashSet<long> bridges = new HashSet<long>();
            Stack<int> stack = new Stack<int>();
            int clock = 0;
            for (int root = 0; root < count; root++)
            {
                if (discovery[root] != 0) continue;
                discovery[root] = low[root] = ++clock;
                stack.Push(root);
                while (stack.Count > 0)
                {
                    int node = stack.Peek();
                    if (nextNeighbor[node] < adjacency[node].Count)
                    {
                        int neighbor = adjacency[node][nextNeighbor[node]++];
                        if (neighbor == parent[node]) continue;
                        if (discovery[neighbor] == 0)
                        {
                            parent[neighbor] = node;
                            discovery[neighbor] = low[neighbor] = ++clock;
                            stack.Push(neighbor);
                        }
                        else
                        {
                            low[node] = Math.Min(low[node], discovery[neighbor]);
                        }

                        continue;
                    }

                    stack.Pop();
                    int parentNode = parent[node];
                    if (parentNode < 0) continue;
                    if (low[node] > discovery[parentNode]) bridges.Add(EdgeKey(node, parentNode));
                    low[parentNode] = Math.Min(low[parentNode], low[node]);
                }
            }

            return bridges;
        }

        private static int CountConnectedComponents(List<int>[] adjacency)
        {
            bool[] visited = new bool[adjacency.Length];
            Queue<int> queue = new Queue<int>();
            int components = 0;
            for (int root = 0; root < adjacency.Length; root++)
            {
                if (visited[root]) continue;
                components++;
                visited[root] = true;
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    int node = queue.Dequeue();
                    for (int index = 0; index < adjacency[node].Count; index++)
                    {
                        int neighbor = adjacency[node][index];
                        if (visited[neighbor]) continue;
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return components;
        }

        private static bool[] MarkDeadEndBranches(List<int>[] adjacency)
        {
            bool[] deadEndNodes = new bool[adjacency.Length];
            for (int endpoint = 0; endpoint < adjacency.Length; endpoint++)
            {
                if (adjacency[endpoint].Count != 1) continue;
                int previous = -1;
                int current = endpoint;
                while (!deadEndNodes[current])
                {
                    deadEndNodes[current] = true;
                    if (adjacency[current].Count != 2) break;
                    int next = adjacency[current][0] == previous ? adjacency[current][1] : adjacency[current][0];
                    previous = current;
                    current = next;
                }
            }

            return deadEndNodes;
        }

        private static List<Vector3> FindJunctionCenters(List<Node> nodes, List<int>[] adjacency)
        {
            bool[] visited = new bool[nodes.Count];
            Queue<int> queue = new Queue<int>();
            List<Vector3> centers = new List<Vector3>();
            for (int root = 0; root < nodes.Count; root++)
            {
                if (visited[root] || adjacency[root].Count < 3) continue;
                Vector3 center = Vector3.zero;
                int members = 0;
                visited[root] = true;
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    int node = queue.Dequeue();
                    center += nodes[node].Position;
                    members++;
                    for (int index = 0; index < adjacency[node].Count; index++)
                    {
                        int neighbor = adjacency[node][index];
                        if (visited[neighbor] || adjacency[neighbor].Count < 3) continue;
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                centers.Add(center / members);
            }

            return centers;
        }

        private static long EdgeKey(int first, int second)
        {
            int low = Math.Min(first, second);
            int high = Math.Max(first, second);
            return ((long)low << 32) | (uint)high;
        }

        internal sealed class Snapshot
        {
            public Snapshot(
                List<Node> nodes,
                List<int>[] adjacency,
                List<Edge> edges,
                List<Vector3> junctionCenters,
                bool[] deadEndNodes,
                List<Vector3> walkableCellPositions,
                int gridCellCount,
                int walkableSamples,
                int triangleCount,
                int deadEndEndpoints,
                int cycleRank,
                int alternativeEdgeCount,
                int connectedComponents,
                string agentTypeName)
            {
                Nodes = nodes;
                Adjacency = adjacency;
                Edges = edges;
                JunctionCenters = junctionCenters;
                DeadEndNodes = deadEndNodes;
                WalkableCellPositions = walkableCellPositions;
                GridCellCount = gridCellCount;
                WalkableSamples = walkableSamples;
                TriangleCount = triangleCount;
                DeadEndEndpoints = deadEndEndpoints;
                CycleRank = cycleRank;
                AlternativeEdgeCount = alternativeEdgeCount;
                ConnectedComponents = connectedComponents;
                AgentTypeName = agentTypeName;
            }

            public List<Node> Nodes { get; }
            public List<int>[] Adjacency { get; }
            public List<Edge> Edges { get; }
            public List<Vector3> JunctionCenters { get; }
            public bool[] DeadEndNodes { get; }
            public List<Vector3> WalkableCellPositions { get; }
            public int GridCellCount { get; }
            public int WalkableSamples { get; }
            public int TriangleCount { get; }
            public int DeadEndEndpoints { get; }
            public int CycleRank { get; }
            public int AlternativeEdgeCount { get; }
            public int ConnectedComponents { get; }
            public string AgentTypeName { get; }
        }

        internal sealed class Node
        {
            public Node(Vector3 position) { Position = position; }
            public Vector3 Position { get; }
            public int Degree { get; set; }
            public bool IsDeadEnd { get; set; }
        }

        internal struct Edge
        {
            public Edge(int from, int to) { From = from; To = to; IsAlternative = false; }
            public int From { get; }
            public int To { get; }
            public bool IsAlternative { get; set; }
        }
    }
}
