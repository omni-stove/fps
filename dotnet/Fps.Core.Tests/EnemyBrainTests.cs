using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class EnemyBrainTests
    {
        // 視認 20m、攻撃 10m、見失って 1 秒で Search、Search 2 秒、反応 0.5 秒。
        private static EnemySpec DefaultSpec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f);

        private static EnemyPerception Sees(Vector3 self, Vector3 target) =>
            new EnemyPerception(self, target, true, false, null);

        private static EnemyPerception Nothing(Vector3 self) =>
            new EnemyPerception(self, null, false, false, null);

        private static EnemyPerception Hears(Vector3 self, Vector3 noise) =>
            new EnemyPerception(self, null, false, true, noise);

        private static EnemyBrain PatrollingBrain(out List<EnemyState> changes)
        {
            var brain = new EnemyBrain(DefaultSpec());
            brain.SetPatrolRoute(new[] { new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f) });
            var log = new List<EnemyState>();
            brain.StateChanged += (_, next) => log.Add(next);
            changes = log;
            return brain;
        }

        [Test]
        public void StartsIdleWithoutRoute()
        {
            var brain = new EnemyBrain(DefaultSpec());

            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(decision.MoveTo, Is.Null);
            Assert.That(decision.LookAt, Is.Null);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void SettingRouteStartsPatrol()
        {
            var brain = new EnemyBrain(DefaultSpec());
            var changes = new List<(EnemyState, EnemyState)>();
            brain.StateChanged += (previous, next) => changes.Add((previous, next));

            brain.SetPatrolRoute(new[] { new Vector3(0f, 0f, 10f) });
            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(changes, Is.EqualTo(new[] { (EnemyState.Idle, EnemyState.Patrol) }));
        }

        [Test]
        public void ClearingRouteDuringPatrolGoesIdle()
        {
            var brain = PatrollingBrain(out var changes);

            brain.SetPatrolRoute(Array.Empty<Vector3>());

            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.Idle }));
        }

        [Test]
        public void PatrolAdvancesWithinArrivalDistanceAndWrapsAround()
        {
            var a = new Vector3(0f, 0f, 0f);
            var b = new Vector3(10f, 0f, 0f);
            var c = new Vector3(10f, 0f, 10f);
            var brain = new EnemyBrain(DefaultSpec());
            brain.SetPatrolRoute(new[] { a, b, c });

            Assert.That(brain.Tick(0.1f, Nothing(new Vector3(5f, 0f, 0f))).MoveTo, Is.EqualTo(a));
            Assert.That(brain.Tick(0.1f, Nothing(new Vector3(0.4f, 0f, 0f))).MoveTo, Is.EqualTo(b));
            Assert.That(brain.Tick(0.1f, Nothing(new Vector3(9.6f, 0f, 0f))).MoveTo, Is.EqualTo(c));
            Assert.That(brain.Tick(0.1f, Nothing(new Vector3(10f, 0f, 9.5f))).MoveTo, Is.EqualTo(a));
            Assert.That(brain.PatrolIndex, Is.Zero);
        }

        [Test]
        public void PatrolDoesNotAdvanceOutsideArrivalDistance()
        {
            var a = new Vector3(0f, 0f, 0f);
            var brain = new EnemyBrain(DefaultSpec());
            brain.SetPatrolRoute(new[] { a, new Vector3(10f, 0f, 0f) });

            var decision = brain.Tick(0.1f, Nothing(new Vector3(0.6f, 0f, 0f)));

            Assert.That(decision.MoveTo, Is.EqualTo(a));
        }

        [Test]
        public void SetPatrolRouteCopiesTheList()
        {
            var route = new List<Vector3> { new Vector3(0f, 0f, 10f) };
            var brain = new EnemyBrain(DefaultSpec());
            brain.SetPatrolRoute(route);

            route[0] = new Vector3(99f, 0f, 99f);

            Assert.That(brain.Tick(0.1f, Nothing(Vector3.Zero)).MoveTo, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void SeeingTargetInSightRangeChases()
        {
            var brain = PatrollingBrain(out var changes);
            var target = new Vector3(0f, 0f, 15f);

            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, target));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.Chase }));
        }

        [Test]
        public void TargetOutsideSightRangeIsIgnored()
        {
            var brain = PatrollingBrain(out _);

            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 25f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
        }

        [Test]
        public void TargetWithoutLineOfSightIsIgnored()
        {
            var brain = PatrollingBrain(out _);

            brain.Tick(0.1f, new EnemyPerception(Vector3.Zero, new Vector3(0f, 0f, 5f), false, false, null));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
        }

        [Test]
        public void TargetInAttackRangeAttacksInPlace()
        {
            var brain = PatrollingBrain(out _);
            var target = new Vector3(0f, 0f, 5f);

            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, target));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.MoveTo, Is.Null);
            Assert.That(decision.LookAt, Is.EqualTo(target));
        }

        [Test]
        public void ChaseTurnsIntoAttackWhenCloseEnough()
        {
            var brain = PatrollingBrain(out var changes);

            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            brain.Tick(0.1f, Sees(new Vector3(0f, 0f, 6f), new Vector3(0f, 0f, 15f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.Chase, EnemyState.Attack }));
        }

        [Test]
        public void AttackTurnsIntoChaseWhenTargetMovesAway()
        {
            var brain = PatrollingBrain(out _);

            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 15f)));
        }

        [Test]
        public void DoesNotFireBeforeReactionSeconds()
        {
            var brain = PatrollingBrain(out _);
            var target = new Vector3(0f, 0f, 5f);

            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, target)).Fire, Is.False, "発見した tick");
            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, target)).Fire, Is.False, "0.25 秒後");
            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, target)).Fire, Is.True, "0.5 秒後");
            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, target)).Fire, Is.True, "以後は撃ち続ける");
        }

        [Test]
        public void ReactionTimeCountsFromDiscoveryDuringChase()
        {
            var brain = PatrollingBrain(out _);

            brain.Tick(0.25f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            brain.Tick(0.25f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            var decision = brain.Tick(0.25f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.True);
        }

        [Test]
        public void ZeroReactionFiresOnDiscovery()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0f));

            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));

            Assert.That(decision.Fire, Is.True);
        }

        [Test]
        public void LosingSightGoesToLastSeenThenSearchesThenPatrols()
        {
            var brain = PatrollingBrain(out var changes);
            var lastSeen = new Vector3(0f, 0f, 5f);
            brain.Tick(0.5f, Sees(Vector3.Zero, lastSeen));

            var lost = brain.Tick(0.5f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(lost.MoveTo, Is.EqualTo(lastSeen));
            Assert.That(lost.Fire, Is.False);

            var search = brain.Tick(0.5f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search), "LoseSightSeconds 経過");
            Assert.That(search.MoveTo, Is.EqualTo(lastSeen));

            for (var i = 0; i < 3; i++)
            {
                brain.Tick(0.5f, Nothing(Vector3.Zero));
                Assert.That(brain.State, Is.EqualTo(EnemyState.Search), "SearchSeconds 未満");
            }

            var patrol = brain.Tick(0.5f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol), "SearchSeconds 経過");
            Assert.That(patrol.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(changes, Is.EqualTo(new[]
            {
                EnemyState.Attack, EnemyState.Chase, EnemyState.Search, EnemyState.Patrol,
            }));
        }

        [Test]
        public void SearchReturnsToIdleWithoutRoute()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 0f, 0f, 0f));
            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));

            brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));

            brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void SeeingTargetAgainDuringSearchChasesAndResetsReaction()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 0f, 5f, 0.5f));
            brain.Tick(1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            brain.Tick(1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));

            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void HearingNoiseWhilePatrollingSearchesThere()
        {
            var brain = PatrollingBrain(out var changes);
            var noise = new Vector3(-5f, 0f, 3f);

            var decision = brain.Tick(0.1f, Hears(Vector3.Zero, noise));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));
            Assert.That(decision.MoveTo, Is.EqualTo(noise));
            Assert.That(decision.Fire, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.Search }));
        }

        [Test]
        public void HearingNoiseWhileIdleSearchesThere()
        {
            var brain = new EnemyBrain(DefaultSpec());
            var noise = new Vector3(3f, 0f, 3f);

            var decision = brain.Tick(0.1f, Hears(Vector3.Zero, noise));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));
            Assert.That(decision.MoveTo, Is.EqualTo(noise));
        }

        [Test]
        public void NewNoiseDuringSearchMovesTheSearchAndRestartsTimer()
        {
            var brain = PatrollingBrain(out _);
            brain.Tick(0.5f, Hears(Vector3.Zero, new Vector3(1f, 0f, 0f)));
            brain.Tick(1.5f, Nothing(Vector3.Zero));

            var decision = brain.Tick(0.5f, Hears(Vector3.Zero, new Vector3(2f, 0f, 0f)));
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(2f, 0f, 0f)));

            brain.Tick(1.5f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search), "タイマーがやり直されている");
        }

        [Test]
        public void NoiseWithoutPositionIsIgnored()
        {
            var brain = PatrollingBrain(out _);

            brain.Tick(0.1f, new EnemyPerception(Vector3.Zero, null, false, true, null));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
        }

        [Test]
        public void NoiseDoesNotInterruptChase()
        {
            var brain = PatrollingBrain(out _);
            var target = new Vector3(0f, 0f, 15f);
            brain.Tick(0.1f, Sees(Vector3.Zero, target));

            var decision = brain.Tick(0.1f, Hears(Vector3.Zero, new Vector3(-5f, 0f, 0f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(decision.MoveTo, Is.EqualTo(target));
        }

        [Test]
        public void DeadDecidesNothing()
        {
            var brain = PatrollingBrain(out var changes);
            brain.Kill();

            var seen = brain.Tick(1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            var heard = brain.Tick(1f, Hears(Vector3.Zero, new Vector3(1f, 0f, 0f)));
            brain.SetPatrolRoute(new[] { new Vector3(1f, 0f, 1f) });
            var patrol = brain.Tick(1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(brain.IsDead, Is.True);
            foreach (var decision in new[] { seen, heard, patrol })
            {
                Assert.That(decision.MoveTo, Is.Null);
                Assert.That(decision.LookAt, Is.Null);
                Assert.That(decision.Fire, Is.False);
            }

            Assert.That(changes, Is.EqualTo(new[] { EnemyState.Dead }));
        }

        [Test]
        public void KillFiresStateChangedOnce()
        {
            var brain = PatrollingBrain(out var changes);

            brain.Kill();
            brain.Kill();

            Assert.That(changes, Is.EqualTo(new[] { EnemyState.Dead }));
        }

        [Test]
        public void KilledFromStateChangedHandlerDoesNotFire()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0f));
            brain.StateChanged += (_, next) =>
            {
                if (next == EnemyState.Attack)
                {
                    brain.Kill();
                }
            };

            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(decision.Fire, Is.False);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDelta(float delta)
        {
            var brain = new EnemyBrain(DefaultSpec());

            Assert.Throws<ArgumentOutOfRangeException>(() => brain.Tick(delta, Nothing(Vector3.Zero)));
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new EnemyBrain(null));
            Assert.Throws<ArgumentNullException>(() => new EnemyBrain(DefaultSpec()).SetPatrolRoute(null));
        }

        [TestCase(0f, 1f, 0f, 0f, 0f)]
        [TestCase(-1f, 1f, 0f, 0f, 0f)]
        [TestCase(float.NaN, 1f, 0f, 0f, 0f)]
        [TestCase(10f, 0f, 0f, 0f, 0f)]
        [TestCase(10f, 11f, 0f, 0f, 0f)]
        [TestCase(10f, 5f, -1f, 0f, 0f)]
        [TestCase(10f, 5f, 0f, -1f, 0f)]
        [TestCase(10f, 5f, 0f, 0f, -1f)]
        [TestCase(10f, 5f, 0f, 0f, float.NaN)]
        public void SpecRejectsInvalidValues(float sight, float attack, float loseSight, float search, float reaction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemySpec(sight, attack, loseSight, search, reaction));
        }
    }
}
