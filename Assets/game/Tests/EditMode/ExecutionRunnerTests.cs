using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// ExecutionRunner が倒れた参加者をダウンさせ、とどめか出血で倒れるまでラウンドを続け、味方の蘇生でその場に立たせ、
    /// ラウンドの始めに全員を出現点へ並べ直すことを確かめる。bot (EnemyAgent) は NavMesh が要るので、Damageable と SideMember だけの参加者で確かめる。
    /// </summary>
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
        public void TheHudShowsRoundWinsAndWhoIsLeft()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            var hud = BoundHudView();
            Begin(new[] { player, enemy }, player, hud);

            Assert.That(hud.ScoreTexts[0], Is.EqualTo("ALPHA 0"));
            Assert.That(hud.Scores[0].IsOwn, Is.True);
            Assert.That(hud.Lines, Does.Contain("ラウンド 1 (2 本先取)"));
            Assert.That(hud.Lines, Does.Contain("開始まで 1 秒"));

            _runner.Tick(PreparationSeconds);
            KillBy(player, enemy);
            _runner.Tick(0f);

            Assert.That(hud.Lines, Does.Contain("ALPHA 残り 1"));
            Assert.That(hud.Lines, Does.Contain("BRAVO 残り 1 (ダウン 1)"));
            Assert.That(hud.ResultText, Is.EqualTo(HudFormat.NoResult));
        }

        [Test]
        public void WinningTheLastRoundEndsTheMatch()
        {
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            var hud = BoundHudView();
            Begin(new[] { player, enemy }, player, hud);

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
            Assert.That(hud.ResultText, Is.EqualTo("ALPHA の勝ち"));
            Assert.That(hud.ScoreTexts[0], Is.EqualTo("ALPHA 2"));
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

        private void Begin(IReadOnlyList<SideMember> members, SideMember localPlayer, ModeHudView hud = null)
        {
            var execution = ExecutionRunner.CreateExecution(2, PreparationSeconds, 60f, ActionRules.Default, ExecuteSeconds);
            _runner.Begin(execution, members, _spawns, localPlayer, hud);
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

        private ModeHudView BoundHudView()
        {
            var view = Track(new GameObject("ModeHud")).AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            return view;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
