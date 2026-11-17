using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // シーンを組む処理は開いているシーンを置き換えるので、-executeMethod Shooter.Editor.GuardianSceneBuilder.Build で確かめる。
    public sealed class GuardianSceneBuilderTests
    {
        [Test]
        public void TheGuardianSceneUsesTheGuardianRulesAndIsInTheBuild()
        {
            var builder = new GuardianSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.GuardianScene));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.GuardianName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.GuardianPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Guardian/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[GuardianSceneBuilder]"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.GuardianScene));
        }

        [Test]
        public void BothTeamsFieldTheSameNumberIncludingThePlayer()
        {
            var sides = ModeSceneBuilder.TwoSides(GuardianSceneBuilder.TeamSize - 1, GuardianSceneBuilder.TeamSize);

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, Side.Alpha), Is.SameAs(sides));
            Assert.That(sides[0].BotCount + 1, Is.EqualTo(sides[1].BotCount), "the player fills the last Alpha slot");
        }
    }
}
