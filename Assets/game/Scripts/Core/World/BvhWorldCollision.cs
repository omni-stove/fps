using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    public sealed class BvhWorldCollision : IWorldCollision
    {
        private const int _leafSize = 4;

        // 葉を数の中央で割るので深さは log2(形の数) で収まり、int の数の形でも 64 段の探索の積み荷を超えない
        private const int _stackSize = 64;

        private readonly Primitive[] _primitives;
        private readonly Node[] _nodes;
        private readonly int[] _colliderIds;
        private readonly int[] _colliderLayers;
        private readonly bool[] _colliderEnabled;
        private readonly Dictionary<int, int[]> _groupColliders;

        public BvhWorldCollision(WorldMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var colliders = map.Colliders;
            _colliderIds = new int[colliders.Count];
            _colliderLayers = new int[colliders.Count];
            _colliderEnabled = new bool[colliders.Count];
            var groups = new Dictionary<int, List<int>>();
            var primitives = new List<Primitive>();
            for (var i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                _colliderIds[i] = collider.Id;
                _colliderLayers[i] = collider.Layer;
                _colliderEnabled[i] = true;
                if (!groups.TryGetValue(collider.Group, out var members))
                {
                    members = new List<int>();
                    groups.Add(collider.Group, members);
                }

                members.Add(i);
                AddPrimitives(collider, i, primitives);
            }

            _groupColliders = new Dictionary<int, int[]>(groups.Count);
            foreach (var pair in groups)
            {
                _groupColliders.Add(pair.Key, pair.Value.ToArray());
            }

            _primitives = primitives.ToArray();
            _nodes = Build(_primitives);
        }

        public int PrimitiveCount => _primitives.Length;

        public bool SetGroupEnabled(int group, bool enabled)
        {
            if (!_groupColliders.TryGetValue(group, out var members))
            {
                return false;
            }

            foreach (var collider in members)
            {
                _colliderEnabled[collider] = enabled;
            }

            return true;
        }

        public bool IsGroupEnabled(int group)
        {
            return _groupColliders.TryGetValue(group, out var members) && _colliderEnabled[members[0]];
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
        {
            hit = default;
            if (!TryNormalize(direction, out var dir) || !(maxDistance > 0f) || !WorldGeometry.IsFinite(origin) || _nodes.Length == 0)
            {
                return false;
            }

            var best = maxDistance;
            var bestPrimitive = -1;
            var bestNormal = Vector3.Zero;
            Span<int> stack = stackalloc int[_stackSize];
            var top = 0;
            stack[top++] = 0;
            while (top > 0)
            {
                ref readonly var node = ref _nodes[stack[--top]];
                if ((node.LayerBits & layerMask) == 0 || !WorldGeometry.RayAabb(origin, dir, node.Min, node.Max, best))
                {
                    continue;
                }

                if (node.Count == 0)
                {
                    stack[top++] = node.Start;
                    stack[top++] = node.Start + 1;
                    continue;
                }

                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    ref readonly var primitive = ref _primitives[i];
                    if (!Selectable(primitive.Collider, layerMask))
                    {
                        continue;
                    }

                    if (RaycastPrimitive(primitive, origin, dir, out var distance, out var normal) && distance <= best)
                    {
                        if (distance < best || bestPrimitive < 0)
                        {
                            best = distance;
                            bestPrimitive = i;
                            bestNormal = normal;
                        }
                    }
                }
            }

            if (bestPrimitive < 0)
            {
                return false;
            }

            var collider = _primitives[bestPrimitive].Collider;
            hit = new WorldHit(origin + (dir * best), bestNormal, best, _colliderIds[collider], _colliderLayers[collider]);
            return true;
        }

        public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!(radius > 0f) || !WorldGeometry.IsFinite(point0) || !WorldGeometry.IsFinite(point1) || _nodes.Length == 0 || results.Length == 0)
            {
                return 0;
            }

            var extent = new Vector3(radius);
            var min = Vector3.Min(point0, point1) - extent;
            var max = Vector3.Max(point0, point1) + extent;
            var radiusSq = radius * radius;
            var count = 0;
            Span<int> stack = stackalloc int[_stackSize];
            var top = 0;
            stack[top++] = 0;
            while (top > 0)
            {
                ref readonly var node = ref _nodes[stack[--top]];
                if ((node.LayerBits & layerMask) == 0 || !WorldGeometry.AabbOverlap(min, max, node.Min, node.Max))
                {
                    continue;
                }

                if (node.Count == 0)
                {
                    stack[top++] = node.Start;
                    stack[top++] = node.Start + 1;
                    continue;
                }

                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    ref readonly var primitive = ref _primitives[i];
                    if (!Selectable(primitive.Collider, layerMask)
                        || !WorldGeometry.AabbOverlap(min, max, primitive.Min, primitive.Max)
                        || Contains(results, count, _colliderIds[primitive.Collider]))
                    {
                        continue;
                    }

                    if (DistanceSq(primitive, point0, point1, out _, out _) <= radiusSq)
                    {
                        results[count++] = _colliderIds[primitive.Collider];
                        if (count == results.Length)
                        {
                            return count;
                        }
                    }
                }
            }

            return count;
        }

        public bool CapsuleCast(
            Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit)
        {
            hit = default;
            if (!TryNormalize(direction, out var dir) || !(maxDistance > 0f) || !(radius > 0f)
                || !WorldGeometry.IsFinite(point0) || !WorldGeometry.IsFinite(point1) || _nodes.Length == 0)
            {
                return false;
            }

            var center = (point0 + point1) * 0.5f;
            var extent = Vector3.Abs(point1 - point0) * 0.5f + new Vector3(radius);
            var best = maxDistance;
            var bestPrimitive = -1;
            var bestNormal = Vector3.Zero;
            Span<int> stack = stackalloc int[_stackSize];
            // Unity は掃引前から重なっているコライダーを丸ごと外すので、メッシュは三角形ごとでなくコライダーごとに印を付ける
            // 大きさをコライダー数で決めると巨大なマップでスタックを使い切り、呼ぶたびのゼロ埋めも総数に比例するので、重なった物だけを小さな表に持つ
            Span<int> overlapped = stackalloc int[16];
            var overlappedCount = 0;
            var overlapMin = Vector3.Min(point0, point1) - new Vector3(radius);
            var overlapMax = Vector3.Max(point0, point1) + new Vector3(radius);
            var radiusSq = radius * radius;
            var top = 0;
            stack[top++] = 0;
            while (top > 0)
            {
                ref readonly var node = ref _nodes[stack[--top]];
                if ((node.LayerBits & layerMask) == 0 || !WorldGeometry.AabbOverlap(overlapMin, overlapMax, node.Min, node.Max))
                {
                    continue;
                }

                if (node.Count == 0)
                {
                    stack[top++] = node.Start;
                    stack[top++] = node.Start + 1;
                    continue;
                }

                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    ref readonly var primitive = ref _primitives[i];
                    if (primitive.Kind == WorldShapeKind.Mesh
                        && !overlapped[..overlappedCount].Contains(primitive.Collider)
                        && Selectable(primitive.Collider, layerMask)
                        && WorldGeometry.AabbOverlap(overlapMin, overlapMax, primitive.Min, primitive.Max)
                        && DistanceSq(primitive, point0, point1, out _, out _) < radiusSq)
                    {
                        if (overlappedCount == overlapped.Length)
                        {
                            // あふれたときだけヒープに移す。印を落とすと重なったメッシュに当たってしまう
                            var grown = new int[overlapped.Length * 2];
                            overlapped.CopyTo(grown);
                            overlapped = grown;
                        }

                        overlapped[overlappedCount++] = primitive.Collider;
                    }
                }
            }

            top = 0;
            stack[top++] = 0;
            while (top > 0)
            {
                ref readonly var node = ref _nodes[stack[--top]];
                if ((node.LayerBits & layerMask) == 0
                    || !WorldGeometry.RayAabb(center, dir, node.Min - extent, node.Max + extent, best))
                {
                    continue;
                }

                if (node.Count == 0)
                {
                    stack[top++] = node.Start;
                    stack[top++] = node.Start + 1;
                    continue;
                }

                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    ref readonly var primitive = ref _primitives[i];
                    if (overlapped[..overlappedCount].Contains(primitive.Collider)
                        || !Selectable(primitive.Collider, layerMask)
                        || !WorldGeometry.RayAabb(center, dir, primitive.Min - extent, primitive.Max + extent, best))
                    {
                        continue;
                    }

                    if (SweepPrimitive(primitive, point0, point1, radius, dir, out var distance, out var normal)
                        && (distance < best || (distance <= best && bestPrimitive < 0)))
                    {
                        best = distance;
                        bestPrimitive = i;
                        bestNormal = normal;
                    }
                }
            }

            if (bestPrimitive < 0)
            {
                return false;
            }

            ref readonly var winner = ref _primitives[bestPrimitive];
            var offset = dir * best;
            DistanceSq(winner, point0 + offset, point1 + offset, out _, out var onShape);
            hit = new WorldHit(onShape, bestNormal, best, _colliderIds[winner.Collider], _colliderLayers[winner.Collider]);
            return true;
        }

        private bool Selectable(int collider, int layerMask)
        {
            return _colliderEnabled[collider] && (layerMask & (1 << _colliderLayers[collider])) != 0;
        }

        private static bool Contains(int[] results, int count, int id)
        {
            for (var i = 0; i < count; i++)
            {
                if (results[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryNormalize(Vector3 direction, out Vector3 normalized)
        {
            var lengthSq = direction.LengthSquared();
            if (!(lengthSq > 0f) || !WorldGeometry.IsFinite(lengthSq))
            {
                normalized = Vector3.Zero;
                return false;
            }

            normalized = direction / (float)Math.Sqrt(lengthSq);
            return true;
        }

        private static bool RaycastPrimitive(in Primitive primitive, Vector3 origin, Vector3 direction, out float distance, out Vector3 normal)
        {
            switch (primitive.Kind)
            {
                case WorldShapeKind.Mesh:
                    normal = primitive.Normal;
                    return WorldGeometry.RayTriangle(origin, direction, primitive.A, primitive.B, primitive.C, out distance);
                case WorldShapeKind.Box:
                    var inverse = Quaternion.Conjugate(primitive.Rotation);
                    var localOrigin = Vector3.Transform(origin - primitive.A, inverse);
                    var localDirection = Vector3.Transform(direction, inverse);
                    if (WorldGeometry.RayBox(localOrigin, localDirection, primitive.B, out distance, out var localNormal))
                    {
                        normal = Vector3.Transform(localNormal, primitive.Rotation);
                        return true;
                    }

                    normal = Vector3.Zero;
                    return false;
                case WorldShapeKind.Capsule:
                    // Unity と同じく、始点が中にある形には当てない
                    var axisPoint = WorldGeometry.ClosestPointOnSegment(origin, primitive.A, primitive.B);
                    if (Vector3.DistanceSquared(origin, axisPoint) <= primitive.Radius * primitive.Radius)
                    {
                        distance = 0f;
                        normal = Vector3.Zero;
                        return false;
                    }

                    return WorldGeometry.RayCapsule(origin, direction, primitive.A, primitive.B, primitive.Radius, out distance, out normal);
                default:
                    if (Vector3.DistanceSquared(origin, primitive.A) <= primitive.Radius * primitive.Radius
                        || !WorldGeometry.RaySphere(origin, direction, primitive.A, primitive.Radius, out distance))
                    {
                        distance = 0f;
                        normal = Vector3.Zero;
                        return false;
                    }

                    normal = WorldGeometry.SafeNormalize(origin + (direction * distance) - primitive.A, -direction);
                    return true;
            }
        }

        private static float DistanceSq(in Primitive primitive, Vector3 p, Vector3 q, out Vector3 onSegment, out Vector3 onShape)
        {
            switch (primitive.Kind)
            {
                case WorldShapeKind.Mesh:
                    return WorldGeometry.SegmentTriangleDistanceSq(
                        p, q, primitive.A, primitive.B, primitive.C, primitive.Normal, out onSegment, out onShape);
                case WorldShapeKind.Box:
                    var inverse = Quaternion.Conjugate(primitive.Rotation);
                    var distanceSq = WorldGeometry.SegmentBoxDistanceSq(
                        Vector3.Transform(p - primitive.A, inverse),
                        Vector3.Transform(q - primitive.A, inverse),
                        primitive.B,
                        out var localSegment,
                        out var localBox);
                    onSegment = Vector3.Transform(localSegment, primitive.Rotation) + primitive.A;
                    onShape = Vector3.Transform(localBox, primitive.Rotation) + primitive.A;
                    return distanceSq;
                default:
                    WorldGeometry.ClosestPointsSegmentSegment(p, q, primitive.A, primitive.B, out onSegment, out var onAxis);
                    var toSegment = onSegment - onAxis;
                    var axisDistance = toSegment.Length();
                    var surface = Math.Max(0f, axisDistance - primitive.Radius);
                    onShape = onAxis + (WorldGeometry.SafeNormalize(toSegment, Vector3.UnitY) * Math.Min(axisDistance, primitive.Radius));
                    return surface * surface;
            }
        }

        private static bool SweepPrimitive(
            in Primitive primitive, Vector3 p, Vector3 q, float radius, Vector3 direction, out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = Vector3.Zero;
            var best = float.PositiveInfinity;
            switch (primitive.Kind)
            {
                case WorldShapeKind.Mesh:
                    // 裏から近づく三角形は、Raycast と同じく当てない
                    if (Vector3.Dot(primitive.Normal, direction) > 0f)
                    {
                        return false;
                    }

                    WorldGeometry.SweepSegmentSegment(p, q, direction, primitive.A, primitive.B, radius, ref best, ref normal);
                    WorldGeometry.SweepSegmentSegment(p, q, direction, primitive.B, primitive.C, radius, ref best, ref normal);
                    WorldGeometry.SweepSegmentSegment(p, q, direction, primitive.C, primitive.A, radius, ref best, ref normal);
                    SweepTriangleFace(primitive, p, direction, radius, ref best, ref normal);
                    SweepTriangleFace(primitive, q, direction, radius, ref best, ref normal);
                    break;
                case WorldShapeKind.Box:
                    var inverse = Quaternion.Conjugate(primitive.Rotation);
                    var localP = Vector3.Transform(p - primitive.A, inverse);
                    var localQ = Vector3.Transform(q - primitive.A, inverse);
                    var localDirection = Vector3.Transform(direction, inverse);
                    if (WorldGeometry.SegmentBoxDistanceSq(localP, localQ, primitive.B, out _, out _) < radius * radius)
                    {
                        return false;
                    }

                    for (var axis = 0; axis < 3; axis++)
                    {
                        for (var corner = 0; corner < 4; corner++)
                        {
                            WorldGeometry.BoxEdge(primitive.B, axis, corner, out var e0, out var e1);
                            WorldGeometry.SweepSegmentSegment(localP, localQ, localDirection, e0, e1, radius, ref best, ref normal);
                        }

                        SweepBoxFace(primitive.B, axis, localP, localDirection, radius, ref best, ref normal);
                        SweepBoxFace(primitive.B, axis, localQ, localDirection, radius, ref best, ref normal);
                    }

                    if (!float.IsPositiveInfinity(best))
                    {
                        normal = Vector3.Transform(normal, primitive.Rotation);
                    }

                    break;
                default:
                    var total = radius + primitive.Radius;
                    WorldGeometry.ClosestPointsSegmentSegment(p, q, primitive.A, primitive.B, out var onSegment, out var onAxis);
                    if (Vector3.DistanceSquared(onSegment, onAxis) < total * total)
                    {
                        return false;
                    }

                    WorldGeometry.SweepSegmentSegment(p, q, direction, primitive.A, primitive.B, total, ref best, ref normal);
                    break;
            }

            if (float.IsPositiveInfinity(best))
            {
                return false;
            }

            distance = best;
            return true;
        }

        private static void SweepTriangleFace(
            in Primitive primitive, Vector3 endpoint, Vector3 direction, float radius, ref float best, ref Vector3 bestNormal)
        {
            if (!WorldGeometry.SlabEntry(endpoint, direction, primitive.A, primitive.Normal, radius, out var t, out var faceNormal)
                || faceNormal != primitive.Normal
                || !(t < best))
            {
                return;
            }

            var contact = endpoint + (direction * t) - (faceNormal * radius);
            if (WorldGeometry.PointInTriangle(contact, primitive.A, primitive.B, primitive.C))
            {
                best = t;
                bestNormal = faceNormal;
            }
        }

        private static void SweepBoxFace(
            Vector3 half, int axis, Vector3 endpoint, Vector3 direction, float radius, ref float best, ref Vector3 bestNormal)
        {
            var axisNormal = axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ;
            var h = WorldGeometry.Axis(half, axis);
            for (var side = -1; side <= 1; side += 2)
            {
                var n = axisNormal * side;
                if (!WorldGeometry.SlabEntry(endpoint, direction, n * h, n, radius, out var t, out var faceNormal)
                    || faceNormal != n
                    || !(t < best))
                {
                    continue;
                }

                var contact = endpoint + (direction * t);
                var inside = true;
                for (var other = 0; other < 3; other++)
                {
                    if (other != axis && Math.Abs(WorldGeometry.Axis(contact, other)) > WorldGeometry.Axis(half, other))
                    {
                        inside = false;
                    }
                }

                if (inside)
                {
                    best = t;
                    bestNormal = n;
                }
            }
        }

        private static void AddPrimitives(WorldCollider collider, int colliderIndex, List<Primitive> primitives)
        {
            switch (collider.Kind)
            {
                case WorldShapeKind.Mesh:
                    var vertices = collider.Vertices;
                    var indices = collider.Indices;
                    for (var i = 0; i < indices.Count; i += 3)
                    {
                        var a = vertices[indices[i]];
                        var b = vertices[indices[i + 1]];
                        var c = vertices[indices[i + 2]];
                        var cross = Vector3.Cross(b - a, c - a);
                        var length = cross.Length();
                        // 面積の無い三角形は法線が決まらず、PhysX も取り込むときに捨てる
                        if (!(length > 0f))
                        {
                            continue;
                        }

                        primitives.Add(new Primitive
                        {
                            Kind = WorldShapeKind.Mesh,
                            Collider = colliderIndex,
                            A = a,
                            B = b,
                            C = c,
                            Normal = cross / length,
                            Rotation = Quaternion.Identity,
                            Min = Vector3.Min(a, Vector3.Min(b, c)),
                            Max = Vector3.Max(a, Vector3.Max(b, c)),
                        });
                    }

                    break;
                case WorldShapeKind.Box:
                    var rotation = collider.Rotation;
                    var half = collider.HalfExtents;
                    var extent = Vector3.Abs(Vector3.Transform(new Vector3(half.X, 0f, 0f), rotation))
                        + Vector3.Abs(Vector3.Transform(new Vector3(0f, half.Y, 0f), rotation))
                        + Vector3.Abs(Vector3.Transform(new Vector3(0f, 0f, half.Z), rotation));
                    primitives.Add(new Primitive
                    {
                        Kind = WorldShapeKind.Box,
                        Collider = colliderIndex,
                        A = collider.Center,
                        B = half,
                        Rotation = rotation,
                        Min = collider.Center - extent,
                        Max = collider.Center + extent,
                    });
                    break;
                case WorldShapeKind.Capsule:
                    var capsuleExtent = new Vector3(collider.Radius);
                    primitives.Add(new Primitive
                    {
                        Kind = WorldShapeKind.Capsule,
                        Collider = colliderIndex,
                        A = collider.Point0,
                        B = collider.Point1,
                        Radius = collider.Radius,
                        Rotation = Quaternion.Identity,
                        Min = Vector3.Min(collider.Point0, collider.Point1) - capsuleExtent,
                        Max = Vector3.Max(collider.Point0, collider.Point1) + capsuleExtent,
                    });
                    break;
                default:
                    var sphereExtent = new Vector3(collider.Radius);
                    // 球は両端の重なったカプセルとして扱い、重なり・掃引を同じ式で解く
                    primitives.Add(new Primitive
                    {
                        Kind = WorldShapeKind.Sphere,
                        Collider = colliderIndex,
                        A = collider.Center,
                        B = collider.Center,
                        Radius = collider.Radius,
                        Rotation = Quaternion.Identity,
                        Min = collider.Center - sphereExtent,
                        Max = collider.Center + sphereExtent,
                    });
                    break;
            }
        }

        private Node[] Build(Primitive[] primitives)
        {
            if (primitives.Length == 0)
            {
                return Array.Empty<Node>();
            }

            var nodes = new List<Node>(Math.Max(1, (2 * primitives.Length / _leafSize) + 1));
            var keys = new float[primitives.Length];
            nodes.Add(default);
            BuildNode(primitives, keys, nodes, 0, 0, primitives.Length, 1);
            return nodes.ToArray();
        }

        private void BuildNode(Primitive[] primitives, float[] keys, List<Node> nodes, int nodeIndex, int start, int count, int depth)
        {
            if (depth >= _stackSize)
            {
                throw new InvalidOperationException("collision tree too deep");
            }

            var min = primitives[start].Min;
            var max = primitives[start].Max;
            var centroidMin = primitives[start].Min + primitives[start].Max;
            var centroidMax = centroidMin;
            var layerBits = 0;
            for (var i = start; i < start + count; i++)
            {
                min = Vector3.Min(min, primitives[i].Min);
                max = Vector3.Max(max, primitives[i].Max);
                var centroid = primitives[i].Min + primitives[i].Max;
                centroidMin = Vector3.Min(centroidMin, centroid);
                centroidMax = Vector3.Max(centroidMax, centroid);
                layerBits |= 1 << _colliderLayers[primitives[i].Collider];
            }

            if (count <= _leafSize)
            {
                nodes[nodeIndex] = new Node { Min = min, Max = max, Start = start, Count = count, LayerBits = layerBits };
                return;
            }

            var spread = centroidMax - centroidMin;
            var axis = spread.X >= spread.Y && spread.X >= spread.Z ? 0 : spread.Y >= spread.Z ? 1 : 2;
            for (var i = start; i < start + count; i++)
            {
                keys[i] = WorldGeometry.Axis(primitives[i].Min + primitives[i].Max, axis);
            }

            Array.Sort(keys, primitives, start, count);
            var leftCount = count / 2;
            var left = nodes.Count;
            nodes.Add(default);
            nodes.Add(default);
            nodes[nodeIndex] = new Node { Min = min, Max = max, Start = left, Count = 0, LayerBits = layerBits };
            BuildNode(primitives, keys, nodes, left, start, leftCount, depth + 1);
            BuildNode(primitives, keys, nodes, left + 1, start + leftCount, count - leftCount, depth + 1);
        }

        private struct Primitive
        {
            public WorldShapeKind Kind;
            public int Collider;
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public Vector3 Normal;
            public Quaternion Rotation;
            public float Radius;
            public Vector3 Min;
            public Vector3 Max;
        }

        private struct Node
        {
            public Vector3 Min;
            public Vector3 Max;
            public int Start;
            public int Count;
            public int LayerBits;
        }
    }
}
