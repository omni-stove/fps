using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.DownedSceneBuilder.Build で確かめる。
    public sealed class DownedSceneBuilderTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
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
        public void TheDownedSceneUsesTheExecutionRulesUnderAShortName()
        {
            var builder = new DownedSceneBuilder();

            Assert.That(DownedSceneBuilder.SceneName, Is.EqualTo("Downed"));
            Assert.That(builder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/Downed.unity"));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.DownedScene));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ExecutionPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Downed/NavMesh-Floor.asset"));
            Assert.That(GameRulesAssetBuilder.ExecutionRules().Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.DownedScene));
        }

        [Test]
        public void TheDownedAllyAndEnemyLieWithinAFewStepsOfThePlayer()
        {
            var sides = DownedSceneBuilder.Layout();

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
            Assert.That(sides[0].BotCount, Is.EqualTo(1));
            Assert.That(sides[1].BotCount, Is.EqualTo(2));
            Assert.That(sides[0].Spawns[1].Feet, Is.EqualTo(new Vector3(-2f, 0f, 2f)));
            Assert.That(sides[1].Spawns[0].Feet, Is.EqualTo(new Vector3(2f, 0f, 4f)));
            Assert.That(sides[1].Spawns[1].Feet, Is.EqualTo(new Vector3(0f, 0f, 40f)));
            Assert.That(Vector3.Distance(ModeSceneBuilder.PlayerFeet, DownedSceneBuilder.DownedAllyFeet), Is.LessThan(3f));
            Assert.That(Vector3.Distance(ModeSceneBuilder.PlayerFeet, DownedSceneBuilder.DownedEnemyFeet), Is.LessThan(5f));
            Assert.That(DownedSceneBuilder.DownedAtStartNames, Is.EqualTo(new[] { "Bot-0-1", "Bot-1-1" }));
        }

        [Test]
        public void DownedAtStartPicksTheFirstBotOfEachSide()
        {
            var ally = Bot("Bot-0-1", 0);
            var enemy = Bot("Bot-1-1", 1);
            var standing = Bot("Bot-1-2", 1);

            var members = DownedSceneBuilder.DownedAtStart(new[] { standing, enemy, ally });

            Assert.That(members, Is.EqualTo(new[] { enemy.GetComponent<SideMember>(), ally.GetComponent<SideMember>() }));
        }

        [Test]
        public void DownedAtStartRejectsAMissingBot()
        {
            var enemy = Bot("Bot-1-1", 1);

            Assert.Throws<InvalidOperationException>(() => DownedSceneBuilder.DownedAtStart(new[] { enemy }));
        }

        [Test]
        public void TheFeatureMenuBuildsTheDownedSceneToo()
        {
            Assert.That(BuildSceneList.FeatureScenes, Has.Member(BuildSceneList.DownedScene));
            Assert.That(FeatureSceneBuilder.LegacyAssetPaths(), Has.No.Member(BuildSceneList.DownedScene), "the old Feature_Downed goes, the new Downed stays");
        }

        private EnemyAgent Bot(string name, int side)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            var agent = go.AddComponent<EnemyAgent>();
            go.AddComponent<SideMember>().Side = new Side(side);
            return agent;
        }
    }
}
