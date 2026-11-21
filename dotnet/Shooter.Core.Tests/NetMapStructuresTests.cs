using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
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

        [Test]
        public void WrittenFortificationsReadBackInStructureOrder()
        {
            var layout = new StructureLayout(_health, _grounded, _adjacency);
            var original = new NetMapStructures(
                new[] { layout, layout, layout },
                new[]
                {
                    new MapFortification(2, new Vector3(4f, 0f, -1.5f), false, "Barricade North"),
                    new MapFortification(0, new Vector3(-2f, 0.5f, 3f), true, "Wall"),
                });

            var read = NetMapStructures.Parse(NetMapStructures.Write(original), "HordeArena.structures.json");

            Assert.That(read.Fortifications, Has.Count.EqualTo(2));
            Assert.That(read.Fortifications[0].Structure, Is.EqualTo(0));
            Assert.That(read.Fortifications[0].Position, Is.EqualTo(new Vector3(-2f, 0.5f, 3f)));
            Assert.That(read.Fortifications[0].StartsBuilt, Is.True);
            Assert.That(read.Fortifications[0].NameKey, Is.EqualTo("Wall"));
            Assert.That(read.Fortifications[1].Structure, Is.EqualTo(2));
            Assert.That(read.Fortifications[1].Position, Is.EqualTo(new Vector3(4f, 0f, -1.5f)));
            Assert.That(read.Fortifications[1].StartsBuilt, Is.False);
            Assert.That(read.Fortifications[1].NameKey, Is.EqualTo("Barricade North"));
        }

        [Test]
        public void AMapWithoutFortificationsIsWrittenAsBefore()
        {
            var text = NetMapStructures.Write(new NetMapStructures(new[] { new StructureLayout(_health, _grounded, _adjacency) }));

            Assert.That(text, Does.Not.Contain("fortifications"));
            Assert.That(NetMapStructures.Parse(text, "x").Fortifications, Is.Empty);
        }

        [Test]
        public void AFortificationStartsBuiltAndNamelessUnlessWritten()
        {
            var text = "{\"structures\": [{\"pieces\": [{\"maxHealth\": 10, \"grounded\": true}]}], "
                + "\"fortifications\": [{\"structure\": 0, \"position\": [1, 2, 3]}]}";

            var fortification = NetMapStructures.Parse(text, "x").Fortifications[0];

            Assert.That(fortification.StartsBuilt, Is.True);
            Assert.That(fortification.NameKey, Is.Empty);
            Assert.That(fortification.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [TestCase(1, "fortifications[0].structure")]
        [TestCase(-1, "fortifications[0].structure")]
        public void AFortificationOfAStructureTheMapDoesNotHaveIsRejected(int structure, string item)
        {
            var text = "{\"structures\": [{\"pieces\": [{\"maxHealth\": 10, \"grounded\": true}]}], "
                + "\"fortifications\": [{\"structure\": " + structure + ", \"position\": [0, 0, 0]}]}";

            var e = Assert.Throws<ServerDataJsonException>(() => NetMapStructures.Parse(text, "bad.structures.json"));

            Assert.That(e.Message, Does.Contain("bad.structures.json").And.Contain(item).And.Contain("1 structure(s)"));
        }

        [Test]
        public void TheSameStructureCannotBeAFortificationTwice()
        {
            var text = "{\"structures\": [{\"pieces\": [{\"maxHealth\": 10, \"grounded\": true}]}], \"fortifications\": ["
                + "{\"structure\": 0, \"position\": [0, 0, 0]}, {\"structure\": 0, \"position\": [1, 0, 0]}]}";

            var e = Assert.Throws<ServerDataJsonException>(() => NetMapStructures.Parse(text, "x"));

            Assert.That(e.Message, Does.Contain("fortifications[1].structure").And.Contain("a fortification twice"));
            var layout = new StructureLayout(_health, _grounded, _adjacency);
            Assert.Throws<ArgumentException>(() => _ = new NetMapStructures(
                new[] { layout }, new[] { new MapFortification(0, Vector3.Zero, true, "a"), new MapFortification(0, Vector3.Zero, true, "b") }));
            Assert.Throws<ArgumentException>(() => _ = new NetMapStructures(
                new[] { layout }, new[] { new MapFortification(1, Vector3.Zero, true, "a") }));
        }

        [Test]
        public void TheReachIsMeasuredWithoutTheHeight()
        {
            var fortification = new MapFortification(0, new Vector3(1f, 5f, 1f), true, "a");

            Assert.That(fortification.HorizontalDistanceTo(new Vector3(4f, -20f, 5f)), Is.EqualTo(5f).Within(1e-5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MapFortification(0, new Vector3(float.NaN, 0f, 0f), true, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MapFortification(-1, Vector3.Zero, true, "a"));
        }
    }
}
