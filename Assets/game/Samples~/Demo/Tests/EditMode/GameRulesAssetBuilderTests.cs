using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // リポジトリの Assets/game/Rules を触らないよう、一時フォルダに走らせる。
    public sealed class GameRulesAssetBuilderTests
    {
        private const int TpsRangeIndex = 5;
        private const int DominationIndex = 6;

        // BuildAt と同じ並び (teamDeathmatch, campaignEasy, campaignNormal, campaignHard, range, tpsRange, domination)。
        private static readonly ExpectedActions[] _expectedActions =
        {
            new ExpectedActions(
                Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On, grenade: Grenade.On, grenadeCount: GameRulesAssetBuilder.TeamDeathmatchGrenadeCount, melee: Melee.On),
            new ExpectedActions(Downed.Off, Cover.Off, BlindFire.Off, ActiveReload.On, melee: Melee.On),
            new ExpectedActions(Downed.Off, Cover.Off, BlindFire.Off, ActiveReload.On, melee: Melee.On),
            new ExpectedActions(Downed.Off, Cover.Off, BlindFire.Off, ActiveReload.On, melee: Melee.On),
            new ExpectedActions(Downed.Off, Cover.Off, BlindFire.Off, ActiveReload.On, melee: Melee.On),
            new ExpectedActions(
                Downed.Off, Cover.On, BlindFire.On, ActiveReload.On, Dodge.On, Vault.On, Grenade.On, GameRulesAssetBuilder.TpsRangeGrenadeCount, Melee.Off),
            new ExpectedActions(
                Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On, grenade: Grenade.On, grenadeCount: GameRulesAssetBuilder.TeamDeathmatchGrenadeCount, melee: Melee.On),
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
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchPath, Is.EqualTo(DemoSample.Root + "/Rules/TeamDeathmatch.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignEasyPath, Is.EqualTo(DemoSample.Root + "/Rules/CampaignEasy.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignNormalPath, Is.EqualTo(DemoSample.Root + "/Rules/CampaignNormal.asset"));
            Assert.That(GameRulesAssetBuilder.CampaignHardPath, Is.EqualTo(DemoSample.Root + "/Rules/CampaignHard.asset"));
            Assert.That(GameRulesAssetBuilder.RangePath, Is.EqualTo(DemoSample.Root + "/Rules/Range.asset"));
            Assert.That(GameRulesAssetBuilder.TpsRangePath, Is.EqualTo(DemoSample.Root + "/Rules/TpsRange.asset"));
            Assert.That(GameRulesAssetBuilder.DominationPath, Is.EqualTo(DemoSample.Root + "/Rules/Domination.asset"));
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
            Assert.That(rules.MaxWeapons, Is.EqualTo(range.MaxWeapons));
            Assert.That(rules.ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            AssertActions(rules.Actions, _expectedActions[TpsRangeIndex], _tpsRangePath);
        }

        [Test]
        public void OnlyTheTpsRangeIsThirdPerson()
        {
            Assert.That(GameRulesAssetBuilder.TpsRangeRules().ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(GameRulesAssetBuilder.CampaignEasyRules().ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(GameRulesAssetBuilder.CampaignNormalRules().ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(GameRulesAssetBuilder.CampaignHardRules().ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(GameRulesAssetBuilder.RangeRules().ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(GameRulesAssetBuilder.DominationRules().ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            foreach (var mode in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(mode).ViewMode, Is.EqualTo(ViewMode.FirstPerson), mode);
            }
        }

        [Test]
        public void OnlyTheCampaignLeavesOutStreaks()
        {
            Assert.That(GameRulesAssetBuilder.CampaignEasyRules().Streaks, Is.EqualTo(Streaks.Off));
            Assert.That(GameRulesAssetBuilder.CampaignNormalRules().Streaks, Is.EqualTo(Streaks.Off));
            Assert.That(GameRulesAssetBuilder.CampaignHardRules().Streaks, Is.EqualTo(Streaks.Off));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().Streaks, Is.EqualTo(Streaks.On));
            Assert.That(GameRulesAssetBuilder.RangeRules().Streaks, Is.EqualTo(Streaks.On));
            Assert.That(GameRulesAssetBuilder.TpsRangeRules().Streaks, Is.EqualTo(Streaks.On));
            Assert.That(GameRulesAssetBuilder.DominationRules().Streaks, Is.EqualTo(Streaks.On));
            Assert.That(GameRulesAssetBuilder.FeatureBaseRules().Streaks, Is.EqualTo(Streaks.On));
            foreach (var mode in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(mode).Streaks, Is.EqualTo(Streaks.On), mode);
            }
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

            // オンラインの試合の勝敗を拠点で決めるのはドミネーションだけ
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(teamDeathmatch.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(BuildAndLoad(_teamDeathmatchPath).Objective, Is.EqualTo(MatchObjective.None));
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
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Fallen));
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
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Fallen));
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
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Fallen));
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
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Fallen));
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
        public void FirstPersonCampaignsActiveReloadWithoutCoverBlindFireOrDowned()
        {
            var campaigns = new[]
            {
                GameRulesAssetBuilder.CampaignEasyRules(),
                GameRulesAssetBuilder.CampaignNormalRules(),
                GameRulesAssetBuilder.CampaignHardRules(),
            };

            foreach (var rules in campaigns)
            {
                Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.Off));
                Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.Off));
                Assert.That(rules.Actions.ActiveReload, Is.EqualTo(ActiveReload.On));
                Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Off));
            }
        }

        [Test]
        public void FirstPersonRangeActiveReloadsWithoutCoverOrBlindFire()
        {
            var actions = GameRulesAssetBuilder.RangeRules().Actions;

            Assert.That(actions.Cover, Is.EqualTo(Cover.Off));
            Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.Off));
            Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.On));
            Assert.That(actions.Downed, Is.EqualTo(Downed.Off));
        }

        [Test]
        public void BothRangesLetThePlayerEmoteAndRunOutOfStamina()
        {
            foreach (var rules in new[] { GameRulesAssetBuilder.RangeRules(), GameRulesAssetBuilder.TpsRangeRules() })
            {
                Assert.That(rules.Actions.Emote, Is.EqualTo(Emote.On));
                Assert.That(rules.Actions.Stamina, Is.EqualTo(Stamina.On));
            }

            Assert.That(GameRulesAssetBuilder.TeamDeathmatchActions().Emote, Is.EqualTo(Emote.Off), "online players do not emote");
            Assert.That(GameRulesAssetBuilder.CampaignActions().Stamina, Is.EqualTo(Stamina.Off));
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
            Assert.That(rules.Actions.Mantle, Is.EqualTo(Mantle.On));
            Assert.That(rules.Actions.Grenade, Is.EqualTo(Grenade.On));
            Assert.That(rules.Actions.GrenadeCount, Is.GreaterThan(0), "grenades are on, so there must be some to throw");
            Assert.That(rules.Actions.Prone, Is.EqualTo(Prone.On));
            Assert.That(rules.Actions.Stealth, Is.EqualTo(Stealth.On), "the range has a stealth area to try the detection meter in");
            Assert.That(rules.Actions.Slide, Is.EqualTo(Slide.On));
            Assert.That(rules.Actions.Stagger, Is.EqualTo(Stagger.On));
            Assert.That(rules.Actions.Suppression, Is.EqualTo(Suppression.On));
        }

        [Test]
        public void OnlyTheTpsRangeTurnsTheJumpOffAndKeepsVaultAndMantleToClimb()
        {
            var tps = GameRulesAssetBuilder.TpsRangeActions();

            Assert.That(tps.Jump, Is.EqualTo(Jump.Off));
            Assert.That(tps.Vault, Is.EqualTo(Vault.On), "without a jump, low walls and platforms are climbed by vaulting");
            Assert.That(tps.Mantle, Is.EqualTo(Mantle.On), "without a jump, high ledges are climbed by mantling");
            Assert.That(GameRulesAssetBuilder.RangeActions().Jump, Is.EqualTo(Jump.On));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchActions().Jump, Is.EqualTo(Jump.On));
            Assert.That(GameRulesAssetBuilder.CampaignActions().Jump, Is.EqualTo(Jump.On));
        }

        [Test]
        public void OnlyTheTpsRangeSlides()
        {
            Assert.That(GameRulesAssetBuilder.RangeActions().Slide, Is.EqualTo(Slide.Off));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchActions().Slide, Is.EqualTo(Slide.Off));
        }

        [Test]
        public void OnlyTheTpsRangeTurnsSuppressionOn()
        {
            Assert.That(GameRulesAssetBuilder.RangeActions().Suppression, Is.EqualTo(Suppression.Off));
            Assert.That(GameRulesAssetBuilder.CampaignActions().Suppression, Is.EqualTo(Suppression.Off));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchActions().Suppression, Is.EqualTo(Suppression.Off));
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
            Assert.That(actual.Melee, Is.EqualTo(expected.Melee), message);
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
            public readonly Melee Melee;

            public ExpectedActions(
                Downed downed,
                Cover cover,
                BlindFire blindFire,
                ActiveReload activeReload,
                Dodge dodge = Dodge.Off,
                Vault vault = Vault.Off,
                Grenade grenade = Grenade.Off,
                int grenadeCount = 0,
                Melee melee = Melee.Off)
            {
                Downed = downed;
                Cover = cover;
                BlindFire = blindFire;
                ActiveReload = activeReload;
                Dodge = dodge;
                Vault = vault;
                Grenade = grenade;
                GrenadeCount = grenadeCount;
                Melee = melee;
            }
        }

        // TeamDeathmatch (0) と、その値を引き継ぐ Domination だけが出現直後に無敵になる
        private static float ExpectedSpawnProtection(int index)
        {
            return index == 0 || index == DominationIndex ? GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds : 0f;
        }

        [Test]
        public void EveryDemoModeHoldsTwoWeapons()
        {
            Assert.That(GameRulesAssetBuilder.DemoMaxWeapons, Is.EqualTo(2));

            foreach (var definition in Build())
            {
                Assert.That(definition.ToRules().MaxWeapons, Is.EqualTo(2), definition.name);
            }

            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(name).MaxWeapons, Is.EqualTo(2), name);
            }
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
        public void TheRepositoryBattleRoyaleAssetOpensTheParachute()
        {
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.BattleRoyalePath).ToRules();
            Assert.That(
                rules.Actions.Parachute,
                Is.EqualTo(Parachute.On),
                GameRulesAssetBuilder.BattleRoyalePath + $" (rerun {nameof(GameRulesAssetBuilder)}.Build)");
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
        public void ModeNamesAreTheThirteenModesWithPathsUnderGameRules()
        {
            Assert.That(
                GameRulesAssetBuilder.ModeNames,
                Is.EqualTo(new[]
                {
                    "FreeForAll", "KillConfirmed", "GunGame", "Hardpoint", "Guardian", "Execution", "SearchAndDestroy", "CaptureTheFlag", "Horde",
                    "BattleRoyale", "Infected", "Extraction", "Payload",
                }));
            Assert.That(GameRulesAssetBuilder.FreeForAllPath, Is.EqualTo(DemoSample.Root + "/Rules/FreeForAll.asset"));
            Assert.That(GameRulesAssetBuilder.CaptureTheFlagPath, Is.EqualTo(DemoSample.Root + "/Rules/CaptureTheFlag.asset"));
            Assert.That(GameRulesAssetBuilder.BattleRoyalePath, Is.EqualTo(DemoSample.Root + "/Rules/BattleRoyale.asset"));
            Assert.That(GameRulesAssetBuilder.InfectedPath, Is.EqualTo(DemoSample.Root + "/Rules/Infected.asset"));
            Assert.That(GameRulesAssetBuilder.ExtractionPath, Is.EqualTo(DemoSample.Root + "/Rules/Extraction.asset"));
            Assert.That(GameRulesAssetBuilder.PayloadPath, Is.EqualTo(DemoSample.Root + "/Rules/Payload.asset"));
            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRulesPath(name), Is.EqualTo(DemoSample.Root + "/Rules/" + name + ".asset"));
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
            Assert.That(GameRulesAssetBuilder.ExtractionRules().RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(GameRulesAssetBuilder.HordeRules().RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(GameRulesAssetBuilder.HordeRules().WaveIntervalSeconds, Is.EqualTo(GameRulesAssetBuilder.HordeWaveIntervalSeconds));

            foreach (var name in new[] { GameRulesAssetBuilder.FreeForAllName, GameRulesAssetBuilder.KillConfirmedName, GameRulesAssetBuilder.GunGameName,
                         GameRulesAssetBuilder.HardpointName, GameRulesAssetBuilder.GuardianName, GameRulesAssetBuilder.CaptureTheFlagName,
                         GameRulesAssetBuilder.InfectedName, GameRulesAssetBuilder.PayloadName })
            {
                var rules = GameRulesAssetBuilder.ModeRules(name);
                Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual), name);
                Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchSpawnProtectionSeconds), name);
            }

            Assert.That(GameRulesAssetBuilder.ExecutionRules().SpawnProtectionSeconds, Is.Zero, "nobody respawns, so nobody needs protecting");
            Assert.That(GameRulesAssetBuilder.SearchAndDestroyRules().SpawnProtectionSeconds, Is.Zero);
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().SpawnProtectionSeconds, Is.Zero);
            Assert.That(GameRulesAssetBuilder.ExtractionRules().SpawnProtectionSeconds, Is.Zero);
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
            Assert.That(GameRulesAssetBuilder.CaptureTheFlagRules().Actions.Downed, Is.EqualTo(Downed.Off), "a shot carrier drops the flag at once");
            Assert.That(GameRulesAssetBuilder.InfectedRules().Actions.Downed, Is.EqualTo(Downed.Off), "a fallen survivor turns at once");
            Assert.That(GameRulesAssetBuilder.ExtractionRules().Actions.Downed, Is.EqualTo(Downed.Off), "nobody is there to revive a raider");
            Assert.That(
                GameRulesAssetBuilder.CaptureTheFlagRules().Actions.GrenadeCount,
                Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchGrenadeCount),
                "only downing differs from team deathmatch");

            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                var actions = GameRulesAssetBuilder.ModeRules(name).Actions;
                Assert.That(actions.Cover, Is.EqualTo(Cover.Off), name + ": the online player does not support cover");
                Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.Off), name);
                Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.On), name);
            }
        }

        [Test]
        public void EveryDemoRulesSwingsMeleeExceptTheTpsRange()
        {
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.DominationRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.CampaignEasyRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.CampaignNormalRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.CampaignHardRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.RangeRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.PlaygroundRules().Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesAssetBuilder.HighDropRules().Actions.Melee, Is.EqualTo(Melee.On));
            foreach (var name in GameRulesAssetBuilder.ModeNames)
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(name).Actions.Melee, Is.EqualTo(Melee.On), name);
            }

            Assert.That(GameRulesAssetBuilder.TpsRangeRules().Actions.Melee, Is.EqualTo(Melee.Off), "the melee smoke steps check the refusal while Off");
        }

        [Test]
        public void HordeAndBattleRoyaleCarryMoreReserveAmmoAndGunGameNeverRunsOut()
        {
            Assert.That(GameRulesAssetBuilder.HordeRules().ReserveAmmoMultiplier, Is.GreaterThan(1f));
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().ReserveAmmoMultiplier, Is.GreaterThan(1f));
            Assert.That(GameRulesAssetBuilder.GunGameRules().InfiniteReserve, Is.True);
            Assert.That(GameRulesAssetBuilder.GunGameRules().Actions.Melee, Is.EqualTo(Melee.On), "a melee kill sends the victim back a tier");
            Assert.That(GameRulesAssetBuilder.FreeForAllRules().DeathCamera, Is.EqualTo(DeathCamera.Killer), "there is no ally to watch");

            foreach (var name in new[] { GameRulesAssetBuilder.FreeForAllName, GameRulesAssetBuilder.KillConfirmedName, GameRulesAssetBuilder.HardpointName,
                         GameRulesAssetBuilder.GuardianName, GameRulesAssetBuilder.ExecutionName, GameRulesAssetBuilder.SearchAndDestroyName,
                         GameRulesAssetBuilder.CaptureTheFlagName, GameRulesAssetBuilder.InfectedName, GameRulesAssetBuilder.ExtractionName,
                         GameRulesAssetBuilder.PayloadName })
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

        [Test]
        public void PayloadIsTeamDeathmatchAimedAtThePayload()
        {
            var payload = GameRulesAssetBuilder.PayloadRules();
            var teamDeathmatch = GameRulesAssetBuilder.TeamDeathmatchRules();

            Assert.That(payload.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(GameRulesAssetBuilder.ModeRules(GameRulesAssetBuilder.PayloadName).Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(payload.RespawnMode, Is.EqualTo(RespawnMode.Individual), "pushers come back to the cart");
            Assert.That(payload.SpawnProtectionSeconds, Is.EqualTo(teamDeathmatch.SpawnProtectionSeconds));
            Assert.That(payload.IdleKickSeconds, Is.EqualTo(GameRulesAssetBuilder.OnlineIdleKickSeconds));
            Assert.That(payload.MaxWeapons, Is.EqualTo(GameRulesAssetBuilder.DemoMaxWeapons));
            AssertActions(payload.Actions, _expectedActions[0], nameof(GameRulesAssetBuilder.PayloadRules));

            foreach (var name in GameRulesAssetBuilder.ModeNames.Where(name => name != GameRulesAssetBuilder.PayloadName))
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(name).Objective, Is.Not.EqualTo(MatchObjective.Payload), name);
            }
        }

        [Test]
        public void GuardianIsTeamDeathmatchWithAbilitiesAndRespawnsAtABeaconFirst()
        {
            var guardian = GameRulesAssetBuilder.GuardianRules();
            var teamDeathmatch = GameRulesAssetBuilder.TeamDeathmatchRules();

            Assert.That(GameRulesAssetBuilder.GuardianRespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Beacon, RespawnAnchor.Base }));
            Assert.That(guardian.Actions.Abilities, Is.EqualTo(Abilities.On), "the beacon sits in an ability slot");
            Assert.That(guardian.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Beacon, RespawnAnchor.Base }));
            Assert.That(guardian.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(guardian.SpawnProtectionSeconds, Is.EqualTo(teamDeathmatch.SpawnProtectionSeconds));
            Assert.That(guardian.Objective, Is.EqualTo(teamDeathmatch.Objective));
            Assert.That(guardian.Actions.Downed, Is.EqualTo(teamDeathmatch.Actions.Downed));
            Assert.That(guardian.Actions.Melee, Is.EqualTo(teamDeathmatch.Actions.Melee));
            Assert.That(guardian.Actions.GrenadeCount, Is.EqualTo(teamDeathmatch.Actions.GrenadeCount));
            Assert.That(GameRulesAssetBuilder.ModeRules(GameRulesAssetBuilder.GuardianName).RespawnAnchors, Is.EqualTo(guardian.RespawnAnchors));
        }

        [Test]
        public void OnlyGuardianTurnsAbilitiesOnOrUsesTheBeacon()
        {
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().Actions.Abilities, Is.EqualTo(Abilities.Off));
            Assert.That(GameRulesAssetBuilder.TeamDeathmatchRules().RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Base }));

            foreach (var name in GameRulesAssetBuilder.ModeNames.Where(name => name != GameRulesAssetBuilder.GuardianName))
            {
                var rules = GameRulesAssetBuilder.ModeRules(name);
                Assert.That(rules.Actions.Abilities, Is.EqualTo(Abilities.Off), name);
                Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Base }), name);
            }
        }

        [Test]
        public void TheBuiltGuardianAssetKeepsTheBeaconAnchorAndARerunRestoresIt()
        {
            var built = GameRulesAssetBuilder.BuildModesAt(_folder);
            var guardian = built[Array.IndexOf(ToArray(GameRulesAssetBuilder.ModeNames), GameRulesAssetBuilder.GuardianName)];

            Assert.That(guardian.ToRules().RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Beacon, RespawnAnchor.Base }));
            Assert.That(guardian.ToRules().Actions.Abilities, Is.EqualTo(Abilities.On));

            guardian.RespawnAnchors = new[] { RespawnAnchor.Base };
            EditorUtility.SetDirty(guardian);
            AssetDatabase.SaveAssets();
            GameRulesAssetBuilder.BuildModesAt(_folder);

            Assert.That(guardian.ToRules().RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Beacon, RespawnAnchor.Base }));
        }

        [Test]
        public void BattleRoyaleAndExtractionNameTheirObjectiveForTheDedicatedServer()
        {
            Assert.That(GameRulesAssetBuilder.BattleRoyaleRules().Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(GameRulesAssetBuilder.ExtractionRules().Objective, Is.EqualTo(MatchObjective.Extraction));
            Assert.That(
                GameRulesAssetBuilder.ModeRules(GameRulesAssetBuilder.BattleRoyaleName).Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(GameRulesAssetBuilder.ModeRules(GameRulesAssetBuilder.ExtractionName).Objective, Is.EqualTo(MatchObjective.Extraction));

            var built = GameRulesAssetBuilder.BuildModesAt(_folder);
            var names = ToArray(GameRulesAssetBuilder.ModeNames);
            Assert.That(
                built[Array.IndexOf(names, GameRulesAssetBuilder.BattleRoyaleName)].ToRules().Objective,
                Is.EqualTo(MatchObjective.BattleRoyale),
                "the objective survives the asset round trip");
            Assert.That(built[Array.IndexOf(names, GameRulesAssetBuilder.ExtractionName)].ToRules().Objective, Is.EqualTo(MatchObjective.Extraction));
        }

        [Test]
        public void BattleRoyaleOpensTheParachuteForRedeploysButOnlyThere()
        {
            var battleRoyale = GameRulesAssetBuilder.BattleRoyaleRules();

            Assert.That(battleRoyale.Actions.Parachute, Is.EqualTo(Parachute.On), "a redeployed player drops in by parachute");
            Assert.That(GameRulesAssetBuilder.BattleRoyaleActions().Parachute, Is.EqualTo(Parachute.On));
            Assert.That(battleRoyale.RespawnMode, Is.EqualTo(RespawnMode.None), "only the runner's redeploy brings a player back");
            Assert.That(battleRoyale.Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(battleRoyale.Actions.GrenadeCount, Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchGrenadeCount));

            foreach (var name in GameRulesAssetBuilder.ModeNames.Where(name => name != GameRulesAssetBuilder.BattleRoyaleName))
            {
                Assert.That(GameRulesAssetBuilder.ModeRules(name).Actions.Parachute, Is.EqualTo(Parachute.Off), name);
            }
        }

        [Test]
        public void SearchAndDestroyLeavesRoomForTheBoughtWeaponsAndThrowables()
        {
            var rules = GameRulesAssetBuilder.SearchAndDestroyRules();
            var asset = Resources.Load<TextAsset>(EconomyJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/" + EconomyJson.ResourcePath + ".json is bundled");
            var economy = EconomyJson.Parse(asset.text, EconomyJson.ResourcePath);
            Resources.UnloadAsset(asset);

            Assert.That(rules.InfiniteReserve, Is.False, "ammo is worth buying");
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
            Assert.That(rules.Actions.Grenade, Is.EqualTo(Grenade.On), "throwables are sold");
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.None), "a fallen player loses the bought weapons until the next round");
            foreach (var item in economy.Catalog.Items.Where(item => item.Kind == BuyItemKind.Weapon))
            {
                Assert.That(item.Slot, Is.LessThan(rules.MaxWeapons), $"{item.Id} goes to slot {item.Slot}");
            }
        }

        [Test]
        public void BuildModesAtWritesThePayloadRules()
        {
            var built = GameRulesAssetBuilder.BuildModesAt(_folder);
            var payload = built[Array.IndexOf(ToArray(GameRulesAssetBuilder.ModeNames), GameRulesAssetBuilder.PayloadName)];

            Assert.That(AssetDatabase.GetAssetPath(payload), Is.EqualTo(_folder + "/Payload.asset"));
            Assert.That(payload.ToRules().Objective, Is.EqualTo(MatchObjective.Payload));

            var battleRoyale = built[Array.IndexOf(ToArray(GameRulesAssetBuilder.ModeNames), GameRulesAssetBuilder.BattleRoyaleName)];
            Assert.That(battleRoyale.ToRules().Actions.Parachute, Is.EqualTo(Parachute.On), "the parachute survives the asset round trip");
        }

        [Test]
        public void PlaygroundTurnsOnExactlyItsTogglesInThirdPerson()
        {
            var rules = GameRulesAssetBuilder.PlaygroundRules();
            var baseRules = GameRulesAssetBuilder.FeatureBaseRules();

            foreach (var field in GameRulesDebugDraft.Fields.Where(GameRulesDebugDraft.IsToggle))
            {
                var expected = GameRulesAssetBuilder.PlaygroundToggles.Contains(field) ? 1 : GameRulesDebugDraft.Read(baseRules, field);
                Assert.That(GameRulesDebugDraft.Read(rules, field), Is.EqualTo(expected), field.ToString());
            }

            Assert.That(GameRulesAssetBuilder.PlaygroundToggles, Has.Count.EqualTo(21));
            Assert.That(GameRulesAssetBuilder.PlaygroundToggles, Has.No.Member(GameRulesDebugField.AimToFire), "aiming to fire would tie up the other moves");
            Assert.That(GameRulesAssetBuilder.PlaygroundToggles, Has.No.Member(GameRulesDebugField.Downed), "a solo match never downs the player");
            Assert.That(rules.ViewMode, Is.EqualTo(ViewMode.ThirdPerson), "cover is shown in third person");
            Assert.That(rules.Actions.GrenadeCount, Is.EqualTo(GameRulesDebugDraft.DefaultGrenadeCount));
            Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off), "the double jump steps do not hurt");
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Fallen));
        }

        [Test]
        public void HighDropTurnsOnTheParachuteAndTheFallDamageInFirstPerson()
        {
            var rules = GameRulesAssetBuilder.HighDropRules();

            Assert.That(GameRulesAssetBuilder.HighDropToggles, Is.EqualTo(new[]
            {
                GameRulesDebugField.Parachute,
                GameRulesDebugField.FallDamage,
                GameRulesDebugField.Ladder,
                GameRulesDebugField.Jump,
                GameRulesDebugField.Melee,
            }));
            Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.On));
            Assert.That(rules.Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(GameRulesDebugDraft.Read(rules, GameRulesDebugField.Parachute), Is.EqualTo(1));
            Assert.That(GameRulesDebugDraft.Read(rules, GameRulesDebugField.Ladder), Is.EqualTo(1));
            Assert.That(GameRulesDebugDraft.Read(rules, GameRulesDebugField.Cover), Is.EqualTo(0));
            Assert.That(rules.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void FeatureBaseRulesKeepEveryToggleAtTheKitDefault()
        {
            var baseRules = GameRulesAssetBuilder.FeatureBaseRules();

            foreach (var field in GameRulesDebugDraft.Fields.Where(GameRulesDebugDraft.IsToggle))
            {
                Assert.That(GameRulesDebugDraft.Read(baseRules, field), Is.EqualTo(GameRulesDebugDraft.Read(GameRules.Default, field)), field.ToString());
            }

            Assert.That(baseRules.InfiniteReserve, Is.False);
            Assert.That(baseRules.Destruction, Is.EqualTo(Destruction.Off));
            Assert.That(baseRules.FallDamage, Is.EqualTo(FallDamage.Off));
            Assert.That(baseRules.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(baseRules.Actions.Jump, Is.EqualTo(Jump.On), "the kit default keeps jumping on");
        }

        [Test]
        public void FeatureRulesPathIsNamedAfterTheScene()
        {
            Assert.That(GameRulesAssetBuilder.FeatureRulesPath("Playground"), Is.EqualTo(DemoSample.Root + "/Rules/Features/Playground.asset"));
            Assert.Throws<ArgumentException>(() => GameRulesAssetBuilder.FeatureRulesPath(" "));
        }

        [Test]
        public void LoadOrBuildFeatureRulesCreatesOnceAndKeepsEditedValues()
        {
            var path = _folder + "/Playground.asset";

            var built = GameRulesAssetBuilder.LoadOrBuildFeatureRules(path, GameRulesAssetBuilder.PlaygroundRules());
            Assert.That(built, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path)));
            Assert.That(built.ToRules().Actions.Zipline, Is.EqualTo(Zipline.On));

            GameRulesAssetBuilder.Configure(built, GameRulesAssetBuilder.PlaygroundRules().WithFallDamage(FallDamage.On));
            var loaded = GameRulesAssetBuilder.LoadOrBuildFeatureRules(path, GameRulesAssetBuilder.PlaygroundRules());

            Assert.That(loaded, Is.SameAs(built));
            Assert.That(loaded.ToRules().FallDamage, Is.EqualTo(FallDamage.On), "an existing asset is not overwritten");
            Assert.Throws<ArgumentNullException>(() => GameRulesAssetBuilder.LoadOrBuildFeatureRules(path, null));
        }

        [Test]
        public void LoadOrBuildRulesAtCreatesAMissingModeOnceAndKeepsEditedValues()
        {
            var path = _folder + "/" + GameRulesAssetBuilder.CaptureTheFlagName + ".asset";

            var built = GameRulesAssetBuilder.LoadOrBuildRulesAt(_folder, path);
            Assert.That(built, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path)));
            Assert.That(built.ToRules().Actions.Downed, Is.EqualTo(Downed.Off), "CaptureTheFlagActions turns downed off");

            GameRulesAssetBuilder.Configure(built, GameRulesAssetBuilder.CaptureTheFlagRules().WithFallDamage(FallDamage.On));
            var loaded = GameRulesAssetBuilder.LoadOrBuildRulesAt(_folder, path);

            Assert.That(loaded, Is.SameAs(built));
            Assert.That(loaded.ToRules().FallDamage, Is.EqualTo(FallDamage.On), "an existing asset is not overwritten");
        }

        [Test]
        public void LoadOrBuildRulesAtRefusesAMissingPathThatIsNotAMode()
        {
            var path = _folder + "/Unknown.asset";

            var error = Assert.Throws<InvalidOperationException>(() => GameRulesAssetBuilder.LoadOrBuildRulesAt(_folder, path));

            Assert.That(error.Message, Does.Contain(GameRulesAssetBuilder.MenuPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path), Is.Null);
        }

        [Test]
        public void ModeNameOfFindsTheModeByItsPathInTheFolder()
        {
            Assert.That(GameRulesAssetBuilder.ModeNameOf(_folder, _folder + "/CaptureTheFlag.asset"), Is.EqualTo("CaptureTheFlag"));
            Assert.That(GameRulesAssetBuilder.ModeNameOf(_folder + "/", _folder + "/Horde.asset"), Is.EqualTo("Horde"));
            Assert.That(GameRulesAssetBuilder.ModeNameOf(_folder, _folder + "/TeamDeathmatch.asset"), Is.Null, "TDM is built by BuildAt");
            Assert.That(GameRulesAssetBuilder.ModeNameOf(_folder, _folder + "/Sub/Horde.asset"), Is.Null);
            Assert.That(GameRulesAssetBuilder.ModeNameOf(_folder, _folder + "/horde.asset"), Is.Null, "names are case sensitive");
            Assert.That(GameRulesAssetBuilder.ModeNameOf(GameRulesAssetBuilder.Folder, GameRulesAssetBuilder.CaptureTheFlagPath), Is.EqualTo("CaptureTheFlag"));
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
