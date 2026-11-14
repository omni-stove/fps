using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Fps.Core
{
    public static class FpsMapFile
    {
        public const ushort Version = 1;

        public const string Extension = ".fpsmap";

        private static readonly byte[] _magic = { (byte)'F', (byte)'P', (byte)'S', (byte)'M' };

        private const int _colliderHeaderSize = 1 + 4 + 1 + 4;
        private const int _vector3Size = 12;
        private const int _triangleSize = 12;

        public static void Write(Stream stream, WorldMap map)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            // BinaryWriter は環境によらずリトルエンディアンで書くので、Unity と .NET のどちらで書いても同じバイト列になる
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(_magic);
                writer.Write(Version);
                writer.Write(map.Colliders.Count);
                foreach (var collider in map.Colliders)
                {
                    writer.Write((byte)collider.Kind);
                    writer.Write(collider.Id);
                    writer.Write((byte)collider.Layer);
                    writer.Write(collider.Group);
                    switch (collider.Kind)
                    {
                        case WorldShapeKind.Mesh:
                            writer.Write(collider.Vertices.Count);
                            foreach (var vertex in collider.Vertices)
                            {
                                WriteVector3(writer, vertex);
                            }

                            writer.Write(collider.Indices.Count / 3);
                            foreach (var index in collider.Indices)
                            {
                                writer.Write(index);
                            }

                            break;
                        case WorldShapeKind.Box:
                            WriteVector3(writer, collider.Center);
                            WriteVector3(writer, collider.HalfExtents);
                            writer.Write(collider.Rotation.X);
                            writer.Write(collider.Rotation.Y);
                            writer.Write(collider.Rotation.Z);
                            writer.Write(collider.Rotation.W);
                            break;
                        case WorldShapeKind.Capsule:
                            WriteVector3(writer, collider.Point0);
                            WriteVector3(writer, collider.Point1);
                            writer.Write(collider.Radius);
                            break;
                        case WorldShapeKind.Sphere:
                            WriteVector3(writer, collider.Center);
                            writer.Write(collider.Radius);
                            break;
                        default:
                            throw new ArgumentException("unknown shape kind " + collider.Kind, nameof(map));
                    }
                }
            }
        }

        public static WorldMap Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            try
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var magic = reader.ReadBytes(_magic.Length);
                    if (magic.Length != _magic.Length || magic[0] != _magic[0] || magic[1] != _magic[1] || magic[2] != _magic[2] || magic[3] != _magic[3])
                    {
                        throw new InvalidDataException("not an .fpsmap file");
                    }

                    var version = reader.ReadUInt16();
                    if (version != Version)
                    {
                        throw new InvalidDataException("unsupported .fpsmap version " + version + " (expected " + Version + ")");
                    }

                    var count = ReadCount(reader, stream, _colliderHeaderSize);
                    var colliders = new WorldCollider[count];
                    for (var i = 0; i < count; i++)
                    {
                        colliders[i] = ReadCollider(reader, stream);
                    }

                    return new WorldMap(colliders);
                }
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidDataException("the .fpsmap file ends early", e);
            }
            catch (ArgumentException e)
            {
                throw new InvalidDataException("the .fpsmap file holds an invalid collider: " + e.Message, e);
            }
        }

        private static WorldCollider ReadCollider(BinaryReader reader, Stream stream)
        {
            var kind = (WorldShapeKind)reader.ReadByte();
            var id = reader.ReadInt32();
            var layer = reader.ReadByte();
            var group = reader.ReadInt32();
            switch (kind)
            {
                case WorldShapeKind.Mesh:
                    var vertices = new Vector3[ReadCount(reader, stream, _vector3Size)];
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = ReadVector3(reader);
                    }

                    var indices = new int[ReadCount(reader, stream, _triangleSize) * 3];
                    for (var i = 0; i < indices.Length; i++)
                    {
                        indices[i] = reader.ReadInt32();
                    }

                    return WorldCollider.Mesh(id, layer, group, vertices, indices);
                case WorldShapeKind.Box:
                    var center = ReadVector3(reader);
                    var halfExtents = ReadVector3(reader);
                    var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    return WorldCollider.Box(id, layer, group, center, halfExtents, rotation);
                case WorldShapeKind.Capsule:
                    var point0 = ReadVector3(reader);
                    var point1 = ReadVector3(reader);
                    return WorldCollider.Capsule(id, layer, group, point0, point1, reader.ReadSingle());
                case WorldShapeKind.Sphere:
                    var sphereCenter = ReadVector3(reader);
                    return WorldCollider.Sphere(id, layer, group, sphereCenter, reader.ReadSingle());
                default:
                    throw new InvalidDataException("unknown shape kind " + (byte)kind);
            }
        }

        // 壊れた数で巨大な配列を確保しないよう、残りのバイト数で収まらない数は読む前に弾く
        private static int ReadCount(BinaryReader reader, Stream stream, int bytesPerItem)
        {
            var count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("negative count " + count);
            }

            if (stream.CanSeek && (long)count * bytesPerItem > stream.Length - stream.Position)
            {
                throw new InvalidDataException("count " + count + " does not fit in the rest of the file");
            }

            return count;
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
