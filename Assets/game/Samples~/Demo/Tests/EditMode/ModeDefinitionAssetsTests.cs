using System;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
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
            Assert.That(snd.FuseSeconds, Is.EqualTo(Shooter.Core.SearchAndDestroySpec.DefaultFuseSeconds));
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
        public void TheSearchAndDestroyDefinitionGetsTheEconomyOnlyWhenItHasNone()
        {
            var snd = SearchAndDestroySceneBuilder.LoadOrCreateDefinition(_folder + "/SearchAndDestroy.asset");
            var economy = SearchAndDestroySceneBuilder.LoadOrCreateEconomy(_folder + "/SearchAndDestroyEconomy.asset", null);
            var other = SearchAndDestroySceneBuilder.LoadOrCreateEconomy(_folder + "/OtherEconomy.asset", null);

            Assert.That(snd.Economy, Is.Null, "a new definition starts without an economy");
            Assert.That(economy.Tuning, Is.Null, "the amounts and items come from Resources/Tuning/economy.json");

            Assert.That(SearchAndDestroySceneBuilder.WireEconomy(snd, economy), Is.True);
            Assert.That(snd.Economy, Is.SameAs(economy));

            Assert.That(SearchAndDestroySceneBuilder.WireEconomy(snd, other), Is.False, "a game's own economy is kept");
            Assert.That(snd.Economy, Is.SameAs(economy));
            Assert.That(SearchAndDestroySceneBuilder.EconomyPath, Is.EqualTo(DemoSample.Root + "/Modes/SearchAndDestroyEconomy.asset"));
        }

        [Test]
        public void APathOutsideAssetsIsRefused()
        {
            Assert.Throws<ArgumentException>(() => SearchAndDestroySceneBuilder.LoadOrCreateDefinition("Packages/SearchAndDestroy.asset"));
        }
    }
}
