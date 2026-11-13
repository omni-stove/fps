using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="GameRulesAssetBuilder"/> を一時フォルダに走らせて、出力と GUID の保持を確かめる。
    /// リポジトリの Assets/game/Rules は触らない。
    /// </summary>
    public sealed class GameRulesAssetBuilderTests
    {
        private string _folder;
        private string _teamDeathmatchPath;
        private string _campaignEasyPath;
        private string _campaignNormalPath;
        private string _campaignHardPath;
        private string _rangePath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__GameRulesAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _teamDeathmatchPath = _folder + "/TeamDeathmatch.asset";
            _campaignEasyPath = _folder + "/CampaignEasy.asset";
            _campaignNormalPath = _folder + "/CampaignNormal.asset";
            _campaignHardPath = _folder + "/CampaignHard.asset";
            _rangePath = _folder + "/Range.asset";
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
        public void DefaultPathsAreUnderGameRules()
        {
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchPath, Is.EqualTo("Assets/game/Rules/TeamDeathmatch.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignEasyPath, Is.EqualTo("Assets/game/Rules/CampaignEasy.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignNormalPath, Is.EqualTo("Assets/game/Rules/CampaignNormal.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignHardPath, Is.EqualTo("Assets/game/Rules/CampaignHard.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo("Assets/game/Rules/Range.asset"));
        }

        [Test]
        public void BuildsTeamDeathmatch()
        {
            var rules = BuildAndLoad(_teamDeathmatchPath);

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(4f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(rules.InfiniteReserve, Is.False);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void BuildsCampaignEasy()
        {
            var rules = BuildAndLoad(_campaignEasyPath);

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(3f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(25f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.None));
            Assert.That(rules.InfiniteReserve, Is.False);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void BuildsCampaignNormalAsTheDefaultWithCollapse()
        {
            var rules = BuildAndLoad(_campaignNormalPath);
            var expected = GameRules.Default;

            Assert.That(rules.FriendlyFire, Is.EqualTo(expected.FriendlyFire));
            Assert.That(rules.RespawnMode, Is.EqualTo(expected.RespawnMode));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(expected.WaveIntervalSeconds));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(expected.RegenDelaySeconds));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(expected.RegenPerSecond));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(expected.DeathCamera));
            Assert.That(rules.InfiniteReserve, Is.EqualTo(expected.InfiniteReserve));
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void BuildsCampaignHard()
        {
            var rules = BuildAndLoad(_campaignHardPath);

            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f), "no regeneration");
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.InfiniteReserve, Is.False);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(0.6f));
        }

        [Test]
        public void BuildsRange()
        {
            var rules = BuildAndLoad(_rangePath);

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f), "no regeneration");
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Vanish));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.None));
            Assert.That(rules.InfiniteReserve, Is.True);
        }

        [Test]
        public void BuildAtReturnsTheAssetsInOrder()
        {
            var built = Build();

            Assert.That(built, Has.Length.EqualTo(5));
            Assert.That(built[0], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_teamDeathmatchPath)));
            Assert.That(built[1], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_campaignEasyPath)));
            Assert.That(built[2], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_campaignNormalPath)));
            Assert.That(built[3], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_campaignHardPath)));
            Assert.That(built[4], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_rangePath)));
        }

        [Test]
        public void LoadRulesReturnsTheBuiltAsset()
        {
            Build();

            var loaded = GameRulesAssetBuilder.LoadRules(_rangePath);

            Assert.That(loaded, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_rangePath)));
        }

        [Test]
        public void LoadRulesRejectsAMissingWrongOrInvalidAsset()
        {
            Assert.Throws<InvalidOperationException>(() => GameRulesAssetBuilder.LoadRules(_rangePath));
            Assert.Throws<ArgumentException>(() => GameRulesAssetBuilder.LoadRules("Range.asset"));

            Build();
            var otherPath = _folder + "/Other.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<WeaponCatalogAsset>(), otherPath);
            Assert.Throws<InvalidOperationException>(
                () => GameRulesAssetBuilder.LoadRules(otherPath), "a weapon catalog is not game rules");

            var range = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_rangePath);
            range.ReserveAmmoMultiplier = -1f;
            Assert.Throws<GameRulesDefinitionException>(() => GameRulesAssetBuilder.LoadRules(_rangePath));
        }

        [Test]
        public void SecondRunKeepsTheGuidsAndRestoresTheValues()
        {
            Build();
            var paths = new[] { _teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath };
            var guids = Array.ConvertAll(paths, AssetDatabase.AssetPathToGUID);
            foreach (var guid in guids)
            {
                Assert.That(guid, Is.Not.Empty);
            }

            // 手で書き換えられたアセットも、2 回目で GUID を保ったまま既定の値に戻る。
            var teamDeathmatch = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_teamDeathmatchPath);
            teamDeathmatch.FriendlyFire = FriendlyFire.On;
            teamDeathmatch.RegenPerSecond = 1f;
            teamDeathmatch.InfiniteReserve = true;
            EditorUtility.SetDirty(teamDeathmatch);
            AssetDatabase.SaveAssets();

            var rebuilt = Build();

            Assert.That(Array.ConvertAll(paths, AssetDatabase.AssetPathToGUID), Is.EqualTo(guids));
            Assert.That(rebuilt[0], Is.SameAs(teamDeathmatch));
            var restored = rebuilt[0].ToRules();
            Assert.That(restored.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(restored.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(restored.InfiniteReserve, Is.False);
        }

        [Test]
        public void RefusesToOverwriteAnAssetOfAnotherType()
        {
            Build();
            AssetDatabase.DeleteAsset(_rangePath);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<WeaponCatalogAsset>(), _rangePath);

            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_rangePath), Is.InstanceOf<WeaponCatalogAsset>());
        }

        [Test]
        public void RejectsInvalidOrSharedPaths()
        {
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt("TeamDeathmatch.asset", _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath));
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt(_teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, "Range.prefab"));
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt(_teamDeathmatchPath, _campaignEasyPath, _campaignEasyPath, _campaignHardPath, _rangePath));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void ConfigureRejectsNullArguments()
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => GameRulesAssetBuilder.Configure(null, GameRules.Default));
                Assert.Throws<ArgumentNullException>(() => GameRulesAssetBuilder.Configure(definition, null));
                Assert.That(
                    () => SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(
                        _rangePath, d => GameRulesAssetBuilder.Configure(d, null)),
                    Throws.InstanceOf<ArgumentNullException>());
                Assert.That(AssetDatabase.LoadMainAssetAtPath(_rangePath), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private GameRulesDefinition[] Build()
        {
            return GameRulesAssetBuilder.BuildAt(_teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath);
        }

        private GameRules BuildAndLoad(string path)
        {
            Build();
            var definition = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
            Assert.That(definition, Is.Not.Null, path);
            return definition.ToRules();
        }
    }
}
