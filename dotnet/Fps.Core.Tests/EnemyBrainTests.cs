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

        private static readonly float[] _expectedOpeningSpreads = { 6f, 4f, 2f, 0f, 0f };

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
        public void RestartSendsALivingBrainBackToTheStartOfItsRoute()
        {
            var brain = PatrollingBrain(out _);
            brain.Tick(0.1f, Nothing(Vector3.Zero));
            brain.Tick(1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            brain.Tick(1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            Assert.That(brain.State, Is.Not.EqualTo(EnemyState.Patrol), "seeing a target leaves the patrol");

            brain.Restart();
            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(brain.ShotsInAttack, Is.Zero);
        }

        [Test]
        public void RestartRevivesADeadBrainLikeRevive()
        {
            var brain = PatrollingBrain(out _);
            brain.Kill();

            brain.Restart();

            Assert.That(brain.IsDead, Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
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
        public void EnteringAttackFromChaseDoesNotFireAtOnce()
        {
            var brain = PatrollingBrain(out _);

            brain.Tick(0.5f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            brain.Tick(0.5f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            var decision = brain.Tick(0.25f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.False, "Chase の時間は反応時間に数えない");
        }

        [Test]
        public void FiresReactionSecondsAfterEnteringAttackFromChase()
        {
            var brain = PatrollingBrain(out _);
            var far = new Vector3(0f, 0f, 15f);
            var near = new Vector3(0f, 0f, 5f);
            brain.Tick(1f, Sees(Vector3.Zero, far));

            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, near)).Fire, Is.False, "Attack に入った tick");
            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, near)).Fire, Is.False, "0.25 秒後");
            Assert.That(brain.Tick(0.25f, Sees(Vector3.Zero, near)).Fire, Is.True, "0.5 秒後");
        }

        [Test]
        public void LeavingAttackRangeRestartsReaction()
        {
            var brain = PatrollingBrain(out _);
            var far = new Vector3(0f, 0f, 15f);
            var near = new Vector3(0f, 0f, 5f);
            brain.Tick(0.1f, Sees(Vector3.Zero, near));
            Assert.That(brain.Tick(1f, Sees(Vector3.Zero, near)).Fire, Is.True);

            brain.Tick(0.1f, Sees(Vector3.Zero, far));
            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, near));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void SpreadNarrowsOverTheOpeningShotsAndResetsOnEnteringAttack()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0f, 6f, 0f, 3));
            var near = new Vector3(0f, 0f, 5f);
            brain.Tick(0.1f, Sees(Vector3.Zero, near));

            var spreads = new List<float>();
            for (var i = 0; i < 5; i++)
            {
                spreads.Add(brain.NextShotSpreadDegrees);
                brain.RecordShot();
            }

            Assert.That(spreads, Is.EqualTo(_expectedOpeningSpreads).Within(1e-4f));
            Assert.That(brain.ShotsInAttack, Is.EqualTo(5));

            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            brain.Tick(0.1f, Sees(Vector3.Zero, near));

            Assert.That(brain.ShotsInAttack, Is.Zero);
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(6f));
        }

        [Test]
        public void SpreadStaysSettledWhenThereAreNoOpeningShots()
        {
            var spec = new EnemySpec(20f, 10f, 1f, 2f, 0f, 6f, 1f, 0);

            Assert.That(spec.SpreadDegreesForShot(0), Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.SpreadDegreesForShot(-1));
        }

        [TestCase(-1f, 0f, 0)]
        [TestCase(float.NaN, 0f, 0)]
        [TestCase(91f, 0f, 0)]
        [TestCase(0f, -1f, 0)]
        [TestCase(0f, 91f, 0)]
        [TestCase(0f, 0f, -1)]
        public void SpecRejectsInvalidSpread(float opening, float settled, int shots)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemySpec(20f, 10f, 1f, 2f, 0f, opening, settled, shots));
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
        public void AbandoningDuringPatrolSkipsToTheNextPoint()
        {
            var brain = PatrollingBrain(out _);
            brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.AbandonGoal(), Is.True);

            Assert.That(brain.Tick(0.1f, Nothing(Vector3.Zero)).MoveTo, Is.EqualTo(new Vector3(10f, 0f, 10f)));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
        }

        [Test]
        public void AbandoningTheLastPatrolPointWrapsAround()
        {
            var brain = PatrollingBrain(out _);
            brain.AbandonGoal();
            brain.AbandonGoal();

            Assert.That(brain.Tick(0.1f, Nothing(Vector3.Zero)).MoveTo, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void AbandoningSearchReturnsToPatrolOnTheNextTick()
        {
            var brain = PatrollingBrain(out _);
            brain.Tick(0.1f, Hears(Vector3.Zero, new Vector3(3f, 0f, 0f)));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));

            Assert.That(brain.AbandonGoal(), Is.True);
            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(decision.MoveTo, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void AbandoningChaseAfterLosingSightReturnsToPatrol()
        {
            var brain = PatrollingBrain(out _);
            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));

            Assert.That(brain.AbandonGoal(), Is.True);
            brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
        }

        [Test]
        public void AbandoningChaseStillChasesATargetThatIsSeen()
        {
            var brain = PatrollingBrain(out _);
            var target = new Vector3(0f, 0f, 15f);
            brain.Tick(0.1f, Sees(Vector3.Zero, target));

            brain.AbandonGoal();
            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, target));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(decision.MoveTo, Is.EqualTo(target));
        }

        [Test]
        public void AbandoningWithoutAGoalDoesNothing()
        {
            var idle = new EnemyBrain(DefaultSpec());
            Assert.That(idle.AbandonGoal(), Is.False);
            Assert.That(idle.State, Is.EqualTo(EnemyState.Idle));

            var attacking = new EnemyBrain(DefaultSpec());
            attacking.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 5f)));
            Assert.That(attacking.AbandonGoal(), Is.False);
            Assert.That(attacking.State, Is.EqualTo(EnemyState.Attack));

            var dead = new EnemyBrain(DefaultSpec());
            dead.Kill();
            Assert.That(dead.AbandonGoal(), Is.False);
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new EnemyBrain(null));
            Assert.Throws<ArgumentNullException>(() => new EnemyBrain(DefaultSpec()).SetPatrolRoute(null));
        }

        // プレイヤーは +Z にいる。A と C は +Z を遮り (A の方が出現地点に近い)、B は +X を遮る。
        private static readonly Vector3 _player = new Vector3(0f, 0f, 10f);
        private static readonly CoverPoint _coverA = new CoverPoint(new Vector3(2f, 0f, 2f), new Vector3(0f, 0f, 1f), CoverHeight.Low);
        private static readonly CoverPoint _coverB = new CoverPoint(new Vector3(-3f, 0f, 0f), new Vector3(1f, 0f, 0f), CoverHeight.High);
        private static readonly CoverPoint _coverC = new CoverPoint(new Vector3(-2f, 0f, 3f), new Vector3(0f, 0f, 1f), CoverHeight.Low);

        // 反応 0.5 秒、隠れて 1 秒、覗いて 1 秒。
        private static EnemySpec CoverSpec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, coverHideSeconds: 1f, coverPeekSeconds: 1f);

        private static EnemyPerception Watches(Vector3 self, Vector3 target, bool lineOfSight) =>
            new EnemyPerception(self, target, lineOfSight, false, null);

        private static EnemyPerception Shot(Vector3 self, Vector3 target, bool lineOfSight) =>
            new EnemyPerception(self, target, lineOfSight, false, null, true, target, 0.9f);

        private static EnemyPerception Wounded(Vector3 self, Vector3 target, float healthFraction) =>
            new EnemyPerception(self, target, false, false, null, false, null, healthFraction);

        private static EnemyBrain CoverBrain(CoverPointSelector selector, out List<EnemyState> changes)
        {
            var brain = new EnemyBrain(CoverSpec());
            brain.SetPatrolRoute(new[] { new Vector3(0f, 0f, -10f) });
            brain.UseCover(selector);
            var log = new List<EnemyState>();
            brain.StateChanged += (_, next) => log.Add(next);
            changes = log;
            return brain;
        }

        [Test]
        public void BeingShotTakesCoverAtTheNearestPointThatBlocksTheShooter()
        {
            var selector = new CoverPointSelector(new[] { _coverB, _coverC, _coverA });
            var brain = CoverBrain(selector, out var changes);

            var decision = brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(2), "B は向きが合わず、C より A が近い");
            Assert.That(brain.CurrentCoverPoint?.Position, Is.EqualTo(_coverA.Position));
            Assert.That(selector.IndexClaimedBy(brain), Is.EqualTo(2));
            Assert.That(decision.MoveTo, Is.EqualTo(_coverA.Position));
            Assert.That(decision.LookAt, Is.EqualTo(_player));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.TakeCover }));
        }

        [Test]
        public void BeingShotFromAnUnknownTargetUsesTheShooterPosition()
        {
            var selector = new CoverPointSelector(new[] { _coverA });
            var brain = CoverBrain(selector, out _);

            brain.Tick(0.1f, new EnemyPerception(Vector3.Zero, null, false, false, null, true, _player, 0.9f));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPointIndex, Is.Zero);
        }

        [Test]
        public void DroppingBelowHalfHealthTakesCover()
        {
            var brain = CoverBrain(new CoverPointSelector(new[] { _coverA }), out _);

            brain.Tick(0.1f, Wounded(Vector3.Zero, _player, 0.5f));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol), "ちょうど半分ではまだ移らない");

            brain.Tick(0.1f, Wounded(Vector3.Zero, _player, 0.49f));
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void BeingShotWithoutCoverPointsKeepsTheUsualBehaviour()
        {
            var shooter = new Vector3(0f, 0f, 8f);
            var withoutSelector = new EnemyBrain(CoverSpec());
            withoutSelector.Tick(0.1f, Shot(Vector3.Zero, shooter, true));
            Assert.That(withoutSelector.State, Is.EqualTo(EnemyState.Attack));

            var facingAway = new EnemyBrain(CoverSpec());
            facingAway.UseCover(new CoverPointSelector(new[] { _coverB }));
            facingAway.Tick(0.1f, Shot(Vector3.Zero, shooter, true));
            Assert.That(facingAway.State, Is.EqualTo(EnemyState.Attack), "遮る点が無い");
            Assert.That(facingAway.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
        }

        [Test]
        public void HidesCrouchedThenPeeksAndFiresAfterReaction()
        {
            var brain = CoverBrain(new CoverPointSelector(new[] { _coverA }), out _);
            var at = _coverA.Position;
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            var hide = brain.Tick(0.1f, Watches(at, _player, false));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding), "着いたらしゃがむ");
            Assert.That(hide.MoveTo, Is.Null);
            Assert.That(hide.LookAt, Is.EqualTo(_player));
            Assert.That(hide.Crouch, Is.True);
            Assert.That(hide.Fire, Is.False);

            Assert.That(brain.Tick(0.5f, Watches(at, _player, false)).Crouch, Is.True, "CoverHideSeconds 未満");

            var peek = brain.Tick(0.5f, Watches(at, _player, true));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Peeking));
            Assert.That(peek.Crouch, Is.False);
            Assert.That(peek.Fire, Is.False, "覗いた瞬間には撃たない");
            Assert.That(brain.ShotsInAttack, Is.Zero);

            Assert.That(brain.Tick(0.25f, Watches(at, _player, true)).Fire, Is.False, "0.25 秒後");
            Assert.That(brain.Tick(0.25f, Watches(at, _player, true)).Fire, Is.True, "ReactionSeconds 後");
            Assert.That(brain.Tick(0.25f, Watches(at, _player, false)).Fire, Is.False, "見えていなければ撃たない");

            var again = brain.Tick(0.25f, Watches(at, _player, true));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding), "CoverPeekSeconds 経ったら隠れ直す");
            Assert.That(again.Crouch, Is.True);
            Assert.That(again.Fire, Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void BeingFlankedMovesToAnotherPoint()
        {
            var selector = new CoverPointSelector(new[] { _coverA, _coverB });
            var brain = CoverBrain(selector, out var changes);
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));
            brain.Tick(0.1f, Watches(_coverA.Position, _player, false));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding));

            var flanker = new Vector3(12f, 0f, 2f);
            var decision = brain.Tick(0.1f, Watches(_coverA.Position, flanker, true));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Moving));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(1));
            Assert.That(decision.MoveTo, Is.EqualTo(_coverB.Position));
            Assert.That(decision.LookAt, Is.EqualTo(flanker));
            Assert.That(decision.Crouch, Is.False);
            Assert.That(selector.IsOccupied(0), Is.False, "前の点を放す");
            Assert.That(selector.IndexClaimedBy(brain), Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.TakeCover }), "点を移っても状態は TakeCover のまま");
        }

        [Test]
        public void BeingFlankedWithoutAnotherPointLeavesCoverAndFights()
        {
            var selector = new CoverPointSelector(new[] { _coverA });
            var brain = CoverBrain(selector, out var changes);
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));
            brain.Tick(0.1f, Watches(_coverA.Position, _player, false));

            var flanker = new Vector3(10f, 0f, 2f);
            var decision = brain.Tick(0.1f, Watches(_coverA.Position, flanker, true));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(-1));
            Assert.That(selector.IsOccupied(0), Is.False);
            Assert.That(decision.LookAt, Is.EqualTo(flanker));
            Assert.That(decision.Fire, Is.False, "Attack の反応時間は測り直す");
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.TakeCover, EnemyState.Attack }));
        }

        [Test]
        public void DoesNotPickAPointAnotherEnemyUses()
        {
            var selector = new CoverPointSelector(new[] { _coverA, _coverC });
            var first = CoverBrain(selector, out _);
            var second = CoverBrain(selector, out _);

            first.Tick(0.1f, Shot(Vector3.Zero, _player, false));
            var decision = second.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            Assert.That(first.CoverPointIndex, Is.Zero);
            Assert.That(second.CoverPointIndex, Is.EqualTo(1));
            Assert.That(decision.MoveTo, Is.EqualTo(_coverC.Position));
        }

        [Test]
        public void KillingReleasesThePoint()
        {
            var selector = new CoverPointSelector(new[] { _coverA });
            var first = CoverBrain(selector, out _);
            var second = CoverBrain(selector, out _);
            first.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            first.Kill();
            second.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            Assert.That(first.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(second.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(second.CoverPointIndex, Is.Zero);
        }

        [Test]
        public void AbandoningTheWayToAPointPicksAnotherOne()
        {
            var selector = new CoverPointSelector(new[] { _coverA, _coverC });
            var brain = CoverBrain(selector, out _);
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            Assert.That(brain.AbandonGoal(), Is.True);
            var decision = brain.Tick(0.1f, Watches(Vector3.Zero, _player, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(1));
            Assert.That(decision.MoveTo, Is.EqualTo(_coverC.Position));
            Assert.That(selector.IsOccupied(0), Is.False);
        }

        [Test]
        public void AbandoningAfterReachingThePointDoesNothing()
        {
            var brain = CoverBrain(new CoverPointSelector(new[] { _coverA }), out _);
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));
            brain.Tick(0.1f, Watches(_coverA.Position, _player, false));

            Assert.That(brain.AbandonGoal(), Is.False);
            Assert.That(brain.CoverPointIndex, Is.Zero);
        }

        [Test]
        public void LosingTheTargetLeavesCoverAndGoesToWhereItWas()
        {
            var selector = new CoverPointSelector(new[] { _coverA });
            var brain = CoverBrain(selector, out var changes);
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));
            brain.Tick(0.1f, Watches(_coverA.Position, _player, false));

            var decision = brain.Tick(0.1f, Nothing(_coverA.Position));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(decision.MoveTo, Is.EqualTo(_player));
            Assert.That(decision.Crouch, Is.False);
            Assert.That(selector.IsOccupied(0), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { EnemyState.TakeCover, EnemyState.Chase }));
        }

        [Test]
        public void RemovingCoverPointsLeavesCover()
        {
            var selector = new CoverPointSelector(new[] { _coverA });
            var brain = CoverBrain(selector, out _);
            brain.Tick(0.1f, Shot(Vector3.Zero, _player, false));

            brain.UseCover(null);
            brain.Tick(0.1f, Watches(Vector3.Zero, _player, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(selector.IsOccupied(0), Is.False);
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void PerceptionRejectsInvalidHealth(float healthFraction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Wounded(Vector3.Zero, _player, healthFraction));
        }

        [TestCase(-1f, 1f, 0.5f)]
        [TestCase(float.PositiveInfinity, 1f, 0.5f)]
        [TestCase(1f, -1f, 0.5f)]
        [TestCase(1f, float.NaN, 0.5f)]
        [TestCase(1f, 1f, -0.1f)]
        [TestCase(1f, 1f, 1.1f)]
        public void SpecRejectsInvalidCoverValues(float hide, float peek, float health)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemySpec(20f, 10f, 1f, 2f, 0f, 0f, 0f, 0, hide, peek, health));
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
