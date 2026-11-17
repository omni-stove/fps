using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class FreeForAllAndGuardianDefinitionTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var runner in Object.FindObjectsByType<ModeRunnerBase>(FindObjectsInactive.Include))
            {
                runner.Stop();
            }

            foreach (var obj in _objects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ANewFreeForAllDefinitionHasTheModeDefaults()
        {
            var definition = Create<FreeForAllDefinition>("Ffa");

            Assert.That(definition.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(definition.ScoreLimit, Is.EqualTo(15));
            Assert.That(definition.RespawnSeconds, Is.EqualTo(3f));
            Assert.That(definition.PointsPerKill, Is.EqualTo(1));
        }

        [Test]
        public void TheFreeForAllDefinitionBuildsASessionFromItsValues()
        {
            var definition = Ffa("ShortFfa", 90f, 3, 1.5f, 2);

            var session = definition.CreateSession();
            session.AddPlayer(1, new Side(0));
            session.AddPlayer(2, new Side(1));
            session.Start();

            Assert.That(session.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(session.ScoreLimit, Is.EqualTo(3d));

            session.ReportKill(1, 2);
            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(2d));
            Assert.That(session.GetRespawnRemaining(2), Is.EqualTo(1.5f));
        }

        [TestCase(0f, 15, 3f, 1)]
        [TestCase(300f, 0, 3f, 1)]
        [TestCase(300f, 15, -1f, 1)]
        [TestCase(300f, 15, 3f, 0)]
        public void TheFreeForAllDefinitionRejectsInvalidValuesWithItsName(float timeLimit, int scoreLimit, float respawn, int pointsPerKill)
        {
            var definition = Ffa("BrokenFfa", timeLimit, scoreLimit, respawn, pointsPerKill);

            var error = Assert.Throws<InvalidOperationException>(() => definition.CreateSession());
            Assert.That(error.Message, Does.Contain("BrokenFfa"));
        }

        [Test]
        public void TheFreeForAllRunnerReadsItsDefinitionBeforeGameContentAndTheInspector()
        {
            var runner = FreeForAllRunner();
            runner.TimeLimitSeconds = 40f;
            runner.ScoreLimit = 9;
            GameContent.Use(Content(Ffa("ContentFfa", 200f, 7, 2f, 1), null));
            runner.Definition = Ffa("WiredFfa", 120f, 3, 1f, 1);

            var session = runner.CreateConfiguredSession();

            Assert.That(session.TimeLimitSeconds, Is.EqualTo(120f));
            Assert.That(session.ScoreLimit, Is.EqualTo(3d));
        }

        [Test]
        public void TheFreeForAllRunnerReadsGameContentWhenNothingIsWired()
        {
            var runner = FreeForAllRunner();
            runner.TimeLimitSeconds = 40f;
            runner.ScoreLimit = 9;
            GameContent.Use(Content(Ffa("ContentFfa", 200f, 7, 2f, 1), null));

            var session = runner.CreateConfiguredSession();

            Assert.That(session.TimeLimitSeconds, Is.EqualTo(200f));
            Assert.That(session.ScoreLimit, Is.EqualTo(7d));
        }

        [Test]
        public void TheFreeForAllRunnerUsesTheInspectorWithoutADefinition()
        {
            var runner = FreeForAllRunner();
            runner.TimeLimitSeconds = 40f;
            runner.ScoreLimit = 9;
            runner.RespawnSeconds = 0.5f;
            runner.PointsPerKill = 3;
            GameContent.Use(Content(null, null));

            var session = runner.CreateConfiguredSession();
            session.AddPlayer(1, new Side(0));
            session.AddPlayer(2, new Side(1));
            session.Start();
            session.ReportKill(1, 2);

            Assert.That(session.TimeLimitSeconds, Is.EqualTo(40f));
            Assert.That(session.ScoreLimit, Is.EqualTo(9d));
            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(3d));
            Assert.That(session.GetRespawnRemaining(2), Is.EqualTo(0.5f));
        }

        [Test]
        public void TheFreeForAllRunnersMatchEndsAtTheDefinitionsScoreLimit()
        {
            var runner = FreeForAllRunner();
            runner.Definition = Ffa("ThreeKillFfa", 300f, 3, 0f, 1);
            var a = Member("A", 0);
            var b = Member("B", 1);

            runner.Begin(runner.CreateConfiguredSession(), new[] { a, b }, Array.Empty<SideSpawnPoint>());
            var session = runner.Session;
            var aId = runner.IdOf(a);
            var bId = runner.IdOf(b);

            session.ReportKill(aId, bId);
            session.ReportKill(aId, bId);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));

            session.ReportKill(aId, bId);
            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo(new Side(0)));
        }

        [Test]
        public void ANewGuardianDefinitionHasTheRunnerDefaults()
        {
            var definition = Create<GuardianDefinition>("Guardian");

            Assert.That(definition.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(definition.RespawnSeconds, Is.EqualTo(3f));
            Assert.That(definition.PointsPerKill, Is.EqualTo(1));
        }

        [Test]
        public void TheGuardianDefinitionBuildsASessionFromItsValues()
        {
            var definition = Guardian("ShortGuardian", 75f, 2.5f, 4);

            var session = definition.CreateSession(new System.Random(1), out var leaders);
            session.AddPlayer(1, Side.Alpha);
            session.AddPlayer(2, Side.Alpha);
            session.AddPlayer(3, Side.Bravo);
            session.AddPlayer(4, Side.Bravo);
            session.Start();

            Assert.That(leaders, Is.Not.Null);
            Assert.That(session.TimeLimitSeconds, Is.EqualTo(75f));

            var victim = leaders.GetLeader(Side.Bravo) == 3 ? 4 : 3;
            session.ReportKill(1, victim);
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(4d));
            Assert.That(session.GetRespawnRemaining(victim), Is.EqualTo(2.5f));
        }

        [TestCase(0f, 3f, 1)]
        [TestCase(300f, -1f, 1)]
        [TestCase(300f, 3f, 0)]
        public void TheGuardianDefinitionRejectsInvalidValuesWithItsName(float timeLimit, float respawn, int pointsPerKill)
        {
            var definition = Guardian("BrokenGuardian", timeLimit, respawn, pointsPerKill);

            var error = Assert.Throws<InvalidOperationException>(() => definition.CreateSession(new System.Random(1), out _));
            Assert.That(error.Message, Does.Contain("BrokenGuardian"));
        }

        [Test]
        public void TheGuardianRunnerReadsItsDefinitionThenGameContentThenTheInspector()
        {
            var runner = GuardianRunner();
            runner.TimeLimitSeconds = 40f;
            runner.LeaderSeed = 1;
            runner.ShowsLeaderMarkers = false;
            var members = new[] { Member("Alpha-1", 0), Member("Alpha-2", 0), Member("Bravo-1", 1), Member("Bravo-2", 1) };

            GameContent.Use(Content(null, null));
            runner.BeginGuardian(members, Array.Empty<SideSpawnPoint>());
            Assert.That(runner.Session.TimeLimitSeconds, Is.EqualTo(40f), "the Inspector without a definition");
            runner.Stop();

            GameContent.Use(Content(null, Guardian("ContentGuardian", 200f, 3f, 1)));
            runner.BeginGuardian(members, Array.Empty<SideSpawnPoint>());
            Assert.That(runner.Session.TimeLimitSeconds, Is.EqualTo(200f), "the GameContent definition");
            runner.Stop();

            runner.Definition = Guardian("WiredGuardian", 120f, 3f, 1);
            runner.BeginGuardian(members, Array.Empty<SideSpawnPoint>());
            Assert.That(runner.Session.TimeLimitSeconds, Is.EqualTo(120f), "the wired definition");
        }

        [Test]
        public void TheMatchRunnerReadsTheTimeAndKillsFromTheFreeForAllDefinition()
        {
            var runner = Track(new GameObject("Match")).AddComponent<MatchRunner>();
            runner.TimeLimitSeconds = 0f;
            runner.KillsToWin = 10;

            GameContent.Use(Content(null, null));
            var inspector = runner.CreateMatch();
            Assert.That(inspector.HasTimeLimit, Is.False);
            Assert.That(inspector.KillsToWin, Is.EqualTo(10));

            GameContent.Use(Content(Ffa("ContentFfa", 60f, 5, 3f, 1), null));
            var fromContent = runner.CreateMatch();
            Assert.That(fromContent.TimeLimitSeconds, Is.EqualTo(60f));
            Assert.That(fromContent.KillsToWin, Is.EqualTo(5));

            runner.Definition = Ffa("WiredFfa", 45f, 3, 3f, 1);
            var fromDefinition = runner.CreateMatch();
            Assert.That(fromDefinition.TimeLimitSeconds, Is.EqualTo(45f));
            Assert.That(fromDefinition.KillsToWin, Is.EqualTo(3));
            Assert.That(fromDefinition.PlayerId, Is.EqualTo(MatchRunner.PlayerId));
        }

        [Test]
        public void TheMatchRunnerNamesAnInvalidFreeForAllDefinition()
        {
            var runner = Track(new GameObject("Match")).AddComponent<MatchRunner>();
            runner.Definition = Ffa("BrokenMatchFfa", 0f, 3, 3f, 1);

            var error = Assert.Throws<ArgumentException>(() => runner.CreateMatch());
            Assert.That(error.Message, Does.Contain("BrokenMatchFfa"));
        }

        private FreeForAllDefinition Ffa(string name, float timeLimit, int scoreLimit, float respawn, int pointsPerKill)
        {
            var definition = Create<FreeForAllDefinition>(name);
            definition.TimeLimitSeconds = timeLimit;
            definition.ScoreLimit = scoreLimit;
            definition.RespawnSeconds = respawn;
            definition.PointsPerKill = pointsPerKill;
            return definition;
        }

        private GuardianDefinition Guardian(string name, float timeLimit, float respawn, int pointsPerKill)
        {
            var definition = Create<GuardianDefinition>(name);
            definition.TimeLimitSeconds = timeLimit;
            definition.RespawnSeconds = respawn;
            definition.PointsPerKill = pointsPerKill;
            return definition;
        }

        private GameContent Content(FreeForAllDefinition freeForAll, GuardianDefinition guardian)
        {
            var content = Create<GameContent>("Content");
            content.FreeForAll = freeForAll;
            content.Guardian = guardian;
            return content;
        }

        private FreeForAllRunner FreeForAllRunner()
        {
            return Track(new GameObject("FfaRunner")).AddComponent<FreeForAllRunner>();
        }

        private GuardianRunner GuardianRunner()
        {
            return Track(new GameObject("GuardianRunner")).AddComponent<GuardianRunner>();
        }

        private SideMember Member(string name, int side)
        {
            var go = Track(new GameObject(name, typeof(CapsuleCollider)));
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

        private T Create<T>(string name)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _objects.Add(asset);
            return asset;
        }
    }
}
