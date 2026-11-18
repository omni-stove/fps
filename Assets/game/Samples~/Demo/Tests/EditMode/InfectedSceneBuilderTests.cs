using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.InfectedSceneBuilder.Build で確かめる。
    public sealed class InfectedSceneBuilderTests
    {
        [Test]
        public void TheInfectedSceneIsListedWithTheOtherModesUnderItsRulesName()
        {
            var builder = new InfectedSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/Infected.unity"));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.Folder + "/Infected.asset"));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Infected/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[InfectedSceneBuilder]"));
            Assert.That(InfectedSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/Infected.asset"));
            Assert.That(InfectedSceneBuilder.ModeName, Is.EqualTo(GameRulesAssetBuilder.InfectedName));
            Assert.That(GameRulesAssetBuilder.ModeNames, Has.Member(InfectedSceneBuilder.ModeName));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(InfectedSceneBuilder.ModeName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(InfectedSceneBuilder.ModeName)));
            Assert.That(BuildSceneList.Scenes, Has.Member(builder.ScenePath));
        }

        [Test]
        public void EveryoneStartsOnTheSurvivorSideWithSpawnsForTheInfected()
        {
            var sides = ModeSceneBuilder.TwoSides(InfectedSceneBuilder.SurvivorBots, 0);

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, Infected.SurvivorSide), Is.SameAs(sides));
            Assert.That(sides[0].Side, Is.EqualTo(Infected.SurvivorSide));
            Assert.That(sides[0].BotCount, Is.EqualTo(InfectedSceneBuilder.SurvivorBots));
            Assert.That(sides[1].Side, Is.EqualTo(Infected.InfectedSide));
            Assert.That(sides[1].BotCount, Is.Zero, "the infected are picked once the round starts");
            Assert.That(sides[1].Spawns, Is.Not.Empty);
        }

        [Test]
        public void TheRulesRespawnEachPlayerWithoutBeingDowned()
        {
            var rules = GameRulesAssetBuilder.InfectedRules();

            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds));
            Assert.That(GameRulesAssetBuilder.ModeRules(InfectedSceneBuilder.ModeName).Actions.Downed, Is.EqualTo(Downed.Off));
        }

        [Test]
        public void TheInfectedSwitchToADifferentWeaponFromTheBots()
        {
            Assert.That(InfectedSceneBuilder.InfectedWeaponId, Is.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(InfectedSceneBuilder.InfectedWeaponId, Is.Not.EqualTo(WeaponAssetBuilder.RifleId));
        }
    }
}
