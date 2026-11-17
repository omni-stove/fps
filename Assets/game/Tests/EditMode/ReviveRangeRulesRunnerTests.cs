using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ReviveRangeRulesRunnerTests
    {
        private static readonly Vector3 AlphaSpawn = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AlphaSpawnTwo = new Vector3(-6f, 0f, 0f);
        private static readonly Vector3 BravoSpawn = new Vector3(0f, 0f, 40f);
        private static readonly Vector3 BravoSpawnTwo = new Vector3(6f, 0f, 40f);
        private static readonly Vector3 TwoAndAHalfMeters = new Vector3(2.5f, 0f, 0f);

        private readonly List<Object> _objects = new List<Object>();
        private SideSpawnPoint[] _spawns;

        [SetUp]
        public void SetUp()
        {
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
        public void BattleRoyaleRevivesASquadmateTwoAndAHalfMetersAwayWhenTheRulesReachThree()
        {
            var runner = Track(new GameObject("Runner")).AddComponent<BattleRoyaleRunner>();
            var player = Member("Player", 0, AlphaSpawn);
            var mate = Member("Bot-0-1", 0, AlphaSpawnTwo);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            runner.Begin(
                BattleRoyaleRunner.CreateBattleRoyale(4, 2, 0f, ActionRules.Default.WithReviveRange(3f)),
                new[] { player, mate, enemy },
                _spawns,
                player);

            KillBy(enemy, mate);
            runner.Tick(0.1f);
            player.transform.position = mate.transform.position + TwoAndAHalfMeters;

            Assert.That(runner.HoldRevive(player, 1f), Is.EqualTo(ReviveStep.Progressed));
            Assert.That(runner.LocalReviveTarget, Is.SameAs(mate));
        }

        [Test]
        public void BattleRoyaleCannotReviveFromTwoAndAHalfMetersWithTheDefaultReach()
        {
            var runner = Track(new GameObject("Runner")).AddComponent<BattleRoyaleRunner>();
            var player = Member("Player", 0, AlphaSpawn);
            var mate = Member("Bot-0-1", 0, AlphaSpawnTwo);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            runner.Begin(BattleRoyaleRunner.CreateBattleRoyale(4, 2, 0f, ActionRules.Default), new[] { player, mate, enemy }, _spawns, player);

            KillBy(enemy, mate);
            runner.Tick(0.1f);
            player.transform.position = mate.transform.position + TwoAndAHalfMeters;

            Assert.That(ActionRules.Default.ReviveRange, Is.EqualTo(1.5f));
            Assert.That(runner.HoldRevive(player, 1f), Is.EqualTo(ReviveStep.Rejected));
        }

        [Test]
        public void ExecutionRevivesAnAllyTwoAndAHalfMetersAwayWhenTheRulesReachThree()
        {
            var runner = Track(new GameObject("Runner")).AddComponent<ExecutionRunner>();
            var player = Member("Player", 0, AlphaSpawn);
            var downed = Member("Bot-1-1", 1, BravoSpawn);
            var reviver = Member("Bot-1-2", 1, BravoSpawnTwo);
            runner.Begin(
                ExecutionRunner.CreateExecution(2, 1f, 60f, ActionRules.Default.WithReviveRange(3f), 1.5f),
                new[] { player, downed, reviver },
                _spawns,
                player);
            runner.Tick(1f);

            KillBy(player, downed);
            reviver.transform.position = downed.transform.position + TwoAndAHalfMeters;

            Assert.That(runner.HoldInteract(reviver, 0.5f), Is.EqualTo(ExecutionStep.Reviving));
        }

        [Test]
        public void ExecutionKeepsItsOwnReachForAnEnemyWhenTheRulesReachThree()
        {
            var runner = Track(new GameObject("Runner")).AddComponent<ExecutionRunner>();
            var player = Member("Player", 0, AlphaSpawn);
            var enemy = Member("Bot-1-1", 1, BravoSpawn);
            runner.Begin(
                ExecutionRunner.CreateExecution(2, 1f, 60f, ActionRules.Default.WithReviveRange(3f), 1.5f),
                new[] { player, enemy },
                _spawns,
                player);
            runner.Tick(1f);

            KillBy(player, enemy);
            player.transform.position = enemy.transform.position + TwoAndAHalfMeters;

            Assert.That(runner.ExecuteRange, Is.EqualTo(2f));
            Assert.That(runner.HoldInteract(player, 0.5f), Is.EqualTo(ExecutionStep.Rejected), "2.5 m is past the 2 m execute reach");

            player.transform.position = enemy.transform.position + new Vector3(1.5f, 0f, 0f);
            Assert.That(runner.HoldInteract(player, 0.5f), Is.EqualTo(ExecutionStep.Executing));
        }

        private static void KillBy(SideMember killer, SideMember victim)
        {
            using (CombatEvents.Attribute(killer.gameObject, "rifle"))
            {
                victim.Damageable.Kill();
            }
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
