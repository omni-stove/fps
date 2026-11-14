using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="KillConfirmedSceneBuilder"/> の置き場所・ルール・配置・bot の落とす物の設定を確かめる。シーンを組んで NavMesh を焼く処理は、
    /// 開いているシーンを置き換える (NewScene Single) ので <c>-executeMethod Fps.Editor.KillConfirmedSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class KillConfirmedSceneBuilderTests
    {
        [Test]
        public void TheKillConfirmedSceneUsesTheKillConfirmedRules()
        {
            var builder = new KillConfirmedSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.KillConfirmedScene));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.KillConfirmedName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.KillConfirmedPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/KillConfirmed/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[KillConfirmedSceneBuilder]"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.KillConfirmedScene));
            Assert.That(KillConfirmedSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/KillConfirmed.asset"));
        }

        [Test]
        public void TheRulesRespawnEachPlayer()
        {
            Assert.That(GameRulesAssetBuilder.KillConfirmedRules().RespawnMode, Is.EqualTo(RespawnMode.Individual));
        }

        [Test]
        public void BothTeamsHaveTheSameNumberOfPlayers()
        {
            Assert.That(KillConfirmedSceneBuilder.PlayerSideBots + 1, Is.EqualTo(KillConfirmedSceneBuilder.EnemySideBots), "the player fills the last seat");

            var sides = ModeSceneBuilder.TwoSides(KillConfirmedSceneBuilder.PlayerSideBots, KillConfirmedSceneBuilder.EnemySideBots);
            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
        }

        [Test]
        public void BotsDropNothingButTheTagTheRunnerAdds()
        {
            var go = new GameObject("Bot", typeof(Damageable));
            try
            {
                var drop = go.AddComponent<DropOnDeath>();
                drop.CollectMode = PickupCollectMode.Interact;

                KillConfirmedSceneBuilder.ConfigureBotDrop(drop);

                Assert.That(drop.Drops, Is.Empty, "the default reserve ammo is removed");
                Assert.That(drop.CollectMode, Is.EqualTo(PickupCollectMode.Touch), "tags are picked up by walking over them");
                Assert.That(drop.LifetimeSeconds, Is.EqualTo(KillConfirmedRunner.DefaultTagLifetimeSeconds));
                Assert.Throws<ArgumentNullException>(() => KillConfirmedSceneBuilder.ConfigureBotDrop(null));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
