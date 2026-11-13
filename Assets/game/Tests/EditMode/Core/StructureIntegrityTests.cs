using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StructureIntegrityTests
    {
        // 部品 row * 3 + col。row 0 が下段で、下段だけ地面に接する
        private static StructureLayout Grid3x3(int maxHealth)
        {
            var health = new int[9];
            var grounded = new bool[9];
            var adjacency = new List<(int A, int B)>();
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    var piece = (row * 3) + col;
                    health[piece] = maxHealth;
                    grounded[piece] = row == 0;
                    if (col < 2)
                    {
                        adjacency.Add((piece, piece + 1));
                    }

                    if (row < 2)
                    {
                        adjacency.Add((piece, piece + 3));
                    }
                }
            }

            return new StructureLayout(health, grounded, adjacency);
        }

        // 部品 0 が地面に立つ柱、1..beamLength が柱の上から横へ一列に並ぶ梁
        private static StructureLayout PillarAndBeam(int beamLength)
        {
            var count = beamLength + 1;
            var health = new int[count];
            var grounded = new bool[count];
            var adjacency = new List<(int A, int B)>();
            for (var i = 0; i < count; i++)
            {
                health[i] = 10;
                if (i > 0)
                {
                    adjacency.Add((i - 1, i));
                }
            }

            grounded[0] = true;
            return new StructureLayout(health, grounded, adjacency);
        }

        // 部品 0 だけ地面に接し、0-1-2-... と一列につながる
        private static StructureLayout Chain(int count)
        {
            var health = new int[count];
            var grounded = new bool[count];
            var adjacency = new List<(int A, int B)>();
            for (var i = 0; i < count; i++)
            {
                health[i] = 1;
                if (i > 0)
                {
                    adjacency.Add((i - 1, i));
                }
            }

            grounded[0] = true;
            return new StructureLayout(health, grounded, adjacency);
        }

        [Test]
        public void StartsAtFullHealthWithNothingDestroyed()
        {
            var integrity = new StructureIntegrity(Grid3x3(5));

            for (var i = 0; i < 9; i++)
            {
                Assert.That(integrity.Health(i), Is.EqualTo(5));
                Assert.That(integrity.IsDestroyed(i), Is.False);
            }

            Assert.That(integrity.DestroyedCount, Is.EqualTo(0));
        }

        [Test]
        public void DamageBelowHealthOnlyLowersHealth()
        {
            var integrity = new StructureIntegrity(Grid3x3(5));

            var result = integrity.ApplyDamage(4, 3);

            Assert.That(result.Any, Is.False);
            Assert.That(result.Destroyed, Is.Empty);
            Assert.That(result.Collapsed, Is.Empty);
            Assert.That(integrity.Health(4), Is.EqualTo(2));
            Assert.That(integrity.IsDestroyed(4), Is.False);
        }

        [Test]
        public void DestroyingTheMiddleCenterKeepsTheGridStanding()
        {
            var integrity = new StructureIntegrity(Grid3x3(5));

            var result = integrity.ApplyDamage(4, 5);

            Assert.That(result.Destroyed, Is.EqualTo(new[] { 4 }));
            Assert.That(result.Collapsed, Is.Empty);
            Assert.That(integrity.IsDestroyed(4), Is.True);
            Assert.That(integrity.Health(4), Is.EqualTo(0));
            Assert.That(integrity.DestroyedCount, Is.EqualTo(1));
        }

        [Test]
        public void DestroyingTheWholeMiddleRowCollapsesTheTopRow()
        {
            var integrity = new StructureIntegrity(Grid3x3(5));

            Assert.That(integrity.ApplyDamage(4, 5).Collapsed, Is.Empty);
            Assert.That(integrity.ApplyDamage(3, 5).Collapsed, Is.Empty);
            var result = integrity.ApplyDamage(5, 100);

            Assert.That(result.Destroyed, Is.EqualTo(new[] { 5 }));
            Assert.That(result.Collapsed, Is.EqualTo(new[] { 6, 7, 8 }));
            for (var i = 0; i < 9; i++)
            {
                Assert.That(integrity.IsDestroyed(i), Is.EqualTo(i >= 3), $"piece {i}");
            }

            Assert.That(integrity.Health(7), Is.EqualTo(0));
            Assert.That(integrity.DestroyedCount, Is.EqualTo(6));
        }

        [Test]
        public void DestroyingThePillarCollapsesTheBeam()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(3));

            var result = integrity.ApplyDamage(0, 10);

            Assert.That(result.Destroyed, Is.EqualTo(new[] { 0 }));
            Assert.That(result.Collapsed, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(integrity.DestroyedCount, Is.EqualTo(4));
        }

        [Test]
        public void BreakingTheBeamOnlyCollapsesTheFarSide()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(4));

            var result = integrity.ApplyDamage(2, 10);

            Assert.That(result.Destroyed, Is.EqualTo(new[] { 2 }));
            Assert.That(result.Collapsed, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(integrity.IsDestroyed(1), Is.False);
        }

        [Test]
        public void DamageToADestroyedPieceIsIgnored()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(1));
            integrity.ApplyDamage(1, 10);

            var result = integrity.ApplyDamage(1, 10);

            Assert.That(result.Any, Is.False);
            Assert.That(integrity.Health(1), Is.EqualTo(0));
            Assert.That(integrity.DestroyedCount, Is.EqualTo(1));
        }

        [Test]
        public void NonPositiveDamageIsIgnored()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(1));

            Assert.That(integrity.ApplyDamage(0, 0).Any, Is.False);
            Assert.That(integrity.ApplyDamage(0, -5).Any, Is.False);
            Assert.That(integrity.Health(0), Is.EqualTo(10));
        }

        [Test]
        public void DamageToAPieceOutOfRangeThrows()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(1));

            Assert.Throws<ArgumentOutOfRangeException>(() => integrity.ApplyDamage(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => integrity.ApplyDamage(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => integrity.Health(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => integrity.IsDestroyed(-1));
        }

        [Test]
        public void BitsetRoundTrips()
        {
            var source = new StructureIntegrity(Grid3x3(5));
            source.ApplyDamage(3, 5);
            source.ApplyDamage(4, 5);
            source.ApplyDamage(5, 5);

            var bitset = source.ToBitset();
            Assert.That(bitset, Is.EqualTo(new[] { 0b1_1111_1000UL }));

            var copy = new StructureIntegrity(Grid3x3(5));
            copy.ApplyDestroyedSet(bitset);

            for (var i = 0; i < 9; i++)
            {
                Assert.That(copy.IsDestroyed(i), Is.EqualTo(source.IsDestroyed(i)), $"piece {i}");
            }

            Assert.That(copy.ToBitset(), Is.EqualTo(bitset));
            Assert.That(copy.DestroyedCount, Is.EqualTo(6));
        }

        [Test]
        public void ApplyingADestroyedSetDoesNotCollapse()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(2));

            integrity.ApplyDestroyedSet(new[] { true, false, false });

            Assert.That(integrity.IsDestroyed(0), Is.True);
            Assert.That(integrity.IsDestroyed(1), Is.False);
            Assert.That(integrity.IsDestroyed(2), Is.False);
            Assert.That(integrity.DestroyedCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyingADestroyedSetRestoresRepairedPiecesAndKeepsOtherHealth()
        {
            var integrity = new StructureIntegrity(PillarAndBeam(2));
            integrity.ApplyDamage(0, 3);
            integrity.ApplyDamage(2, 10);

            integrity.ApplyDestroyedSet(new[] { false, true, false });

            Assert.That(integrity.Health(0), Is.EqualTo(7));
            Assert.That(integrity.Health(1), Is.EqualTo(0));
            Assert.That(integrity.Health(2), Is.EqualTo(10));
            Assert.That(integrity.IsDestroyed(2), Is.False);
            Assert.That(integrity.DestroyedCount, Is.EqualTo(1));
        }

        [Test]
        public void HandlesMoreThan64Pieces()
        {
            var integrity = new StructureIntegrity(Chain(130));
            Assert.That(integrity.BitsetLength, Is.EqualTo(3));

            var result = integrity.ApplyDamage(64, 1);

            Assert.That(result.Destroyed, Is.EqualTo(new[] { 64 }));
            Assert.That(result.Collapsed.Count, Is.EqualTo(65));
            Assert.That(result.Collapsed[0], Is.EqualTo(65));
            Assert.That(result.Collapsed[64], Is.EqualTo(129));

            var bitset = integrity.ToBitset();
            Assert.That(bitset[0], Is.EqualTo(0UL));
            Assert.That(bitset[1], Is.EqualTo(ulong.MaxValue));
            Assert.That(bitset[2], Is.EqualTo(0b11UL));

            var copy = new StructureIntegrity(Chain(130));
            copy.ApplyDestroyedSet(bitset);
            Assert.That(copy.IsDestroyed(63), Is.False);
            Assert.That(copy.IsDestroyed(64), Is.True);
            Assert.That(copy.IsDestroyed(129), Is.True);
            Assert.That(copy.DestroyedCount, Is.EqualTo(66));
        }

        [Test]
        public void ExactlyFullWordsHaveNoSpareBits()
        {
            var integrity = new StructureIntegrity(Chain(64));
            Assert.That(integrity.BitsetLength, Is.EqualTo(1));

            integrity.ApplyDestroyedSet(new[] { 1UL << 63 });

            Assert.That(integrity.IsDestroyed(63), Is.True);
            Assert.That(integrity.ToBitset(), Is.EqualTo(new[] { 1UL << 63 }));
        }

        [Test]
        public void ApplyingAMalformedDestroyedSetThrows()
        {
            var integrity = new StructureIntegrity(Chain(65));

            Assert.Throws<ArgumentNullException>(() => integrity.ApplyDestroyedSet((ulong[])null));
            Assert.Throws<ArgumentNullException>(() => integrity.ApplyDestroyedSet((IReadOnlyList<bool>)null));
            Assert.Throws<ArgumentException>(() => integrity.ApplyDestroyedSet(new ulong[1]));
            Assert.Throws<ArgumentException>(() => integrity.ApplyDestroyedSet(new ulong[3]));
            Assert.Throws<ArgumentException>(() => integrity.ApplyDestroyedSet(new[] { 0UL, 0b10UL }));
            Assert.Throws<ArgumentException>(() => integrity.ApplyDestroyedSet(new bool[64]));
            Assert.That(integrity.DestroyedCount, Is.EqualTo(0));
        }

        [Test]
        public void LayoutNormalizesAndSortsAdjacency()
        {
            var layout = new StructureLayout(
                new[] { 1, 2, 3 },
                new[] { true, false, false },
                new[] { (2, 1), (1, 0) });

            Assert.That(layout.PieceCount, Is.EqualTo(3));
            Assert.That(layout.MaxHealth(2), Is.EqualTo(3));
            Assert.That(layout.IsGrounded(0), Is.True);
            Assert.That(layout.IsGrounded(1), Is.False);
            Assert.That(layout.Adjacency, Is.EqualTo(new[] { (0, 1), (1, 2) }));
            Assert.That(layout.Neighbors(1), Is.EqualTo(new[] { 0, 2 }));
            Assert.That(layout.Neighbors(0), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void LayoutDoesNotFollowLaterChangesToItsInputs()
        {
            var health = new[] { 4, 4 };
            var grounded = new[] { true, false };
            var adjacency = new List<(int A, int B)> { (0, 1) };
            var layout = new StructureLayout(health, grounded, adjacency);

            health[0] = 99;
            grounded[1] = true;
            adjacency.Clear();

            Assert.That(layout.MaxHealth(0), Is.EqualTo(4));
            Assert.That(layout.IsGrounded(1), Is.False);
            Assert.That(layout.Adjacency.Count, Is.EqualTo(1));
        }

        [Test]
        public void LayoutRejectsInvalidInput()
        {
            var noEdges = Array.Empty<(int A, int B)>();
            var twoHealth = new[] { 1, 1 };
            var groundFirst = new[] { true, false };
            var zeroHealth = new[] { 1, 0 };
            var negativeHealth = new[] { 1, -3 };

            var oneHealth = new[] { 1 };
            var oneGround = new[] { true };
            Assert.Throws<ArgumentNullException>(() => new StructureLayout(null, oneGround, noEdges));
            Assert.Throws<ArgumentNullException>(() => new StructureLayout(oneHealth, null, noEdges));
            Assert.Throws<ArgumentNullException>(() => new StructureLayout(oneHealth, oneGround, null));
            Assert.Throws<ArgumentException>(() => new StructureLayout(Array.Empty<int>(), Array.Empty<bool>(), noEdges));
            Assert.Throws<ArgumentException>(() => new StructureLayout(twoHealth, oneGround, noEdges));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StructureLayout(zeroHealth, groundFirst, noEdges));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StructureLayout(negativeHealth, groundFirst, noEdges));
            var outOfRange = new[] { (0, 2) };
            var negativeIndex = new[] { (-1, 0) };
            var selfEdge = new[] { (1, 1) };
            var duplicate = new[] { (0, 1), (0, 1) };
            var reversed = new[] { (0, 1), (1, 0) };
            Assert.Throws<ArgumentOutOfRangeException>(() => new StructureLayout(twoHealth, groundFirst, outOfRange));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StructureLayout(twoHealth, groundFirst, negativeIndex));
            Assert.Throws<ArgumentException>(() => new StructureLayout(twoHealth, groundFirst, selfEdge));
            Assert.Throws<ArgumentException>(() => new StructureLayout(twoHealth, groundFirst, duplicate));
            Assert.Throws<ArgumentException>(() => new StructureLayout(twoHealth, groundFirst, reversed));
        }

        [Test]
        public void IntegrityRejectsNullLayout()
        {
            Assert.Throws<ArgumentNullException>(() => new StructureIntegrity(null));
        }
    }
}
