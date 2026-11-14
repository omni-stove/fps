using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    public sealed class WorldCollider
    {
        public const int StaticGroup = 0;

        private static readonly Vector3[] _noVertices = Array.Empty<Vector3>();
        private static readonly int[] _noIndices = Array.Empty<int>();

        private readonly Vector3[] _vertices;
        private readonly int[] _indices;

        private WorldCollider(
            WorldShapeKind kind,
            int id,
            int layer,
            int group,
            Vector3[] vertices,
            int[] indices,
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rotation,
            Vector3 point0,
            Vector3 point1,
            float radius)
        {
            if (layer < 0 || layer > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(layer), layer, "layer must be 0-31");
            }

            Kind = kind;
            Id = id;
            Layer = layer;
            Group = group;
            _vertices = vertices;
            _indices = indices;
            Center = center;
            HalfExtents = halfExtents;
            Rotation = rotation;
            Point0 = point0;
            Point1 = point1;
            Radius = radius;
        }

        public WorldShapeKind Kind { get; }

        public int Id { get; }

        public int Layer { get; }

        public int Group { get; }

        public IReadOnlyList<Vector3> Vertices => _vertices;

        public IReadOnlyList<int> Indices => _indices;

        public Vector3 Center { get; }

        public Vector3 HalfExtents { get; }

        public Quaternion Rotation { get; }

        public Vector3 Point0 { get; }

        public Vector3 Point1 { get; }

        public float Radius { get; }

        public static WorldCollider Mesh(int id, int layer, int group, IReadOnlyList<Vector3> vertices, IReadOnlyList<int> indices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            if (indices.Count % 3 != 0)
            {
                throw new ArgumentException("index count must be a multiple of 3", nameof(indices));
            }

            var vertexCopy = new Vector3[vertices.Count];
            for (var i = 0; i < vertexCopy.Length; i++)
            {
                RequireFinite(vertices[i], nameof(vertices));
                vertexCopy[i] = vertices[i];
            }

            var indexCopy = new int[indices.Count];
            for (var i = 0; i < indexCopy.Length; i++)
            {
                if (indices[i] < 0 || indices[i] >= vertexCopy.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), indices[i], "index out of the vertex range");
                }

                indexCopy[i] = indices[i];
            }

            return new WorldCollider(
                WorldShapeKind.Mesh, id, layer, group, vertexCopy, indexCopy,
                Vector3.Zero, Vector3.Zero, Quaternion.Identity, Vector3.Zero, Vector3.Zero, 0f);
        }

        public static WorldCollider Box(int id, int layer, int group, Vector3 center, Vector3 halfExtents, Quaternion rotation)
        {
            RequireFinite(center, nameof(center));
            RequireFinite(halfExtents, nameof(halfExtents));
            if (halfExtents.X < 0f || halfExtents.Y < 0f || halfExtents.Z < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtents), halfExtents, "half extents must not be negative");
            }

            var length = rotation.Length();
            if (!(Math.Abs(length - 1f) < 1e-3f))
            {
                throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "rotation must be a unit quaternion");
            }

            return new WorldCollider(
                WorldShapeKind.Box, id, layer, group, _noVertices, _noIndices,
                center, halfExtents, Quaternion.Normalize(rotation), Vector3.Zero, Vector3.Zero, 0f);
        }

        public static WorldCollider Capsule(int id, int layer, int group, Vector3 point0, Vector3 point1, float radius)
        {
            RequireFinite(point0, nameof(point0));
            RequireFinite(point1, nameof(point1));
            RequirePositive(radius, nameof(radius));
            return new WorldCollider(
                WorldShapeKind.Capsule, id, layer, group, _noVertices, _noIndices,
                Vector3.Zero, Vector3.Zero, Quaternion.Identity, point0, point1, radius);
        }

        public static WorldCollider Sphere(int id, int layer, int group, Vector3 center, float radius)
        {
            RequireFinite(center, nameof(center));
            RequirePositive(radius, nameof(radius));
            return new WorldCollider(
                WorldShapeKind.Sphere, id, layer, group, _noVertices, _noIndices,
                center, Vector3.Zero, Quaternion.Identity, Vector3.Zero, Vector3.Zero, radius);
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, "must be finite");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || !IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "must be positive and finite");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
