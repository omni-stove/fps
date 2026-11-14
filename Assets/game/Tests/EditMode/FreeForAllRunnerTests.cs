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
    /// FreeForAllRunner が 1 人 1 陣営でキルを数え、HUD に自分の順位を出し、得点上限か時間切れで勝者を決めることを確かめる。
    /// bot (EnemyAgent) は NavMesh が要るので、ここでは Damageable と SideMember だけの参加者で確かめる。
    /// </summary>
    public sealed class FreeForAllRunnerTests
    {
        private const float TimeLimit = 30f;
        private const int ScoreLimit = 3;
        private const float RespawnSeconds = 1f;

        private readonly List<Object> _objects = new List<Object>();
        private FreeForAllRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<FreeForAllRunner>();
            _runner.TimeLimitSeconds = TimeLimit;
            _runner.ScoreLimit = ScoreLimit;
            _runner.RespawnSeconds = RespawnSeconds;
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
        public void TheDefaultsComeFromTheMode()
        {
            var runner = Track(new GameObject("Defaults")).AddComponent<FreeForAllRunner>();

            Assert.That(runner.TimeLimitSeconds, Is.EqualTo(FreeForAllMode.DefaultTimeLimitSeconds));
            Assert.That(runner.ScoreLimit, Is.EqualTo(FreeForAllMode.DefaultScoreLimit));
            Assert.That(runner.RespawnSeconds, Is.EqualTo(FreeForAllMode.DefaultRespawnSeconds));
        }

        [Test]
        public void BotsOnDifferentSidesScoreAgainstEachOther()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            Begin(null, player, a, b);

            KillBy(a, b);

            var session = _runner.Session;
            Assert.That(session.GetScore(new Side(1)), Is.EqualTo(1d));
            Assert.That(session.GetScore(new Side(0)), Is.Zero);
            Assert.That(session.IsAwaitingRespawn(_runner.IdOf(b)), Is.True);
        }

        [Test]
        public void TheHudShowsTheOwnRankAndTheGapToTheLeader()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            var hud = BoundHudView();
            Begin(hud, player, a, b);

            Assert.That(hud.Lines, Is.EqualTo(new[] { "順位 1/3  1位と同点" }));

            KillBy(a, b);
            _runner.Tick(0f);
            Assert.That(hud.Lines, Is.EqualTo(new[] { "順位 2/3  1位と -1" }));

            KillBy(player, a);
            _runner.Tick(RespawnSeconds);
            KillBy(player, b);
            _runner.Tick(0f);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(hud.Lines, Is.EqualTo(new[] { "順位 1/3  2位と +1" }));
        }

        [Test]
        public void TheHudKeepsTheRespawnCountdownAboveTheRank()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var hud = BoundHudView();
            Begin(hud, player, a);

            KillBy(a, player);
            _runner.Tick(0f);

            Assert.That(hud.Lines, Is.EqualTo(new[] { HudFormat.Respawn(1), "順位 2/2  1位と -1" }));
        }

        [Test]
        public void ReachingTheScoreLimitNamesTheWinner()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            var hud = BoundHudView();
            Begin(hud, player, a, b);

            KillBy(b, player);
            KillBy(b, a);
            _runner.Tick(RespawnSeconds);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));

            KillBy(b, player);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)new Side(2)));
            Assert.That(hud.ResultText, Is.EqualTo("SIDE 2 の勝ち"));
        }

        [Test]
        public void TimeUpWithATieIsADraw()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            var c = Member("Bot-3-1", 3);
            var hud = BoundHudView();
            Begin(hud, player, a, b, c);

            KillBy(player, a);
            KillBy(b, c);
            _runner.Tick(TimeLimit);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.Null);
            Assert.That(hud.ResultText, Is.EqualTo(HudFormat.Draw));
        }

        [Test]
        public void TwoMembersOnOneSideCannotStart()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-1-2", 1);

            Assert.Throws<ArgumentException>(
                () => _runner.Begin(FreeForAllMode.Create(TimeLimit, ScoreLimit, RespawnSeconds), new[] { player, a, b }, Array.Empty<SideSpawnPoint>(), player));
            Assert.That(_runner.IsStarted, Is.False);
        }

        private void Begin(ModeHudView hud, params SideMember[] members)
        {
            _runner.Begin(FreeForAllMode.Create(TimeLimit, ScoreLimit, RespawnSeconds), members, Array.Empty<SideSpawnPoint>(), members[0], hud);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private SideMember Member(string name, int side)
        {
            var go = Track(new GameObject(name));
            go.transform.position = new Vector3(side * 5f, 0f, 0f);
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
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
