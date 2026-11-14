using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class FpsMapFileTests
    {
        private static readonly Vector3[] _oneVertex = { Vector3.Zero };
        private static readonly int[] _twoIndices = { 0, 0 };
        private static readonly int[] _outOfRangeIndices = { 0, 0, 1 };

        [Test]
        public void RoundTripKeepsEveryShapeLayerAndGroup()
        {
            var original = BvhWorldCollisionTests.BuildWorld();
            var read = FpsMapFile.Read(new MemoryStream(Write(original)));

            Assert.That(read.Colliders, Has.Count.EqualTo(original.Colliders.Count));
            for (var i = 0; i < original.Colliders.Count; i++)
            {
                var expected = original.Colliders[i];
                var actual = read.Colliders[i];
                Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.Layer, Is.EqualTo(expected.Layer));
                Assert.That(actual.Group, Is.EqualTo(expected.Group));
                Assert.That(actual.Vertices, Is.EqualTo(expected.Vertices));
                Assert.That(actual.Indices, Is.EqualTo(expected.Indices));
                Assert.That(actual.Center, Is.EqualTo(expected.Center));
                Assert.That(actual.HalfExtents, Is.EqualTo(expected.HalfExtents));
                Assert.That(actual.Rotation, Is.EqualTo(expected.Rotation));
                Assert.That(actual.Point0, Is.EqualTo(expected.Point0));
                Assert.That(actual.Point1, Is.EqualTo(expected.Point1));
                Assert.That(actual.Radius, Is.EqualTo(expected.Radius));
            }
        }

        [Test]
        public void SameMapWritesTheSameBytes()
        {
            var first = Write(BvhWorldCollisionTests.BuildWorld());
            var second = Write(BvhWorldCollisionTests.BuildWorld());
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void StartsWithTheMagicAndTheVersion()
        {
            var bytes = Write(new WorldMap(Array.Empty<WorldCollider>()));
            Assert.That(bytes, Is.EqualTo(new byte[] { (byte)'F', (byte)'P', (byte)'S', (byte)'M', (byte)FpsMapFile.Version, 0, 0, 0, 0, 0 }));
        }

        [Test]
        public void RejectsAnotherFile()
        {
            var bytes = Write(new WorldMap(Array.Empty<WorldCollider>()));
            bytes[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(bytes)));
            Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(new byte[2])));
        }

        [Test]
        public void RejectsAnotherVersion()
        {
            var bytes = Write(new WorldMap(Array.Empty<WorldCollider>()));
            bytes[4] = FpsMapFile.Version + 1;
            Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void RejectsATruncatedFile()
        {
            var bytes = Write(BvhWorldCollisionTests.BuildWorld());
            for (var length = 0; length < bytes.Length; length += 7)
            {
                var truncated = bytes.AsSpan(0, length).ToArray();
                Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(truncated)), "length " + length);
            }
        }

        [Test]
        public void RejectsACountLargerThanTheFile()
        {
            var bytes = Write(new WorldMap(Array.Empty<WorldCollider>()));
            BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 6);
            Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void RejectsAnInvalidCollider()
        {
            var map = new WorldMap(new[] { WorldCollider.Sphere(1, 0, 0, Vector3.Zero, 1f) });
            var bytes = Write(map);
            BitConverter.GetBytes(-1f).CopyTo(bytes, bytes.Length - 4);
            Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(bytes)));

            bytes = Write(map);
            bytes[10] = 99;
            Assert.Throws<InvalidDataException>(() => FpsMapFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void MapRejectsDuplicateIds()
        {
            Assert.Throws<ArgumentException>(() => new WorldMap(new[]
            {
                WorldCollider.Sphere(1, 0, 0, Vector3.Zero, 1f),
                WorldCollider.Sphere(1, 0, 0, Vector3.One, 1f),
            }));
        }

        [Test]
        public void ColliderRejectsInvalidShapes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldCollider.Sphere(1, 32, 0, Vector3.Zero, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldCollider.Sphere(1, 0, 0, Vector3.Zero, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldCollider.Capsule(1, 0, 0, new Vector3(float.NaN), Vector3.Zero, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldCollider.Box(1, 0, 0, Vector3.Zero, -Vector3.One, Quaternion.Identity));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldCollider.Box(1, 0, 0, Vector3.Zero, Vector3.One, new Quaternion(0f, 0f, 0f, 2f)));
            Assert.Throws<ArgumentException>(() => WorldCollider.Mesh(1, 0, 0, _oneVertex, _twoIndices));
            Assert.Throws<ArgumentOutOfRangeException>(() => WorldCollider.Mesh(1, 0, 0, _oneVertex, _outOfRangeIndices));
        }

        private static byte[] Write(WorldMap map)
        {
            using (var stream = new MemoryStream())
            {
                FpsMapFile.Write(stream, map);
                return stream.ToArray();
            }
        }
    }
}
