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

        // 試合の部分のビットまでの頭 (Id・版・tick・基準の tick・部分のビット (2))
        private const int _headBytes = 2 + 1 + 4 + 4 + 2;

        // エリアの中心 (8)・半径 (4)・次の中心 (8)・次の半径 (4)・段階 (4)・段階の残り秒 (4)・残っている分隊 (4)
        private const int _battleRoyaleBytes = 8 + 4 + 8 + 4 + 4 + 4 + 4;

        // 25 で持ち主にスタミナ・よろけ・制圧が増え、26 で同期するルールにエモート (MatchRulesSync.Emote) が増え、
        // 27 で乗り物に撒いたフレア (VehicleSnapshot.Flares) と、持ち主に Horde の補強の案内 (OwnerSnapshot.HordeOffer) が増え、
        // 28 でダウン (DownedSync) にとどめを刺している人と進み具合が増えた
        [Test]
        public void TheVersionIs28()
        {
            Assert.That(SnapshotMessage.CurrentVersion, Is.EqualTo(28));
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
        public void TheParachuteRuleRoundTripsAndIsSentWhenChanged()
        {
            var baseline = Build(100);
            var current = Build(101);
            current.Match.Rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithParachute(Parachute.On)));

            var full = Send(current, null);
            var delta = Send(current, baseline);

            Assert.That(MatchRulesSync.From(GameRules.Default).Parachute, Is.EqualTo(Parachute.Off));
            Assert.That(default(MatchRulesSync).Parachute, Is.EqualTo(Parachute.Off), "nothing opens before the rules arrive");
            Assert.That(full.Match.Rules.Parachute, Is.EqualTo(Parachute.On));
            Assert.That(current.Match.Rules.SameAs(baseline.Match.Rules), Is.False);
            Assert.That(delta.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(delta.Match.Rules.Parachute, Is.EqualTo(Parachute.On));
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
        public void ACartItemRoundTrips()
        {
            var sent = Build(100);
            AddItem(sent, 4, ObjectiveItemKind.Cart, Team.Alpha, PlayerIds.None, new Vector3(5f, 0f, -3f), ObjectiveItemState.Home, PlayerIds.None);

            var got = Send(sent, null);

            Assert.That((int)ObjectiveItemKind.Cart, Is.EqualTo(3));
            Assert.That(got.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Cart));
            Assert.That(got.ObjectiveItems[0].Position, Is.EqualTo(new Vector3(5f, 0f, -3f)));
            Assert.That(got.ObjectiveItems[0].ChangedBits(sent.ObjectiveItems[0]), Is.Zero);
        }

        [Test]
        public void ThePayloadRoundTrips()
        {
            var sent = Build(100);
            sent.Match.PayloadProgress = 0.625f;
            sent.Match.PayloadCheckpointsPassed = 2;
            sent.Match.PayloadPushers = 3;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Match.PayloadProgress, Is.EqualTo(0.625f));
            Assert.That(got.Match.PayloadCheckpointsPassed, Is.EqualTo(2));
            Assert.That(got.Match.PayloadPushers, Is.EqualTo(3));
        }

        [Test]
        public void ADefaultPayloadRoundTripsAsZero()
        {
            var got = Send(Build(100), null);

            Assert.That(got.Match.PayloadProgress, Is.Zero);
            Assert.That(got.Match.PayloadCheckpointsPassed, Is.Zero);
            Assert.That(got.Match.PayloadPushers, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void ThePayloadEndsRoundTrip(float progress)
        {
            var sent = Build(100);
            sent.Match.PayloadProgress = progress;

            Assert.That(Send(sent, null).Match.PayloadProgress, Is.EqualTo(progress));
        }

        [Test]
        public void AMovingCartSendsOnlyThePayloadBytesAndResolves()
        {
            var baseline = Build(100);
            baseline.Match.PayloadProgress = 0.25f;
            baseline.Match.PayloadCheckpointsPassed = 1;
            var unchanged = Clone(baseline, 101);
            var current = Clone(baseline, 101);
            current.Match.PayloadProgress = 0.5f;
            current.Match.PayloadPushers = 2;

            var got = Send(current, baseline);

            // 進み (4)・通った中継点 (4)・押している人数 (4)
            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(4 + 4 + 4));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.PayloadProgress, Is.EqualTo(0.5f));
            Assert.That(got.Match.PayloadCheckpointsPassed, Is.EqualTo(1));
            Assert.That(got.Match.PayloadPushers, Is.EqualTo(2));
            Assert.That(got.Match.RemainingSeconds, Is.EqualTo(300));
        }

        [Test]
        public void AnUnchangedPayloadIsTakenFromTheBaseline()
        {
            var baseline = Build(100);
            baseline.Match.PayloadProgress = 0.75f;
            baseline.Match.PayloadCheckpointsPassed = 3;
            baseline.Match.PayloadPushers = 1;
            var current = Clone(baseline, 101);
            current.Match.AlphaScore = 2;

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.PayloadProgress, Is.EqualTo(0.75f));
            Assert.That(got.Match.PayloadCheckpointsPassed, Is.EqualTo(3));
            Assert.That(got.Match.PayloadPushers, Is.EqualTo(1));
            Assert.That(got.Match.AlphaScore, Is.EqualTo(2));
        }

        [TestCase(-0.1f, 0, 0)]
        [TestCase(1.1f, 0, 0)]
        [TestCase(float.NaN, 0, 0)]
        [TestCase(0f, -1, 0)]
        [TestCase(0f, 0, -1)]
        public void AnImpossiblePayloadIsNotWritten(float progress, int checkpoints, int pushers)
        {
            var sent = Build(100);
            sent.Match.PayloadProgress = progress;
            sent.Match.PayloadCheckpointsPassed = checkpoints;
            sent.Match.PayloadPushers = pushers;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(8)]
        public void AnImpossiblePayloadIsUnreadable(int at)
        {
            var baseline = Build(100);
            var current = Clone(baseline, 101);
            current.Match.PayloadProgress = 0.5f;
            var bytes = Bytes(current, baseline);

            // 差分に載るのは試合の台車の部分だけなので、頭の直後が 進み・中継点・人数 の順に並ぶ
            for (var i = 0; i < 4; i++)
            {
                bytes[_headBytes + at + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheOwnersMoneyRoundTrips()
        {
            var sent = Build(100);
            sent.HasOwner = true;
            sent.Owner.Money = 4200;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Owner.Money, Is.EqualTo(4200));
        }

        [Test]
        public void NoMoneyRoundTripsAsZero()
        {
            var sent = Build(100);
            sent.HasOwner = true;

            Assert.That(Send(sent, null).Owner.Money, Is.Zero);
        }

        [Test]
        public void ChangedMoneySendsOnlyItsBytesAndUnchangedMoneyIsTakenFromTheBaseline()
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            baseline.Owner.Money = 800;
            var unchanged = Clone(baseline, 101);
            var spent = Clone(baseline, 101);
            spent.Owner.Money = 150;

            var gotUnchanged = Send(unchanged, baseline);
            var gotSpent = Send(spent, baseline);

            Assert.That(Size(spent, baseline) - Size(unchanged, baseline), Is.EqualTo(4));
            Assert.That(gotUnchanged.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotUnchanged.Owner.Money, Is.EqualTo(800));
            Assert.That(gotSpent.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotSpent.Owner.Money, Is.EqualTo(150));
        }

        [Test]
        public void NegativeMoneyIsNotWritten()
        {
            var sent = Build(100);
            sent.HasOwner = true;
            sent.Owner.Money = -1;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void NegativeMoneyIsUnreadable()
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            var current = Clone(baseline, 101);
            current.Owner.Money = 1;
            var bytes = Bytes(current, baseline);

            // 試合の部分が空の差分では、頭・持ち主の有無 (1)・持ち主の部分のビット (2)・最後に処理した入力の tick (4) の後にお金が並ぶ
            for (var i = 0; i < 4; i++)
            {
                bytes[_headBytes + 1 + 2 + 4 + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ClearingAndCopyingASnapshotHandleTheMoneyAndThePayload()
        {
            var sent = Build(100);
            sent.HasOwner = true;
            sent.Owner.Money = 300;
            sent.Match.PayloadProgress = 0.5f;
            sent.Match.PayloadCheckpointsPassed = 1;
            sent.Match.PayloadPushers = 2;
            var copy = new SnapshotMessage();

            copy.CopyFrom(sent);
            sent.Clear();

            Assert.That(copy.Owner.Money, Is.EqualTo(300));
            Assert.That(copy.Match.PayloadProgress, Is.EqualTo(0.5f));
            Assert.That(copy.Match.PayloadCheckpointsPassed, Is.EqualTo(1));
            Assert.That(copy.Match.PayloadPushers, Is.EqualTo(2));
            Assert.That(sent.Owner.Money, Is.Zero);
            Assert.That(sent.Match.PayloadProgress, Is.Zero);
            Assert.That(sent.Match.PayloadPushers, Is.Zero);
        }

        [Test]
        public void TheBattleRoyaleRoundTrips()
        {
            var sent = Build(100);
            SetZone(sent, new Vector2(120.5f, -40f), 300f, new Vector2(100f, -20.25f), 150f, 2, 45, 17);

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Match.ZoneCenter, Is.EqualTo(new Vector2(120.5f, -40f)));
            Assert.That(got.Match.ZoneRadius, Is.EqualTo(300f));
            Assert.That(got.Match.NextZoneCenter, Is.EqualTo(new Vector2(100f, -20.25f)));
            Assert.That(got.Match.NextZoneRadius, Is.EqualTo(150f));
            Assert.That(got.Match.ZonePhase, Is.EqualTo(2));
            Assert.That(got.Match.ZonePhaseRemainingSeconds, Is.EqualTo(45));
            Assert.That(got.Match.SquadsRemaining, Is.EqualTo(17));
            Assert.That(got.Match.RemainingSeconds, Is.EqualTo(300));
        }

        [Test]
        public void ABattleRoyaleObjectiveRoundTrips()
        {
            var sent = Build(100);
            sent.Match.Objective = MatchObjective.BattleRoyale;
            var extraction = Build(100);
            extraction.Match.Objective = MatchObjective.Extraction;

            Assert.That(Send(sent, null).Match.Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(Send(extraction, null).Match.Objective, Is.EqualTo(MatchObjective.Extraction));
        }

        // 列挙の値はバイト 1 つで送るので、足した値も前からある値もそのまま往復することを確かめる
        [Test]
        public void EveryObjectiveRoundTrips()
        {
            foreach (var objective in Enum.GetValues<MatchObjective>())
            {
                var sent = Build(100);
                sent.Match.Objective = objective;

                Assert.That(Send(sent, null).Match.Objective, Is.EqualTo(objective));
            }
        }

        [Test]
        public void AnObjectivePastTheLastValueIsRejected()
        {
            var sent = Build(100);
            sent.Match.Objective = (MatchObjective)Enum.GetValues<MatchObjective>().Length;

            Assert.Throws<MessageReadException>(() => Send(sent, null));
        }

        [Test]
        public void ADefaultBattleRoyaleRoundTripsAsZero()
        {
            var got = Send(Build(100), null);

            Assert.That(got.Match.ZoneCenter, Is.EqualTo(Vector2.Zero));
            Assert.That(got.Match.ZoneRadius, Is.Zero);
            Assert.That(got.Match.NextZoneCenter, Is.EqualTo(Vector2.Zero));
            Assert.That(got.Match.NextZoneRadius, Is.Zero);
            Assert.That(got.Match.ZonePhase, Is.Zero);
            Assert.That(got.Match.ZonePhaseRemainingSeconds, Is.Zero);
            Assert.That(got.Match.SquadsRemaining, Is.Zero);
        }

        [Test]
        public void AShrinkingZoneSendsOnlyTheBattleRoyaleBytesAndResolves()
        {
            var baseline = Build(100);
            SetZone(baseline, new Vector2(10f, 10f), 200f, new Vector2(5f, 5f), 100f, 1, 30, 20);
            var unchanged = Clone(baseline, 101);
            var current = Clone(baseline, 101);
            SetZone(current, new Vector2(9f, 9.5f), 180f, new Vector2(5f, 5f), 100f, 1, 29, 19);

            var got = Send(current, baseline);

            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(_battleRoyaleBytes));
            Assert.That(got.IsComplete, Is.False);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.ZoneCenter, Is.EqualTo(new Vector2(9f, 9.5f)));
            Assert.That(got.Match.ZoneRadius, Is.EqualTo(180f));
            Assert.That(got.Match.ZonePhaseRemainingSeconds, Is.EqualTo(29));
            Assert.That(got.Match.SquadsRemaining, Is.EqualTo(19));
            Assert.That(got.Match.RemainingSeconds, Is.EqualTo(300));
        }

        [Test]
        public void AnUnchangedBattleRoyaleIsTakenFromTheBaseline()
        {
            var baseline = Build(100);
            SetZone(baseline, new Vector2(-3f, 4f), 75f, new Vector2(-1f, 2f), 25f, 4, 12, 3);
            var current = Clone(baseline, 101);
            current.Match.AlphaScore = 5;

            var got = Send(current, baseline);

            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Match.ZoneCenter, Is.EqualTo(new Vector2(-3f, 4f)));
            Assert.That(got.Match.ZoneRadius, Is.EqualTo(75f));
            Assert.That(got.Match.NextZoneCenter, Is.EqualTo(new Vector2(-1f, 2f)));
            Assert.That(got.Match.NextZoneRadius, Is.EqualTo(25f));
            Assert.That(got.Match.ZonePhase, Is.EqualTo(4));
            Assert.That(got.Match.ZonePhaseRemainingSeconds, Is.EqualTo(12));
            Assert.That(got.Match.SquadsRemaining, Is.EqualTo(3));
            Assert.That(got.Match.AlphaScore, Is.EqualTo(5));
        }

        [TestCase(float.NaN, 0f, 1f, 1f, 0, 0, 0)]
        [TestCase(float.PositiveInfinity, 0f, 1f, 1f, 0, 0, 0)]
        [TestCase(0f, float.NegativeInfinity, 1f, 1f, 0, 0, 0)]
        [TestCase(0f, 0f, -0.5f, 1f, 0, 0, 0)]
        [TestCase(0f, 0f, float.NaN, 1f, 0, 0, 0)]
        [TestCase(0f, 0f, 1f, -1f, 0, 0, 0)]
        [TestCase(0f, 0f, 1f, float.PositiveInfinity, 0, 0, 0)]
        [TestCase(0f, 0f, 1f, 1f, -1, 0, 0)]
        [TestCase(0f, 0f, 1f, 1f, 0, -1, 0)]
        [TestCase(0f, 0f, 1f, 1f, 0, 0, -1)]
        public void AnImpossibleBattleRoyaleIsNotWritten(
            float centerX, float nextCenterZ, float radius, float nextRadius, int phase, int seconds, int squads)
        {
            var sent = Build(100);
            SetZone(sent, new Vector2(centerX, 0f), radius, new Vector2(0f, nextCenterZ), nextRadius, phase, seconds, squads);

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(16)]
        [TestCase(20)]
        [TestCase(24)]
        [TestCase(28)]
        [TestCase(32)]
        public void AnImpossibleBattleRoyaleIsUnreadable(int at)
        {
            var baseline = Build(100);
            var current = Clone(baseline, 101);
            SetZone(current, new Vector2(1f, 2f), 50f, new Vector2(3f, 4f), 25f, 1, 10, 8);
            var bytes = Bytes(current, baseline);

            // 差分に載るのは試合の BR の部分だけなので、頭の直後が 中心・半径・次の中心・次の半径・段階・残り秒・分隊 の順に並ぶ。
            // 0xFF の 4 バイトは浮動小数なら NaN、整数なら -1
            for (var i = 0; i < 4; i++)
            {
                bytes[_headBytes + at + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [TestCase(8)]
        [TestCase(20)]
        public void ANegativeZoneRadiusIsUnreadable(int at)
        {
            var baseline = Build(100);
            var current = Clone(baseline, 101);
            SetZone(current, Vector2.Zero, 50f, Vector2.Zero, 25f, 1, 10, 8);
            var bytes = Bytes(current, baseline);
            BitConverter.TryWriteBytes(new Span<byte>(bytes, _headBytes + at, 4), -1f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheMatchPartsAreWrittenInTwoBytesAndAnUnknownPartIsUnreadable()
        {
            var baseline = Build(100);
            var current = Clone(baseline, 101);
            SetZone(current, Vector2.Zero, 1f, Vector2.Zero, 1f, 1, 1, 1);
            var bytes = Bytes(current, baseline);

            Assert.That(BitConverter.ToUInt16(bytes, _headBytes - 2), Is.EqualTo((ushort)MatchFields.BattleRoyale));
            Assert.That((int)MatchFields.BattleRoyale, Is.EqualTo(1 << 8));

            bytes[_headBytes - 1] |= 1 << 1;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [TestCase(ObjectiveItemKind.LootBox, 4, false, 1.5f)]
        [TestCase(ObjectiveItemKind.SupplyCrate, 5, true, 2f)]
        [TestCase(ObjectiveItemKind.DeathBox, 6, true, 1.25f)]
        [TestCase(ObjectiveItemKind.ExtractionPoint, 7, true, 8f)]
        [TestCase(ObjectiveItemKind.WeaponPickup, 9, false, 1f)]
        [TestCase(ObjectiveItemKind.AmmoPickup, 10, true, 1f)]
        [TestCase(ObjectiveItemKind.HealthPickup, 11, false, 1.5f)]
        [TestCase(ObjectiveItemKind.ArmorPickup, 12, true, 1f)]
        [TestCase(ObjectiveItemKind.ItemPickup, 13, false, 0.5f)]
        [TestCase(ObjectiveItemKind.AmmoCrate, 14, true, 1.6f)]
        public void ABoxOrAnExtractionPointRoundTrips(ObjectiveItemKind kind, int value, bool opened, float radius)
        {
            var sent = Build(100);
            AddItem(sent, 9, kind, Team.Alpha, 3, new Vector3(12f, 1f, -7f), ObjectiveItemState.Home, PlayerIds.None);
            sent.ObjectiveItems[0].Opened = opened;
            sent.ObjectiveItems[0].Radius = radius;

            var got = Send(sent, null);

            Assert.That((int)kind, Is.EqualTo(value));
            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.ObjectiveItems[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(got.ObjectiveItems[0].Kind, Is.EqualTo(kind));
            Assert.That(got.ObjectiveItems[0].OwnerId, Is.EqualTo(3));
            Assert.That(got.ObjectiveItems[0].Position, Is.EqualTo(new Vector3(12f, 1f, -7f)));
            Assert.That(got.ObjectiveItems[0].Opened, Is.EqualTo(opened));
            Assert.That(got.ObjectiveItems[0].Radius, Is.EqualTo(radius));
            Assert.That(got.ObjectiveItems[0].ChangedBits(sent.ObjectiveItems[0]), Is.Zero);
        }

        [Test]
        public void AFullSnapshotLeavesOutTheAccessWhileItIsDefault()
        {
            var plain = Build(100);
            AddItem(plain, 1, ObjectiveItemKind.LootBox, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            var opened = Build(100);
            AddItem(opened, 1, ObjectiveItemKind.LootBox, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            opened.ObjectiveItems[0].Opened = true;

            var got = Send(plain, null);

            // 開けたか (1)・半径 (4)
            Assert.That(Size(opened, null), Is.EqualTo(Size(plain, null) + 1 + 4));
            Assert.That(got.ObjectiveItems[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(got.ObjectiveItems[0].Opened, Is.False);
            Assert.That(got.ObjectiveItems[0].Radius, Is.Zero);
        }

        [Test]
        public void AFullSnapshotResetsTheAccessLeftOutToDefault()
        {
            var table = MatchMessages.CreateClientTable();
            var writer = new MessageWriter();
            var opened = Build(100);
            AddItem(opened, 1, ObjectiveItemKind.SupplyCrate, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            opened.ObjectiveItems[0].Opened = true;
            opened.ObjectiveItems[0].Radius = 2f;
            writer.WriteMessage(opened);
            table.Read(writer.AsSegment());
            writer.Reset();
            var plain = Build(101);
            AddItem(plain, 1, ObjectiveItemKind.SupplyCrate, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            writer.WriteMessage(plain);

            var got = (SnapshotMessage)table.Read(writer.AsSegment());

            Assert.That(got.ObjectiveItems[0].Opened, Is.False);
            Assert.That(got.ObjectiveItems[0].Radius, Is.Zero);
        }

        [Test]
        public void OpeningABoxSendsOnlyItsAccessAndResolves()
        {
            var baseline = Build(100);
            AddItem(baseline, 2, ObjectiveItemKind.LootBox, Team.Alpha, PlayerIds.None, new Vector3(4f, 0f, 4f), ObjectiveItemState.Home, PlayerIds.None);
            baseline.ObjectiveItems[0].Radius = 1.5f;
            var current = Clone(baseline, 101);
            current.ObjectiveItems[0].Opened = true;

            var got = Send(current, baseline);

            Assert.That(got.ObjectiveItems[0].Present, Is.EqualTo(ObjectiveItemFields.Access));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.ObjectiveItems[0].Opened, Is.True);
            Assert.That(got.ObjectiveItems[0].Radius, Is.EqualTo(1.5f));
            Assert.That(got.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.LootBox));
            Assert.That(got.ObjectiveItems[0].Position, Is.EqualTo(new Vector3(4f, 0f, 4f)));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AnImpossibleItemRadiusIsNotWritten(float radius)
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.ExtractionPoint, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            sent.ObjectiveItems[0].Radius = radius;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void AnImpossibleItemRadiusIsUnreadable(float radius)
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.ExtractionPoint, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            sent.ObjectiveItems[0].Radius = 6f;
            var bytes = Bytes(sent, null);

            // 末尾は 半径 (4)・設置物の並びの数 (1)
            BitConverter.TryWriteBytes(new Span<byte>(bytes, bytes.Length - 1 - 4, 4), radius);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AnUnknownItemKindIsUnreadable()
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.AmmoCrate, Team.Alpha, PlayerIds.None, Vector3.Zero, ObjectiveItemState.Home, PlayerIds.None);
            var bytes = Bytes(sent, null);

            // 物の種類は、末尾の 種類 (1)・陣営 (1)・持ち主 (4)・位置 (12)・状態 (1)・運んでいる人 (4)・設置物の並びの数 (1) の頭にある
            var kindAt = bytes.Length - (1 + 1 + 4 + 12 + 1 + 4 + 1);
            Assert.That(bytes[kindAt], Is.EqualTo((byte)ObjectiveItemKind.AmmoCrate));
            bytes[kindAt] = (byte)(ObjectiveItemKind.AmmoCrate + 1);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheItemKindsKeepTheirValues()
        {
            Assert.That((int)ObjectiveItemKind.Flag, Is.EqualTo(0));
            Assert.That((int)ObjectiveItemKind.ExtractionPoint, Is.EqualTo(7));
            Assert.That((int)ObjectiveItemKind.Leader, Is.EqualTo(8));
            Assert.That((int)ObjectiveItemKind.WeaponPickup, Is.EqualTo(9));
            Assert.That((int)ObjectiveItemKind.AmmoCrate, Is.EqualTo(14));
            Assert.That(Enum.GetValues<ObjectiveItemKind>().Length, Is.EqualTo(15));
        }

        [TestCase(ObjectiveItemState.Carried, 4)]
        [TestCase(ObjectiveItemState.Dropped, PlayerIds.None)]
        public void AGuardianLeaderRoundTrips(ObjectiveItemState state, int carrier)
        {
            var sent = Build(100);
            AddItem(sent, 1, ObjectiveItemKind.Leader, Team.Bravo, 4, new Vector3(3f, 1f, -4f), state, carrier);

            var got = Send(sent, null);

            Assert.That(got.ObjectiveItems.Count, Is.EqualTo(1));
            Assert.That(got.ObjectiveItems[0].Kind, Is.EqualTo(ObjectiveItemKind.Leader));
            Assert.That(got.ObjectiveItems[0].Team, Is.EqualTo(Team.Bravo));
            Assert.That(got.ObjectiveItems[0].HasOwner, Is.True);
            Assert.That(got.ObjectiveItems[0].OwnerId, Is.EqualTo(4));
            Assert.That(got.ObjectiveItems[0].State, Is.EqualTo(state));
            Assert.That(got.ObjectiveItems[0].HasCarrier, Is.EqualTo(state == ObjectiveItemState.Carried));
            Assert.That(got.ObjectiveItems[0].ChangedBits(sent.ObjectiveItems[0]), Is.Zero);
        }

        [Test]
        public void TheSquadAndTheParachuteRoundTrip()
        {
            var sent = Build(100);
            ref var player = ref AddPlayer(sent, 1);
            player.SquadId = 12;
            player.IsParachuting = true;
            player.IsAiming = true;

            var got = Send(sent, null);

            Assert.That(got.Players[0].SquadId, Is.EqualTo(12));
            Assert.That(got.Players[0].IsParachuting, Is.True);
            Assert.That(got.Players[0].IsAiming, Is.True);
            Assert.That(got.Players[0].IsGrounded, Is.False);
            Assert.That(got.Players[0].ChangedBits(sent.Players[0]), Is.Zero);
        }

        [Test]
        public void ADefaultSquadAndParachuteRoundTripAsZero()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).SquadId = byte.MaxValue;
            var plain = Build(101);
            AddPlayer(plain, 1);

            Assert.That(Send(sent, null).Players[0].SquadId, Is.EqualTo(byte.MaxValue));
            Assert.That(Send(plain, null).Players[0].SquadId, Is.Zero);
            Assert.That(Send(plain, null).Players[0].IsParachuting, Is.False);
        }

        [Test]
        public void AChangedSquadIsSentWithTheIdentity()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).SquadId = 2;
            var current = Build(101);
            AddPlayer(current, 1).SquadId = 5;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Identity));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].SquadId, Is.EqualTo(5));
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("rifle"));
        }

        [Test]
        public void BeingOutsideTheSquadsRoundTripsWithTheIdentity()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1);
            var current = Build(101);
            ref var outside = ref AddPlayer(current, 1);
            outside.OutsideSquad = true;
            outside.IsBot = true;
            outside.Concealed = true;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Identity));
            Assert.That(got.Players[0].OutsideSquad, Is.True);
            Assert.That(got.Players[0].IsBot, Is.True);
            Assert.That(got.Players[0].Concealed, Is.True);
            Assert.That(got.Players[0].SquadId, Is.Zero);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].OutsideSquad, Is.True, "the baseline does not overwrite a sent flag");
            Assert.That(Send(baseline, null).Players[0].OutsideSquad, Is.False);
        }

        [Test]
        public void ConcealedAndBotRoundTripTogether()
        {
            var sent = Build(100);
            ref var concealedBot = ref AddPlayer(sent, 1);
            concealedBot.IsBot = true;
            concealedBot.Concealed = true;
            AddPlayer(sent, 2).Concealed = true;
            AddPlayer(sent, 3).IsBot = true;
            AddPlayer(sent, 4);

            var got = Send(sent, null);

            Assert.That(got.Players[0].IsBot, Is.True);
            Assert.That(got.Players[0].Concealed, Is.True);
            Assert.That(got.Players[1].IsBot, Is.False);
            Assert.That(got.Players[1].Concealed, Is.True);
            Assert.That(got.Players[2].IsBot, Is.True);
            Assert.That(got.Players[2].Concealed, Is.False);
            Assert.That(got.Players[3].IsBot, Is.False);
            Assert.That(got.Players[3].Concealed, Is.False);
            Assert.That(got.Players[0].ChangedBits(sent.Players[0]), Is.Zero);
        }

        [Test]
        public void ConcealingDoesNotGrowTheRow()
        {
            var plain = Build(100);
            AddPlayer(plain, 1);
            var concealed = Build(100);
            AddPlayer(concealed, 1).Concealed = true;

            Assert.That(Size(concealed, null), Is.EqualTo(Size(plain, null)));
        }

        [Test]
        public void ConcealingIsSentWithTheIdentityAndResolves()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).SquadId = 4;
            var current = Build(101);
            ref var player = ref AddPlayer(current, 1);
            player.SquadId = 4;
            player.Concealed = true;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Identity));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Concealed, Is.True);
            Assert.That(got.Players[0].SquadId, Is.EqualTo(4));
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("rifle"));
        }

        [Test]
        public void ShowingAgainIsSentWithTheIdentityAndResolves()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).Concealed = true;
            var current = Build(101);
            AddPlayer(current, 1);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Identity));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Concealed, Is.False);
        }

        [Test]
        public void AnUnchangedConcealedRowIsNotResent()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).Concealed = true;
            var current = Build(101);
            AddPlayer(current, 1).Concealed = true;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.None));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Concealed, Is.True);
        }

        [Test]
        public void AnUnknownIdentityFlagIsRejected()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).IsBot = true;
            var plain = Build(100);
            AddPlayer(plain, 1);
            var bytes = Bytes(sent, null);
            var other = Bytes(plain, null);

            // 旗のバイトは Bot の有無だけで変わるので、Bot でない行と比べて場所を見つける
            var at = 0;
            while (bytes[at] == other[at])
            {
                at++;
            }

            Assert.That(bytes[at], Is.EqualTo(1));
            bytes[at] = 1 << 3;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void OpeningTheParachuteIsSentWithTheMotion()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).SquadId = 3;
            var current = Build(101);
            ref var player = ref AddPlayer(current, 1);
            player.SquadId = 3;
            player.IsParachuting = true;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Motion));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].IsParachuting, Is.True);
            Assert.That(got.Players[0].SquadId, Is.EqualTo(3));
        }

        [Test]
        public void TheBotArchetypeAndCueRoundTrip()
        {
            var sent = Build(100);
            ref var charger = ref AddPlayer(sent, 1);
            charger.IsBot = true;
            charger.Archetype = EnemyArchetype.Charger;
            charger.ArchetypeCue = BotArchetypeCue.Warning;
            AddPlayer(sent, 2);

            var got = Send(sent, null);

            Assert.That(got.Players[0].Archetype, Is.EqualTo(EnemyArchetype.Charger));
            Assert.That(got.Players[0].ArchetypeCue, Is.EqualTo(BotArchetypeCue.Warning));
            Assert.That(got.Players[1].Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(got.Players[1].ArchetypeCue, Is.EqualTo(BotArchetypeCue.None));
            Assert.That(got.Players[0].ChangedBits(sent.Players[0]), Is.Zero);
        }

        [Test]
        public void ChangingTheCueIsSentWithTheIdentityAndResolves()
        {
            var baseline = Build(100);
            ref var before = ref AddPlayer(baseline, 1);
            before.Archetype = EnemyArchetype.Sniper;
            before.SquadId = 2;
            var current = Build(101);
            ref var after = ref AddPlayer(current, 1);
            after.Archetype = EnemyArchetype.Sniper;
            after.SquadId = 2;
            after.ArchetypeCue = BotArchetypeCue.Attacking;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Identity));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Archetype, Is.EqualTo(EnemyArchetype.Sniper));
            Assert.That(got.Players[0].ArchetypeCue, Is.EqualTo(BotArchetypeCue.Attacking));
            Assert.That(got.Players[0].SquadId, Is.EqualTo(2));
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("rifle"));
        }

        [Test]
        public void AnUnchangedArchetypeIsNotResent()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).Archetype = EnemyArchetype.Bomber;
            var current = Build(101);
            AddPlayer(current, 1).Archetype = EnemyArchetype.Bomber;

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.None));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Archetype, Is.EqualTo(EnemyArchetype.Bomber));
        }

        // 種類と予兆は Identity にいつも 1 バイトずつ載るので、値を変えても Identity の大きさは変わらない
        [Test]
        public void TheArchetypeDoesNotGrowAnIdentityThatIsSentAnyway()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1);
            var plain = Build(101);
            AddPlayer(plain, 1).SquadId = 1;
            var bot = Build(101);
            ref var row = ref AddPlayer(bot, 1);
            row.SquadId = 1;
            row.Archetype = EnemyArchetype.Shield;
            row.ArchetypeCue = BotArchetypeCue.Exposed;

            Assert.That(Size(bot, baseline), Is.EqualTo(Size(plain, baseline)));
        }

        [TestCase(EnemyArchetype.Bomber, 5)]
        public void AnUnknownArchetypeIsRejected(EnemyArchetype archetype, int unknown)
        {
            var sent = Build(100);
            AddPlayer(sent, 1).Archetype = archetype;
            var plain = Build(100);
            AddPlayer(plain, 1);
            var bytes = Bytes(sent, null);
            var other = Bytes(plain, null);

            // 種類のバイトだけが違うので、種類の無い行と比べて場所を見つける
            var at = 0;
            while (bytes[at] == other[at])
            {
                at++;
            }

            Assert.That(bytes[at], Is.EqualTo((byte)archetype));
            bytes[at] = (byte)unknown;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AnUnknownCueIsRejected()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).ArchetypeCue = BotArchetypeCue.Exposed;
            var plain = Build(100);
            AddPlayer(plain, 1);
            var bytes = Bytes(sent, null);
            var other = Bytes(plain, null);
            var at = 0;
            while (bytes[at] == other[at])
            {
                at++;
            }

            Assert.That(bytes[at], Is.EqualTo((byte)BotArchetypeCue.Exposed));
            bytes[at] = 4;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheOwnersRedeployAndExtractionRoundTrip()
        {
            var sent = Build(100);
            sent.HasOwner = true;
            sent.Owner.RedeploysLeft = 2;
            sent.Owner.RedeploySeconds = 15;
            sent.Owner.ExtractionProgress = 0.375f;
            sent.Owner.HasExtractionPoint = true;
            sent.Owner.ExtractionPointId = 7;

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Owner.RedeploysLeft, Is.EqualTo(2));
            Assert.That(got.Owner.RedeploySeconds, Is.EqualTo(15));
            Assert.That(got.Owner.ExtractionProgress, Is.EqualTo(0.375f));
            Assert.That(got.Owner.HasExtractionPoint, Is.True);
            Assert.That(got.Owner.ExtractionPointId, Is.EqualTo(7));
        }

        [Test]
        public void NoRedeployAndNoExtractionRoundTripAsNone()
        {
            var sent = Build(100);
            sent.HasOwner = true;
            sent.Owner.ExtractionPointId = 9;

            var got = Send(sent, null);

            Assert.That(got.Owner.RedeploysLeft, Is.Zero);
            Assert.That(got.Owner.RedeploySeconds, Is.Null);
            Assert.That(got.Owner.ExtractionProgress, Is.Zero);
            Assert.That(got.Owner.HasExtractionPoint, Is.False);
            Assert.That(got.Owner.ExtractionPointId, Is.Zero);
        }

        [Test]
        public void ChangedRedeploySendsOnlyItsBytesAndResolves()
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            baseline.Owner.RedeploysLeft = 1;
            baseline.Owner.HasExtractionPoint = true;
            baseline.Owner.ExtractionPointId = 4;
            var unchanged = Clone(baseline, 101);
            var current = Clone(baseline, 101);
            current.Owner.RedeploySeconds = 9;

            var got = Send(current, baseline);

            // 残り回数 (4)・待っているか (1)・残り秒 (4)
            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(4 + 1 + 4));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.RedeploysLeft, Is.EqualTo(1));
            Assert.That(got.Owner.RedeploySeconds, Is.EqualTo(9));
            Assert.That(got.Owner.HasExtractionPoint, Is.True);
            Assert.That(got.Owner.ExtractionPointId, Is.EqualTo(4));
        }

        [Test]
        public void ChangedExtractionSendsOnlyItsBytesAndResolves()
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            baseline.Owner.RedeploysLeft = 3;
            baseline.Owner.HasExtractionPoint = true;
            baseline.Owner.ExtractionPointId = 6;
            baseline.Owner.ExtractionProgress = 0.25f;
            var unchanged = Clone(baseline, 101);
            var current = Clone(baseline, 101);
            current.Owner.ExtractionProgress = 0.5f;

            var got = Send(current, baseline);

            // 進み (4)・脱出地点 (4)
            Assert.That(Size(current, baseline) - Size(unchanged, baseline), Is.EqualTo(4 + 4));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.ExtractionProgress, Is.EqualTo(0.5f));
            Assert.That(got.Owner.ExtractionPointId, Is.EqualTo(6));
            Assert.That(got.Owner.RedeploysLeft, Is.EqualTo(3));
        }

        [Test]
        public void TheOwnerPartsAreWrittenInTwoBytes()
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            var current = Clone(baseline, 101);
            current.Owner.HasExtractionPoint = true;
            current.Owner.ExtractionPointId = 1;
            var bytes = Bytes(current, baseline);

            // 試合の部分が空の差分では、頭・持ち主の有無 (1) の後に持ち主の部分のビットが並ぶ
            Assert.That(BitConverter.ToUInt16(bytes, _headBytes + 1), Is.EqualTo((ushort)OwnerFields.Extraction));
            Assert.That((int)OwnerFields.Extraction, Is.EqualTo(1 << 8));

            // 上のバイトの 1 << 4 は 1 << 12 で、制圧 (1 << 11) の次のまだ無い部分
            bytes[_headBytes + 1 + 1] |= 1 << 4;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [TestCase(-1, null, 0f, false, 0)]
        [TestCase(0, -1, 0f, false, 0)]
        [TestCase(0, null, -0.1f, true, 1)]
        [TestCase(0, null, 1.1f, true, 1)]
        [TestCase(0, null, float.NaN, true, 1)]
        [TestCase(0, null, 0.5f, false, 0)]
        [TestCase(0, null, 0f, true, -1)]
        public void AnImpossibleRedeployOrExtractionIsNotWritten(int left, int? seconds, float progress, bool hasPoint, int point)
        {
            var sent = Build(100);
            sent.HasOwner = true;
            sent.Owner.RedeploysLeft = left;
            sent.Owner.RedeploySeconds = seconds;
            sent.Owner.ExtractionProgress = progress;
            sent.Owner.HasExtractionPoint = hasPoint;
            sent.Owner.ExtractionPointId = point;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase(0)]
        [TestCase(5)]
        public void AnImpossibleRedeployIsUnreadable(int at)
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            var current = Clone(baseline, 101);
            current.Owner.RedeploysLeft = 1;
            current.Owner.RedeploySeconds = 3;
            var bytes = Bytes(current, baseline);

            // 試合の部分が空の差分では、頭・持ち主の有無 (1)・持ち主の部分のビット (2)・最後に処理した入力の tick (4) の後に
            // 残り回数 (4)・待っているか (1)・残り秒 (4) が並ぶ
            for (var i = 0; i < 4; i++)
            {
                bytes[_headBytes + 1 + 2 + 4 + at + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [TestCase(0, 0xFF)]
        [TestCase(4, 0xFE)]
        public void AnImpossibleExtractionIsUnreadable(int at, int fill)
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            var current = Clone(baseline, 101);
            current.Owner.HasExtractionPoint = true;
            current.Owner.ExtractionPointId = 2;
            current.Owner.ExtractionProgress = 0.5f;
            var bytes = Bytes(current, baseline);

            // 進み (4)・脱出地点 (4) が並ぶ。0xFF の 4 バイトの進みは NaN、0xFE で始まる 0xFF の脱出地点は -2
            bytes[_headBytes + 1 + 2 + 4 + at] = (byte)fill;
            for (var i = 1; i < 4; i++)
            {
                bytes[_headBytes + 1 + 2 + 4 + at + i] = 0xFF;
            }

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ExtractionProgressWithoutAPointIsUnreadable()
        {
            var baseline = Build(100);
            baseline.HasOwner = true;
            var current = Clone(baseline, 101);
            current.Owner.HasExtractionPoint = true;
            current.Owner.ExtractionPointId = 2;
            current.Owner.ExtractionProgress = 0.5f;
            var bytes = Bytes(current, baseline);
            BitConverter.TryWriteBytes(new Span<byte>(bytes, _headBytes + 1 + 2 + 4 + 4, 4), PlayerIds.None);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ClearingAndCopyingASnapshotHandleTheBattleRoyaleAndExtraction()
        {
            var sent = Build(100);
            sent.HasOwner = true;
            SetZone(sent, new Vector2(1f, 2f), 30f, new Vector2(3f, 4f), 10f, 2, 5, 6);
            sent.Owner.RedeploysLeft = 1;
            sent.Owner.RedeploySeconds = 4;
            sent.Owner.ExtractionProgress = 0.75f;
            sent.Owner.HasExtractionPoint = true;
            sent.Owner.ExtractionPointId = 8;
            var copy = new SnapshotMessage();

            copy.CopyFrom(sent);
            sent.Clear();

            Assert.That(copy.Match.ZoneRadius, Is.EqualTo(30f));
            Assert.That(copy.Match.SquadsRemaining, Is.EqualTo(6));
            Assert.That(copy.Owner.RedeploysLeft, Is.EqualTo(1));
            Assert.That(copy.Owner.RedeploySeconds, Is.EqualTo(4));
            Assert.That(copy.Owner.ExtractionProgress, Is.EqualTo(0.75f));
            Assert.That(copy.Owner.HasExtractionPoint, Is.True);
            Assert.That(copy.Owner.ExtractionPointId, Is.EqualTo(8));
            Assert.That(sent.Match.ZoneRadius, Is.Zero);
            Assert.That(sent.Match.SquadsRemaining, Is.Zero);
            Assert.That(sent.Owner.RedeploysLeft, Is.Zero);
            Assert.That(sent.Owner.RedeploySeconds, Is.Null);
            Assert.That(sent.Owner.ExtractionProgress, Is.Zero);
            Assert.That(sent.Owner.HasExtractionPoint, Is.False);
            Assert.That(sent.Owner.ExtractionPointId, Is.Zero);
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
        public void ABeaconRoundTripsWithItsKind()
        {
            var sent = Build(100);
            AddDeployable(sent, 7, DeployableKind.Beacon, "deploy-beacon", Team.Alpha, 3, new Vector3(-2f, 0.5f, 4f), 180f, 150);

            var got = Send(sent, null);

            Assert.That(got.Deployables.Count, Is.EqualTo(1));
            Assert.That(got.Deployables[0].Kind, Is.EqualTo(DeployableKind.Beacon));
            Assert.That(got.Deployables[0].TypeId, Is.EqualTo("deploy-beacon"));
            Assert.That(got.Deployables[0].OwnerId, Is.EqualTo(3));
            Assert.That(got.Deployables[0].Yaw, Is.EqualTo(180f));
            Assert.That(got.Deployables[0].Health, Is.EqualTo(150));
            Assert.That(got.Deployables[0].ChangedBits(sent.Deployables[0]), Is.Zero);
        }

        [TestCase(DeployableKind.Mine)]
        [TestCase(DeployableKind.RemoteCharge)]
        [TestCase(DeployableKind.Drone)]
        public void TheAddedKindsRoundTrip(DeployableKind kind)
        {
            var sent = Build(100);
            AddDeployable(sent, 8, kind, "deploy-added", Team.Bravo, 4, new Vector3(1f, 0f, -2f), 45f, 60);

            var got = Send(sent, null);

            Assert.That(got.Deployables.Count, Is.EqualTo(1));
            Assert.That(got.Deployables[0].Kind, Is.EqualTo(kind));
            Assert.That(got.Deployables[0].ChangedBits(sent.Deployables[0]), Is.Zero);
        }

        [Test]
        public void TheDeployableKindsKeepTheirValues()
        {
            Assert.That((int)DeployableKind.Turret, Is.EqualTo(0));
            Assert.That((int)DeployableKind.Shield, Is.EqualTo(1));
            Assert.That((int)DeployableKind.Decoy, Is.EqualTo(2));
            Assert.That((int)DeployableKind.Beacon, Is.EqualTo(3));
            Assert.That((int)DeployableKind.Mine, Is.EqualTo(4));
            Assert.That((int)DeployableKind.RemoteCharge, Is.EqualTo(5));
            Assert.That((int)DeployableKind.Drone, Is.EqualTo(6));
            Assert.That(Enum.GetValues<DeployableKind>().Length, Is.EqualTo(7));
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

        [Test]
        public void TheExecutorAndTheExecuteProgressRoundTrip()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(7, 45);
            AddPlayer(sent, 2).Downed = new DownedSync(true, 9, 30);

            var got = Send(sent, null);

            Assert.That(got.Players[0].Downed.IsDowned, Is.True);
            Assert.That(got.Players[0].Downed.BleedoutSeconds, Is.EqualTo(12));
            Assert.That(got.Players[0].Downed.HasExecutor, Is.True);
            Assert.That(got.Players[0].Downed.ExecutorId, Is.EqualTo(7));
            Assert.That(got.Players[0].Downed.ExecutePercent, Is.EqualTo(45));
            Assert.That(got.Players[1].Downed.HasExecutor, Is.False);
            Assert.That(got.Players[1].Downed.ExecutePercent, Is.Zero);
            Assert.That(got.Players[1].Downed.RevivePercent, Is.EqualTo(30));
            Assert.That(got.Players[0].ChangedBits(sent.Players[0]), Is.Zero);
        }

        // 刺している人 (4) と進み具合 (1) は、刺されている間だけ載る
        [Test]
        public void TheExecutorIsWrittenOnlyWhileSomeoneIsExecuting()
        {
            var plain = Build(100);
            AddPlayer(plain, 1).Downed = new DownedSync(true, 12, 0);
            var executing = Build(100);
            AddPlayer(executing, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(3, 10);

            Assert.That(Size(executing, null) - Size(plain, null), Is.EqualTo(4 + 1));
        }

        [Test]
        public void AdvancingTheExecutionSendsOnlyTheDownedPartAndResolves()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(3, 10);
            var current = Build(101);
            AddPlayer(current, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(3, 60);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Downed));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Downed.ExecutorId, Is.EqualTo(3));
            Assert.That(got.Players[0].Downed.ExecutePercent, Is.EqualTo(60));
            Assert.That(got.Players[0].WeaponId, Is.EqualTo("rifle"));
        }

        [Test]
        public void StoppingTheExecutionIsSentAndClearsTheExecutor()
        {
            var baseline = Build(100);
            AddPlayer(baseline, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(3, 80);
            var current = Build(101);
            AddPlayer(current, 1).Downed = new DownedSync(true, 12, 0);

            var got = Send(current, baseline);

            Assert.That(got.Players[0].Present, Is.EqualTo(PlayerFields.Downed));
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Players[0].Downed.HasExecutor, Is.False);
            Assert.That(got.Players[0].Downed.ExecutePercent, Is.Zero);
        }

        [Test]
        public void AStandingPlayerHasNoExecutor()
        {
            var standing = new DownedSync(false, 0, 0).WithExecutor(3, 50);

            Assert.That(standing.HasExecutor, Is.False);
            Assert.That(standing.SameAs(DownedSync.None), Is.True);
        }

        [Test]
        public void AnExecutePercentPastFullIsClampedToFull()
        {
            Assert.That(new DownedSync(true, 5, 0).WithExecutor(3, 200).ExecutePercent, Is.EqualTo(DownedSync.FullPercent));
        }

        [Test]
        public void AnExecutorOnAStandingPlayerIsNotWritten()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).Downed = new DownedSync(false, 0, 0) { HasExecutor = true, ExecutorId = 3, ExecutePercent = 10 };

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnExecutePercentPastFullIsUnreadable()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(3, 77);
            var plain = Build(100);
            AddPlayer(plain, 1).Downed = new DownedSync(true, 12, 0).WithExecutor(3, 76);
            var bytes = Bytes(sent, null);
            var other = Bytes(plain, null);

            // 進み具合のバイトだけが違うので、1 違いの行と比べて場所を見つける
            var at = 0;
            while (bytes[at] == other[at])
            {
                at++;
            }

            Assert.That(bytes[at], Is.EqualTo(77));
            bytes[at] = DownedSync.FullPercent + 1;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void AnExecutorOnAStandingPlayerIsUnreadable()
        {
            var sent = Build(100);
            AddPlayer(sent, 1).Downed = new DownedSync(true, 0, 0).WithExecutor(3, 10);
            var standing = Build(100);
            AddPlayer(standing, 1).Downed = new DownedSync(false, 0, 0);
            var bytes = Bytes(sent, null);
            var other = Bytes(standing, null);

            // ダウンしているかのバイトが最初に違う
            var at = 0;
            while (bytes[at] == other[at])
            {
                at++;
            }

            Assert.That(bytes[at], Is.EqualTo(1));
            bytes[at] = 0;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
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

        private static void SetZone(
            SnapshotMessage snapshot, Vector2 center, float radius, Vector2 nextCenter, float nextRadius, int phase, int seconds, int squads)
        {
            ref var match = ref snapshot.Match;
            match.ZoneCenter = center;
            match.ZoneRadius = radius;
            match.NextZoneCenter = nextCenter;
            match.NextZoneRadius = nextRadius;
            match.ZonePhase = phase;
            match.ZonePhaseRemainingSeconds = seconds;
            match.SquadsRemaining = squads;
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
