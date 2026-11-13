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
            teamDeathmatch.Destruction = Destruction.Off;
            teamDeathmatch.Downed = Downed.Revive;
            teamDeathmatch.ReviveSeconds = 9f;
            teamDeathmatch.Cover = Cover.On;
            EditorUtility.SetDirty(teamDeathmatch);
            AssetDatabase.SaveAssets();

            var rebuilt = Build();

            Assert.That(Array.ConvertAll(paths, AssetDatabase.AssetPathToGUID), Is.EqualTo(guids));
            Assert.That(rebuilt[0], Is.SameAs(teamDeathmatch));
            var restored = rebuilt[0].ToRules();
            Assert.That(restored.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(restored.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(restored.InfiniteReserve, Is.False);
            Assert.That(restored.Destruction, Is.EqualTo(Destruction.On));
            AssertDefaultActions(restored.Actions, rebuilt[0].name);
        }

        [Test]
        public void EveryModeKeepsTheDefaultActions()
        {
            var built = Build();

            foreach (var definition in built)
            {
                AssertDefaultActions(definition.ToRules().Actions, definition.name);
            }
        }

        [Test]
        public void EveryModeRulesUseTheDefaultActions()
        {
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().Actions, Is.SameAs(ActionRules.Default));
            Assert.That(GameRulesAssetBuilder.CampaignEasyRules().Actions, Is.SameAs(ActionRules.Default));
            Assert.That(GameRulesAssetBuilder.CampaignNormalRules().Actions, Is.SameAs(ActionRules.Default));
            Assert.That(GameRulesAssetBuilder.CampaignHardRules().Actions, Is.SameAs(ActionRules.Default));
            Assert.That(GameRulesAssetBuilder.RangeRules().Actions, Is.SameAs(ActionRules.Default));
        }

        private static void AssertDefaultActions(ActionRules actual, string message)
        {
            var expected = ActionRules.Default;
            Assert.That(actual.Downed, Is.EqualTo(expected.Downed), message);
            Assert.That(actual.DownedBleedoutSeconds, Is.EqualTo(expected.DownedBleedoutSeconds), message);
            Assert.That(actual.DownedHealthFraction, Is.EqualTo(expected.DownedHealthFraction), message);
            Assert.That(actual.ReviveSeconds, Is.EqualTo(expected.ReviveSeconds), message);
            Assert.That(actual.Cover, Is.EqualTo(expected.Cover), message);
            Assert.That(actual.BlindFire, Is.EqualTo(expected.BlindFire), message);
            Assert.That(actual.ActiveReload, Is.EqualTo(expected.ActiveReload), message);
            Assert.That(actual.Dodge, Is.EqualTo(Dodge.Off), message);
            Assert.That(actual.Vault, Is.EqualTo(Vault.Off), message);
            Assert.That(actual.Grenade, Is.EqualTo(Grenade.Off), message);
            Assert.That(actual.GrenadeCount, Is.EqualTo(0), message);
        }

        [Test]
        public void EveryModeKeepsNoSpawnProtectionNoFallDamageAndTheFullHud()
        {
            var built = Build();

            foreach (var definition in built)
            {
                Assert.That(definition.SpawnProtectionSeconds, Is.EqualTo(0f), definition.name);
                Assert.That(definition.FallDamage, Is.EqualTo(FallDamage.Off), definition.name);
                Assert.That(definition.HudDensity, Is.EqualTo(HudDensity.Full), definition.name);
                Assert.That(definition.GrenadeCount, Is.EqualTo(0), definition.name);

                var rules = definition.ToRules();
                Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(0f), definition.name);
                Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off), definition.name);
                Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Full), definition.name);
            }
        }

        [Test]
        public void SecondRunRestoresTheNewValues()
        {
            Build();
            var range = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_rangePath);
            range.SpawnProtectionSeconds = 3f;
            range.FallDamage = FallDamage.On;
            range.HudDensity = HudDensity.Hardcore;
            range.Dodge = Dodge.On;
            range.Vault = Vault.On;
            range.Grenade = Grenade.On;
            range.GrenadeCount = 4;
            EditorUtility.SetDirty(range);
            AssetDatabase.SaveAssets();

            var restored = Build()[4].ToRules();

            Assert.That(restored.SpawnProtectionSeconds, Is.EqualTo(0f));
            Assert.That(restored.FallDamage, Is.EqualTo(FallDamage.Off));
            Assert.That(restored.HudDensity, Is.EqualTo(HudDensity.Full));
            AssertDefaultActions(restored.Actions, range.name);
        }

        [Test]
        public void TheRepositoryRuleAssetsLoadWithTheNewValuesAtTheirDefaults()
        {
            var paths = new[]
            {
                GameRulesAssetBuilder.TeamDeathmatchPath,
                GameRulesAssetBuilder.CampaignEasyPath,
                GameRulesAssetBuilder.CampaignNormalPath,
                GameRulesAssetBuilder.CampaignHardPath,
                GameRulesAssetBuilder.RangePath,
            };

            foreach (var path in paths)
            {
                var rules = GameRulesAssetBuilder.LoadRules(path).ToRules();
                Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(0f), path);
                Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off), path);
                Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Full), path);
                AssertDefaultActions(rules.Actions, path);
            }
        }

        [Test]
        public void DestructionIsOnOnlyForTeamDeathmatchAndRange()
        {
            var built = Build();

            Assert.That(built[0].ToRules().Destruction, Is.EqualTo(Destruction.On), built[0].name);
            Assert.That(built[1].ToRules().Destruction, Is.EqualTo(Destruction.Off), built[1].name);
            Assert.That(built[2].ToRules().Destruction, Is.EqualTo(Destruction.Off), built[2].name);
            Assert.That(built[3].ToRules().Destruction, Is.EqualTo(Destruction.Off), built[3].name);
            Assert.That(built[4].ToRules().Destruction, Is.EqualTo(Destruction.On), built[4].name);
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
