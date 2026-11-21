using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GameContentTests
    {
        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;

        [SetUp]
        public void SetUp()
        {
            _rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _handgun = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            _handgun.name = "Handgun";
            WeaponAssets.Configure(_handgun, TestWeapons.HandgunSpec());
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void SwappingTheEntrySwapsTheRulesAndWeaponsOfAnUnwiredBootstrap()
        {
            GameContent.Use(Content("A", regenPerSecond: 10f, weapon: _rifle));
            var first = BuildUnwiredBootstrap();
            try
            {
                Assert.That(first.Rules.RegenPerSecond, Is.EqualTo(10f));
                Assert.That(first.Weapon.Definitions, Is.EqualTo(new[] { _rifle }));
            }
            finally
            {
                DestroyWorld(first);
            }

            GameContent.Use(Content("B", regenPerSecond: 25f, weapon: _handgun));
            var second = BuildUnwiredBootstrap();
            try
            {
                Assert.That(second.Rules.RegenPerSecond, Is.EqualTo(25f));
                Assert.That(second.Weapon.Definitions, Is.EqualTo(new[] { _handgun }));
            }
            finally
            {
                DestroyWorld(second);
            }
        }

        [Test]
        public void SwappingTheEntrySwapsTheModeValuesOfUnwiredRunners()
        {
            var deathmatch = Track(new GameObject("Deathmatch")).AddComponent<TeamDeathmatchRunner>();
            var domination = Track(new GameObject("Domination")).AddComponent<DominationRunner>();
            var killConfirmed = Track(new GameObject("KillConfirmed")).AddComponent<KillConfirmedRunner>();

            GameContent.Use(Content("A", scoreLimit: 30));
            Assert.That(deathmatch.CreateSpec().ScoreLimit, Is.EqualTo(30));
            Assert.That(domination.CreateSpec(3).ScoreLimit, Is.EqualTo(30));
            Assert.That(killConfirmed.EffectiveDefinition.name, Is.EqualTo("A-KillConfirmed"));

            GameContent.Use(Content("B", scoreLimit: 75));
            Assert.That(deathmatch.CreateSpec().ScoreLimit, Is.EqualTo(75));
            Assert.That(domination.CreateSpec(3).ScoreLimit, Is.EqualTo(75));
            Assert.That(killConfirmed.EffectiveDefinition.name, Is.EqualTo("B-KillConfirmed"));
        }

        [Test]
        public void AWiredDefinitionWinsOverTheEntry()
        {
            var runner = Track(new GameObject("Deathmatch")).AddComponent<TeamDeathmatchRunner>();
            var wired = Track(ScriptableObject.CreateInstance<TeamDeathmatchDefinition>());
            wired.ScoreLimit = 5;
            runner.Definition = wired;

            GameContent.Use(Content("A", scoreLimit: 30));

            Assert.That(runner.EffectiveDefinition, Is.SameAs(wired));
            Assert.That(runner.CreateSpec().ScoreLimit, Is.EqualTo(5));
        }

        [Test]
        public void TheEntryProvidesMovementMenuScenesTuningAndCatalogs()
        {
            var content = Content("A");
            content.Motor = Track(ScriptableObject.CreateInstance<MotorSettings>());
            content.Motor.WalkSpeed = 2.5f;
            content.MenuScenes = Track(ScriptableObject.CreateInstance<MenuSceneSettings>());
            content.MenuScenes.Campaign = "Chapter1";
            content.GrenadeTuning = Track(new TextAsset("{ \"damageRadius\": 9 }"));
            content.Presentation = Track(ScriptableObject.CreateInstance<PresentationCatalog>());
            content.CameraShake = Track(ScriptableObject.CreateInstance<CameraShakeSettings>());
            content.DamageNumbers = Track(ScriptableObject.CreateInstance<DamageNumberCatalog>());
            content.AimBlock = Track(ScriptableObject.CreateInstance<AimBlockCatalog>());
            GameContent.Use(content);

            Assert.That(MotorSettings.Resolve(null).WalkSpeed, Is.EqualTo(2.5f));
            Assert.That(MenuSceneSettings.LoadScenes().Campaign, Is.EqualTo("Chapter1"));
            Assert.That(KitTuning.Grenades.Carried[0].DamageRadius, Is.EqualTo(9f));
            Assert.That(PresentationCatalog.Load(), Is.SameAs(content.Presentation));
            Assert.That(CameraShakeSettings.Load(), Is.SameAs(content.CameraShake));
            Assert.That(DamageNumberCatalog.Load(), Is.SameAs(content.DamageNumbers));
            Assert.That(AimBlockCatalog.Load(), Is.SameAs(content.AimBlock));
        }

        [Test]
        public void TheReticleCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.Reticles, Is.Null);
            Assert.That(ReticleCatalog.Load(), Is.SameAs(Resources.Load<ReticleCatalog>(ReticleCatalog.ResourcePath)));

            content.Reticles = Track(ScriptableObject.CreateInstance<ReticleCatalog>());
            Assert.That(ReticleCatalog.Load(), Is.SameAs(content.Reticles));

            content.Reticles = null;
            Assert.That(ReticleCatalog.Load(), Is.SameAs(Resources.Load<ReticleCatalog>(ReticleCatalog.ResourcePath)));
        }

        [Test]
        public void TheStatusEffectVisualCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.StatusEffectVisuals, Is.Null);
            Assert.That(StatusEffectVisualCatalog.Load(), Is.SameAs(Resources.Load<StatusEffectVisualCatalog>(StatusEffectVisualCatalog.ResourcePath)));

            content.StatusEffectVisuals = Track(ScriptableObject.CreateInstance<StatusEffectVisualCatalog>());
            Assert.That(StatusEffectVisualCatalog.Load(), Is.SameAs(content.StatusEffectVisuals));

            content.StatusEffectVisuals = null;
            Assert.That(StatusEffectVisualCatalog.Load(), Is.SameAs(Resources.Load<StatusEffectVisualCatalog>(StatusEffectVisualCatalog.ResourcePath)));
        }

        [Test]
        public void TheFlashOverlayCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.FlashOverlay, Is.Null);
            Assert.That(FlashOverlayCatalog.Load(), Is.SameAs(Resources.Load<FlashOverlayCatalog>(FlashOverlayCatalog.ResourcePath)));

            content.FlashOverlay = Track(ScriptableObject.CreateInstance<FlashOverlayCatalog>());
            Assert.That(FlashOverlayCatalog.Load(), Is.SameAs(content.FlashOverlay));

            content.FlashOverlay = null;
            Assert.That(FlashOverlayCatalog.Load(), Is.SameAs(Resources.Load<FlashOverlayCatalog>(FlashOverlayCatalog.ResourcePath)));
        }

        [Test]
        public void FillEmptyFillsTheReticleStatusEffectAndFlashCatalogs()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            var reticles = Track(ScriptableObject.CreateInstance<ReticleCatalog>());
            var statusEffects = Track(ScriptableObject.CreateInstance<StatusEffectVisualCatalog>());
            var flashA = Track(ScriptableObject.CreateInstance<FlashOverlayCatalog>());
            var flashB = Track(ScriptableObject.CreateInstance<FlashOverlayCatalog>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(ReticleCatalog), new Object[] { reticles } },
                { typeof(StatusEffectVisualCatalog), new Object[] { statusEffects } },
                { typeof(FlashOverlayCatalog), new Object[] { flashA, flashB } },
            };

            var left = GameContentAssets.FillEmpty(content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(content.Reticles, Is.SameAs(reticles));
            Assert.That(content.StatusEffectVisuals, Is.SameAs(statusEffects));
            Assert.That(content.FlashOverlay, Is.Null);
            Assert.That(left, Does.Contain("FlashOverlay"));
            Assert.That(left, Does.Not.Contain("Reticles").And.Not.Contain("StatusEffectVisuals"));
        }

        [Test]
        public void TheCosmeticVisualCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.CosmeticVisuals, Is.Null);
            Assert.That(CosmeticVisualCatalog.Load(), Is.SameAs(Resources.Load<CosmeticVisualCatalog>(CosmeticVisualCatalog.ResourcePath)));

            content.CosmeticVisuals = Track(ScriptableObject.CreateInstance<CosmeticVisualCatalog>());
            Assert.That(CosmeticVisualCatalog.Load(), Is.SameAs(content.CosmeticVisuals));

            var left = GameContentAssets.FillEmpty(
                Track(ScriptableObject.CreateInstance<GameContent>()), _ => new Object[0], _ => new TextAsset[0]);
            Assert.That(left, Does.Contain("CosmeticVisuals"), "the entry lists the cosmetic visuals as a slot to fill");
        }

        [Test]
        public void TheStreakIconCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.StreakIcons, Is.Null);
            Assert.That(StreakIconCatalog.Load(), Is.SameAs(Resources.Load<StreakIconCatalog>(StreakIconCatalog.ResourcePath)));

            content.StreakIcons = Track(ScriptableObject.CreateInstance<StreakIconCatalog>());
            Assert.That(StreakIconCatalog.Load(), Is.SameAs(content.StreakIcons));

            var left = GameContentAssets.FillEmpty(
                Track(ScriptableObject.CreateInstance<GameContent>()), _ => new Object[0], _ => new TextAsset[0]);
            Assert.That(left, Does.Contain("StreakIcons"), "the entry lists the streak icons as a slot to fill");
        }

        [Test]
        public void TheEmoteCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.EmoteAnimations, Is.Null);
            Assert.That(EmoteCatalog.Load(), Is.SameAs(Resources.Load<EmoteCatalog>(EmoteCatalog.ResourcePath)));

            content.EmoteAnimations = Track(ScriptableObject.CreateInstance<EmoteCatalog>());
            Assert.That(EmoteCatalog.Load(), Is.SameAs(content.EmoteAnimations));

            var left = GameContentAssets.FillEmpty(
                Track(ScriptableObject.CreateInstance<GameContent>()), _ => new Object[0], _ => new TextAsset[0]);
            Assert.That(left, Does.Contain("EmoteAnimations"), "the entry lists the emote animations as a slot to fill");
            Assert.That(left, Does.Not.Contain("EmotesTuning"), "a tuning slot with no file falls back to Resources/Tuning");
        }

        [Test]
        public void TheVisionVisualCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.VisionVisuals, Is.Null);
            Assert.That(VisionVisualCatalog.Load(), Is.SameAs(Resources.Load<VisionVisualCatalog>(VisionVisualCatalog.ResourcePath)));

            content.VisionVisuals = Track(ScriptableObject.CreateInstance<VisionVisualCatalog>());
            Assert.That(VisionVisualCatalog.Load(), Is.SameAs(content.VisionVisuals));

            content.VisionVisuals = null;
            Assert.That(VisionVisualCatalog.Load(), Is.SameAs(Resources.Load<VisionVisualCatalog>(VisionVisualCatalog.ResourcePath)));

            var left = GameContentAssets.FillEmpty(
                Track(ScriptableObject.CreateInstance<GameContent>()), _ => new Object[0], _ => new TextAsset[0]);
            Assert.That(left, Does.Contain("VisionVisuals"), "the entry lists the vision visuals as a slot to fill");
        }

        [Test]
        public void TheMusicCatalogComesFromTheEntryAndFallsBackToResources()
        {
            var content = Content("A");
            GameContent.Use(content);
            Assert.That(content.Music, Is.Null);
            Assert.That(MusicCatalog.Load(), Is.SameAs(Resources.Load<MusicCatalog>(MusicCatalog.ResourcePath)));

            content.Music = Track(ScriptableObject.CreateInstance<MusicCatalog>());
            Assert.That(MusicCatalog.Load(), Is.SameAs(content.Music));

            content.Music = null;
            Assert.That(MusicCatalog.Load(), Is.SameAs(Resources.Load<MusicCatalog>(MusicCatalog.ResourcePath)));

            var left = GameContentAssets.FillEmpty(
                Track(ScriptableObject.CreateInstance<GameContent>()), _ => new Object[0], _ => new TextAsset[0]);
            Assert.That(left, Does.Contain("Music"), "the entry lists the music as a slot to fill");
        }

        [Test]
        public void WithoutAVisionCatalogTheDefaultColorsAreUsed()
        {
            Assert.That(VisionVisualCatalog.ColorFor(null, VisionMode.Night), Is.EqualTo(VisionVisualCatalog.DefaultNightColor));
            Assert.That(VisionVisualCatalog.ColorFor(null, VisionMode.Thermal), Is.EqualTo(VisionVisualCatalog.DefaultThermalColor));
            Assert.That(VisionVisualCatalog.ColorFor(null, VisionMode.None), Is.EqualTo(Color.clear));
            Assert.That(VisionVisualCatalog.ImageFor(null, VisionMode.Night), Is.Null);

            var catalog = Track(ScriptableObject.CreateInstance<VisionVisualCatalog>());
            Assert.That(catalog.NightColor, Is.EqualTo(VisionVisualCatalog.DefaultNightColor), "a new catalog starts from the defaults");
            Assert.That(catalog.ThermalColor, Is.EqualTo(VisionVisualCatalog.DefaultThermalColor));

            catalog.ThermalColor = Color.red;
            catalog.NightImage = Track(new Texture2D(2, 2));
            catalog.SwitchVolume = 3f;
            Assert.That(VisionVisualCatalog.ColorFor(catalog, VisionMode.Thermal), Is.EqualTo(Color.red));
            Assert.That(VisionVisualCatalog.ImageFor(catalog, VisionMode.Night), Is.SameAs(catalog.NightImage));
            Assert.That(VisionVisualCatalog.ImageFor(catalog, VisionMode.Thermal), Is.Null);
            Assert.That(catalog.SwitchVolume, Is.EqualTo(1f));
        }

        [TestCase("_visionVisuals", "Resources/Presentation/VisionVisualsCatalog")]
        [TestCase("_music", "Resources/Presentation/MusicCatalog")]
        [TestCase("_emoteAnimations", "Resources/Presentation/EmoteCatalog")]
        [TestCase("_cosmeticVisuals", "Resources/Presentation/CosmeticVisualCatalog")]
        [TestCase("_streakIcons", "Resources/Presentation/StreakIconCatalog")]
        [TestCase("_reticles", "Resources/Presentation/ReticleCatalog")]
        [TestCase("_statusEffectVisuals", "Resources/Presentation/StatusEffectVisualsCatalog")]
        [TestCase("_flashOverlay", "Resources/Presentation/FlashOverlayCatalog")]
        public void TheCatalogFieldsNameTheirResourceFallback(string field, string resourcePath)
        {
            var info = typeof(GameContent).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var tooltip = (TooltipAttribute)System.Attribute.GetCustomAttribute(info, typeof(TooltipAttribute));

            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.tooltip, Does.Contain("空なら " + resourcePath + "、"));
        }

        [Test]
        public void TheStreakAimAssistAndProgressionTuningAreKeptAndStartEmpty()
        {
            var content = Content("A");

            Assert.That(content.StreaksTuning, Is.Null);
            Assert.That(content.AimAssistTuning, Is.Null);
            Assert.That(content.ProgressionTuning, Is.Null);

            var streaks = Track(new TextAsset("{}"));
            var aimAssist = Track(new TextAsset("{}"));
            var progression = Track(new TextAsset("{}"));
            content.StreaksTuning = streaks;
            content.AimAssistTuning = aimAssist;
            content.ProgressionTuning = progression;

            Assert.That(content.StreaksTuning, Is.SameAs(streaks));
            Assert.That(content.AimAssistTuning, Is.SameAs(aimAssist));
            Assert.That(content.ProgressionTuning, Is.SameAs(progression));
        }

        [Test]
        public void TheMinimapAssistAndRumbleTuningAreKeptAndStartEmpty()
        {
            var content = Content("A");

            Assert.That(content.MinimapTuning, Is.Null);
            Assert.That(content.AssistTuning, Is.Null);
            Assert.That(content.RumbleTuning, Is.Null);

            var minimap = Track(new TextAsset("{}"));
            var assist = Track(new TextAsset("{}"));
            var rumble = Track(new TextAsset("{}"));
            content.MinimapTuning = minimap;
            content.AssistTuning = assist;
            content.RumbleTuning = rumble;

            Assert.That(content.MinimapTuning, Is.SameAs(minimap));
            Assert.That(content.AssistTuning, Is.SameAs(assist));
            Assert.That(content.RumbleTuning, Is.SameAs(rumble));
        }

        [Test]
        public void TheRecordContentAndModeTuningAreKeptAndStartEmpty()
        {
            var content = Content("A");
            var slots = new (System.Func<TextAsset> get, System.Action<TextAsset> set)[]
            {
                (() => content.ChatWordsTuning, v => content.ChatWordsTuning = v),
                (() => content.AchievementsTuning, v => content.AchievementsTuning = v),
                (() => content.LeaderboardsTuning, v => content.LeaderboardsTuning = v),
                (() => content.CosmeticsTuning, v => content.CosmeticsTuning = v),
                (() => content.EmotesTuning, v => content.EmotesTuning = v),
                (() => content.WeatherTuning, v => content.WeatherTuning = v),
                (() => content.TelemetryTuning, v => content.TelemetryTuning = v),
                (() => content.InfectedTuning, v => content.InfectedTuning = v),
                (() => content.LootTuning, v => content.LootTuning = v),
                (() => content.StashTuning, v => content.StashTuning = v),
                (() => content.ExtractionTuning, v => content.ExtractionTuning = v),
                (() => content.ReplayTuning, v => content.ReplayTuning = v),
                (() => content.PayloadTuning, v => content.PayloadTuning = v),
                (() => content.EconomyTuning, v => content.EconomyTuning = v),
                (() => content.SupplyDropsTuning, v => content.SupplyDropsTuning = v),
                (() => content.RedeployTuning, v => content.RedeployTuning = v),
                (() => content.MutatorsTuning, v => content.MutatorsTuning = v),
                (() => content.PlaylistsTuning, v => content.PlaylistsTuning = v),
                (() => content.KeySharingTuning, v => content.KeySharingTuning = v),
                (() => content.NameplatesTuning, v => content.NameplatesTuning = v),
                (() => content.PalettesTuning, v => content.PalettesTuning = v),
                (() => content.LoadoutPresetsTuning, v => content.LoadoutPresetsTuning = v),
                (() => content.GraphicsTuning, v => content.GraphicsTuning = v),
            };

            foreach (var slot in slots)
            {
                Assert.That(slot.get(), Is.Null);
            }

            var assigned = new List<TextAsset>();
            foreach (var slot in slots)
            {
                var asset = Track(new TextAsset("{}"));
                assigned.Add(asset);
                slot.set(asset);
            }

            for (var i = 0; i < slots.Length; i++)
            {
                Assert.That(slots[i].get(), Is.SameAs(assigned[i]), "slot " + i + " keeps its own file");
            }
        }

        [Test]
        public void FillEmptyFillsTheRecordContentAndModeTuning()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            var weather = Track(new TextAsset("{}"));
            var loot = Track(new TextAsset("{}"));
            var stashA = Track(new TextAsset("{}"));
            var stashB = Track(new TextAsset("{}"));
            var files = new Dictionary<string, TextAsset[]>
            {
                { "weather", new[] { weather } },
                { "loot", new[] { loot } },
                { "stash", new[] { stashA, stashB } },
            };

            var left = GameContentAssets.FillEmpty(content, _ => new Object[0], name => files.TryGetValue(name, out var found) ? found : new TextAsset[0]);

            Assert.That(content.WeatherTuning, Is.SameAs(weather));
            Assert.That(content.LootTuning, Is.SameAs(loot));
            Assert.That(content.StashTuning, Is.Null);
            Assert.That(content.ChatWordsTuning, Is.Null);
            Assert.That(left, Does.Contain("StashTuning"));
            Assert.That(left, Does.Not.Contain("WeatherTuning").And.Not.Contain("LootTuning"));
            Assert.That(left, Does.Not.Contain("ChatWordsTuning"), "a tuning slot with no file falls back to Resources/Tuning");
        }

        [Test]
        public void TheSkillTuningIsKeptAndStartsEmpty()
        {
            var content = Content("A");

            Assert.That(content.SkillTuning, Is.Null);

            var skill = Track(new TextAsset("{}"));
            content.SkillTuning = skill;

            Assert.That(content.SkillTuning, Is.SameAs(skill));
        }

        [TestCase("_streaksTuning", "Resources/Tuning/streaks")]
        [TestCase("_aimAssistTuning", "Resources/Tuning/aim-assist")]
        [TestCase("_progressionTuning", "Resources/Tuning/progression")]
        [TestCase("_skillTuning", "Resources/Tuning/skill")]
        [TestCase("_minimapTuning", "Resources/Tuning/minimap")]
        [TestCase("_assistTuning", "Resources/Tuning/assist")]
        [TestCase("_rumbleTuning", "Resources/Tuning/rumble")]
        [TestCase("_chatWordsTuning", "Resources/Tuning/chat-words")]
        [TestCase("_achievementsTuning", "Resources/Tuning/achievements")]
        [TestCase("_leaderboardsTuning", "Resources/Tuning/leaderboards")]
        [TestCase("_cosmeticsTuning", "Resources/Tuning/cosmetics")]
        [TestCase("_weatherTuning", "Resources/Tuning/weather")]
        [TestCase("_telemetryTuning", "Resources/Tuning/telemetry")]
        [TestCase("_infectedTuning", "Resources/Tuning/infected")]
        [TestCase("_lootTuning", "Resources/Tuning/loot")]
        [TestCase("_stashTuning", "Resources/Tuning/stash")]
        [TestCase("_extractionTuning", "Resources/Tuning/extraction")]
        [TestCase("_replayTuning", "Resources/Tuning/replay")]
        [TestCase("_payloadTuning", "Resources/Tuning/payload")]
        [TestCase("_economyTuning", "Resources/Tuning/economy")]
        [TestCase("_supplyDropsTuning", "Resources/Tuning/supply-drops")]
        [TestCase("_redeployTuning", "Resources/Tuning/redeploy")]
        [TestCase("_mutatorsTuning", "Resources/Tuning/mutators")]
        [TestCase("_playlistsTuning", "Resources/Tuning/playlists")]
        [TestCase("_loadoutPresetsTuning", "Resources/Tuning/loadout-presets")]
        [TestCase("_graphicsTuning", "Resources/Tuning/graphics")]
        public void TheTuningFieldsNameTheirResourceFallback(string field, string resourcePath)
        {
            var info = typeof(GameContent).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var tooltip = (TooltipAttribute)System.Attribute.GetCustomAttribute(info, typeof(TooltipAttribute));

            Assert.That(info.FieldType, Is.EqualTo(typeof(TextAsset)));
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.tooltip, Does.EndWith("空なら " + resourcePath));
        }

        [Test]
        public void AnUnwiredEnemyTakesTheEntryDataOfItsArchetype()
        {
            var sniper = Track(ScriptableObject.CreateInstance<EnemyDefinition>());
            sniper.Archetype = EnemyArchetype.Sniper;
            var rifleman = Track(ScriptableObject.CreateInstance<EnemyDefinition>());
            rifleman.Archetype = EnemyArchetype.Rifleman;
            rifleman.SightRange = 77f;
            var content = Content("A");
            content.SetEnemies(new[] { sniper, rifleman });
            GameContent.Use(content);

            var agent = Track(new GameObject("Enemy")).AddComponent<EnemyAgent>();

            Assert.That(agent.EffectiveDefinition, Is.SameAs(rifleman));
            Assert.That(agent.CreateSpec().SightRange, Is.EqualTo(77f));
        }

        [Test]
        public void MoreThanOneEntryInResourcesIsReportedAndTheFirstByNameIsUsed()
        {
            var b = Content("B");
            var a = Content("A");

            LogAssert.Expect(LogType.Error, new Regex(@"^\[GameContent\] 2 assets in Resources/ShooterKit/ \(A, B\); using 'A'"));
            Assert.That(GameContent.Find(new[] { b, a }), Is.SameAs(a));
            Assert.That(GameContent.Find(new GameContent[0]), Is.Null);
        }

        [Test]
        public void FillEmptyTakesTheOnlyCandidateAndLeavesAmbiguousSlotsEmpty()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            var catalog = Track(ScriptableObject.CreateInstance<WeaponCatalogAsset>());
            var rulesA = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            var rulesB = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            var keptLoadout = Track(ScriptableObject.CreateInstance<LoadoutDefinition>());
            content.Loadout = keptLoadout;
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(WeaponCatalogAsset), new Object[] { catalog } },
                { typeof(GameRulesDefinition), new Object[] { rulesA, rulesB } },
                { typeof(LoadoutDefinition), new Object[] { Track(ScriptableObject.CreateInstance<LoadoutDefinition>()) } },
            };

            var left = GameContentAssets.FillEmpty(content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(content.WeaponCatalog, Is.SameAs(catalog));
            Assert.That(content.Rules, Is.Null);
            Assert.That(content.Loadout, Is.SameAs(keptLoadout), "a filled slot is kept");
            Assert.That(left, Does.Contain("Rules"));
            Assert.That(left, Does.Not.Contain("WeaponCatalog"));
        }

        [Test]
        public void FillEmptyFillsTheOnlyAbilityCatalogAndLeavesTwoEmpty()
        {
            var catalog = Track(ScriptableObject.CreateInstance<AbilityCatalogAsset>());
            var content = Track(ScriptableObject.CreateInstance<GameContent>());

            var left = GameContentAssets.FillEmpty(
                content, t => t == typeof(AbilityCatalogAsset) ? new Object[] { catalog } : new Object[0], _ => new TextAsset[0]);

            Assert.That(content.AbilityCatalog, Is.SameAs(catalog));
            Assert.That(left, Does.Not.Contain("AbilityCatalog"));

            var other = Track(ScriptableObject.CreateInstance<AbilityCatalogAsset>());
            var ambiguous = Track(ScriptableObject.CreateInstance<GameContent>());
            left = GameContentAssets.FillEmpty(
                ambiguous, t => t == typeof(AbilityCatalogAsset) ? new Object[] { catalog, other } : new Object[0], _ => new TextAsset[0]);

            Assert.That(ambiguous.AbilityCatalog, Is.Null);
            Assert.That(left, Does.Contain("AbilityCatalog"));

            var kept = Track(ScriptableObject.CreateInstance<GameContent>());
            kept.AbilityCatalog = other;
            GameContentAssets.FillEmpty(
                kept, t => t == typeof(AbilityCatalogAsset) ? new Object[] { catalog } : new Object[0], _ => new TextAsset[0]);

            Assert.That(kept.AbilityCatalog, Is.SameAs(other), "a filled slot is kept");
        }

        [Test]
        public void FillEmptyFillsTheNewModeDefinitions()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            var freeForAll = Track(ScriptableObject.CreateInstance<FreeForAllDefinition>());
            var guardian = Track(ScriptableObject.CreateInstance<GuardianDefinition>());
            var execution = Track(ScriptableObject.CreateInstance<ExecutionDefinition>());
            var captureTheFlag = Track(ScriptableObject.CreateInstance<CaptureTheFlagDefinition>());
            var royaleA = Track(ScriptableObject.CreateInstance<BattleRoyaleDefinition>());
            var royaleB = Track(ScriptableObject.CreateInstance<BattleRoyaleDefinition>());
            var candidates = new Dictionary<System.Type, Object[]>
            {
                { typeof(FreeForAllDefinition), new Object[] { freeForAll } },
                { typeof(GuardianDefinition), new Object[] { guardian } },
                { typeof(ExecutionDefinition), new Object[] { execution } },
                { typeof(CaptureTheFlagDefinition), new Object[] { captureTheFlag } },
                { typeof(BattleRoyaleDefinition), new Object[] { royaleA, royaleB } },
            };

            var left = GameContentAssets.FillEmpty(content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0], _ => new TextAsset[0]);

            Assert.That(content.FreeForAll, Is.SameAs(freeForAll));
            Assert.That(content.Guardian, Is.SameAs(guardian));
            Assert.That(content.Execution, Is.SameAs(execution));
            Assert.That(content.CaptureTheFlag, Is.SameAs(captureTheFlag));
            Assert.That(content.BattleRoyale, Is.Null);
            Assert.That(left, Does.Contain("BattleRoyale"));
            Assert.That(left, Does.Not.Contain("FreeForAll").And.Not.Contain("Guardian").And.Not.Contain("Execution").And.Not.Contain("CaptureTheFlag"));
        }

        [Test]
        public void FillEmptyTakesTheOnlyTuningFileOfEachName()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            var minimap = Track(new TextAsset("{}"));
            var assist = Track(new TextAsset("{}"));
            var rumbleA = Track(new TextAsset("{}"));
            var rumbleB = Track(new TextAsset("{}"));
            var keptGrenade = Track(new TextAsset("{}"));
            content.GrenadeTuning = keptGrenade;
            var files = new Dictionary<string, TextAsset[]>
            {
                { "minimap", new[] { minimap } },
                { "assist", new[] { assist } },
                { "rumble", new[] { rumbleA, rumbleB } },
                { "grenade", new[] { Track(new TextAsset("{}")) } },
            };
            var asked = new List<string>();

            var left = GameContentAssets.FillEmpty(content, _ => new Object[0], name =>
            {
                asked.Add(name);
                return files.TryGetValue(name, out var found) ? found : new TextAsset[0];
            });

            Assert.That(content.MinimapTuning, Is.SameAs(minimap));
            Assert.That(content.AssistTuning, Is.SameAs(assist));
            Assert.That(content.RumbleTuning, Is.Null);
            Assert.That(content.GrenadeTuning, Is.SameAs(keptGrenade), "a filled slot is kept");
            Assert.That(content.PingTuning, Is.Null);
            Assert.That(left, Does.Contain("RumbleTuning"));
            Assert.That(left, Does.Not.Contain("MinimapTuning").And.Not.Contain("AssistTuning").And.Not.Contain("GrenadeTuning"));
            Assert.That(left, Does.Not.Contain("PingTuning"), "a tuning slot with no file falls back to Resources/Tuning");
            Assert.That(asked, Does.Contain("aim-assist").And.Contain("skill").And.Contain("progression").And.Not.Contain("grenade"));
            Assert.That(asked, Does.Contain("chat-words").And.Contain("achievements").And.Contain("leaderboards").And.Contain("cosmetics"));
            Assert.That(asked, Does.Contain("weather").And.Contain("telemetry").And.Contain("infected").And.Contain("loot"));
            Assert.That(asked, Does.Contain("stash").And.Contain("extraction").And.Contain("replay"));
            Assert.That(asked, Does.Contain("payload").And.Contain("economy").And.Contain("supply-drops").And.Contain("mutators").And.Contain("playlists"));
            Assert.That(asked, Does.Contain("redeploy").And.Contain("key-sharing").And.Contain("nameplates").And.Contain("palettes"));
            Assert.That(asked, Does.Contain("loadout-presets"));
            Assert.That(asked, Does.Contain("graphics"));
            Assert.That(asked, Has.Count.EqualTo(40));
        }

        [TestCase("Assets/MyGame/Tuning/minimap.json", "minimap", "Packages/com.example.kit/Resources/Tuning", true)]
        [TestCase("Assets/MyGame/Resources/Tuning/minimap.txt", "minimap", "Packages/com.example.kit/Resources/Tuning", true)]
        [TestCase("Assets/MyGame/Tuning/aim-assist.json", "aim-assist", "Assets/game/Resources/Tuning", true)]
        [TestCase("Assets/MyGame/Tuning/minimap-old.json", "minimap", "Assets/game/Resources/Tuning", false)]
        [TestCase("Assets/MyGame/Tuning/assist.json", "aim-assist", "Assets/game/Resources/Tuning", false)]
        [TestCase("Assets/MyGame/Tuning/Minimap.json", "minimap", "Assets/game/Resources/Tuning", false)]
        [TestCase("Assets/game/Resources/Tuning/minimap.json", "minimap", "Assets/game/Resources/Tuning", false)]
        [TestCase("Assets/game/Resources/Tuning/minimap.json", "minimap", "Assets/game/Resources/Tuning/", false)]
        [TestCase("Assets/game/Resources/TuningExtra/minimap.json", "minimap", "Assets/game/Resources/Tuning", true)]
        [TestCase("Assets/MyGame/Tuning/minimap.json", "minimap", null, true)]
        [TestCase("Assets/Samples/Shooter Kit/1.0.0/Demo/Resources/Localization/en/minimap.json", "minimap", "Assets/game/Resources/Tuning", false)]
        [TestCase("Assets/Samples/Shooter Kit/1.0.0/Demo/Resources/Localization/ja/squad.json", "squad", null, false)]
        [TestCase("", "minimap", "Assets/game/Resources/Tuning", false)]
        [TestCase("Assets/MyGame/Tuning/minimap.json", "", "Assets/game/Resources/Tuning", false)]
        public void ATuningCandidateHasTheFileNameAndIsNotTheBundledCopy(string assetPath, string fileName, string bundled, bool expected)
        {
            Assert.That(GameContentAssets.IsTuningCandidate(assetPath, fileName, bundled), Is.EqualTo(expected));
        }

        [Test]
        public void ThePathArgumentIsReadOnce()
        {
            Assert.That(GameContentAssets.ResolvePath(new[] { "Unity" }, "Assets/Game/Resources/ShooterKit/GameContent.asset"),
                Is.EqualTo("Assets/Game/Resources/ShooterKit/GameContent.asset"));
            Assert.That(GameContentAssets.ResolvePath(new[] { "-gameContent", "Assets/X/Resources/ShooterKit/Mine.asset" }, "Assets/Y.asset"),
                Is.EqualTo("Assets/X/Resources/ShooterKit/Mine.asset"));
            Assert.Throws<System.ArgumentException>(() => GameContentAssets.ResolvePath(new[] { "-gameContent" }, "Assets/Y.asset"));
            Assert.Throws<System.ArgumentException>(
                () => GameContentAssets.ResolvePath(new[] { "-gameContent", "Assets/A.asset", "-gameContent", "Assets/B.asset" }, "Assets/Y.asset"));
        }

        private GameContent Content(string name, float regenPerSecond = 10f, WeaponDefinition weapon = null, int scoreLimit = 50)
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = name;

            content.Rules = Track(ScriptableObject.CreateInstance<GameRulesDefinition>());
            content.Rules.name = name + "-Rules";
            GameRulesAssets.Configure(content.Rules, new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                1f,
                regenPerSecond,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off));

            if (weapon != null)
            {
                content.Loadout = Track(ScriptableObject.CreateInstance<LoadoutDefinition>());
                content.Loadout.SetSlots(new[] { new LoadoutSlotDefinition(new[] { weapon.Category }, weapon) });
            }

            content.TeamDeathmatch = Track(ScriptableObject.CreateInstance<TeamDeathmatchDefinition>());
            content.TeamDeathmatch.ScoreLimit = scoreLimit;
            content.Domination = Track(ScriptableObject.CreateInstance<DominationDefinition>());
            content.Domination.ScoreLimit = scoreLimit;
            content.KillConfirmed = Track(ScriptableObject.CreateInstance<KillConfirmedDefinition>());
            content.KillConfirmed.name = name + "-KillConfirmed";

            return content;
        }

        private BootstrapWorld BuildUnwiredBootstrap()
        {
            var host = Track(new GameObject("Bootstrap"));
            var bootstrap = host.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            return bootstrap.Build();
        }

        private static void DestroyWorld(BootstrapWorld world)
        {
            if (world == null)
            {
                return;
            }

            Object.DestroyImmediate(world.Player);
            Object.DestroyImmediate(world.Light.gameObject);
            Object.DestroyImmediate(world.TargetRoot);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
