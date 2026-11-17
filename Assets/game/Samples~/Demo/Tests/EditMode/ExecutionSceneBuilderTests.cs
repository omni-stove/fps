using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.ExecutionSceneBuilder.Build で確かめる。
    public sealed class ExecutionSceneBuilderTests
    {
        [Test]
        public void TheExecutionSceneUsesTheExecutionRules()
        {
            var builder = new ExecutionSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ExecutionScene));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.ExecutionName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ExecutionPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Execution/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[ExecutionSceneBuilder]"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.ExecutionScene));
        }

        [Test]
        public void TheRulesDownWithoutRespawning()
        {
            var rules = GameRulesAssetBuilder.ExecutionRules();

            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Revive));
        }

        [Test]
        public void BothTeamsHaveTheSameNumberOfPlayers()
        {
            Assert.That(ExecutionSceneBuilder.PlayerSideBots + 1, Is.EqualTo(ExecutionSceneBuilder.EnemySideBots), "the player fills the last seat");

            var sides = ModeSceneBuilder.TwoSides(ExecutionSceneBuilder.PlayerSideBots, ExecutionSceneBuilder.EnemySideBots);
            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
        }
    }
}
