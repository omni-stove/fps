using System.Numerics;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerSightBlockersTests
    {
        [Test]
        public void NothingBlocksUntilABlockerIsAdded()
        {
            var blockers = new ServerSightBlockers();

            Assert.That(blockers.Blocks(Vector3.Zero, Vector3.UnitX), Is.False);
        }

        [Test]
        public void AnyBlockerBlocksAndRemovingItClearsTheView()
        {
            var blockers = new ServerSightBlockers();
            var wall = new BlockBeyond(5f);
            blockers.Add(new BlockBeyond(100f));
            blockers.Add(wall);

            Assert.That(blockers.Blocks(Vector3.Zero, new Vector3(10f, 0f, 0f)), Is.True);
            Assert.That(blockers.Blocks(Vector3.Zero, new Vector3(3f, 0f, 0f)), Is.False);
            Assert.That(blockers.Remove(wall), Is.True);
            Assert.That(blockers.Blocks(Vector3.Zero, new Vector3(10f, 0f, 0f)), Is.False);
        }

        private sealed class BlockBeyond : ISightBlocker
        {
            private readonly float _distance;

            public BlockBeyond(float distance)
            {
                _distance = distance;
            }

            public bool Blocks(Vector3 from, Vector3 to) => Vector3.Distance(from, to) > _distance;
        }
    }
}
