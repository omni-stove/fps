using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // bot (EnemyAgent) は NavMesh が要るので、Damageable と SideMember だけの参加者で確かめる。
    public sealed class ExecutionRunnerTests
    {
        private const float PreparationSeconds = 1f;
        private const float ExecuteSeconds = 1.5f;

        private static readonly Vector3 AlphaSpawn = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AlphaSpawnTwo = new Vector3(-6f, 0f, 0f);
        private static readonly Vector3 BravoSpawn = new Vector3(0f, 0f, 40f);
        private static readonly Vector3 BravoSpawnTwo = new Vector3(6f, 0f, 40f);

        private readonly List<Object> _objects = new List<Object>();
        private ExecutionRunner _runner;
        private SideSpawnPoint[] _spawns;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<ExecutionRunner>();
            _spawns = new[]
            {
                SpawnPoint("Spawn-0-1", 0, AlphaSpawn),
                SpawnPoint("Spawn-0-2", 0, AlphaSpawnTwo),
                SpawnPoint("Spawn-1-1", 1, BravoSpawn),
                SpawnPoint("Spawn-1-2", 1, BravoSpawnTwo),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void TheFirstRoundLinesEveryoneUpOnTheirOwnSpawnPoints()
        {
            var player = Member("Player", 0, new Vector3(3f, 0f, 3f));
            var ally = Member("Bot-0-1", 0, new Vector3(5f, 0f, 5f));
            var enemy = Member("Bot-1-1", 1, new Vector3(7f, 0f, 7f));

            Begin(new[] { player, ally, enemy }, player);

            Assert.That(player.transform.position, Is.EqualTo(AlphaSpawn));
            Assert.That(ally.transform.position, Is.EqualTo(AlphaSpawnTwo), "allies do not share a spawn point");
            Assert.That(enemy.transform.position, Is.EqualTo(BravoSpawn));
            Assert.That(_runner.Execution.Round.Phase, Is.EqualTo(RoundPhase.Preparing));
        }

        [Test]
        public void DowningAnEnemyKeepsTheRoundGoingUntilTheExecution()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            Begin(new[] { player, enemy }, player);
            _runner.Tick(PreparationSeconds);

            KillBy(player, enemy);
            _runner.Tick(1f);

            var execution = _runner.Execution;
            Assert.That(enemy.Damageable.IsDowned, Is.True);
            Assert.That(execution.IsDowned(_runner.IdOf(enemy)), Is.True);
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(1), "a down alone does not end the round");
            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.Zero);

            MoveNextTo(player, enemy);
            Assert.That(_runner.HoldInteract(player, 1f), Is.EqualTo(ExecutionStep.Executing));
            _runner.Tick(0.1f);
            Assert.That(_runner.HoldInteract(player, ExecuteSeconds - 1f), Is.EqualTo(ExecutionStep.Executed));
            Assert.That(enemy.Damageable.IsDowned, Is.False);
            Assert.That(enemy.IsAlive, Is.False);

            _runner.Tick(0f);

            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(2));
            Assert.That(enemy.IsAlive, Is.True, "the next round brings everyone back");
            Assert.That(enemy.transform.position, Is.EqualTo(BravoSpawn));
        }

        [Test]
        public void BleedingOutTheLastEnemyWinsTheRound()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            Begin(new[] { player, enemy }, player);
            _runner.Tick(PreparationSeconds);

            KillBy(player, enemy);
            _runner.Tick(ActionRules.DefaultDownedBleedoutSeconds);

            Assert.That(_runner.Execution.Round.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(_runner.Execution.Round.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void AnAllyRevivesADownedPlayerWhereTheyLie()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var downed = Member("Bot-1-1", 1, BravoSpawn);
            var reviver = Member("Bot-1-2", 1, BravoSpawnTwo);
            Begin(new[] { player, downed, reviver }, player);
            _runner.Tick(PreparationSeconds);

            KillBy(player, downed);
            MoveNextTo(reviver, downed);
            var lyingAt = downed.transform.position;

            Assert.That(_runner.HoldInteract(reviver, 2f), Is.EqualTo(ExecutionStep.Reviving));
            _runner.Tick(0.1f);
            Assert.That(_runner.HoldInteract(reviver, 1f), Is.EqualTo(ExecutionStep.Revived));

            Assert.That(downed.IsAlive, Is.True);
            Assert.That(downed.Damageable.IsDowned, Is.False);
            Assert.That(downed.Damageable.Health.Current, Is.EqualTo(30), "revived with 30% of 100");
            Assert.That(downed.transform.position, Is.EqualTo(lyingAt));
        }

        [Test]
        public void NothingHappensWithoutADownedPlayerInReach()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            Begin(new[] { player, enemy }, player);
            _runner.Tick(PreparationSeconds);

            Assert.That(_runner.HoldInteract(player, 1f), Is.EqualTo(ExecutionStep.Rejected), "nobody is downed");

            KillBy(player, enemy);
            Assert.That(_runner.HoldInteract(player, 1f), Is.EqualTo(ExecutionStep.Rejected), "the downed enemy is out of reach");
        }

        [Test]
        public void TheStatusCarriesTheExecutionAndTheLocalPlayer()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            Begin(new[] { player, enemy }, player);

            var detail = (ExecutionStatus)_runner.Status.Detail;
            Assert.That(detail.Session, Is.SameAs(_runner.Execution));
            Assert.That(detail.LocalPlayerId, Is.EqualTo(_runner.IdOf(player)));
            Assert.That(detail.InteractTargetId, Is.Null);
            Assert.That(_runner.Status.OwnSide, Is.EqualTo((Side?)Side.Alpha));

            _runner.Tick(PreparationSeconds);
            KillBy(player, enemy);
            _runner.Tick(0f);

            detail = (ExecutionStatus)_runner.Status.Detail;
            Assert.That(detail.Session.CountRemaining(Side.Bravo), Is.EqualTo(1));
            Assert.That(detail.Session.CountDowned(Side.Bravo), Is.EqualTo(1));
            Assert.That(detail.InteractTargetId, Is.Null, "the player is not holding Interact");
            Assert.That(_runner.Status.IsEnded, Is.False);
        }

        [Test]
        public void WinningTheLastRoundEndsTheMatch()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            Begin(new[] { player, enemy }, player);

            for (var round = 0; round < 2; round++)
            {
                _runner.Tick(PreparationSeconds);
                KillBy(player, enemy);
                MoveNextTo(player, enemy);
                _runner.HoldInteract(player, ExecuteSeconds);
                _runner.Tick(0f);
            }

            Assert.That(_runner.Execution.Round.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            var detail = (ExecutionStatus)_runner.Status.Detail;
            Assert.That(detail.Session.Round.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(detail.Session.Round.GetRoundWins(Side.Alpha), Is.EqualTo(2));
        }

        [Test]
        public void MembersListedToStartDownedGoDownWhenEachRoundGoesLive()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var ally = Member("Bot-0-1", 0, AlphaSpawnTwo);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            var standing = Member("Bot-1-2", 1, BravoSpawnTwo);
            _runner.DownedAtRoundStart = new List<SideMember> { ally, enemy, null };
            Begin(new[] { player, ally, enemy, standing }, player);

            Assert.That(ally.Damageable.IsDowned, Is.False, "nobody goes down while lining up");
            _runner.Tick(PreparationSeconds);

            var execution = _runner.Execution;
            Assert.That(ally.Damageable.IsDowned, Is.True);
            Assert.That(enemy.Damageable.IsDowned, Is.True);
            Assert.That(execution.IsDowned(_runner.IdOf(ally)), Is.True);
            Assert.That(execution.IsDowned(_runner.IdOf(enemy)), Is.True);
            Assert.That(player.Damageable.IsDowned, Is.False);
            Assert.That(standing.Damageable.IsDowned, Is.False);
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(1), "a side with someone standing keeps the round going");

            MoveNextTo(player, ally);
            Assert.That(_runner.HoldInteract(player, 2f), Is.EqualTo(ExecutionStep.Reviving));
            _runner.Tick(0.1f);
            Assert.That(_runner.HoldInteract(player, 1f), Is.EqualTo(ExecutionStep.Revived));
            Assert.That(ally.Damageable.IsDowned, Is.False);

            MoveNextTo(player, enemy);
            Assert.That(_runner.HoldInteract(player, ExecuteSeconds), Is.EqualTo(ExecutionStep.Executed));

            KillBy(player, standing);
            MoveNextTo(player, standing);
            Assert.That(_runner.HoldInteract(player, ExecuteSeconds), Is.EqualTo(ExecutionStep.Executed));
            _runner.Tick(0f);
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(2));
            Assert.That(enemy.IsAlive, Is.True, "the next round brings the executed enemy back");

            _runner.Tick(PreparationSeconds);
            Assert.That(ally.Damageable.IsDowned, Is.True, "every round starts with the same downs");
            Assert.That(enemy.Damageable.IsDowned, Is.True);
        }

        [Test]
        public void TheStartDownedListIsNeverNull()
        {
            Assert.That(_runner.DownedAtRoundStart, Is.Empty);

            _runner.DownedAtRoundStart = null;

            Assert.That(_runner.DownedAtRoundStart, Is.Not.Null.And.Empty);
        }

        [Test]
        public void BeginNeedsAnExecutionSession()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var session = TeamDeathmatchRunner.CreateSession(2, 4, 60f, 5, 1f);

            Assert.Throws<ArgumentException>(() => _runner.Begin(session, new[] { player }, _spawns, player));
            Assert.That(_runner.IsStarted, Is.False);
            Assert.Throws<ArgumentNullException>(() => _runner.Begin((ExecutionSession)null, new[] { player }, _spawns, player));
        }

        [Test]
        public void CreateExecutionAlwaysUsesDowns()
        {
            var execution = ExecutionRunner.CreateExecution(2, 0f, 60f, ActionRules.Default, ExecuteSeconds);

            Assert.That(ActionRules.Default.Downed, Is.EqualTo(Downed.Off));
            Assert.That(execution.UsesDowned, Is.True);
        }

        private void Begin(IReadOnlyList<SideMember> members, SideMember localPlayer)
        {
            var execution = ExecutionRunner.CreateExecution(2, PreparationSeconds, 60f, ActionRules.Default, ExecuteSeconds);
            _runner.Begin(execution, members, _spawns, localPlayer);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private static void MoveNextTo(SideMember mover, SideMember target)
        {
            mover.transform.position = target.transform.position + new Vector3(1f, 0f, 0f);
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private SideSpawnPoint SpawnPoint(string name, int side, Vector3 position)
        {
            var point = Track(new GameObject(name)).AddComponent<SideSpawnPoint>();
            point.transform.position = position;
            point.Side = new Side(side);
            return point;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
