using System;
using System.IO;
using System.Numerics;
using Fps.Core.Navigation;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NavMeshFileTests
    {
        private static readonly float[] _twoCosts = { 1f, 2.5f };
        private static readonly float[] _belowOne = { 0.5f };
        private static readonly float[] _oneCost = { 1f };

        [Test]
        public void WritingAndReadingKeepsEverything()
        {
            var file = new NavMeshFile(
                new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0f, 1f) },
                new[] { new NavTriangle(0, 1, 2, 4) },
                _twoCosts,
                new[] { new NavLink(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), false, 2, NavLinkKind.Ladder) });

            var read = RoundTrip(file);

            Assert.That(read.Vertices, Is.EqualTo(file.Vertices));
            Assert.That(read.Triangles, Is.EqualTo(file.Triangles));
            Assert.That(read.AreaCosts, Has.Count.EqualTo(NavMeshFile.AreaCount));
            Assert.That(read.AreaCosts[1], Is.EqualTo(2.5f));
            Assert.That(read.AreaCosts[31], Is.EqualTo(1f));
            Assert.That(read.Links, Is.EqualTo(file.Links));
            Assert.That(read.Links[0].Bidirectional, Is.False);
            Assert.That(read.Links[0].Kind, Is.EqualTo(NavLinkKind.Ladder));
        }

        [Test]
        public void MissingAreaCostsAndLinksDefaultToOneAndNone()
        {
            var file = new NavMeshFile(new[] { Vector3.Zero }, Array.Empty<NavTriangle>());

            Assert.That(file.AreaCosts, Has.Count.EqualTo(NavMeshFile.AreaCount));
            Assert.That(file.AreaCosts, Is.All.EqualTo(1f));
            Assert.That(file.Links, Is.Empty);
        }

        [Test]
        public void TheFileStartsWithItsMagicAndVersion()
        {
            var bytes = Bytes(new NavMeshFile(Array.Empty<Vector3>(), Array.Empty<NavTriangle>()));

            Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("FPSN"));
            Assert.That(BitConverter.ToInt32(bytes, 4), Is.EqualTo(NavMeshFile.Version));
        }

        [Test]
        public void ReadingRejectsAnotherFormat()
        {
            var bytes = Bytes(Small());
            bytes[0] = (byte)'X';

            Assert.Throws<InvalidDataException>(() => NavMeshFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void ReadingRejectsAnotherVersion()
        {
            var bytes = Bytes(Small());
            BitConverter.GetBytes(NavMeshFile.Version + 1).CopyTo(bytes, 4);

            Assert.Throws<InvalidDataException>(() => NavMeshFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void ReadingRejectsATruncatedFile()
        {
            var bytes = Bytes(Small());

            for (var length = 0; length < bytes.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(bytes, truncated, length);
                Assert.Throws<InvalidDataException>(() => NavMeshFile.Read(new MemoryStream(truncated)), $"length {length}");
            }
        }

        [Test]
        public void ReadingRejectsAVertexIndexOutOfRange()
        {
            var bytes = Bytes(Small());

            const int header = 8;
            const int vertexBlock = 4 + 3 * 12;
            const int triangleCount = 4;
            BitConverter.GetBytes(3).CopyTo(bytes, header + vertexBlock + triangleCount);

            Assert.Throws<InvalidDataException>(() => NavMeshFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void ReadingRejectsANegativeCount()
        {
            var bytes = Bytes(Small());
            BitConverter.GetBytes(-1).CopyTo(bytes, 8);

            Assert.Throws<InvalidDataException>(() => NavMeshFile.Read(new MemoryStream(bytes)));
        }

        [Test]
        public void TheConstructorRejectsBadValues()
        {
            var vertices = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitZ };
            var triangle = new[] { new NavTriangle(0, 1, 2, 0) };

            Assert.Throws<ArgumentNullException>(() => _ = new NavMeshFile(null, triangle));
            Assert.Throws<ArgumentNullException>(() => _ = new NavMeshFile(vertices, null));
            Assert.Throws<ArgumentException>(() => _ = new NavMeshFile(new[] { new Vector3(float.NaN, 0f, 0f) }, Array.Empty<NavTriangle>()));
            Assert.Throws<ArgumentException>(() => _ = new NavMeshFile(vertices, new[] { new NavTriangle(0, 1, 3, 0) }));
            Assert.Throws<ArgumentException>(() => _ = new NavMeshFile(vertices, triangle, _belowOne));
            Assert.Throws<ArgumentException>(() => _ = new NavMeshFile(vertices, triangle, new float[NavMeshFile.AreaCount + 1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new NavTriangle(-1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new NavTriangle(0, 0, 0, NavMeshFile.AreaCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new NavLink(Vector3.Zero, Vector3.One, true, -1, NavLinkKind.Walk));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new NavLink(Vector3.Zero, Vector3.One, true, 0, (NavLinkKind)9));
            Assert.Throws<ArgumentException>(() => _ = new NavLink(new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.One, true, 0, NavLinkKind.Walk));
        }

        private static NavMeshFile Small()
        {
            return new NavMeshFile(
                new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitZ },
                new[] { new NavTriangle(0, 1, 2, 0) },
                _oneCost,
                new[] { new NavLink(Vector3.Zero, Vector3.UnitY, true, 0, NavLinkKind.Walk) });
        }

        private static byte[] Bytes(NavMeshFile file)
        {
            using (var stream = new MemoryStream())
            {
                file.Write(stream);
                return stream.ToArray();
            }
        }

        private static NavMeshFile RoundTrip(NavMeshFile file)
        {
            using (var stream = new MemoryStream(Bytes(file)))
            {
                return NavMeshFile.Read(stream);
            }
        }
    }
}
