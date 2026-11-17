using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class KnockbackSyncTests
    {
        [Test]
        public void TheOwnersFlightRoundTrips()
        {
            var sent = Snapshot(10, CharacterKnockbackState.Flying(new Vector3(4.5f, 3f, -1.25f), 0.35f));

            var got = Send(sent, null);

            Assert.That(got.Owner.Knockback.IsFlying, Is.True);
            Assert.That(got.Owner.Knockback.Velocity, Is.EqualTo(new Vector3(4.5f, 3f, -1.25f)));
            Assert.That(got.Owner.Knockback.ElapsedSeconds, Is.EqualTo(0.35f));
        }

        [Test]
        public void AnUnchangedFlightIsFilledFromTheBaselineAndALandingIsSent()
        {
            var flying = CharacterKnockbackState.Flying(new Vector3(1f, 2f, 0f), 0.1f);
            var baseline = Snapshot(10, flying);
            var unchanged = Snapshot(11, flying);
            var landed = Snapshot(12, CharacterKnockbackState.None);

            var gotUnchanged = Send(unchanged, baseline);
            var gotLanded = Send(landed, baseline);

            Assert.That(gotUnchanged.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotUnchanged.Owner.Knockback, Is.EqualTo(flying));
            Assert.That(gotLanded.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotLanded.Owner.Knockback, Is.EqualTo(CharacterKnockbackState.None));
            Assert.That(Size(unchanged, baseline), Is.LessThan(Size(Snapshot(11, CharacterKnockbackState.Flying(new Vector3(1f, 1f, 0f), 0.2f)), baseline)));
        }

        [Test]
        public void ClearingASnapshotForgetsTheFlight()
        {
            var snapshot = Snapshot(10, CharacterKnockbackState.Flying(Vector3.UnitY, 0f));

            snapshot.Clear();

            Assert.That(snapshot.Owner.Knockback.IsFlying, Is.False);
        }

        [Test]
        public void ANegativeFlightTimeIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(Snapshot(10, CharacterKnockbackState.Flying(new Vector3(1f, 2f, 3f), 0.5f)));
            var bytes = writer.AsSegment().ToArray();
            var at = IndexOf(bytes, BitConverter.GetBytes(0.5f));
            BitConverter.TryWriteBytes(new Span<byte>(bytes, at, 4), -0.5f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheKnockbackSettingsRoundTripInTheConnectAccepted()
        {
            var knockback = new KnockbackSpec(
                explosion: new KnockbackForce(12f, 0.5f, 0.8f),
                melee: new KnockbackForce(3f, 0.1f, 1f),
                charge: new KnockbackForce(9f, 0.4f, 0.5f),
                minSpeed: 2f,
                minAirSeconds: 0.2f,
                maxAirSeconds: 2.5f,
                airControl: 1.5f,
                gravity: 15f,
                recoverSeconds: 0.6f,
                landingSnapRadius: 3f,
                playerWeight: 1.25f,
                enemyWeight: 2f);
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100, Knockback = knockback });

            var got = ((ConnectAcceptedMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment())).Knockback;

            Assert.That(got.Explosion.Speed, Is.EqualTo(12f));
            Assert.That(got.Explosion.UpwardRatio, Is.EqualTo(0.5f));
            Assert.That(got.Explosion.WeightFactor, Is.EqualTo(0.8f));
            Assert.That(got.Melee.Speed, Is.EqualTo(3f));
            Assert.That(got.Charge.WeightFactor, Is.EqualTo(0.5f));
            Assert.That(got.MinSpeed, Is.EqualTo(2f));
            Assert.That(got.MinAirSeconds, Is.EqualTo(0.2f));
            Assert.That(got.MaxAirSeconds, Is.EqualTo(2.5f));
            Assert.That(got.AirControl, Is.EqualTo(1.5f));
            Assert.That(got.Gravity, Is.EqualTo(15f));
            Assert.That(got.RecoverSeconds, Is.EqualTo(0.6f));
            Assert.That(got.LandingSnapRadius, Is.EqualTo(3f));
            Assert.That(got.PlayerWeight, Is.EqualTo(1.25f));
            Assert.That(got.EnemyWeight, Is.EqualTo(2f));
        }

        [Test]
        public void KnockbackSettingsOutOfRangeAreUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            // 吹き飛ばしの値 18 個が末尾に並び、重力は後ろから 5 番目
            var gravity = bytes.Length - (5 * 4);
            Assert.That(BitConverter.ToSingle(bytes, gravity), Is.EqualTo(KnockbackSpec.DefaultGravity), "the offset points at the gravity");
            BitConverter.TryWriteBytes(new Span<byte>(bytes, gravity, 4), 0f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        private static SnapshotMessage Snapshot(int tick, CharacterKnockbackState knockback)
        {
            var snapshot = new SnapshotMessage { Tick = tick, HasOwner = true };
            snapshot.Owner.LastProcessedInputTick = tick - 1;
            snapshot.Owner.Knockback = knockback;
            snapshot.Players.Add().PlayerId = 1;
            return snapshot;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment()));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.Length;
        }

        private static int IndexOf(byte[] bytes, byte[] pattern)
        {
            for (var i = bytes.Length - pattern.Length; i >= 0; i--)
            {
                if (bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }

            throw new AssertionException("pattern not found");
        }
    }
}
