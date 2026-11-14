using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class SnapshotMessageTests
    {
        [Test]
        public void FullSnapshotRoundTrips()
        {
            var sent = Build(100, 0f);

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.BaselineTick, Is.EqualTo(SnapshotMessage.NoBaseline));
            AssertSame(sent, got);
        }

        [Test]
        public void DeltaCarriesOnlyChangedPartsAndResolvesToTheSameValues()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 1f);
            current.Players[1].Health = 40;
            current.Players[1].HasKiller = true;
            current.Players[1].KillerId = 1;
            current.Match.AlphaScore = 3;
            current.Owner.Weapon = new WeaponSync { AmmoInMagazine = 10, ReserveAmmo = 90, EquippedIndex = 1, WeaponCount = 2 };

            var full = Size(current, null);
            var deltaSize = Size(current, baseline);
            var received = Send(baseline, null);
            var got = Send(current, baseline);

            Assert.That(deltaSize, Is.LessThan(full / 2));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.BaselineTick, Is.EqualTo(100));
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Motion));
            Assert.That(got.Players[1].Present, Is.EqualTo(PlayerFields.Motion | PlayerFields.Health | PlayerFields.Score));
            Assert.That(got.Grenades[0].Present, Is.EqualTo(GrenadeFields.Transform));
            Assert.That(got.ControlPoints[0].Present, Is.EqualTo(ControlPointFields.None));
            Assert.That(got.TryResolve(received), Is.True);
            Assert.That(got.IsComplete, Is.True);
            AssertSame(current, got);
        }

        [Test]
        public void UnchangedSnapshotIsSmall()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);

            // tick・基準の tick・試合と持ち主の部分のビット・処理済みの入力・並びの数と、物ごとの Id と部分のビットだけになる。
            var expected = 2 + 1 + 4 + 4 + 1 + 1 + 1 + 4 + 3 + (2 * 6) + 6 + 6;
            Assert.That(Size(current, baseline), Is.EqualTo(expected));
        }

        [Test]
        public void ResolveFailsAgainstTheWrongBaselineAndLeavesTheSnapshotAsIs()
        {
            var baseline = Build(100, 0f);
            var other = Send(Build(99, 0f), null);
            var got = Send(Build(101, 1f), baseline);

            Assert.That(got.TryResolve(null), Is.False);
            Assert.That(got.TryResolve(other), Is.False);
            Assert.That(got.IsComplete, Is.False);
        }

        [Test]
        public void ResolveFailsWhenAPartialEntityIsMissingFromTheBaseline()
        {
            var baseline = Build(100, 0f);
            var received = Send(baseline, null);
            received.Players.Clear();
            var got = Send(Build(101, 1f), baseline);

            Assert.That(got.TryResolve(received), Is.False);
        }

        [Test]
        public void NewAndRemovedEntitiesFollowTheCurrentList()
        {
            var baseline = Build(100, 0f);
            var current = Build(101, 0f);
            current.Players.Clear();
            ref var newcomer = ref current.Players.Add();
            newcomer.PlayerId = 9;
            newcomer.Team = Team.Bravo;
            newcomer.WeaponId = "pistol";
            newcomer.Health = 100;

            var got = Send(current, baseline);

            Assert.That(got.Players.Count, Is.EqualTo(1));
            Assert.That(got.Players[0].PlayerId, Is.EqualTo(9));
            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.All));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("pistol"));
        }

        [Test]
        public void SnapshotWithoutABaselineMustCarryEveryPart()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.Snapshot);
            writer.WriteByte(SnapshotMessage.CurrentVersion);
            writer.WriteInt32(5);
            writer.WriteInt32(SnapshotMessage.NoBaseline);
            writer.WriteByte((byte)MatchFields.Status);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void BaselineMustBeOlder()
        {
            var current = Build(100, 0f);
            current.Baseline = Build(100, 0f);

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(current));
        }

        [Test]
        public void DuplicatePlayerIsRejected()
        {
            var current = Build(100, 0f);
            current.Players[1].PlayerId = current.Players[0].PlayerId;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(current));
        }

        [Test]
        public void NonFinitePositionIsRejected()
        {
            var current = Build(100, 0f);
            current.Players[0].Position = new Vector3(float.NaN, 0f, 0f);
            var writer = new MessageWriter();
            writer.WriteMessage(current);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void CopyKeepsTheSnapshotWhenTheSourceIsReadAgain()
        {
            var table = MatchMessages.CreateClientTable();
            var writer = new MessageWriter();
            writer.WriteMessage(Build(100, 0f));
            var read = (SnapshotMessage)table.Read(writer.AsSegment());
            var kept = new SnapshotMessage();
            kept.CopyFrom(read);

            writer.Reset();
            var next = Build(101, 5f);
            next.Owner.EquippedWeaponIds.Clear();
            writer.WriteMessage(next);
            table.Read(writer.AsSegment());

            AssertSame(Build(100, 0f), kept);
        }

        [Test]
        public void SteadyStateWriteAndReadDoNotAllocate()
        {
            var table = MatchMessages.CreateClientTable();
            var serverTable = MatchMessages.CreateServerTable();
            var writer = new MessageWriter();
            var history = new SnapshotMessage();
            var baseline = Build(0, 0f);
            var current = Build(1, 0f);
            var input = new InputMessage();
            var shot = new ShotMessage { WeaponId = "rifle", HasImpact = true, ImpactMaterialId = "concrete" };

            long allocated = 0;
            for (var round = 0; round < 3; round++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var tick = 1; tick <= 50; tick++)
                {
                    current.Tick = baseline.Tick + 1;
                    current.Players[0].Position = new Vector3(tick, 0f, 0f);
                    current.Baseline = baseline;
                    writer.Reset();
                    writer.WriteMessage(current);
                    var read = (SnapshotMessage)table.Read(writer.AsSegment());
                    read.TryResolve(history.Tick == baseline.Tick ? history : null);

                    history.CopyFrom(current);
                    history.Baseline = null;
                    baseline.CopyFrom(current);

                    input.Clear();
                    input.AckedSnapshotTick = baseline.Tick;
                    input.Add(new InputFrame(tick + (round * 100), new Vector2(0f, 1f), 0.5f, InputButtons.Sprint));
                    writer.Reset();
                    writer.WriteMessage(input);
                    serverTable.Read(writer.AsSegment());

                    writer.Reset();
                    writer.WriteMessage(shot);
                    table.Read(writer.AsSegment());
                }

                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Assert.That(allocated, Is.Zero);
        }

        private static SnapshotMessage Build(int tick, float moved)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.HasWinner = false;
            match.AlphaScore = 1;
            match.BravoScore = 2;
            match.RemainingSeconds = 300;
            match.NextWaveSeconds = 5;
            match.Rules = MatchRulesSync.From(GameRules.Default);
            match.Objective = MatchObjective.Domination;
            match.AlphaPoints = 10;
            match.BravoPoints = 20;

            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = tick - 2;
            snapshot.Owner.Weapon = new WeaponSync
            {
                AmmoInMagazine = 30,
                ReserveAmmo = 90,
                IsReloading = true,
                EquippedIndex = 0,
                WeaponCount = 2,
                ReloadStartedAt = 12.5,
                ReloadSeconds = 2f,
                ActiveReloadWindowStart = 0.4f,
                ActiveReloadWindowEnd = 0.6f,
            };
            snapshot.Owner.Grenades = new GrenadePouchSync(true, 2);
            snapshot.Owner.EquippedWeaponIds.Add("rifle");
            snapshot.Owner.EquippedWeaponIds.Add("pistol");

            for (var i = 0; i < 2; i++)
            {
                ref var player = ref snapshot.Players.Add();
                player.PlayerId = i + 1;
                player.Team = i == 0 ? Team.Alpha : Team.Bravo;
                player.IsBot = i == 1;
                player.Position = new Vector3(i + moved, 1f, 2f);
                player.Velocity = new Vector3(0f, -2f, 3f);
                player.Yaw = 0.75f;
                player.IsGrounded = true;
                player.IsCrouched = i == 1;
                player.IsAiming = true;
                player.Cover = new CoverSync(
                    new CoverSnapshot(CoverPhase.Peeking, CoverHeight.Low, new Vector3(0f, 0f, 1f), 0.5f, 0.1f, 0.2f, -1f),
                    1.2f,
                    CoverEdge.Left | CoverEdge.Right,
                    0.4f);
                player.Dodge = new DodgeSnapshot(true, new Vector2(1f, 0f), 0.1f, 0.5f);
                player.Vault = new VaultSync(true, new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), new Vector3(0f, 0f, 2f), 0.3f);
                player.Health = 80;
                player.LastHitDirection = new Vector3(0f, 0f, -1f);
                player.Downed = new DownedSync(i == 1, 12, 40);
                player.WeaponId = "rifle";
                player.Kills = 3;
                player.Deaths = 1;
                player.HasKiller = i == 0;
                player.KillerId = i == 0 ? 2 : 0;
                player.SpawnProtectionSeconds = 2;
            }

            ref var grenade = ref snapshot.Grenades.Add();
            grenade.GrenadeId = 5;
            grenade.HasThrower = true;
            grenade.ThrowerId = 1;
            grenade.WeaponId = "frag";
            grenade.Position = new Vector3(moved, 2f, 0f);
            grenade.Rotation = Quaternion.CreateFromYawPitchRoll(moved, 0f, 0f);

            ref var point = ref snapshot.ControlPoints.Add();
            point.Index = 0;
            point.HasOwner = true;
            point.Owner = Team.Bravo;
            point.ControlPercent = -60;
            point.Contested = true;
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

        private static void AssertSame(SnapshotMessage expected, SnapshotMessage actual)
        {
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick));
            var em = expected.Match;
            var am = actual.Match;
            Assert.That(am.State, Is.EqualTo(em.State));
            Assert.That(am.HasWinner, Is.EqualTo(em.HasWinner));
            Assert.That(am.AlphaScore, Is.EqualTo(em.AlphaScore));
            Assert.That(am.BravoScore, Is.EqualTo(em.BravoScore));
            Assert.That(am.RemainingSeconds, Is.EqualTo(em.RemainingSeconds));
            Assert.That(am.NextMatchSeconds, Is.EqualTo(em.NextMatchSeconds));
            Assert.That(am.NextWaveSeconds, Is.EqualTo(em.NextWaveSeconds));
            Assert.That(am.Rules.SameAs(em.Rules), Is.True);
            Assert.That(am.Objective, Is.EqualTo(em.Objective));
            Assert.That(am.AlphaPoints, Is.EqualTo(em.AlphaPoints));
            Assert.That(am.BravoPoints, Is.EqualTo(em.BravoPoints));

            Assert.That(actual.HasOwner, Is.EqualTo(expected.HasOwner));
            Assert.That(actual.Owner.LastProcessedInputTick, Is.EqualTo(expected.Owner.LastProcessedInputTick));
            Assert.That(actual.Owner.Weapon.SameAs(expected.Owner.Weapon), Is.True);
            Assert.That(actual.Owner.Grenades.SameAs(expected.Owner.Grenades), Is.True);
            Assert.That(actual.Owner.EquippedWeaponIds.SameAs(expected.Owner.EquippedWeaponIds), Is.True);

            Assert.That(actual.Players.Count, Is.EqualTo(expected.Players.Count));
            for (var i = 0; i < expected.Players.Count; i++)
            {
                ref var e = ref expected.Players[i];
                ref var a = ref actual.Players[i];
                Assert.That(a.PlayerId, Is.EqualTo(e.PlayerId));
                Assert.That(a.Present, Is.EqualTo(PlayerFields.All));
                Assert.That(a.ChangedBits(e), Is.Zero, "player " + e.PlayerId);
            }

            Assert.That(actual.Grenades.Count, Is.EqualTo(expected.Grenades.Count));
            for (var i = 0; i < expected.Grenades.Count; i++)
            {
                Assert.That(actual.Grenades[i].GrenadeId, Is.EqualTo(expected.Grenades[i].GrenadeId));
                Assert.That(actual.Grenades[i].ChangedBits(expected.Grenades[i]), Is.Zero);
            }

            Assert.That(actual.ControlPoints.Count, Is.EqualTo(expected.ControlPoints.Count));
            for (var i = 0; i < expected.ControlPoints.Count; i++)
            {
                Assert.That(actual.ControlPoints[i].Index, Is.EqualTo(expected.ControlPoints[i].Index));
                Assert.That(actual.ControlPoints[i].ChangedBits(expected.ControlPoints[i]), Is.Zero);
            }
        }
    }
}
