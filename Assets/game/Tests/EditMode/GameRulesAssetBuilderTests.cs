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
        private const int TpsRangeIndex = 5;
        private const int DominationIndex = 6;

        // BuildAt と同じ並び (teamDeathmatch, campaignEasy, campaignNormal, campaignHard, range, tpsRange, domination)。
        private static readonly ExpectedActions[] _expectedActions =
        {
            new ExpectedActions(
                Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On, grenade: Grenade.On, grenadeCount: GameRulesAssetBuilder.TeamDeathmatchGrenadeCount),
            new ExpectedActions(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On),
            new ExpectedActions(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On),
            new ExpectedActions(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On),
            new ExpectedActions(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On),
            new ExpectedActions(
                Downed.Off, Cover.On, BlindFire.On, ActiveReload.On, Dodge.On, Vault.On, Grenade.On, GameRulesAssetBuilder.TpsRangeGrenadeCount),
            new ExpectedActions(
                Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On, grenade: Grenade.On, grenadeCount: GameRulesAssetBuilder.TeamDeathmatchGrenadeCount),
        };

        private string _folder;
        private string _teamDeathmatchPath;
        private string _campaignEasyPath;
        private string _campaignNormalPath;
        private string _campaignHardPath;
        private string _rangePath;
        private string _tpsRangePath;
        private string _dominationPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__GameRulesAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _teamDeathmatchPath = _folder + "/TeamDeathmatch.asset";
            _campaignEasyPath = _folder + "/CampaignEasy.asset";
            _campaignNormalPath = _folder + "/CampaignNormal.asset";
            _campaignHardPath = _folder + "/CampaignHard.asset";
            _rangePath = _folder + "/Range.asset";
            _tpsRangePath = _folder + "/TpsRange.asset";
            _dominationPath = _folder + "/Domination.asset";
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
            Assert.That(GameRulesAssetBuilder.TpsRangePath, Is.EqualTo("Assets/game/Rules/TpsRange.asset"));
            Assert.That(GameRulesAssetBuilder.DominationPath, Is.EqualTo("Assets/game/Rules/Domination.asset"));
        }

        [Test]
        public void BuildsTpsRangeAsTheRangeWithEveryTpsAction()
        {
            var rules = BuildAndLoad(_tpsRangePath);
            var range = GameRulesAssetBuilder.RangeRules();

            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.FriendlyFire, Is.EqualTo(range.FriendlyFire));
            Assert.That(rules.RespawnMode, Is.EqualTo(range.RespawnMode));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(range.RegenDelaySeconds));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(range.RegenPerSecond));
            Assert.That(rules.DeathBody, Is.EqualTo(range.DeathBody));
            Assert.That(rules.DeathCamera, Is.EqualTo(range.DeathCamera));
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(range.ReserveAmmoMultiplier));
            Assert.That(rules.Destruction, Is.EqualTo(range.Destruction));
            AssertActions(rules.Actions, _expectedActions[TpsRangeIndex], _tpsRangePath);
        }

        [Test]
        public void BuildsDominationWithTheTeamDeathmatchValues()
        {
            var rules = BuildAndLoad(_dominationPath);
            var teamDeathmatch = GameRulesAssetBuilder.TeamDeathmatchRules();

            Assert.That(rules.FriendlyFire, Is.EqualTo(teamDeathmatch.FriendlyFire));
            Assert.That(rules.RespawnMode, Is.EqualTo(teamDeathmatch.RespawnMode));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(teamDeathmatch.WaveIntervalSeconds));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(teamDeathmatch.RegenDelaySeconds));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(teamDeathmatch.RegenPerSecond));
            Assert.That(rules.DeathBody, Is.EqualTo(teamDeathmatch.DeathBody));
            Assert.That(rules.DeathCamera, Is.EqualTo(teamDeathmatch.DeathCamera));
            Assert.That(rules.InfiniteReserve, Is.EqualTo(teamDeathmatch.InfiniteReserve));
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(teamDeathmatch.ReserveAmmoMultiplier));
            Assert.That(rules.Destruction, Is.EqualTo(teamDeathmatch.Destruction));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(teamDeathmatch.SpawnProtectionSeconds));
            AssertActions(rules.Actions, _expectedActions[DominationIndex], _dominationPath);
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
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Ally));
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
        public void BuildsCampaignNormalAsTheDefaultWithCollapseAndRegen()
        {
            var rules = BuildAndLoad(_campaignNormalPath);
            var expected = GameRules.Default;

            Assert.That(rules.FriendlyFire, Is.EqualTo(expected.FriendlyFire));
            Assert.That(rules.RespawnMode, Is.EqualTo(expected.RespawnMode));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(expected.WaveIntervalSeconds));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(5f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(15f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(expected.DeathCamera));
            Assert.That(rules.InfiniteReserve, Is.EqualTo(expected.InfiniteReserve));
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void BuildsCampaignHard()
        {
            var rules = BuildAndLoad(_campaignHardPath);

            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(7f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(10f));
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
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(4f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Vanish));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.None));
            Assert.That(rules.InfiniteReserve, Is.True);
        }

        [Test]
        public void BuildAtReturnsTheAssetsInOrder()
        {
            var built = Build();

            Assert.That(built, Has.Length.EqualTo(7));
            Assert.That(built[0], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_teamDeathmatchPath)));
            Assert.That(built[1], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_campaignEasyPath)));
            Assert.That(built[2], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_campaignNormalPath)));
            Assert.That(built[3], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_campaignHardPath)));
            Assert.That(built[4], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_rangePath)));
            Assert.That(built[TpsRangeIndex], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_tpsRangePath)));
            Assert.That(built[DominationIndex], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_dominationPath)));
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
            var paths = new[]
            {
                _teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath, _tpsRangePath, _dominationPath,
            };
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
            teamDeathmatch.Downed = Downed.Off;
            teamDeathmatch.ReviveSeconds = 9f;
            teamDeathmatch.Cover = Cover.On;
            teamDeathmatch.ActiveReload = ActiveReload.Off;
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
            AssertActions(restored.Actions, _expectedActions[0], rebuilt[0].name);
        }

        [Test]
        public void EveryModeAssetGetsItsActions()
        {
            var built = Build();

            for (var i = 0; i < built.Length; i++)
            {
                AssertActions(built[i].ToRules().Actions, _expectedActions[i], built[i].name);
            }
        }

        [Test]
        public void TeamDeathmatchDownsAndActiveReloadsButHasNoCoverOrBlindFire()
        {
            var actions = GameRulesAssetBuilder.TeamDeathmatchRules().Actions;

            Assert.That(actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.On));
            Assert.That(actions.Cover, Is.EqualTo(Cover.Off), "the online player does not support cover");
            Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.Off), "the online player does not support blind fire");
        }

        [Test]
        public void CampaignsCoverBlindFireAndActiveReloadWithoutDowned()
        {
            var campaigns = new[]
            {
                GameRulesAssetBuilder.CampaignEasyRules(),
                GameRulesAssetBuilder.CampaignNormalRules(),
                GameRulesAssetBuilder.CampaignHardRules(),
            };

            foreach (var rules in campaigns)
            {
                Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.On));
                Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.On));
                Assert.That(rules.Actions.ActiveReload, Is.EqualTo(ActiveReload.On));
                Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Off));
            }
        }

        [Test]
        public void RangeCoversBlindFiresAndActiveReloads()
        {
            var actions = GameRulesAssetBuilder.RangeRules().Actions;

            Assert.That(actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.On));
            Assert.That(actions.Downed, Is.EqualTo(Downed.Off));
        }

        [Test]
        public void EveryModeRulesMatchTheirActionFactories()
        {
            var rules = new[]
            {
                GameRulesAssetBuilder.TeamDeathmatchRules(),
                GameRulesAssetBuilder.CampaignEasyRules(),
                GameRulesAssetBuilder.CampaignNormalRules(),
                GameRulesAssetBuilder.CampaignHardRules(),
                GameRulesAssetBuilder.RangeRules(),
                GameRulesAssetBuilder.TpsRangeRules(),
                GameRulesAssetBuilder.DominationRules(),
            };

            for (var i = 0; i < rules.Length; i++)
            {
                AssertActions(rules[i].Actions, _expectedActions[i], $"mode {i}");
            }

            AssertActions(GameRulesAssetBuilder.TeamDeathmatchActions(), _expectedActions[0], nameof(GameRulesAssetBuilder.TeamDeathmatchActions));
            AssertActions(GameRulesAssetBuilder.CampaignActions(), _expectedActions[1], nameof(GameRulesAssetBuilder.CampaignActions));
            AssertActions(GameRulesAssetBuilder.RangeActions(), _expectedActions[4], nameof(GameRulesAssetBuilder.RangeActions));
            AssertActions(GameRulesAssetBuilder.TpsRangeActions(), _expectedActions[TpsRangeIndex], nameof(GameRulesAssetBuilder.TpsRangeActions));
        }

        [Test]
        public void TpsRangeEnablesEveryTpsActionWithInfiniteReserve()
        {
            var rules = GameRulesAssetBuilder.TpsRangeRules();

            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.That(rules.Actions.ActiveReload, Is.EqualTo(ActiveReload.On));
            Assert.That(rules.Actions.Dodge, Is.EqualTo(Dodge.On));
            Assert.That(rules.Actions.Vault, Is.EqualTo(Vault.On));
            Assert.That(rules.Actions.Grenade, Is.EqualTo(Grenade.On));
            Assert.That(rules.Actions.GrenadeCount, Is.GreaterThan(0), "grenades are on, so there must be some to throw");
        }

        private static void AssertActions(ActionRules actual, ExpectedActions expected, string message)
        {
            var defaults = ActionRules.Default;
            Assert.That(actual.Downed, Is.EqualTo(expected.Downed), message);
            Assert.That(actual.DownedBleedoutSeconds, Is.EqualTo(defaults.DownedBleedoutSeconds), message);
            Assert.That(actual.DownedHealthFraction, Is.EqualTo(defaults.DownedHealthFraction), message);
            Assert.That(actual.ReviveSeconds, Is.EqualTo(defaults.ReviveSeconds), message);
            Assert.That(actual.Cover, Is.EqualTo(expected.Cover), message);
            Assert.That(actual.BlindFire, Is.EqualTo(expected.BlindFire), message);
            Assert.That(actual.ActiveReload, Is.EqualTo(expected.ActiveReload), message);
            Assert.That(actual.Dodge, Is.EqualTo(expected.Dodge), message);
            Assert.That(actual.Vault, Is.EqualTo(expected.Vault), message);
            Assert.That(actual.Grenade, Is.EqualTo(expected.Grenade), message);
            Assert.That(actual.GrenadeCount, Is.EqualTo(expected.GrenadeCount), message);
        }

        private readonly struct ExpectedActions
        {
            public readonly Downed Downed;
            public readonly Cover Cover;
            public readonly BlindFire BlindFire;
            public readonly ActiveReload ActiveReload;
            public readonly Dodge Dodge;
            public readonly Vault Vault;
            public readonly Grenade Grenade;
            public readonly int GrenadeCount;

            public ExpectedActions(
                Downed downed,
                Cover cover,
                BlindFire blindFire,
                ActiveReload activeReload,
                Dodge dodge = Dodge.Off,
                Vault vault = Vault.Off,
                Grenade grenade = Grenade.Off,
                int grenadeCount = 0)
            {
                Downed = downed;
                Cover = cover;
                BlindFire = blindFire;
                ActiveReload = activeReload;
                Dodge = dodge;
                Vault = vault;
                Grenade = grenade;
                GrenadeCount = grenadeCount;
            }
        }

        // TeamDeathmatch (0) と、その値を引き継ぐ Domination だけが出現直後に無敵になる
        private static float ExpectedSpawnProtection(int index)
        {
            return index == 0 || index == DominationIndex ? GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds : 0f;
        }

        [Test]
        public void OnlyTheOnlineStyleModesKickIdlePlayersAfterTwoMinutes()
        {
            Assert.That(GameRulesAssetBuilder.OnlineIdleKickSeconds, Is.EqualTo(120f));

            var built = Build();
            Assert.That(built[0].ToRules().IdleKickSeconds, Is.EqualTo(120f), built[0].name);
            Assert.That(built[DominationIndex].ToRules().IdleKickSeconds, Is.EqualTo(120f), built[DominationIndex].name);
            for (var i = 1; i <= TpsRangeIndex; i++)
            {
                Assert.That(built[i].ToRules().IdleKickSeconds, Is.Zero, built[i].name);
            }

            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(name).IdleKickSeconds, Is.EqualTo(120f), name);
            }
        }

        [Test]
        public void TeamDeathmatchProtectsForTwoSecondsAfterSpawning()
        {
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds, Is.EqualTo(2f));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().SpawnProtectionSeconds, Is.EqualTo(2f));
            Assert.That(BuildAndLoad(_teamDeathmatchPath).SpawnProtectionSeconds, Is.EqualTo(2f));
            Assert.That(BuildAndLoad(_dominationPath).SpawnProtectionSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void EveryModeKeepsTheExpectedSpawnProtectionNoFallDamageAndTheFullHud()
        {
            var built = Build();

            for (var i = 0; i < built.Length; i++)
            {
                var definition = built[i];
                Assert.That(definition.SpawnProtectionSeconds, Is.EqualTo(ExpectedSpawnProtection(i)), definition.name);
                Assert.That(definition.FallDamage, Is.EqualTo(FallDamage.Off), definition.name);
                Assert.That(definition.HudDensity, Is.EqualTo(HudDensity.Full), definition.name);
                Assert.That(definition.GrenadeCount, Is.EqualTo(_expectedActions[i].GrenadeCount), definition.name);

                var rules = definition.ToRules();
                Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(ExpectedSpawnProtection(i)), definition.name);
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
            AssertActions(restored.Actions, _expectedActions[4], range.name);
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

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                var rules = GameRulesAssetBuilder.LoadRules(path).ToRules();
                Assert.That(
                    rules.SpawnProtectionSeconds,
                    Is.EqualTo(ExpectedSpawnProtection(i)),
                    path + $" (rerun {nameof(GameRulesAssetBuilder)}.Build)");
                Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off), path);
                Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Full), path);
                AssertActions(rules.Actions, _expectedActions[i], path + $" (rerun {nameof(GameRulesAssetBuilder)}.Build)");
            }
        }

        [Test]
        public void DestructionIsOffOnlyForCampaigns()
        {
            var built = Build();

            Assert.That(built[0].ToRules().Destruction, Is.EqualTo(Destruction.On), built[0].name);
            Assert.That(built[1].ToRules().Destruction, Is.EqualTo(Destruction.Off), built[1].name);
            Assert.That(built[2].ToRules().Destruction, Is.EqualTo(Destruction.Off), built[2].name);
            Assert.That(built[3].ToRules().Destruction, Is.EqualTo(Destruction.Off), built[3].name);
            Assert.That(built[4].ToRules().Destruction, Is.EqualTo(Destruction.On), built[4].name);
            Assert.That(built[TpsRangeIndex].ToRules().Destruction, Is.EqualTo(Destruction.On), built[TpsRangeIndex].name);
            Assert.That(built[DominationIndex].ToRules().Destruction, Is.EqualTo(Destruction.On), built[DominationIndex].name);
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
                () => GameRulesAssetBuilder.BuildAt(
                    "TeamDeathmatch.asset", _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath, _tpsRangePath, _dominationPath));
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt(
                    _teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, "Range.prefab", _tpsRangePath, _dominationPath));
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt(
                    _teamDeathmatchPath, _campaignEasyPath, _campaignEasyPath, _campaignHardPath, _rangePath, _tpsRangePath, _dominationPath));
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt(
                    _teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath, _tpsRangePath, _teamDeathmatchPath));
            Assert.Throws<ArgumentException>(
                () => GameRulesAssetBuilder.BuildAt(
                    _teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath, null, _dominationPath));
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

        [Test]
        public void ModeNamesAreTheNineModesWithPathsUnderGameRules()
        {
            Assert.That(
                GameRulesAssetBuilder.ModeNames,
                Is.EqualTo(new[]
                {
                    "FreeForAll", "KillConfirmed", "GunGame", "Hardpoint", "Guardian", "Execution", "SearchAndDestroy", "Horde", "BattleRoyale",
                }));
            Assert.That(GameRulesAssetBuilder.FreeForAllPath, Is.EqualTo("Assets/game/Rules/FreeForAll.asset"));
            Assert.That(GameRulesAssetBuilder.BattleRoyalePath, Is.EqualTo("Assets/game/Rules/BattleRoyale.asset"));
            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRulesPath(name), Is.EqualTo("Assets/game/Rules/" + name + ".asset"));
            }

            Assert.Throws<ArgumentException>(() => GameRulesAssetBuilder.ModeRulesPath("TeamDeathmatch"), "TDM is built by BuildAt");
            Assert.Throws<ArgumentException>(() => GameRulesAssetBuilder.ModeRules("freeforall"), "names are case sensitive");
            Assert.Throws<ArgumentException>(() => GameRulesAssetBuilder.ModeRulesPath("Library", GameRulesAssetBuilder.HordeName));
            Assert.Throws<ArgumentException>(() => GameRulesAssetBuilder.ModeRulesPath(" ", GameRulesAssetBuilder.HordeName));
        }

        [Test]
        public void BuildModesAtWritesEveryModeInOrderAndKeepsTheGuids()
        {
            var built = GameRulesAssetBuilder.BuildModesAt(_folder + "/");

            Assert.That(built, Has.Length.EqualTo(GameRulesAssetBuilder.ModeNames.Count));
            var guids = new string[built.Length];
            for (var i = 0; i < built.Length; i++)
            {
                var path = _folder + "/" + GameRulesAssetBuilder.ModeNames[i] + ".asset";
                Assert.That(built[i], Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path)), path);
                guids[i] = AssetDatabase.AssetPathToGUID(path);
                Assert.That(guids[i], Is.Not.Empty, path);
            }

            var horde = built[Array.IndexOf(ToArray(GameRulesAssetBuilder.ModeNames), GameRulesAssetBuilder.HordeName)];
            horde.RespawnMode = RespawnMode.Individual;
            horde.ReserveAmmoMultiplier = 1f;
            EditorUtility.SetDirty(horde);
            AssetDatabase.SaveAssets();

            var rebuilt = GameRulesAssetBuilder.BuildModesAt(_folder);

            for (var i = 0; i < rebuilt.Length; i++)
            {
                Assert.That(rebuilt[i], Is.SameAs(built[i]));
                Assert.That(AssetDatabase.AssetPathToGUID(_folder + "/" + GameRulesAssetBuilder.ModeNames[i] + ".asset"), Is.EqualTo(guids[i]));
            }

            var restored = horde.ToRules();
            Assert.That(restored.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(restored.ReserveAmmoMultiplier, Is.EqualTo(GameRulesAssetBuilder.HordeReserveAmmoMultiplier));
        }

        [Test]
        public void EliminationModesDoNotRespawn()
        {
            Assert.That(GameRulesAssetBuilder.ExecutionRules().RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(GameRulesAssetBuilder.SearchAndDestroyRules().RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(GameRulesAssetBuilder.HordeRules().RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(GameRulesAssetBuilder.HordeRules().WaveIntervalSeconds, Is.EqualTo(GameRulesAssetBuilder.HordeWaveIntervalSeconds));

            foreach (var name in new[] { GameRulesAssetBuilder.FreeForAllName, GameRulesAssetBuilder.KillConfirmedName, GameRulesAssetBuilder.GunGameName,
                         GameRulesAssetBuilder.HardpointName, GameRulesAssetBuilder.GuardianName })
            {
                var rules = GameRulesAssetBuilder.ModeRules(name);
                Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual), name);
                Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds), name);
            }

            Assert.That(GameRulesAssetBuilder.ExecutionRules().SpawnProtectionSeconds, Is.Zero, "nobody respawns, so nobody needs protecting");
            Assert.That(GameRulesAssetBuilder.SearchAndDestroyRules().SpawnProtectionSeconds, Is.Zero);
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().SpawnProtectionSeconds, Is.Zero);
        }

        [Test]
        public void ExecutionHordeAndBattleRoyaleDownButFreeForAllAndSearchAndDestroyDoNot()
        {
            Assert.That(GameRulesAssetBuilder.ExecutionRules().Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(GameRulesAssetBuilder.HordeRules().Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(GameRulesAssetBuilder.FreeForAllRules().Actions.Downed, Is.EqualTo(Downed.Off), "nobody can revive in a free-for-all");
            Assert.That(GameRulesAssetBuilder.GunGameRules().Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(GameRulesAssetBuilder.SearchAndDestroyRules().Actions.Downed, Is.EqualTo(Downed.Off));

            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                var actions = GameRulesAssetBuilder.ModeRules(name).Actions;
                Assert.That(actions.Cover, Is.EqualTo(Cover.Off), name + ": the online player does not support cover");
                Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.Off), name);
                Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.On), name);
            }
        }

        [Test]
        public void HordeAndBattleRoyaleCarryMoreReserveAmmoAndGunGameNeverRunsOut()
        {
            Assert.That(GameRulesAssetBuilder.HordeRules().ReserveAmmoMultiplier, Is.GreaterThan(1f));
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().ReserveAmmoMultiplier, Is.GreaterThan(1f));
            Assert.That(GameRulesAssetBuilder.GunGameRules().InfiniteReserve, Is.True);
            Assert.That(GameRulesAssetBuilder.FreeForAllRules().DeathCamera, Is.EqualTo(DeathCamera.Killer), "there is no ally to watch");

            foreach (var name in new[] { GameRulesAssetBuilder.FreeForAllName, GameRulesAssetBuilder.KillConfirmedName, GameRulesAssetBuilder.HardpointName,
                         GameRulesAssetBuilder.GuardianName, GameRulesAssetBuilder.ExecutionName, GameRulesAssetBuilder.SearchAndDestroyName })
            {
                var rules = GameRulesAssetBuilder.ModeRules(name);
                Assert.That(rules.InfiniteReserve, Is.False, name);
                Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f), name);
            }

            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                var rules = GameRulesAssetBuilder.ModeRules(name);
                Assert.That(rules.Destruction, Is.EqualTo(Destruction.On), name);
                Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off), name);
                Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Full), name);
            }
        }

        private static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> list)
        {
            var array = new string[list.Count];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = list[i];
            }

            return array;
        }

        private GameRulesDefinition[] Build()
        {
            return GameRulesAssetBuilder.BuildAt(
                _teamDeathmatchPath, _campaignEasyPath, _campaignNormalPath, _campaignHardPath, _rangePath, _tpsRangePath, _dominationPath);
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
