using Fps.Core;
using Fps.Editor;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="GuardianSceneBuilder"/> のパスと配置を確かめる。シーンを組む処理は開いているシーンを置き換えるので、
    /// <c>-executeMethod Fps.Editor.GuardianSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class GuardianSceneBuilderTests
    {
        [Test]
        public void TheGuardianSceneUsesTheGuardianRulesAndIsInTheBuild()
        {
            var builder = new GuardianSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.GuardianScene));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.GuardianName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.GuardianPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/Guardian/NavMesh-Floor.asset"));
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
