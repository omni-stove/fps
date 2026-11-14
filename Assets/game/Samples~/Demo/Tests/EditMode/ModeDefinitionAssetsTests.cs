using System;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ModeDefinitionAssets"/> がモードの調整値のアセットを無ければ既定値で作り、あれば値を変えずに返すことを確かめる。
    /// </summary>
    public sealed class ModeDefinitionAssetsTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__ModeDefinitionAssetsTests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void TheAssetsSitUnderTheModesFolder()
        {
            Assert.That(ModeDefinitionAssets.Folder, Is.EqualTo(DemoSample.Root + "/Modes"));
            Assert.That(ModeDefinitionAssets.PathOf("Horde"), Is.EqualTo(DemoSample.Root + "/Modes/Horde.asset"));
            Assert.Throws<ArgumentException>(() => ModeDefinitionAssets.PathOf(" "));
        }

        [Test]
        public void TheSceneBuildersCreateTheDefaultValues()
        {
            var snd = SearchAndDestroySceneBuilder.LoadOrCreateDefinition(_folder + "/SearchAndDestroy.asset");
            var kc = KillConfirmedSceneBuilder.LoadOrCreateDefinition(_folder + "/KillConfirmed.asset");
            var tdm = ModeTestSceneBuilder.LoadOrCreateDefinition(_folder + "/TeamDeathmatch.asset");

            Assert.That(AssetDatabase.LoadAssetAtPath<SearchAndDestroyDefinition>(_folder + "/SearchAndDestroy.asset"), Is.SameAs(snd));
            Assert.That(snd.FuseSeconds, Is.EqualTo(Fps.Core.SearchAndDestroySpec.DefaultFuseSeconds));
            Assert.That(kc.TagLifetimeSeconds, Is.EqualTo(KillConfirmedRunner.DefaultTagLifetimeSeconds));
            Assert.That(tdm.ScoreLimit, Is.EqualTo(TeamDeathmatchRunner.DefaultScoreLimit));
        }

        [Test]
        public void AnExistingAssetKeepsItsValuesAndGuid()
        {
            var path = _folder + "/KillConfirmed.asset";
            var first = KillConfirmedSceneBuilder.LoadOrCreateDefinition(path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            first.ScoreLimit = 7;
            EditorUtility.SetDirty(first);
            AssetDatabase.SaveAssets();

            var second = KillConfirmedSceneBuilder.LoadOrCreateDefinition(path);

            Assert.That(second, Is.SameAs(first));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(second.ScoreLimit, Is.EqualTo(7));
        }

        [Test]
        public void AnAssetOfAnotherTypeIsRefused()
        {
            var path = _folder + "/Mixed.asset";
            KillConfirmedSceneBuilder.LoadOrCreateDefinition(path);

            Assert.Throws<InvalidOperationException>(() => ModeTestSceneBuilder.LoadOrCreateDefinition(path));
            Assert.That(AssetDatabase.LoadAssetAtPath<KillConfirmedDefinition>(path), Is.Not.Null);
        }

        [Test]
        public void APathOutsideAssetsIsRefused()
        {
            Assert.Throws<ArgumentException>(() => SearchAndDestroySceneBuilder.LoadOrCreateDefinition("Packages/SearchAndDestroy.asset"));
        }
    }
}
