using System.IO;
using NUnit.Framework;
using Shooter.Editor;
using UnityEngine;

namespace Shooter.Net.Tests
{
    public sealed class GettingStartedGuideNetTests
    {
        private const string GuidePath = "docs/getting-started.md";

        // 導入ガイドはキットのリポジトリにだけあり、サンプルを入れたゲームのプロジェクトには無いので、そこでは読まない。
        private static string ReadGuide()
        {
            var path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, GuidePath);
            if (!File.Exists(path))
            {
                Assert.Ignore(GuidePath + " is not in this project; it ships only with the kit repository");
            }

            return File.ReadAllText(path);
        }

        [Test]
        public void TheGuideNamesTheRulesExportMenuAsTheEditorShowsIt()
        {
            var guide = ReadGuide();

            Assert.That(ServerRulesExporter.MenuPath, Is.EqualTo("Shooter/Server Data/Export Rules And Points"));
            Assert.That(guide, Does.Contain("Shooter > Server Data > Export Rules And Points"));
        }

        [Test]
        public void TheGuideNamesTheListOfServerMapsThePointsExportReads()
        {
            var guide = ReadGuide();

            Assert.That(BuildSceneList.ServerScenes, Does.Contain(BuildSceneList.NetArena01Scene));
            Assert.That(guide, Does.Contain("`" + nameof(BuildSceneList) + "." + nameof(BuildSceneList.ServerScenes) + "`"));
            Assert.That(guide, Does.Contain("`" + nameof(ServerDataMaps) + "`"));
            Assert.That(guide, Does.Contain("`" + nameof(MapCollisionExporter) + "." + nameof(MapCollisionExporter.UseMapScenes) + "`"));
            Assert.That(guide, Does.Contain(MapCollisionExporter.ScenesArgument + " "));
        }
    }
}
