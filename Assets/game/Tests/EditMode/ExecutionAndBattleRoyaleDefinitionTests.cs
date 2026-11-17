using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ExecutionAndBattleRoyaleDefinitionTests
    {
        private static readonly Vector3 AlphaSpawn = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 BravoSpawn = new Vector3(0f, 0f, 40f);
        private static readonly Vector3 TwoAndAHalfMeters = new Vector3(2.5f, 0f, 0f);

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var runner in Object.FindObjectsByType<ModeRunnerBase>(FindObjectsInactive.Include))
            {
                runner.Stop();
            }

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
        public void ANewExecutionDefinitionHasTheRunnerDefaults()
        {
            var definition = Create<ExecutionDefinition>("Execution");

            Assert.That(definition.WinsToWin, Is.EqualTo(3));
            Assert.That(definition.PreparationSeconds, Is.EqualTo(3f));
            Assert.That(definition.RoundTimeLimitSeconds, Is.EqualTo(120f));
            Assert.That(definition.ExecuteSeconds, Is.EqualTo(1.5f));
            Assert.That(definition.ExecuteRange, Is.EqualTo(2f));
        }

        [Test]
        public void TheExecutionDefinitionBuildsASessionFromItsValues()
        {
            var definition = Execution("ShortExecution", 5, 1f, 45f, 0.75f, 3f);

            var execution = definition.CreateExecution(ActionRules.Default.WithDowned(Downed.Off));

            Assert.That(execution.Round.WinsToWin, Is.EqualTo(5));
            Assert.That(execution.Round.PreparationSeconds, Is.EqualTo(1f));
            Assert.That(execution.Mode.TimeLimitSeconds, Is.EqualTo(45f));
            Assert.That(execution.ExecuteSeconds, Is.EqualTo(0.75f));
            Assert.That(execution.UsesDowned, Is.True, "execution always uses the downed state");
        }

        [Test]
        public void TheExecutionDefinitionKeepsTheRulesReviveRange()
        {
            var definition = Execution("ShortExecution", 5, 1f, 45f, 0.75f, 3f);

            var execution = definition.CreateExecution(ActionRules.Default.WithReviveRange(4f));

            Assert.That(execution.Actions.ReviveRange, Is.EqualTo(4f));
        }

        [TestCase(0, 3f, 120f, 1.5f, 2f)]
        [TestCase(3, -1f, 120f, 1.5f, 2f)]
        [TestCase(3, 3f, 0f, 1.5f, 2f)]
        [TestCase(3, 3f, 120f, 0f, 2f)]
        [TestCase(3, 3f, 120f, 1.5f, 0f)]
        [TestCase(3, 3f, 120f, 1.5f, float.PositiveInfinity)]
        public void TheExecutionDefinitionRejectsInvalidValuesWithItsName(
            int winsToWin, float preparation, float roundTime, float executeSeconds, float executeRange)
        {
            var definition = Execution("BrokenExecution", winsToWin, preparation, roundTime, executeSeconds, executeRange);

            var error = Assert.Throws<InvalidOperationException>(() => definition.CreateExecution(ActionRules.Default));
            Assert.That(error.Message, Does.Contain("BrokenExecution"));
        }

        [Test]
        public void TheExecutionRunnerReadsItsDefinitionBeforeGameContentAndTheInspector()
        {
            var runner = ExecutionRunner();
            runner.WinsToWin = 7;
            runner.ExecuteRange = 1f;
            GameContent.Use(Content(Execution("ContentExecution", 4, 2f, 90f, 1f, 2.5f), null));
            runner.Definition = Execution("WiredExecution", 2, 0.5f, 30f, 0.5f, 3.5f);

            var execution = runner.CreateConfiguredExecution(ActionRules.Default);

            Assert.That(execution.Round.WinsToWin, Is.EqualTo(2));
            Assert.That(execution.Round.PreparationSeconds, Is.EqualTo(0.5f));
            Assert.That(execution.Mode.TimeLimitSeconds, Is.EqualTo(30f));
            Assert.That(execution.ExecuteSeconds, Is.EqualTo(0.5f));
            Assert.That(runner.EffectiveExecuteRange, Is.EqualTo(3.5f));
        }

        [Test]
        public void TheExecutionRunnerReadsGameContentWhenNothingIsWired()
        {
            var runner = ExecutionRunner();
            runner.WinsToWin = 7;
            runner.ExecuteRange = 1f;
            GameContent.Use(Content(Execution("ContentExecution", 4, 2f, 90f, 1f, 2.5f), null));

            var execution = runner.CreateConfiguredExecution(ActionRules.Default);

            Assert.That(execution.Round.WinsToWin, Is.EqualTo(4));
            Assert.That(execution.Round.PreparationSeconds, Is.EqualTo(2f));
            Assert.That(execution.Mode.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(execution.ExecuteSeconds, Is.EqualTo(1f));
            Assert.That(runner.EffectiveExecuteRange, Is.EqualTo(2.5f));
        }

        [Test]
        public void TheExecutionRunnerUsesTheInspectorWithoutADefinition()
        {
            var runner = ExecutionRunner();
            runner.WinsToWin = 7;
            runner.PreparationSeconds = 4f;
            runner.RoundTimeLimitSeconds = 75f;
            runner.ExecuteSeconds = 2f;
            runner.ExecuteRange = 1f;
            GameContent.Use(Content(null, null));

            var execution = runner.CreateConfiguredExecution(ActionRules.Default);

            Assert.That(execution.Round.WinsToWin, Is.EqualTo(7));
            Assert.That(execution.Round.PreparationSeconds, Is.EqualTo(4f));
            Assert.That(execution.Mode.TimeLimitSeconds, Is.EqualTo(75f));
            Assert.That(execution.ExecuteSeconds, Is.EqualTo(2f));
            Assert.That(runner.EffectiveExecuteRange, Is.EqualTo(1f));
        }

        [Test]
        public void TheExecutionRunnerExecutesWithinTheDefinitionsRange()
        {
            var runner = ExecutionRunner();
            runner.Definition = Execution("LongReachExecution", 2, 1f, 60f, 1.5f, 3f);
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            runner.Begin(
                runner.CreateConfiguredExecution(ActionRules.Default),
                new[] { player, enemy },
                new[] { SpawnPoint("Spawn-0-1", 0, AlphaSpawn), SpawnPoint("Spawn-1-1", 1, BravoSpawn) },
                player);
            runner.Tick(1f);

            KillBy(player, enemy);
            player.transform.position = enemy.transform.position + TwoAndAHalfMeters;

            Assert.That(runner.HoldInteract(player, 0.5f), Is.EqualTo(ExecutionStep.Executing), "2.5 m is within the definition's 3 m reach");

            player.transform.position = enemy.transform.position + new Vector3(3.5f, 0f, 0f);
            Assert.That(runner.HoldInteract(player, 0.5f), Is.EqualTo(ExecutionStep.Rejected), "3.5 m is past the definition's 3 m reach");
        }

        [Test]
        public void ANewBattleRoyaleDefinitionHasTheRunnerDefaults()
        {
            var definition = Create<BattleRoyaleDefinition>("BattleRoyale");

            Assert.That(definition.SquadCount, Is.EqualTo(4));
            Assert.That(definition.SquadSize, Is.EqualTo(2));
            Assert.That(definition.TimeLimitSeconds, Is.EqualTo(0f));
            Assert.That(definition.BotPlanIntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(definition.BotSearchRadius, Is.EqualTo(25f));
            Assert.That(definition.DropHeight, Is.EqualTo(60f));
        }

        [Test]
        public void TheBattleRoyaleDefinitionBuildsSquadsOfItsSize()
        {
            var definition = BattleRoyale("Trios", 3, 3, 600f, 1f, 10f, 80f);

            var battleRoyale = definition.CreateBattleRoyale(ActionRules.Default.WithDowned(Downed.Off));
            var squads = (Squads)battleRoyale.Mode.Layout;

            Assert.That(squads.SquadCount, Is.EqualTo(3));
            Assert.That(squads.SquadSize, Is.EqualTo(3));
            Assert.That(battleRoyale.Mode.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(battleRoyale.UsesDowned, Is.True, "battle royale always uses the downed state");

            battleRoyale.Mode.AddPlayer(1, new Side(0));
            battleRoyale.Mode.AddPlayer(2, new Side(0));
            battleRoyale.Mode.AddPlayer(3, new Side(0));
            Assert.Throws<InvalidOperationException>(() => battleRoyale.Mode.AddPlayer(4, new Side(0)), "a squad of three is full");
        }

        [Test]
        public void TheBattleRoyaleDefinitionsZeroTimeLimitMeansNoLimit()
        {
            var definition = BattleRoyale("Endless", 4, 2, 0f, 0.5f, 25f, 60f);

            var battleRoyale = definition.CreateBattleRoyale(ActionRules.Default);

            Assert.That(battleRoyale.Mode.TimeLimitSeconds, Is.EqualTo(float.PositiveInfinity));
        }

        [TestCase(1, 2, 0f, 0.5f, 25f, 60f)]
        [TestCase(4, 0, 0f, 0.5f, 25f, 60f)]
        [TestCase(4, 2, -1f, 0.5f, 25f, 60f)]
        [TestCase(4, 2, 0f, 0f, 25f, 60f)]
        [TestCase(4, 2, 0f, 0.5f, -1f, 60f)]
        [TestCase(4, 2, 0f, 0.5f, 25f, -1f)]
        [TestCase(4, 2, 0f, 0.5f, 25f, float.PositiveInfinity)]
        public void TheBattleRoyaleDefinitionRejectsInvalidValuesWithItsName(
            int squadCount, int squadSize, float timeLimit, float planInterval, float searchRadius, float dropHeight)
        {
            var definition = BattleRoyale("BrokenRoyale", squadCount, squadSize, timeLimit, planInterval, searchRadius, dropHeight);

            var error = Assert.Throws<InvalidOperationException>(() => definition.CreateBattleRoyale(ActionRules.Default));
            Assert.That(error.Message, Does.Contain("BrokenRoyale"));
        }

        [Test]
        public void TheBattleRoyaleRunnerReadsItsDefinitionBeforeGameContentAndTheInspector()
        {
            var runner = BattleRoyaleRunner();
            runner.SquadSize = 4;
            runner.BotSearchRadius = 5f;
            GameContent.Use(Content(null, BattleRoyale("ContentRoyale", 5, 3, 300f, 1f, 15f, 70f)));
            runner.Definition = BattleRoyale("WiredRoyale", 6, 2, 240f, 2f, 30f, 90f);

            var squads = (Squads)runner.CreateConfiguredBattleRoyale(ActionRules.Default).Mode.Layout;

            Assert.That(squads.SquadCount, Is.EqualTo(6));
            Assert.That(squads.SquadSize, Is.EqualTo(2));
            Assert.That(runner.EffectiveBotPlanIntervalSeconds, Is.EqualTo(2f));
            Assert.That(runner.EffectiveBotSearchRadius, Is.EqualTo(30f));
            Assert.That(runner.EffectiveDropHeight, Is.EqualTo(90f));
        }

        [Test]
        public void TheBattleRoyaleRunnerReadsGameContentWhenNothingIsWired()
        {
            var runner = BattleRoyaleRunner();
            runner.SquadSize = 4;
            runner.BotSearchRadius = 5f;
            GameContent.Use(Content(null, BattleRoyale("ContentRoyale", 5, 3, 300f, 1f, 15f, 70f)));

            var battleRoyale = runner.CreateConfiguredBattleRoyale(ActionRules.Default);
            var squads = (Squads)battleRoyale.Mode.Layout;

            Assert.That(squads.SquadCount, Is.EqualTo(5));
            Assert.That(squads.SquadSize, Is.EqualTo(3));
            Assert.That(battleRoyale.Mode.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(runner.EffectiveBotPlanIntervalSeconds, Is.EqualTo(1f));
            Assert.That(runner.EffectiveBotSearchRadius, Is.EqualTo(15f));
            Assert.That(runner.EffectiveDropHeight, Is.EqualTo(70f));
        }

        [Test]
        public void TheBattleRoyaleRunnerUsesTheInspectorWithoutADefinition()
        {
            var runner = BattleRoyaleRunner();
            runner.SquadCount = 3;
            runner.SquadSize = 4;
            runner.TimeLimitSeconds = 120f;
            runner.BotPlanIntervalSeconds = 0.25f;
            runner.BotSearchRadius = 5f;
            runner.DropHeight = 40f;
            GameContent.Use(Content(null, null));

            var battleRoyale = runner.CreateConfiguredBattleRoyale(ActionRules.Default);
            var squads = (Squads)battleRoyale.Mode.Layout;

            Assert.That(squads.SquadCount, Is.EqualTo(3));
            Assert.That(squads.SquadSize, Is.EqualTo(4));
            Assert.That(battleRoyale.Mode.TimeLimitSeconds, Is.EqualTo(120f));
            Assert.That(runner.EffectiveBotPlanIntervalSeconds, Is.EqualTo(0.25f));
            Assert.That(runner.EffectiveBotSearchRadius, Is.EqualTo(5f));
            Assert.That(runner.EffectiveDropHeight, Is.EqualTo(40f));
        }

        [Test]
        public void TheBattleRoyaleRunnerFillsSquadsOfTheDefinitionsSize()
        {
            var runner = BattleRoyaleRunner();
            runner.SquadSize = 4;
            runner.StartsUnarmed = false;
            runner.Definition = BattleRoyale("Duos", 2, 2, 0f, 0.5f, 25f, 60f);
            var members = new[]
            {
                Member("A1", 0, AlphaSpawn),
                Member("A2", 0, AlphaSpawn + TwoAndAHalfMeters),
                Member("B1", 1, BravoSpawn),
                Member("B2", 1, BravoSpawn + TwoAndAHalfMeters),
            };

            runner.Begin(
                runner.CreateConfiguredBattleRoyale(ActionRules.Default),
                members,
                new[] { SpawnPoint("Spawn-0-1", 0, AlphaSpawn), SpawnPoint("Spawn-1-1", 1, BravoSpawn) });

            Assert.That(runner.Session.CountMembers(new Side(0)), Is.EqualTo(2));
            Assert.That(runner.Session.CountMembers(new Side(1)), Is.EqualTo(2));
            Assert.That(runner.BattleRoyale.EntrantCount, Is.EqualTo(2));
        }

        [Test]
        public void TheBattleRoyaleRunnerRefusesAThirdMemberInASquadOfTheDefinitionsSize()
        {
            var runner = BattleRoyaleRunner();
            runner.SquadSize = 4;
            runner.StartsUnarmed = false;
            runner.Definition = BattleRoyale("Duos", 2, 2, 0f, 0.5f, 25f, 60f);
            var members = new[]
            {
                Member("A1", 0, AlphaSpawn),
                Member("A2", 0, AlphaSpawn + TwoAndAHalfMeters),
                Member("A3", 0, AlphaSpawn - TwoAndAHalfMeters),
                Member("B1", 1, BravoSpawn),
            };

            Assert.Throws<ArgumentException>(() => runner.Begin(
                runner.CreateConfiguredBattleRoyale(ActionRules.Default),
                members,
                new[] { SpawnPoint("Spawn-0-1", 0, AlphaSpawn), SpawnPoint("Spawn-1-1", 1, BravoSpawn) }));
            Assert.That(runner.IsStarted, Is.False);
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
        }

        private ExecutionDefinition Execution(
            string name, int winsToWin, float preparation, float roundTime, float executeSeconds, float executeRange)
        {
            var definition = Create<ExecutionDefinition>(name);
            definition.WinsToWin = winsToWin;
            definition.PreparationSeconds = preparation;
            definition.RoundTimeLimitSeconds = roundTime;
            definition.ExecuteSeconds = executeSeconds;
            definition.ExecuteRange = executeRange;
            return definition;
        }

        private BattleRoyaleDefinition BattleRoyale(
            string name, int squadCount, int squadSize, float timeLimit, float planInterval, float searchRadius, float dropHeight)
        {
            var definition = Create<BattleRoyaleDefinition>(name);
            definition.SquadCount = squadCount;
            definition.SquadSize = squadSize;
            definition.TimeLimitSeconds = timeLimit;
            definition.BotPlanIntervalSeconds = planInterval;
            definition.BotSearchRadius = searchRadius;
            definition.DropHeight = dropHeight;
            return definition;
        }

        private GameContent Content(ExecutionDefinition execution, BattleRoyaleDefinition battleRoyale)
        {
            var content = Create<GameContent>("Content");
            content.Execution = execution;
            content.BattleRoyale = battleRoyale;
            return content;
        }

        private ExecutionRunner ExecutionRunner()
        {
            return Track(new GameObject("ExecutionRunner")).AddComponent<ExecutionRunner>();
        }

        private BattleRoyaleRunner BattleRoyaleRunner()
        {
            return Track(new GameObject("BattleRoyaleRunner")).AddComponent<BattleRoyaleRunner>();
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
