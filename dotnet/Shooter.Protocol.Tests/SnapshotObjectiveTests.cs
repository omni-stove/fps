using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class SnapshotObjectiveTests
    {
        // 拠点の有無と番号・爆弾の有無と段階 (1 バイトずつ)、目的の残り秒 (4 バイト)
        private const int _objectiveStateBytes = 1 + 1 + 1 + 1 + 4;

        // 試合の部分のビットまでの頭 (Id・版・tick・基準の tick・部分のビット)
        private const int _headBytes = 2 + 1 + 4 + 4 + 1;

        [Test]
        public void TheVersionIs17()
        {
            Assert.That(SnapshotMessage.CurrentVersion, Is.EqualTo(17));
        }

        [Test]
        public void TheMeleeAndStreakRulesRoundTripAndAreSentWhenChanged()
        {
            var baseline = Build(100);
            var current = Build(101);
            current.Match.Rules = MatchRulesSync.From(
                GameRules.Default.WithActions(ActionRules.Default.WithMelee(Melee.On)).WithStreaks(Streaks.On));

            var full = Send(current, null);
            var delta = Send(current, baseline);

            Assert.That(full.Match.Rules.Melee, Is.EqualTo(Melee.On));
            Assert.That(full.Match.Rules.Streaks, Is.EqualTo(Streaks.On));
            Assert.That(current.Match.Rules.SameAs(baseline.Match.Rules), Is.False);
            Assert.That(delta.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(delta.Match.Rules.Melee, Is.EqualTo(Melee.On));
            Assert.That(delta.Match.Rules.Streaks, Is.EqualTo(Streaks.On));
        }

        [Test]
        public void MeleeAndStreaksAreOffBeforeAnyRulesArrive()
        {
            Assert.That(default(MatchRulesSync).Melee, Is.EqualTo(Melee.Off));
            Assert.That(default(MatchRulesSync).Streaks, Is.EqualTo(Streaks.Off));
        }

        [Test]
        public void TheHardpointAndTheBombRoundTrip()
        {
            var sent = Build(100);
            sent.Match.HasActivePoint = true;
            sent.Match.ActivePointIndex = 3;
            sent.Match.HasBomb = true;
            sent.Match.Bomb = BombState.Planted;
            sent.Match.ObjectiveRemainingSeconds = 40;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Match.HasActivePoint, Is.True);
            Assert.That(got.Match.ActivePointIndex, Is.EqualTo(3));
            Assert.That(got.Match.HasBomb, Is.True);
            Assert.That(got.Match.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(got.Match.ObjectiveRemainingSeconds, Is.EqualTo(40));
        }

        [Test]
        public void NoActivePointAndNoBombAreReadAsNone()
        {
            var sent = Build(100);
            sent.Match.HasActivePoint = false;
            sent.Match.ActivePointIndex = 5;
            sent.Match.HasBomb = false;
            sent.Match.Bomb = BombState.Detonated;

            var got = Send(sent, null);

            Assert.That(got.Match.HasActivePoint, Is.False);
            Assert.That(got.Match.ActivePointIndex, Is.Zero);
            Assert.That(got.Match.HasBomb, Is.False);
            Assert.That(got.Match.Bomb, Is.EqualTo(BombState.Carried));
        }

        [Test]
        public void AChangedObjectiveStateSendsOnlyItsBytesAndResolves()
        {
            var baseline = Build(100);
            var unchanged = Build(101);
            var current = Build(101);
            current.Match.HasActivePoint = true;
            current.Match.ActivePointIndex = 1;
            current.Match.ObjectiveRemainingSeconds = 59;

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(_objectiveStateBytes));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.HasActivePoint, Is.True);
            Assert.That(got.Match.ActivePointIndex, Is.EqualTo(1));
            Assert.That(got.Match.ObjectiveRemainingSeconds, Is.EqualTo(59));
            Assert.That(got.Match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void AnUnchangedObjectiveStateIsTakenFromTheBaseline()
        {
            var baseline = Build(100);
            baseline.Match.HasBomb = true;
            baseline.Match.Bomb = BombState.Dropped;
            baseline.Match.ObjectiveRemainingSeconds = 12;
            var current = Build(101);
            current.Match.HasBomb = true;
            current.Match.Bomb = BombState.Dropped;
            current.Match.ObjectiveRemainingSeconds = 12;
            current.Match.AlphaScore = 4;

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.HasBomb, Is.True);
            Assert.That(got.Match.Bomb, Is.EqualTo(BombState.Dropped));
            Assert.That(got.Match.ObjectiveRemainingSeconds, Is.EqualTo(12));
            Assert.That(got.Match.AlphaScore, Is.EqualTo(4));
        }

        [TestCase(-1)]
        [TestCase(SnapshotMessage.MaxControlPoints)]
        public void AnActivePointOutsideTheControlPointsIsNotWritten(int index)
        {
            var sent = Build(100);
            sent.Match.HasActivePoint = true;
            sent.Match.ActivePointIndex = index;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void NegativeObjectiveSecondsAreNotWritten()
        {
            var sent = Build(100);
            sent.Match.ObjectiveRemainingSeconds = -1;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(1, SnapshotMessage.MaxControlPoints)]
        [TestCase(3, 5)]
        [TestCase(0, 2)]
        [TestCase(2, 2)]
        public void AnUnreadableObjectiveStateIsRejected(int at, int value)
        {
            var baseline = Build(100);
            var current = Build(101);
            current.Match.HasActivePoint = true;
            current.Match.HasBomb = true;
            current.Match.ObjectiveRemainingSeconds = 1;
            var bytes = Bytes(current, baseline);
            bytes[_headBytes + at] = (byte)value;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void NegativeObjectiveSecondsAreUnreadable()
        {
            var baseline = Build(100);
            var current = Build(101);
            current.Match.ObjectiveRemainingSeconds = 1;
            var bytes = Bytes(current, baseline);
            for (var i = 0; i < 4; i++)
            {
                bytes[_headBytes + 4 + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheWeatherAndTimeOfDayRoundTripAndAreSentWhenChanged()
        {
            var baseline = Build(100);
            var current = Build(101);
            var rules = current.Match.Rules;
            rules.WeatherId = "rain";
            rules.TimeOfDayId = "dusk";
            current.Match.Rules = rules;

            var full = Send(current, null);
            var delta = Send(current, baseline);

            Assert.That(full.Match.Rules.WeatherId, Is.EqualTo("rain"));
            Assert.That(full.Match.Rules.TimeOfDayId, Is.EqualTo("dusk"));
            Assert.That(full.Match.Rules.SameAs(current.Match.Rules), Is.True);
            Assert.That(current.Match.Rules.SameAs(baseline.Match.Rules), Is.False);
            Assert.That(delta.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(delta.Match.Rules.WeatherId, Is.EqualTo("rain"));
            Assert.That(delta.Match.Rules.TimeOfDayId, Is.EqualTo("dusk"));
        }

        [Test]
        public void RulesWithoutWeatherReadAsEmptyIdsAndMatchNull()
        {
            var got = Send(Build(100), null);

            Assert.That(got.Match.Rules.WeatherId, Is.Empty);
            Assert.That(got.Match.Rules.TimeOfDayId, Is.Empty);
            Assert.That(got.Match.Rules.SameAs(MatchRulesSync.From(GameRules.Default)), Is.True);
        }

        [Test]
        public void ObjectiveItemsRoundTrip()
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, new Vector3(1f, 0f, 2f), ObjectiveItemState.Carried, 7);
            AddItem(sent, 2, ObjectiveItemKind.Bomb, Team.Bravo, PlayerIds.None, new Vector3(-4f, 0.5f, 9f), ObjectiveItemState.Planted, PlayerIds.None);
            AddItem(sent, 3, ObjectiveItemKind.Tag, Team.Bravo, 12, new Vector3(0f, 1f, 0f), ObjectiveItemState.Dropped, PlayerIds.None);

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.ObjectiveItems.Count, Is.EqualTo(3));
            Assert.That(got.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Flag));
            Assert.That(got.ObjectiveItems[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(got.ObjectiveItems[0].HasOwner, Is.False);
            Assert.That(got.ObjectiveItems[0].State, Is.EqualTo(ObjectiveItemState.Carried));
            Assert.That(got.ObjectiveItems[0].HasCarrier, Is.True);
            Assert.That(got.ObjectiveItems[0].CarrierId, Is.EqualTo(7));
            Assert.That(got.ObjectiveItems[1].Kind, Is.EqualTo(ObjectiveItemKind.Bomb));
            Assert.That(got.ObjectiveItems[1].State, Is.EqualTo(ObjectiveItemState.Planted));
            Assert.That(got.ObjectiveItems[1].Position, Is.EqualTo(new Vector3(-4f, 0.5f, 9f)));
            Assert.That(got.ObjectiveItems[1].HasCarrier, Is.False);
            Assert.That(got.ObjectiveItems[2].Kind, Is.EqualTo(ObjectiveItemKind.Tag));
            Assert.That(got.ObjectiveItems[2].HasOwner, Is.True);
            Assert.That(got.ObjectiveItems[2].OwnerId, Is.EqualTo(12));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(got.ObjectiveItems[i].ChangedBits(sent.ObjectiveItems[i]), Is.Zero);
            }
        }

        [Test]
        public void ACarriedFlagThatMovesSendsOnlyItsPosition()
        {
            var baseline = Build(100);
            AddItem(baseline, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Carried, 7);
            var current = Build(101);
            AddItem(current, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, new Vector3(0f, 0f, 1f), ObjectiveItemState.Carried, 7);

            var got = Send(current, baseline);

            Assert.That(got.ObjectiveItems[0].Present, Is.EqualTo(ObjectiveItemFields.Position));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.ObjectiveItems[0].Position, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(got.ObjectiveItems[0].CarrierId, Is.EqualTo(7));
            Assert.That(got.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Flag));
        }

        [Test]
        public void ADroppedFlagSendsOnlyItsState()
        {
            var baseline = Build(100);
            AddItem(baseline, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Carried, 7);
            var current = Build(101);
            AddItem(current, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Dropped, PlayerIds.None);

            var got = Send(current, baseline);

            Assert.That(got.ObjectiveItems[0].Present, Is.EqualTo(ObjectiveItemFields.State));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.ObjectiveItems[0].State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(got.ObjectiveItems[0].HasCarrier, Is.False);
        }

        [TestCase(ObjectiveItemState.Carried, PlayerIds.None)]
        [TestCase(ObjectiveItemState.Dropped, 3)]
        [TestCase(ObjectiveItemState.Home, 3)]
        public void AnItemIsNotWrittenUnlessItHasACarrierExactlyWhenCarried(ObjectiveItemState state, int carrier)
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, state, carrier);

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void ADroppedItemWithACarrierIsUnreadable()
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Carried, 7);
            var bytes = Bytes(sent, null);

            // 末尾は 状態 (1)・運んでいる人 (4)・設置物の並びの数 (1)
            bytes[bytes.Length - 6] = (byte)ObjectiveItemState.Dropped;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AnUnknownItemStateIsUnreadable()
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            var bytes = Bytes(sent, null);
            bytes[bytes.Length - 6] = 4;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void DeployablesRoundTrip()
        {
            var sent = Build(100);
            AddDeployable(sent, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, new Vector3(3f, 0f, 1f), 90f, 150);
            AddDeployable(sent, 5, DeployableKind.Shield, "barrier", Team.Bravo, PlayerIds.None, new Vector3(-1f, 0f, 0f), 0f, 300);
            AddDeployable(sent, 6, DeployableKind.Decoy, "hologram", Team.Bravo, 9, Vector3.Zero, -45f, 1);

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Deployables.Count, Is.EqualTo(3));
            Assert.That(got.Deployables[0].Kind, Is.EqualTo(DeployableKind.Turret));
            Assert.That(got.Deployables[0].TypeId, Is.EqualTo("sentry"));
            Assert.That(got.Deployables[0].OwnerId, Is.EqualTo(2));
            Assert.That(got.Deployables[0].Yaw, Is.EqualTo(90f));
            Assert.That(got.Deployables[0].Health, Is.EqualTo(150));
            Assert.That(got.Deployables[1].Kind, Is.EqualTo(DeployableKind.Shield));
            Assert.That(got.Deployables[1].HasOwner, Is.False);
            Assert.That(got.Deployables[2].Kind, Is.EqualTo(DeployableKind.Decoy));
            Assert.That(got.Deployables[2].Team, Is.EqualTo(Team.Bravo));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(got.Deployables[i].ChangedBits(sent.Deployables[i]), Is.Zero);
            }
        }

        [Test]
        public void ADamagedTurretSendsOnlyItsHealth()
        {
            var baseline = Build(100);
            AddDeployable(baseline, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, Vector3.Zero, 10f, 150);
            var current = Build(101);
            AddDeployable(current, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, Vector3.Zero, 10f, 90);

            var got = Send(current, baseline);

            Assert.That(got.Deployables[0].Present, Is.EqualTo(DeployableFields.Health));
            Assert.That(Size(current, baseline) - Size(Clone(baseline, 101), baseline), Is.EqualTo(4));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Deployables[0].Health, Is.EqualTo(90));
            Assert.That(got.Deployables[0].TypeId, Is.EqualTo("sentry"));
        }

        [Test]
        public void ATurningTurretSendsOnlyItsTransform()
        {
            var baseline = Build(100);
            AddDeployable(baseline, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, Vector3.Zero, 10f, 150);
            var current = Build(101);
            AddDeployable(current, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, Vector3.Zero, 20f, 150);

            var got = Send(current, baseline);

            Assert.That(got.Deployables[0].Present, Is.EqualTo(DeployableFields.Transform));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Deployables[0].Yaw, Is.EqualTo(20f));
        }

        [Test]
        public void NegativeDeployableHealthIsNotWritten()
        {
            var sent = Build(100);
            AddDeployable(sent, 4, DeployableKind.Shield, "barrier", Team.Alpha, 2, Vector3.Zero, 0f, -1);

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void NegativeDeployableHealthIsUnreadable()
        {
            var sent = Build(100);
            AddDeployable(sent, 4, DeployableKind.Shield, "barrier", Team.Alpha, 2, Vector3.Zero, 0f, 5);
            var bytes = Bytes(sent, null);

            // 体力は設置物の最後の 4 バイト
            for (var i = 1; i <= 4; i++)
            {
                bytes[bytes.Length - i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AFullSnapshotCannotLeaveOutAPartOfAnItemOrADeployable()
        {
            var sent = Build(100);
            AddDeployable(sent, 4, DeployableKind.Shield, "barrier", Team.Alpha, 2, Vector3.Zero, 0f, 5);
            var bytes = Bytes(sent, null);

            // 設置物の部分のビット (2 バイト) は、種類 (1)・Id の文字列 (2 + 7)・陣営 (1)・持ち主 (4)・位置と向き (16)・体力 (4) の前にある
            var bitsAt = bytes.Length - (1 + 2 + 7 + 1 + 4 + 16 + 4) - 2;
            bytes[bitsAt] = (byte)(DeployableFields.Identity | DeployableFields.Transform);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ResolveFailsWhenAPartialDeployableIsMissingFromTheBaseline()
        {
            var baseline = Build(100);
            AddDeployable(baseline, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, Vector3.Zero, 10f, 150);
            var current = Build(101);
            AddDeployable(current, 4, DeployableKind.Turret, "sentry", Team.Alpha, 2, Vector3.Zero, 10f, 90);
            var received = Send(baseline, null);
            received.Deployables.Clear();

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(received), Is.False);
        }

        [Test]
        public void CopyKeepsTheNewLists()
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.Bomb, Team.Alpha, PlayerIds.None, Vector3.One, ObjectiveItemState.Dropped, PlayerIds.None);
            AddDeployable(sent, 4, DeployableKind.Decoy, "hologram", Team.Alpha, 2, Vector3.One, 0f, 1);
            var copy = new SnapshotMessage();

            copy.CopyFrom(sent);
            sent.Clear();

            Assert.That(copy.ObjectiveItems.Count, Is.EqualTo(1));
            Assert.That(copy.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Bomb));
            Assert.That(copy.Deployables.Count, Is.EqualTo(1));
            Assert.That(copy.Deployables[0].TypeId, Is.EqualTo("hologram"));
            Assert.That(sent.ObjectiveItems.Count, Is.Zero);
            Assert.That(sent.Deployables.Count, Is.Zero);
        }

        [Test]
        public void TheCosmeticGunGameTierAndStreakRoundTrip()
        {
            var sent = Build(100);
            ref var player = ref AddPlayer(sent, 1);
            player.CosmeticId = "camo-tiger";
            player.GunGameTier = 6;
            player.StreakKills = 4;

            var got = Send(sent, null);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.All));
            Assert.That(got.Players[0].CosmeticId, Is.EqualTo("camo-tiger"));
            Assert.That(got.Players[0].GunGameTier, Is.EqualTo(6));
            Assert.That(got.Players[0].StreakKills, Is.EqualTo(4));
        }

        [Test]
        public void AFullSnapshotLeavesOutTheProgressWhileItIsDefault()
        {
            var plain = Build(100);
            AddPlayer(plain, 1);
            var streaking = Build(100);
            AddPlayer(streaking, 1).StreakKills = 1;

            // Gun Game の段とストリークの 4 バイトずつ
            Assert.That(Size(streaking, null), Is.EqualTo(Size(plain, null) + 8));
            Assert.That(Send(plain, null).Players[0].StreakKills, Is.Zero);
        }

        [Test]
        public void AFullSnapshotResetsTheProgressLeftOutToDefault()
        {
            var table = MatchMessages.CreateClientTable();
            var writer = new MessageWriter();
            var streaking = Build(100);
            ref var before = ref AddPlayer(streaking, 1);
            before.StreakKills = 3;
            before.GunGameTier = 2;
            writer.WriteMessage(streaking);
            table.Read(writer.AsSegment());
            writer.Reset();
            var plain = Build(101);
            AddPlayer(plain, 1);
            writer.WriteMessage(plain);

            var got = (SnapshotMessage)table.Read(writer.AsSegment());

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.All));
            Assert.That(got.Players[0].StreakKills, Is.Zero);
            Assert.That(got.Players[0].GunGameTier, Is.Zero);
        }

        [Test]
        public void ADeltaCarriesTheProgressOnlyWhenItChanges()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1);
            var current = Build(101);
            AddPlayer(current, 1).GunGameTier = 1;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Progress));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].GunGameTier, Is.EqualTo(1));
            Assert.That(got.Players[0].Health, Is.EqualTo(100));
        }

        [Test]
        public void AChangedCosmeticIsSentWithTheIdentity()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).CosmeticId = "default";
            var current = Build(101);
            AddPlayer(current, 1).CosmeticId = "camo-tiger";

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Identity));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].CosmeticId, Is.EqualTo("camo-tiger"));
            Assert.That(got.Players[0].Team, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void AnUnsetCosmeticIsTheSameAsAnEmptyOne()
        {
            var empty = new PlayerSnapshot { CosmeticId = "" };
            var unset = new PlayerSnapshot { CosmeticId = null };

            Assert.That(empty.ChangedBits(unset), Is.Zero);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void NegativeProgressIsNotWritten(int tier, int streak)
        {
            var sent = Build(100);
            ref var player = ref AddPlayer(sent, 1);
            player.GunGameTier = tier;
            player.StreakKills = streak;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void NegativeProgressIsUnreadable()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).StreakKills = 2;
            var bytes = Bytes(sent, null);

            // 末尾は ストリーク (4)・手榴弾から設置物までの並びの数 (6)
            for (var i = 0; i < 4; i++)
            {
                bytes[bytes.Length - 6 - 4 + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void SteadyStateWithItemsAndDeployablesDoesNotAllocate()
        {
            var table = MatchMessages.CreateClientTable();
            var writer = new MessageWriter();
            var baseline = Build(0);
            var current = Build(1);
            foreach (var snapshot in new[] { baseline, current })
            {
                AddPlayer(snapshot, 1).CosmeticId = "camo-tiger";
                AddItem(snapshot, 1, ObjectiveItemKind.Flag, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Carried, 1);
                AddDeployable(snapshot, 4, DeployableKind.Turret, "sentry", Team.Alpha, 1, Vector3.Zero, 0f, 100);
            }

            var rules = current.Match.Rules;
            rules.WeatherId = "rain";
            current.Match.Rules = rules;
            baseline.Match.Rules = rules;

            long allocated = 0;
            for (var round = 0; round < 3; round++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var tick = 1; tick <= 50; tick++)
                {
                    current.Tick = baseline.Tick + 1;
                    current.ObjectiveItems[0].Position = new Vector3(tick, 0f, 0f);
                    current.Deployables[0].Yaw = tick;
                    current.Players[0].StreakKills = tick;

                    // 届いた側が読み直すたびに同じ Id の文字列を作らないかを見るため、毎回すべてを載せる
                    current.Baseline = null;
                    writer.Reset();
                    writer.WriteMessage(current);
                    table.Read(writer.AsSegment());
                    baseline.CopyFrom(current);
                }

                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Assert.That(allocated, Is.Zero);
        }

        private static ref PlayerSnapshot AddPlayer(SnapshotMessage snapshot, int id)
        {
            ref var player = ref snapshot.Players.Add();
            player.PlayerId = id;
            player.Team = Team.Alpha;
            player.WeaponId = "rifle";
            player.Health = 100;
            return ref player;
        }

        private static void AddItem(
            SnapshotMessage snapshot, int id, ObjectiveItemKind kind, Team team, int owner, Vector3 position, ObjectiveItemState state, int carrier)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = id;
            item.Kind = kind;
            item.Team = team;
            item.HasOwner = owner != PlayerIds.None;
            item.OwnerId = item.HasOwner ? owner : 0;
            item.Position = position;
            item.State = state;
            item.HasCarrier = carrier != PlayerIds.None;
            item.CarrierId = item.HasCarrier ? carrier : 0;
        }

        private static void AddDeployable(
            SnapshotMessage snapshot, int id, DeployableKind kind, string typeId, Team team, int owner, Vector3 position, float yaw, int health)
        {
            ref var deployable = ref snapshot.Deployables.Add();
            deployable.DeployableId = id;
            deployable.Kind = kind;
            deployable.TypeId = typeId;
            deployable.Team = team;
            deployable.HasOwner = owner != PlayerIds.None;
            deployable.OwnerId = deployable.HasOwner ? owner : 0;
            deployable.Position = position;
            deployable.Yaw = yaw;
            deployable.Health = health;
        }

        private static SnapshotMessage Build(int tick)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.RemainingSeconds = 300;
            match.Rules = MatchRulesSync.From(GameRules.Default);
            return snapshot;
        }

        private static SnapshotMessage Clone(SnapshotMessage source, int tick)
        {
            var copy = new SnapshotMessage();
            copy.CopyFrom(source);
            copy.Tick = tick;
            return copy;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(Bytes(message, baseline))));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            return Bytes(message, baseline).Length;
        }

        private static byte[] Bytes(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.ToArray();
        }
    }
}
