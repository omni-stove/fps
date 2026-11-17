using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// StreakController が自分の倒した数だけを数え、倒れたら 0 に戻し、使うと報酬 (UAV・空爆) を呼んで GameSignals.Streak を出し直すことを確かめる。
    /// </summary>
    public sealed class StreakControllerTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void ConfiguringLogsTheRewardsAndPublishesAnEmptyStreak()
        {
            LogAssert.Expect(LogType.Log, "[Streak] id=Player rewards=uav@3,airstrike@5");
            var streak = Player(StreakSpec.Default, side: 0);

            Assert.That(streak.IsConfigured, Is.True);
            Assert.That(GameSignals.Streak.Value.HasValue, Is.True);
            Assert.That(GameSignals.Streak.Value.Value.Kills, Is.Zero);
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.Zero);
            Assert.That(GameSignals.Streak.Value.Value.Slots.Select(s => s.RewardId), Is.EqualTo(new[] { "uav", "airstrike" }));
            Assert.Throws<ArgumentNullException>(() => streak.Configure(null));
        }

        [Test]
        public void CountsOnlyItsOwnKillsOfNonAllies()
        {
            var streak = Player(StreakSpec.Default, side: 0);
            var self = CombatEvents.OfflineId(streak.gameObject);
            var enemy = Body("Enemy", side: 1);
            var ally = Body("Ally", side: 0);
            var unaligned = Body("Dummy", side: null);
            var other = Body("Other", side: 1);

            CombatEvents.RaiseKilled(new KillFeedEntry(CombatEvents.OfflineId(other.gameObject), Id(enemy), "rifle", false, false));
            CombatEvents.RaiseKilled(new KillFeedEntry(self, self, string.Empty, false, false));
            CombatEvents.RaiseKilled(new KillFeedEntry(self, Id(enemy), "rifle", false, friendly: true));
            CombatEvents.RaiseKilled(new KillFeedEntry(self, Id(ally), "rifle", false, false));
            Assert.That(streak.Status.Value.Kills, Is.Zero, "another killer, a suicide and team kills are not counted");

            LogAssert.Expect(LogType.Log, "[Streak] id=Player kill kills=1 ready=0");
            CombatEvents.RaiseKilled(new KillFeedEntry(self, Id(enemy), "rifle", false, false));
            CombatEvents.RaiseKilled(new KillFeedEntry(self, Id(unaligned), "rifle", false, false));

            Assert.That(streak.Status.Value.Kills, Is.EqualTo(2), "a body without a side is not an ally");
            Assert.That(GameSignals.Streak.Value.Value.Kills, Is.EqualTo(2));
        }

        [Test]
        public void AKillAttributedToThePlayerIsCountedAndEarnsTheReward()
        {
            var streak = Player(StreakSpec.Default, side: 0);

            Kill(streak, Body("A", side: 1));
            Kill(streak, Body("B", side: 1));
            LogAssert.Expect(LogType.Log, "[Streak] id=Player kill kills=3 ready=1");
            LogAssert.Expect(LogType.Log, "[Streak] id=Player earned reward=uav kills=3 ready=1");
            Kill(streak, Body("C", side: 1));

            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "uav" }));
            Assert.That(GameSignals.Streak.Value.Value.Kills, Is.EqualTo(3));
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1));
            Assert.That(GameSignals.Streak.Value.Value.Slots[0].Ready, Is.True);
        }

        [Test]
        public void DyingResetsTheCountButKeepsEarnedRewards()
        {
            var streak = Player(StreakSpec.Default, side: 0);
            for (var i = 0; i < 4; i++)
            {
                Kill(streak, Body("Enemy" + i, side: 1));
            }

            LogAssert.Expect(LogType.Log, "[Streak] id=Player died lost=4 kills=0 ready=1");
            streak.GetComponent<Damageable>().Kill();

            Assert.That(streak.Status.Value.Kills, Is.Zero);
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "uav" }));
            Assert.That(GameSignals.Streak.Value.Value.Kills, Is.Zero);
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1));
        }

        [Test]
        public void KillsLandingWhileDeadAreNotCounted()
        {
            var streak = Player(StreakSpec.Default, side: 0);
            var enemy = Body("Enemy", side: 1);
            streak.GetComponent<Damageable>().Kill();

            Assert.That(
                streak.HandleKilled(new KillFeedEntry(CombatEvents.OfflineId(streak.gameObject), Id(enemy), "airstrike", false, false)),
                Is.False);
            Assert.That(streak.Status.Value.Kills, Is.Zero);
        }

        [Test]
        public void UsingTheUavMarksEnemiesAndEmptiesTheSlot()
        {
            var spec = new StreakSpec(new[] { new StreakReward("uav", "streak.uav", 1) }, new StreakUavSpec(durationSeconds: 7f));
            var streak = Player(spec, side: 0);
            var pings = Pings(streak);
            Kill(streak, Body("Killed", side: 1));
            var enemy = Body("Enemy", side: 1);
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=uav kills=1 ready=0");
            LogAssert.Expect(LogType.Log, "[Streak] uav user=Player marked=1 seconds=7.00");
            Assert.That(streak.TryUse(), Is.True);

            Assert.That(pings.IsMarked(enemy), Is.True);
            var marker = pings.Markers.Single();
            Assert.That(marker.ExpiresAt - marker.PlacedAt, Is.EqualTo(7f), "the configured UAV seconds");
            Assert.That(streak.ReadyRewardIds, Is.Empty);
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.Zero);
            Assert.That(GameSignals.Streak.Value.Value.Kills, Is.EqualTo(1), "using a reward keeps the count");
        }

        [Test]
        public void UsingTheAirstrikeCallsTheConfiguredStrikes()
        {
            var spec = new StreakSpec(
                new[] { new StreakReward("airstrike", "streak.airstrike", 1) },
                airstrike: new StreakAirstrikeSpec(shellCount: 3, intervalSeconds: 1f, spread: 2f, range: 30f));
            var streak = Player(spec, side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            airstrike.Clock = () => _now;
            streak.Random = new System.Random(7);
            Kill(streak, Body("Killed", side: 1));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=airstrike kills=1 ready=0");
            LogAssert.Expect(LogType.Log, "[Streak] airstrike called user=Player at=(0.0, 1.0, 30.0) strikes=3 interval=1.00 spread=2.0");
            Assert.That(streak.TryUse(), Is.True);

            Assert.That(airstrike.PendingCount, Is.EqualTo(3));
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.Zero);
        }

        [Test]
        public void TheFirstEarnedRewardIsUsedFirst()
        {
            var spec = new StreakSpec(new[]
            {
                new StreakReward("airstrike", "streak.airstrike", 1),
                new StreakReward("uav", "streak.uav", 2),
            });
            var streak = Player(spec, side: 0);
            Pings(streak);
            streak.gameObject.AddComponent<AirstrikeReward>().Clock = () => _now;
            Kill(streak, Body("A", side: 1));
            Kill(streak, Body("B", side: 1));
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike", "uav" }));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=airstrike kills=2 ready=1");
            Assert.That(streak.TryUse(), Is.True);
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "uav" }));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=uav kills=2 ready=0");
            Assert.That(streak.TryUse(), Is.True);
            Assert.That(streak.ReadyRewardIds, Is.Empty);
        }

        [Test]
        public void UsingWithNothingReadyIsRefused()
        {
            var streak = Player(StreakSpec.Default, side: 0);

            LogAssert.Expect(LogType.Log, "[Streak] id=Player use refused nothing-ready");
            Assert.That(streak.TryUse(), Is.False);
        }

        [Test]
        public void TheUavWithoutAPingControllerKeepsTheReward()
        {
            var spec = new StreakSpec(new[] { new StreakReward("uav", "streak.uav", 1) });
            var streak = Player(spec, side: 0);
            Kill(streak, Body("Killed", side: 1));

            LogAssert.Expect(LogType.Warning, "[Streak] id=Player use refused reward=uav missing=PingController");
            Assert.That(streak.TryUse(), Is.False);

            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "uav" }));
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.EqualTo(1));
        }

        [Test]
        public void UsingWhileDeadIsRefusedAndKeepsTheReward()
        {
            var spec = new StreakSpec(new[] { new StreakReward("airstrike", "streak.airstrike", 1) });
            var streak = Player(spec, side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            Kill(streak, Body("Killed", side: 1));
            streak.GetComponent<Damageable>().Kill();

            LogAssert.Expect(LogType.Log, "[Streak] id=Player use refused dead");
            Assert.That(streak.TryUse(), Is.False);

            Assert.That(airstrike.PendingCount, Is.Zero);
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike" }));
        }

        [Test]
        public void ReconfiguringStartsOverAndDoesNotCountTwice()
        {
            var streak = Player(StreakSpec.Default, side: 0);
            Kill(streak, Body("A", side: 1));
            streak.Configure(StreakSpec.Default);

            Assert.That(streak.Status.Value.Kills, Is.Zero);
            Kill(streak, Body("B", side: 1));
            Assert.That(streak.Status.Value.Kills, Is.EqualTo(1), "one subscription after configuring again");
        }

        private StreakController Player(StreakSpec spec, int? side)
        {
            var go = Track(new GameObject("Player"));
            go.transform.position = new Vector3(0f, 1f, 0f);
            go.AddComponent<Damageable>().Initialize("Player", Damageable.DefaultMaxHealth);
            if (side.HasValue)
            {
                go.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            var streak = go.AddComponent<StreakController>();
            streak.Configure(spec);
            return streak;
        }

        private PingController Pings(StreakController streak)
        {
            var pings = streak.gameObject.AddComponent<PingController>();
            pings.Clock = () => _now;
            pings.Configure(GameRules.Default.WithActions(GameRules.Default.Actions.WithPing(Shooter.Core.Ping.On)));
            return pings;
        }

        private Damageable Body(string name, int? side)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = new Vector3(_objects.Count * 3f, 1f, 10f);
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            if (side.HasValue)
            {
                go.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            Physics.SyncTransforms();
            return damageable;
        }

        private static void Kill(StreakController killer, Damageable victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Kill();
            }
        }

        private static int Id(Damageable damageable)
        {
            return CombatEvents.OfflineId(damageable.gameObject);
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
