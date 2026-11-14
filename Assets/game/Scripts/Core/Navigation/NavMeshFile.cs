using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Fps.Core.Navigation
{
    public sealed class NavMeshFile
    {
        public const string Extension = ".fpsnav";

        public const int Version = 1;

        public const int AreaCount = 32;

        private const byte _bidirectionalFlag = 1;

        // 壊れたファイルで巨大な配列を確保しないための上限。
        private const int _maxCount = 1 << 24;

        private static readonly byte[] _magic = Encoding.ASCII.GetBytes("FPSN");

        private readonly Vector3[] _vertices;
        private readonly NavTriangle[] _triangles;
        private readonly float[] _areaCosts;
        private readonly NavLink[] _links;

        public NavMeshFile(
            IReadOnlyList<Vector3> vertices, IReadOnlyList<NavTriangle> triangles, IReadOnlyList<float> areaCosts = null, IReadOnlyList<NavLink> links = null)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            _vertices = new Vector3[vertices.Count];
            for (var i = 0; i < _vertices.Length; i++)
            {
                if (!IsFinite(vertices[i]))
                {
                    throw new ArgumentException($"vertex {i} is not finite", nameof(vertices));
                }

                _vertices[i] = vertices[i];
            }

            _triangles = new NavTriangle[triangles.Count];
            for (var i = 0; i < _triangles.Length; i++)
            {
                var triangle = triangles[i];
                if (triangle.A >= _vertices.Length || triangle.B >= _vertices.Length || triangle.C >= _vertices.Length)
                {
                    throw new ArgumentException($"triangle {i} refers to a vertex out of range", nameof(triangles));
                }

                _triangles[i] = triangle;
            }

            var costCount = areaCosts?.Count ?? 0;
            if (costCount > AreaCount)
            {
                throw new ArgumentException($"at most {AreaCount} area costs", nameof(areaCosts));
            }

            _areaCosts = new float[AreaCount];
            for (var i = 0; i < AreaCount; i++)
            {
                var cost = i < costCount ? areaCosts[i] : 1f;

                // Unity の NavMesh.SetAreaCost も 1 未満を受け付けない。距離をそのまま推定の手間にする A* もこれを前提にする。
                if (float.IsNaN(cost) || float.IsInfinity(cost) || cost < 1f)
                {
                    throw new ArgumentException($"area {i} cost must be a finite value of 1 or more", nameof(areaCosts));
                }

                _areaCosts[i] = cost;
            }

            _links = new NavLink[links?.Count ?? 0];
            for (var i = 0; i < _links.Length; i++)
            {
                _links[i] = links[i];
            }
        }

        public IReadOnlyList<Vector3> Vertices => _vertices;

        public IReadOnlyList<NavTriangle> Triangles => _triangles;

        public IReadOnlyList<float> AreaCosts => _areaCosts;

        public IReadOnlyList<NavLink> Links => _links;

        public static NavMeshFile Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                try
                {
                    return Read(reader);
                }
                catch (EndOfStreamException e)
                {
                    throw new InvalidDataException("the navmesh file ends too early", e);
                }
                catch (ArgumentException e)
                {
                    throw new InvalidDataException("the navmesh file has an invalid value: " + e.Message, e);
                }
            }
        }

        public void Write(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(_magic);
                writer.Write(Version);
                writer.Write(_vertices.Length);
                foreach (var vertex in _vertices)
                {
                    WriteVector(writer, vertex);
                }

                writer.Write(_triangles.Length);
                foreach (var triangle in _triangles)
                {
                    writer.Write(triangle.A);
                    writer.Write(triangle.B);
                    writer.Write(triangle.C);
                    writer.Write((byte)triangle.Area);
                }

                writer.Write(AreaCount);
                foreach (var cost in _areaCosts)
                {
                    writer.Write(cost);
                }

                writer.Write(_links.Length);
                foreach (var link in _links)
                {
                    WriteVector(writer, link.Start);
                    WriteVector(writer, link.End);
                    writer.Write(link.Bidirectional ? _bidirectionalFlag : (byte)0);
                    writer.Write((byte)link.Area);
                    writer.Write((byte)link.Kind);
                }
            }
        }

        internal static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }

        private static NavMeshFile Read(BinaryReader reader)
        {
            var magic = reader.ReadBytes(_magic.Length);
            if (magic.Length != _magic.Length || magic[0] != _magic[0] || magic[1] != _magic[1] || magic[2] != _magic[2] || magic[3] != _magic[3])
            {
                throw new InvalidDataException("not a navmesh file (" + Extension + ")");
            }

            var version = reader.ReadInt32();
            if (version != Version)
            {
                throw new InvalidDataException($"navmesh file version {version} is not supported (expected {Version})");
            }

            var vertices = new Vector3[ReadCount(reader, "vertex", _maxCount)];
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = ReadVector(reader);
            }

            var triangles = new NavTriangle[ReadCount(reader, "triangle", _maxCount)];
            for (var i = 0; i < triangles.Length; i++)
            {
                var a = reader.ReadInt32();
                var b = reader.ReadInt32();
                var c = reader.ReadInt32();
                triangles[i] = new NavTriangle(a, b, c, reader.ReadByte());
            }

            var areaCosts = new float[ReadCount(reader, "area cost", AreaCount)];
            for (var i = 0; i < areaCosts.Length; i++)
            {
                areaCosts[i] = reader.ReadSingle();
            }

            var links = new NavLink[ReadCount(reader, "link", _maxCount)];
            for (var i = 0; i < links.Length; i++)
            {
                var start = ReadVector(reader);
                var end = ReadVector(reader);
                var flags = reader.ReadByte();
                var area = reader.ReadByte();
                var kind = reader.ReadByte();
                links[i] = new NavLink(start, end, (flags & _bidirectionalFlag) != 0, area, (NavLinkKind)kind);
            }

            return new NavMeshFile(vertices, triangles, areaCosts, links);
        }

        private static int ReadCount(BinaryReader reader, string what, int max)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > max)
            {
                throw new InvalidDataException($"{what} count {count} is out of range (0 to {max})");
            }

            return count;
        }

        private static Vector3 ReadVector(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        private static void WriteVector(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.X);
            writer.Write(v.Y);
            writer.Write(v.Z);
        }
    }
}
