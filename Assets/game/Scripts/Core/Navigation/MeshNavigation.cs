using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core.Navigation
{
    // 問い合わせのたびに作業用の配列を使い回すので、同時に複数のスレッドから呼ばない。
    public sealed class MeshNavigation : INavigation
    {
        public const float DefaultSnapDistance = 1f;

        // Unity の NavMesh はタイルの境目で頂点がずれたり T 字に分かれたりするので、頂点の添え字ではなく辺の重なりでつなぐ。
        private const float _edgeHorizontalTolerance = 0.02f;
        private const float _edgeVerticalTolerance = 0.25f;
        private const float _minDegenerateArea = 1e-8f;
        private const float _samePointSq = 1e-8f;

        private readonly NavLink[] _links;
        private readonly float[] _areaCosts;
        private readonly float _snapDistance;

        private readonly Vector3[] _a;
        private readonly Vector3[] _b;
        private readonly Vector3[] _c;
        private readonly int[] _area;
        private readonly XzGrid _grid;

        private readonly int[] _portalStarts;
        private readonly Portal[] _portals;
        private readonly int[] _linkStepStarts;
        private readonly int[] _linkSteps;
        private readonly int[] _linkStartTriangle;
        private readonly int[] _linkEndTriangle;
        private readonly Vector3[] _linkStart;
        private readonly Vector3[] _linkEnd;

        private readonly float[] _g;
        private readonly int[] _parent;
        private readonly int[] _via;
        private readonly Vector3[] _position;
        private readonly int[] _visited;
        private readonly int[] _closed;
        private readonly MinHeap _open = new MinHeap();
        private readonly List<int> _candidates = new List<int>();
        private readonly List<int> _steps = new List<int>();
        private readonly List<(Vector3 Left, Vector3 Right)> _funnelPortals = new List<(Vector3 Left, Vector3 Right)>();
        private readonly List<Vector3> _corners = new List<Vector3>();
        private readonly List<NavPathPoint> _points = new List<NavPathPoint>();
        private int _search;

        public MeshNavigation(NavMeshFile file, float snapDistance = DefaultSnapDistance)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (!(snapDistance > 0f) || float.IsInfinity(snapDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(snapDistance), snapDistance, "snap distance must be positive and finite");
            }

            _snapDistance = snapDistance;
            _areaCosts = new float[NavMeshFile.AreaCount];
            for (var i = 0; i < _areaCosts.Length; i++)
            {
                _areaCosts[i] = file.AreaCosts[i];
            }

            var a = new List<Vector3>();
            var b = new List<Vector3>();
            var c = new List<Vector3>();
            var area = new List<int>();
            foreach (var triangle in file.Triangles)
            {
                var va = file.Vertices[triangle.A];
                var vb = file.Vertices[triangle.B];
                var vc = file.Vertices[triangle.C];
                var winding = NavFunnel.Cross(va, vb, vc);
                if (Math.Abs(winding) < _minDegenerateArea)
                {
                    continue;
                }

                // 辺の左右を決めるのに、上から見て反時計回りにそろえておく。
                if (winding < 0f)
                {
                    (vb, vc) = (vc, vb);
                }

                a.Add(va);
                b.Add(vb);
                c.Add(vc);
                area.Add(triangle.Area);
            }

            _a = a.ToArray();
            _b = b.ToArray();
            _c = c.ToArray();
            _area = area.ToArray();
            var count = _a.Length;
            var boxes = new (float MinX, float MinZ, float MaxX, float MaxZ)[count];
            for (var i = 0; i < count; i++)
            {
                boxes[i] = (
                    Math.Min(_a[i].X, Math.Min(_b[i].X, _c[i].X)),
                    Math.Min(_a[i].Z, Math.Min(_b[i].Z, _c[i].Z)),
                    Math.Max(_a[i].X, Math.Max(_b[i].X, _c[i].X)),
                    Math.Max(_a[i].Z, Math.Max(_b[i].Z, _c[i].Z)));
            }

            _grid = new XzGrid(boxes);
            (_portalStarts, _portals) = BuildPortals();

            _links = new NavLink[file.Links.Count];
            _linkStartTriangle = new int[_links.Length];
            _linkEndTriangle = new int[_links.Length];
            _linkStart = new Vector3[_links.Length];
            _linkEnd = new Vector3[_links.Length];
            var stepLists = new List<int>[count];
            for (var i = 0; i < _links.Length; i++)
            {
                var link = file.Links[i];
                _links[i] = link;
                var startFound = FindNearest(link.Start, _snapDistance, ~0, out _linkStartTriangle[i], out _linkStart[i]);
                var endFound = FindNearest(link.End, _snapDistance, ~0, out _linkEndTriangle[i], out _linkEnd[i]);
                if (!startFound || !endFound || _linkStartTriangle[i] == _linkEndTriangle[i])
                {
                    _linkStartTriangle[i] = -1;
                    _linkEndTriangle[i] = -1;
                    continue;
                }

                AddLinkStep(stepLists, _linkStartTriangle[i], LinkStep(i, false));
                if (link.Bidirectional)
                {
                    AddLinkStep(stepLists, _linkEndTriangle[i], LinkStep(i, true));
                }
            }

            _linkStepStarts = new int[count + 1];
            var steps = new List<int>();
            for (var i = 0; i < count; i++)
            {
                _linkStepStarts[i] = steps.Count;
                if (stepLists[i] != null)
                {
                    steps.AddRange(stepLists[i]);
                }
            }

            _linkStepStarts[count] = steps.Count;
            _linkSteps = steps.ToArray();

            _g = new float[count];
            _parent = new int[count];
            _via = new int[count];
            _position = new Vector3[count];
            _visited = new int[count];
            _closed = new int[count];
        }

        public IReadOnlyList<NavLink> Links => _links;

        public bool IsLinkConnected(int linkId)
        {
            return linkId >= 0 && linkId < _links.Length && _linkStartTriangle[linkId] >= 0;
        }

        public bool SamplePosition(Vector3 position, float maxDistance, int areaMask, out Vector3 nearest)
        {
            if (FindNearest(position, maxDistance, areaMask, out _, out var point))
            {
                nearest = point;
                return true;
            }

            nearest = position;
            return false;
        }

        public bool CalculatePath(Vector3 from, Vector3 destination, int areaMask, NavPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            path.Clear();
            if (!FindNearest(from, _snapDistance, areaMask, out var startTriangle, out var startPoint))
            {
                return false;
            }

            if (!FindNearest(destination, _snapDistance, areaMask, out var goalTriangle, out var goalPoint))
            {
                goalTriangle = -1;
            }

            var reached = Search(startTriangle, startPoint, goalTriangle, goalPoint, destination, areaMask, out var lastTriangle);
            var endPoint = reached ? goalPoint : ClosestPointOnTriangle(lastTriangle, destination);
            BuildPoints(lastTriangle, startPoint, endPoint);
            foreach (var point in _points)
            {
                path.Add(point.Position, point.LinkId);
            }

            path.SetStatus(reached ? NavPathStatus.Complete : NavPathStatus.Partial);
            return true;
        }

        private static int LinkStep(int link, bool reverse)
        {
            return link * 2 + (reverse ? 1 : 0);
        }

        private static void AddLinkStep(List<int>[] lists, int triangle, int step)
        {
            if (lists[triangle] == null)
            {
                lists[triangle] = new List<int>();
            }

            lists[triangle].Add(step);
        }

        private static bool Passes(int area, int areaMask)
        {
            return (areaMask & (1 << area)) != 0;
        }

        private static Vector3 ClosestPoint(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
            {
                return a;
            }

            var bp = p - b;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
            {
                return b;
            }

            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                return a + d1 / (d1 - d3) * ab;
            }

            var cp = p - c;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
            {
                return c;
            }

            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                return a + d2 / (d2 - d6) * ac;
            }

            var va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                return b + (d4 - d3) / (d4 - d3 + (d5 - d6)) * (c - b);
            }

            var denominator = 1f / (va + vb + vc);
            return a + ab * (vb * denominator) + ac * (vc * denominator);
        }

        private Vector3 ClosestPointOnTriangle(int triangle, Vector3 p)
        {
            return ClosestPoint(p, _a[triangle], _b[triangle], _c[triangle]);
        }

        private bool FindNearest(Vector3 position, float maxDistance, int areaMask, out int triangle, out Vector3 point)
        {
            triangle = -1;
            point = position;
            if (!(maxDistance > 0f) || !NavMeshFile.IsFinite(position))
            {
                return false;
            }

            _grid.Query(position.X - maxDistance, position.Z - maxDistance, position.X + maxDistance, position.Z + maxDistance, _candidates);
            var best = maxDistance * maxDistance;
            foreach (var candidate in _candidates)
            {
                if (!Passes(_area[candidate], areaMask))
                {
                    continue;
                }

                var closest = ClosestPointOnTriangle(candidate, position);
                var distance = Vector3.DistanceSquared(closest, position);
                if (distance <= best && (triangle < 0 || distance < best))
                {
                    best = distance;
                    triangle = candidate;
                    point = closest;
                }
            }

            return triangle >= 0;
        }

        private bool Search(int start, Vector3 startPoint, int goal, Vector3 goalPoint, Vector3 destination, int areaMask, out int last)
        {
            if (++_search == int.MaxValue)
            {
                Array.Clear(_visited, 0, _visited.Length);
                Array.Clear(_closed, 0, _closed.Length);
                _search = 1;
            }

            last = start;
            if (start == goal)
            {
                _parent[start] = -1;
                return true;
            }

            _open.Clear();
            _visited[start] = _search;
            _g[start] = 0f;
            _parent[start] = -1;
            _via[start] = 0;
            _position[start] = startPoint;
            _open.Push(Vector3.Distance(startPoint, goal >= 0 ? goalPoint : destination), start);
            var bestDistance = float.MaxValue;
            var target = goal >= 0 ? goalPoint : destination;
            while (_open.TryPop(out var f, out var current))
            {
                if (_closed[current] == _search || f > Total(current, goal, goalPoint, target))
                {
                    continue;
                }

                _closed[current] = _search;
                if (current == goal)
                {
                    last = goal;
                    return true;
                }

                var distance = Vector3.DistanceSquared(ClosestPointOnTriangle(current, destination), destination);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    last = current;
                }

                var here = _position[current];
                var walkCost = _areaCosts[_area[current]];
                for (var k = _portalStarts[current]; k < _portalStarts[current + 1]; k++)
                {
                    var portal = _portals[k];
                    if (!Passes(_area[portal.Neighbor], areaMask))
                    {
                        continue;
                    }

                    var next = CrossingPoint(here, target, portal.Right, portal.Left);
                    Relax(current, portal.Neighbor, k, next, _g[current] + Vector3.Distance(here, next) * walkCost, goal, goalPoint, target);
                }

                for (var k = _linkStepStarts[current]; k < _linkStepStarts[current + 1]; k++)
                {
                    var step = _linkSteps[k];
                    var link = step >> 1;
                    if (!Passes(_links[link].Area, areaMask))
                    {
                        continue;
                    }

                    var reverse = (step & 1) != 0;
                    var neighbor = reverse ? _linkStartTriangle[link] : _linkEndTriangle[link];
                    if (!Passes(_area[neighbor], areaMask))
                    {
                        continue;
                    }

                    var enter = reverse ? _linkEnd[link] : _linkStart[link];
                    var exit = reverse ? _linkStart[link] : _linkEnd[link];
                    var g = _g[current] + Vector3.Distance(here, enter) * walkCost + Vector3.Distance(enter, exit) * _areaCosts[_links[link].Area];
                    Relax(current, neighbor, -(step + 1), exit, g, goal, goalPoint, target);
                }
            }

            return false;
        }

        // 辺の中点を通る道で比べると、細かい三角形が並ぶ所で階段のような通り道を選んでしまうので、目的地へ向かう線が横切る点で比べる。
        private static Vector3 CrossingPoint(Vector3 here, Vector3 target, Vector3 right, Vector3 left)
        {
            var edgeX = left.X - right.X;
            var edgeZ = left.Z - right.Z;
            var lineX = target.X - here.X;
            var lineZ = target.Z - here.Z;
            var denominator = lineX * edgeZ - lineZ * edgeX;
            float t;
            if (Math.Abs(denominator) > _minDegenerateArea)
            {
                t = (lineX * (here.Z - right.Z) - lineZ * (here.X - right.X)) / denominator;
            }
            else
            {
                var lengthSq = edgeX * edgeX + edgeZ * edgeZ;
                t = lengthSq > 0f ? (edgeX * (here.X - right.X) + edgeZ * (here.Z - right.Z)) / lengthSq : 0f;
            }

            return Vector3.Lerp(right, left, Math.Max(0f, Math.Min(1f, t)));
        }

        private void Relax(int from, int to, int via, Vector3 position, float g, int goal, Vector3 goalPoint, Vector3 target)
        {
            if (_closed[to] == _search || (_visited[to] == _search && g >= _g[to]))
            {
                return;
            }

            _visited[to] = _search;
            _g[to] = g;
            _parent[to] = from;
            _via[to] = via;
            _position[to] = position;
            _open.Push(Total(to, goal, goalPoint, target), to);
        }

        private float Total(int triangle, int goal, Vector3 goalPoint, Vector3 target)
        {
            if (triangle == goal)
            {
                return _g[triangle] + Vector3.Distance(_position[triangle], goalPoint) * _areaCosts[_area[triangle]];
            }

            return _g[triangle] + Vector3.Distance(_position[triangle], target);
        }

        private void BuildPoints(int last, Vector3 startPoint, Vector3 endPoint)
        {
            _steps.Clear();
            for (var triangle = last; _parent[triangle] >= 0; triangle = _parent[triangle])
            {
                _steps.Add(triangle);
            }

            _points.Clear();
            _funnelPortals.Clear();
            AddPoint(startPoint, NavPathPoint.NoLink);
            var segmentStart = startPoint;
            for (var i = _steps.Count - 1; i >= 0; i--)
            {
                var via = _via[_steps[i]];
                if (via >= 0)
                {
                    var portal = _portals[via];
                    _funnelPortals.Add((portal.Left, portal.Right));
                    continue;
                }

                var step = -via - 1;
                var link = step >> 1;
                var reverse = (step & 1) != 0;
                var enter = reverse ? _linkEnd[link] : _linkStart[link];
                var exit = reverse ? _linkStart[link] : _linkEnd[link];
                AddWalk(segmentStart, enter);
                AddPoint(enter, link);
                AddPoint(exit, NavPathPoint.NoLink);
                segmentStart = exit;
                _funnelPortals.Clear();
            }

            AddWalk(segmentStart, endPoint);
            AddPoint(endPoint, NavPathPoint.NoLink);
        }

        private void AddWalk(Vector3 from, Vector3 to)
        {
            NavFunnel.StringPull(from, to, _funnelPortals, _corners);
            foreach (var corner in _corners)
            {
                AddPoint(corner, NavPathPoint.NoLink);
            }
        }

        private void AddPoint(Vector3 position, int linkId)
        {
            var last = _points.Count - 1;
            if (last >= 0 && Vector3.DistanceSquared(_points[last].Position, position) < _samePointSq)
            {
                if (linkId != NavPathPoint.NoLink)
                {
                    _points[last] = new NavPathPoint(_points[last].Position, linkId);
                }

                return;
            }

            _points.Add(new NavPathPoint(position, linkId));
        }

        private (int[] Starts, Portal[] Portals) BuildPortals()
        {
            var count = _a.Length;
            var edgeCount = count * 3;
            var boxes = new (float MinX, float MinZ, float MaxX, float MaxZ)[edgeCount];
            for (var e = 0; e < edgeCount; e++)
            {
                Edge(e, out var p0, out var p1);
                boxes[e] = (
                    Math.Min(p0.X, p1.X) - _edgeHorizontalTolerance,
                    Math.Min(p0.Z, p1.Z) - _edgeHorizontalTolerance,
                    Math.Max(p0.X, p1.X) + _edgeHorizontalTolerance,
                    Math.Max(p0.Z, p1.Z) + _edgeHorizontalTolerance);
            }

            var grid = new XzGrid(boxes);
            var lists = new List<Portal>[count];
            var candidates = new List<int>();
            for (var e = 0; e < edgeCount; e++)
            {
                var box = boxes[e];
                grid.Query(box.MinX, box.MinZ, box.MaxX, box.MaxZ, candidates);
                foreach (var other in candidates)
                {
                    if (other <= e || other / 3 == e / 3)
                    {
                        continue;
                    }

                    if (TryShare(e, other, out var right, out var left))
                    {
                        AddPortal(lists, e / 3, new Portal(other / 3, right, left));
                        AddPortal(lists, other / 3, new Portal(e / 3, left, right));
                    }
                }
            }

            var starts = new int[count + 1];
            var portals = new List<Portal>();
            for (var i = 0; i < count; i++)
            {
                starts[i] = portals.Count;
                if (lists[i] != null)
                {
                    portals.AddRange(lists[i]);
                }
            }

            starts[count] = portals.Count;
            return (starts, portals.ToArray());
        }

        private static void AddPortal(List<Portal>[] lists, int triangle, Portal portal)
        {
            if (lists[triangle] == null)
            {
                lists[triangle] = new List<Portal>();
            }

            lists[triangle].Add(portal);
        }

        private void Edge(int edge, out Vector3 p0, out Vector3 p1)
        {
            var triangle = edge / 3;
            switch (edge % 3)
            {
                case 0:
                    p0 = _a[triangle];
                    p1 = _b[triangle];
                    break;
                case 1:
                    p0 = _b[triangle];
                    p1 = _c[triangle];
                    break;
                default:
                    p0 = _c[triangle];
                    p1 = _a[triangle];
                    break;
            }
        }

        // 三角形は反時計回りにそろえてあるので、辺 e を内側から外へ抜けるとき始点が右、終点が左になる。
        private bool TryShare(int e, int other, out Vector3 right, out Vector3 left)
        {
            right = default;
            left = default;
            Edge(e, out var p0, out var p1);
            Edge(other, out var q0, out var q1);
            var dx = p1.X - p0.X;
            var dz = p1.Z - p0.Z;
            var lengthSq = dx * dx + dz * dz;
            if (lengthSq < _minDegenerateArea)
            {
                return false;
            }

            var length = (float)Math.Sqrt(lengthSq);
            if (Math.Abs(NavFunnel.Cross(p0, p1, q0)) / length > _edgeHorizontalTolerance
                || Math.Abs(NavFunnel.Cross(p0, p1, q1)) / length > _edgeHorizontalTolerance
                || dx * (q1.X - q0.X) + dz * (q1.Z - q0.Z) >= 0f)
            {
                return false;
            }

            var t0 = (dx * (q0.X - p0.X) + dz * (q0.Z - p0.Z)) / lengthSq;
            var t1 = (dx * (q1.X - p0.X) + dz * (q1.Z - p0.Z)) / lengthSq;
            var low = Math.Max(0f, Math.Min(t0, t1));
            var high = Math.Min(1f, Math.Max(t0, t1));
            if ((high - low) * length <= _edgeHorizontalTolerance)
            {
                return false;
            }

            right = Vector3.Lerp(p0, p1, low);
            left = Vector3.Lerp(p0, p1, high);
            return Math.Abs(right.Y - HeightOnEdge(q0, q1, right)) <= _edgeVerticalTolerance
                && Math.Abs(left.Y - HeightOnEdge(q0, q1, left)) <= _edgeVerticalTolerance;
        }

        private static float HeightOnEdge(Vector3 q0, Vector3 q1, Vector3 point)
        {
            var dx = q1.X - q0.X;
            var dz = q1.Z - q0.Z;
            var lengthSq = dx * dx + dz * dz;
            var t = lengthSq > 0f ? (dx * (point.X - q0.X) + dz * (point.Z - q0.Z)) / lengthSq : 0f;
            return q0.Y + (q1.Y - q0.Y) * Math.Max(0f, Math.Min(1f, t));
        }

        private readonly struct Portal
        {
            public Portal(int neighbor, Vector3 right, Vector3 left)
            {
                Neighbor = neighbor;
                Right = right;
                Left = left;
            }

            public int Neighbor { get; }

            public Vector3 Right { get; }

            public Vector3 Left { get; }
        }
    }
}
