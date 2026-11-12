using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PeerIdTableTests
    {
        [Test]
        public void AssignsIdsFromFirstId()
        {
            var table = new PeerIdTable(1);

            var a = table.GetOrAdd("a", out var addedA);
            var b = table.GetOrAdd("b", out var addedB);

            Assert.That(a, Is.EqualTo(1UL));
            Assert.That(b, Is.EqualTo(2UL));
            Assert.That(addedA, Is.True);
            Assert.That(addedB, Is.True);
            Assert.That(table.Count, Is.EqualTo(2));
            var expected = new[] { 1UL, 2UL };
            Assert.That(table.Ids, Is.EquivalentTo(expected));
        }

        [Test]
        public void ReturnsSameIdForSameKey()
        {
            var table = new PeerIdTable(1);
            var first = table.GetOrAdd("a", out _);

            var again = table.GetOrAdd("a", out var added);

            Assert.That(again, Is.EqualTo(first));
            Assert.That(added, Is.False);
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void LooksUpBothWays()
        {
            var table = new PeerIdTable(10);
            var id = table.GetOrAdd("peer", out _);

            Assert.That(table.TryGetId("peer", out var foundId), Is.True);
            Assert.That(foundId, Is.EqualTo(id));
            Assert.That(table.TryGetKey(id, out var foundKey), Is.True);
            Assert.That(foundKey, Is.EqualTo("peer"));
            Assert.That(table.TryGetId("other", out _), Is.False);
            Assert.That(table.TryGetId(null, out _), Is.False);
            Assert.That(table.TryGetKey(id + 1, out _), Is.False);
        }

        [Test]
        public void DoesNotReuseRemovedIds()
        {
            var table = new PeerIdTable(1);
            var id = table.GetOrAdd("a", out _);

            Assert.That(table.Remove(id), Is.True);
            Assert.That(table.Remove(id), Is.False);
            Assert.That(table.TryGetId("a", out _), Is.False);

            var again = table.GetOrAdd("a", out var added);
            Assert.That(added, Is.True);
            Assert.That(again, Is.Not.EqualTo(id));
        }

        [Test]
        public void ClearKeepsCounting()
        {
            var table = new PeerIdTable(1);
            table.GetOrAdd("a", out _);
            table.Clear();

            Assert.That(table.Count, Is.Zero);
            Assert.That(table.GetOrAdd("b", out _), Is.EqualTo(2UL));
        }

        [Test]
        public void RejectsEmptyKey()
        {
            var table = new PeerIdTable(1);

            Assert.Throws<ArgumentException>(() => table.GetOrAdd("", out _));
            Assert.Throws<ArgumentException>(() => table.GetOrAdd(null, out _));
        }

        [Test]
        public void ThrowsWhenIdsRunOut()
        {
            var table = new PeerIdTable(ulong.MaxValue - 1);
            table.GetOrAdd("a", out _);

            Assert.Throws<InvalidOperationException>(() => table.GetOrAdd("b", out _));
        }
    }
}
