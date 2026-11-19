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
            // Edit Mode では OnDestroy が呼ばれず、StreakTargeting が作った見下ろすカメラと印が残るので、ここで消す
            foreach (var go in _objects)
            {
                var targeting = go != null ? go.GetComponent<StreakTargeting>() : null;
                if (targeting == null)
                {
                    continue;
                }

                if (targeting.ViewCamera != null)
                {
                    Object.DestroyImmediate(targeting.ViewCamera.gameObject);
                }

                if (targeting.Marker != null)
                {
                    Object.DestroyImmediate(targeting.Marker);
                }
            }

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
        public void UsingTheAirstrikeStartsChoosingAndKeepsTheRewardUntilConfirmed()
        {
            var streak = Player(AirstrikeSpec(), side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            airstrike.Clock = () => _now;
            Kill(streak, Body("Killed", side: 1));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player targeting started reward=airstrike");
            Assert.That(streak.TryUse(), Is.True);

            Assert.That(streak.IsTargeting, Is.True);
            Assert.That(streak.TryUse(), Is.False, "a second press while choosing does not start again");
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike" }), "the slot waits for the choice");
            Assert.That(airstrike.PendingCount, Is.Zero);
            var shown = GameSignals.StreakTargeting.Value;
            Assert.That(shown.HasValue, Is.True);
            Assert.That(shown.Value.RewardId, Is.EqualTo("airstrike"));
            Assert.That(shown.Value.DisplayNameKey, Is.EqualTo("streak.airstrike"));
            Assert.That(shown.Value.Target, Is.EqualTo(new System.Numerics.Vector3(0f, 1f, 0f)), "the aim starts under the user");
            Assert.That(shown.Value.Range, Is.EqualTo(30f));
            Assert.That(shown.Value.LineLength, Is.EqualTo(10f));

            var view = streak.GetComponent<StreakTargeting>().ViewCamera;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.orthographic, Is.True);
            Assert.That(view.orthographicSize, Is.EqualTo(30f * 1.15f).Within(1e-4f));
            Assert.That(view.transform.position, Is.EqualTo(new Vector3(0f, 1f + StreakTargetingSpec.DefaultViewHeight, 0f)));
            Assert.That(Vector3.Angle(view.transform.forward, Vector3.down), Is.LessThan(0.01f), "the view looks straight down");
            Assert.That(Vector3.Angle(view.transform.up, Vector3.forward), Is.LessThan(0.01f), "north is up on the map");
        }

        [Test]
        public void ConfirmingTheChoiceDropsTheAirstrikeAlongTheChosenLine()
        {
            var streak = Player(AirstrikeSpec(), side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            airstrike.Clock = () => _now;
            streak.Random = new System.Random(7);
            Kill(streak, Body("Killed", side: 1));
            streak.TryUse();
            var targeting = streak.GetComponent<StreakTargeting>();

            targeting.Step(new StreakTargetingInput(new Vector2(0f, 1f), 90f, false, false), 0.5f);
            Assert.That(GameSignals.StreakTargeting.Value.Value.Target, Is.EqualTo(new System.Numerics.Vector3(0f, 1f, 20f)), "40 m/s north for half a second");
            Assert.That(GameSignals.StreakTargeting.Value.Value.HeadingDegrees, Is.EqualTo(90f));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player targeting confirmed reward=airstrike at=(0.0, 1.0, 20.0) heading=90.0");
            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=airstrike kills=1 ready=0");
            LogAssert.Expect(LogType.Log, "[Streak] airstrike called user=Player at=(0.0, 1.0, 20.0) heading=90.0 strikes=3 interval=1.00 line=10.0");
            targeting.Step(new StreakTargetingInput(Vector2.zero, 0f, true, false), 0f);

            Assert.That(streak.IsTargeting, Is.False);
            Assert.That(airstrike.PendingCount, Is.EqualTo(3));
            Assert.That(GameSignals.Streak.Value.Value.ReadyCount, Is.Zero);
            Assert.That(GameSignals.StreakTargeting.HasValue, Is.False);
            Assert.That(targeting.ViewCamera.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CancellingTheChoiceKeepsTheReward()
        {
            var streak = Player(AirstrikeSpec(), side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            Kill(streak, Body("Killed", side: 1));
            streak.TryUse();

            LogAssert.Expect(LogType.Log, "[Streak] id=Player targeting cancelled reward=airstrike reason=input");
            streak.GetComponent<StreakTargeting>().Step(new StreakTargetingInput(new Vector2(1f, 0f), 0f, true, true), 1f);

            Assert.That(streak.IsTargeting, Is.False);
            Assert.That(airstrike.PendingCount, Is.Zero, "a cancel in the same frame as a confirm wins");
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike" }));
            Assert.That(GameSignals.StreakTargeting.HasValue, Is.False);
        }

        [Test]
        public void DyingWhileChoosingClosesTheMapAndKeepsTheReward()
        {
            var streak = Player(AirstrikeSpec(), side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            Kill(streak, Body("Killed", side: 1));
            streak.TryUse();

            LogAssert.Expect(LogType.Log, "[Streak] id=Player targeting cancelled reward=airstrike reason=died");
            streak.GetComponent<Damageable>().Kill();

            Assert.That(streak.IsTargeting, Is.False);
            Assert.That(airstrike.PendingCount, Is.Zero);
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike" }));
        }

        [Test]
        public void ChoosingTakesTheMovementWeaponAndLookAwayUntilItEnds()
        {
            var streak = Player(AirstrikeSpec(), side: 0);
            var handoff = new ControlHandoff();
            var look = streak.gameObject.AddComponent<FirstPersonLook>();
            var targeting = streak.gameObject.AddComponent<StreakTargeting>();
            var aim = new StreakAim("airstrike", "streak.airstrike", System.Numerics.Vector3.Zero, 0f, 30f, 10f);

            Assert.That(targeting.Begin(aim, StreakTargetingSpec.Default, new[] { handoff }, look), Is.True);

            Assert.That(handoff.IsHeldBy(targeting), Is.True);
            Assert.That(look.InputSuspended, Is.True);
            Assert.That(targeting.Begin(aim, StreakTargetingSpec.Default, new[] { handoff }, look), Is.False, "already choosing");

            targeting.Cancel("test");

            Assert.That(handoff.IsHandedOff, Is.False);
            Assert.That(look.InputSuspended, Is.False);
        }

        [Test]
        public void AConfirmedChoiceForARewardNoLongerNextIsNotUsed()
        {
            var streak = Player(AirstrikeSpec(), side: 0);
            var airstrike = streak.gameObject.AddComponent<AirstrikeReward>();
            Kill(streak, Body("Killed", side: 1));
            var stale = new StreakTargetingStatus("uav", "streak.uav", System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 0f, 30f, 10f);

            LogAssert.Expect(LogType.Log, "[Streak] id=Player use refused nothing-ready");
            Assert.That(streak.UseAt(stale), Is.False);

            Assert.That(airstrike.PendingCount, Is.Zero);
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike" }));
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

            Assert.That(streak.TryUse(), Is.True);
            Assert.That(streak.IsTargeting, Is.True, "the airstrike comes first, so the map opens for it");

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=airstrike kills=2 ready=1");
            streak.GetComponent<StreakTargeting>().Confirm();
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "uav" }));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=uav kills=2 ready=0");
            Assert.That(streak.TryUse(), Is.True);
            Assert.That(streak.ReadyRewardIds, Is.Empty);
        }

        [Test]
        public void SwitchingChoosesWhichHeldRewardTheUseKeyUses()
        {
            var spec = new StreakSpec(new[]
            {
                new StreakReward("airstrike", "streak.airstrike", 1),
                new StreakReward("uav", "streak.uav", 2),
            });
            var streak = Player(spec, side: 0);
            Pings(streak);
            Kill(streak, Body("A", side: 1));
            Assert.That(streak.SelectNext(), Is.False, "one reward has nothing to switch to");
            Kill(streak, Body("B", side: 1));
            Assert.That(streak.SelectedRewardId, Is.EqualTo("airstrike"));

            LogAssert.Expect(LogType.Log, "[Streak] id=Player selected reward=uav kills=2 ready=2");
            Assert.That(streak.SelectNext(), Is.True);
            Assert.That(GameSignals.Streak.Value.Value.SelectedRewardId, Is.EqualTo("uav"), "the HUD hears the new selection");

            LogAssert.Expect(LogType.Log, "[Streak] id=Player used reward=uav kills=2 ready=1");
            Assert.That(streak.TryUse(), Is.True);
            Assert.That(streak.IsTargeting, Is.False, "the UAV was chosen, so the map does not open for the airstrike");
            Assert.That(streak.ReadyRewardIds, Is.EqualTo(new[] { "airstrike" }));
            Assert.That(streak.SelectedRewardId, Is.EqualTo("airstrike"));
        }

        [Test]
        public void TheSelectionDoesNotChangeWhileChoosingASpot()
        {
            var spec = new StreakSpec(new[]
            {
                new StreakReward("airstrike", "streak.airstrike", 1),
                new StreakReward("uav", "streak.uav", 2),
            });
            var streak = Player(spec, side: 0);
            streak.gameObject.AddComponent<AirstrikeReward>().Clock = () => _now;
            Kill(streak, Body("A", side: 1));
            Kill(streak, Body("B", side: 1));
            Assert.That(streak.TryUse(), Is.True);
            Assert.That(streak.IsTargeting, Is.True);

            Assert.That(streak.SelectNext(), Is.False);
            Assert.That(streak.SelectedRewardId, Is.EqualTo("airstrike"));
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

        [Test]
        public void ApplyAttachesTheStreaksOnlyWhenTheRulesTurnThemOn()
        {
            var off = Track(new GameObject("Off"));
            Assert.That(StreakController.Apply(off, GameRules.Default.WithStreaks(Streaks.Off)), Is.Null);
            Assert.That(off.GetComponent<StreakController>(), Is.Null, "nothing is attached for rules without streaks");

            var on = Track(new GameObject("On"));
            var attached = StreakController.Apply(on, GameRules.Default);
            Assert.That(attached, Is.Not.Null);
            Assert.That(attached.enabled, Is.True);
            Assert.That(StreakController.Apply(on, GameRules.Default), Is.SameAs(attached), "applying again reuses the attached one");
            Assert.That(on.GetComponents<StreakController>(), Has.Length.EqualTo(1));

            Assert.Throws<ArgumentNullException>(() => StreakController.Apply(null, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => StreakController.Apply(on, null));
        }

        [Test]
        public void ApplyingRulesWithoutStreaksStopsCountingUntilTurnedOnAgain()
        {
            var streak = Player(StreakSpec.Default, side: 0);
            Kill(streak, Body("A", side: 1));

            Assert.That(StreakController.Apply(streak.gameObject, GameRules.Default.WithStreaks(Streaks.Off)), Is.Null);

            Assert.That(streak.enabled, Is.False);
            Assert.That(GameSignals.Streak.Value, Is.Null, "a stopped streak takes back what it published");
            Kill(streak, Body("B", side: 1));
            Assert.That(streak.Status.Value.Kills, Is.EqualTo(1), "kills while stopped are not counted");

            Assert.That(StreakController.Apply(streak.gameObject, GameRules.Default), Is.SameAs(streak));

            Assert.That(streak.enabled, Is.True);
            Assert.That(GameSignals.Streak.Value.Value.Kills, Is.EqualTo(1), "turning it on again keeps the count");
            Kill(streak, Body("C", side: 1));
            Assert.That(streak.Status.Value.Kills, Is.EqualTo(2));
        }

        private static StreakSpec AirstrikeSpec()
        {
            return new StreakSpec(
                new[] { new StreakReward("airstrike", "streak.airstrike", 1) },
                airstrike: new StreakAirstrikeSpec(shellCount: 3, intervalSeconds: 1f, spread: 0f, range: 30f, lineLength: 10f, dropHeight: 5f));
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
