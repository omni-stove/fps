using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NetMapStructuresTests
    {
        private static readonly int[] _health = { 100, 80, 60 };
        private static readonly bool[] _grounded = { true, false, false };
        private static readonly (int, int)[] _adjacency = { (0, 1), (1, 2) };

        [Test]
        public void WrittenStructuresReadBackTheSame()
        {
            var layout = new StructureLayout(_health, _grounded, _adjacency);
            var original = new NetMapStructures(new[] { layout });

            var read = NetMapStructures.Parse(NetMapStructures.Write(original), "NetArena01.structures.json");

            Assert.That(read.Structures, Has.Count.EqualTo(1));
            var back = read.Structures[0];
            Assert.That(back.PieceCount, Is.EqualTo(3));
            Assert.That(back.MaxHealth(1), Is.EqualTo(80));
            Assert.That(back.IsGrounded(0), Is.True);
            Assert.That(back.IsGrounded(2), Is.False);
            Assert.That(back.Adjacency, Is.EqualTo(layout.Adjacency));
        }

        [Test]
        public void AnEmptyMapHasNoStructures()
        {
            Assert.That(NetMapStructures.Parse("{\"structures\": []}", "x").Structures, Is.Empty);
        }

        [Test]
        public void AnInvalidLayoutNamesWhereItIs()
        {
            var text = "{\"structures\": [{\"pieces\": [{\"maxHealth\": 10}], \"adjacency\": [{\"a\": 0, \"b\": 3}]}]}";

            var e = Assert.Throws<ServerDataJsonException>(() => NetMapStructures.Parse(text, "bad.structures.json"));

            Assert.That(e.Message, Does.Contain("bad.structures.json").And.Contain("structures[0]"));
        }

        [Test]
        public void UnknownItemsAreRejected()
        {
            Assert.Throws<ServerDataJsonException>(() => NetMapStructures.Parse("{\"structures\": [{\"pieces\": [{\"hp\": 1}]}]}", "x"));
        }

        [Test]
        public void NullStructuresAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new NetMapStructures(null));
        }
    }
}
