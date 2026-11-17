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
            Begin(player, a, b);

            KillBy(a, b);

            var session = _runner.Session;
            Assert.That(session.GetScore(new Side(1)), Is.EqualTo(1d));
            Assert.That(session.GetScore(new Side(0)), Is.Zero);
            Assert.That(session.IsAwaitingRespawn(_runner.IdOf(b)), Is.True);
        }

        [Test]
        public void TheStatusCarriesTheOwnRankAndTheGapToTheLeader()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            Begin(player, a, b);

            Assert.That(_runner.Status.Detail, Is.EqualTo(new FreeForAllStanding(1, 3, 0, 0)));

            KillBy(a, b);
            _runner.Tick(0f);
            Assert.That(_runner.Status.Detail, Is.EqualTo(new FreeForAllStanding(2, 3, 0, -1)));

            KillBy(player, a);
            _runner.Tick(RespawnSeconds);
            KillBy(player, b);
            _runner.Tick(0f);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_runner.Status.Detail, Is.EqualTo(new FreeForAllStanding(1, 3, 2, 1)));
        }

        [Test]
        public void TheStatusCarriesTheRespawnCountdownWithTheRank()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            Begin(player, a);

            KillBy(a, player);
            _runner.Tick(0f);

            Assert.That(_runner.Status.RespawnSeconds, Is.EqualTo(RespawnSeconds));
            Assert.That(_runner.Status.Detail, Is.EqualTo(new FreeForAllStanding(2, 2, 0, -1)));
        }

        [Test]
        public void WithoutALocalPlayerTheStatusHasNoRank()
        {
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            _runner.Begin(FreeForAllMode.Create(TimeLimit, ScoreLimit, RespawnSeconds), new[] { a, b }, Array.Empty<SideSpawnPoint>());

            Assert.That(_runner.Status.Detail, Is.Null);
        }

        [Test]
        public void ReachingTheScoreLimitNamesTheWinner()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            Begin(player, a, b);

            KillBy(b, player);
            KillBy(b, a);
            _runner.Tick(RespawnSeconds);
            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Playing));

            KillBy(b, player);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.EqualTo((Side?)new Side(2)));
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)new Side(2)));
        }

        [Test]
        public void TimeUpWithATieIsADraw()
        {
            var player = Member("Player", 0);
            var a = Member("Bot-1-1", 1);
            var b = Member("Bot-2-1", 2);
            var c = Member("Bot-3-1", 3);
            Begin(player, a, b, c);

            KillBy(player, a);
            KillBy(b, c);
            _runner.Tick(TimeLimit);

            Assert.That(_runner.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_runner.Session.Winner, Is.Null);
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.Null);
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

        private void Begin(params SideMember[] members)
        {
            _runner.Begin(FreeForAllMode.Create(TimeLimit, ScoreLimit, RespawnSeconds), members, Array.Empty<SideSpawnPoint>(), members[0]);
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

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
