using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class GameContentTests
    {
        private const string TextKey = "menu.title";

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
        public void SwappingTheEntrySwapsTheText()
        {
            GameContent.Use(Content("A", title: "Game A"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Get(TextKey), Is.EqualTo("Game A"));
            Assert.That(Localizer.TableOf(GameLanguage.English).Get(TextKey), Is.Not.EqualTo("Game A"), "only the language the tables are for");

            GameContent.Use(Content("B", title: "Game B"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Get(TextKey), Is.EqualTo("Game B"));

            GameContent.Use(null);
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Get(TextKey), Is.Not.EqualTo("Game B"));
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
            GameContent.Use(content);

            Assert.That(MotorSettings.Resolve(null).WalkSpeed, Is.EqualTo(2.5f));
            Assert.That(MenuSceneSettings.LoadScenes().Campaign, Is.EqualTo("Chapter1"));
            Assert.That(KitTuning.Grenade.DamageRadius, Is.EqualTo(9f));
            Assert.That(PresentationCatalog.Load(), Is.SameAs(content.Presentation));
            Assert.That(CameraShakeSettings.Load(), Is.SameAs(content.CameraShake));
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

            LogAssert.Expect(LogType.Error, new Regex(@"^\[GameContent\] 2 assets in Resources/FpsKit/ \(A, B\); using 'A'"));
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

            var left = GameContentAssets.FillEmpty(content, t => candidates.TryGetValue(t, out var found) ? found : new Object[0]);

            Assert.That(content.WeaponCatalog, Is.SameAs(catalog));
            Assert.That(content.Rules, Is.Null);
            Assert.That(content.Loadout, Is.SameAs(keptLoadout), "a filled slot is kept");
            Assert.That(left, Does.Contain("Rules"));
            Assert.That(left, Does.Not.Contain("WeaponCatalog"));
        }

        [Test]
        public void ThePathArgumentIsReadOnce()
        {
            Assert.That(GameContentAssets.ResolvePath(new[] { "Unity" }, "Assets/Game/Resources/FpsKit/GameContent.asset"),
                Is.EqualTo("Assets/Game/Resources/FpsKit/GameContent.asset"));
            Assert.That(GameContentAssets.ResolvePath(new[] { "-gameContent", "Assets/X/Resources/FpsKit/Mine.asset" }, "Assets/Y.asset"),
                Is.EqualTo("Assets/X/Resources/FpsKit/Mine.asset"));
            Assert.Throws<System.ArgumentException>(() => GameContentAssets.ResolvePath(new[] { "-gameContent" }, "Assets/Y.asset"));
            Assert.Throws<System.ArgumentException>(
                () => GameContentAssets.ResolvePath(new[] { "-gameContent", "Assets/A.asset", "-gameContent", "Assets/B.asset" }, "Assets/Y.asset"));
        }

        private GameContent Content(string name, float regenPerSecond = 10f, WeaponDefinition weapon = null, int scoreLimit = 50, string title = null)
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

            if (title != null)
            {
                var table = Track(new TextAsset("{ \"" + TextKey + "\": \"" + title + "\" }"));
                table.name = "menu";
                content.SetTexts(new[] { new GameContentTextTables(GameLanguages.Code(GameLanguage.Japanese), new[] { table }) });
            }

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

            if (world.Hud != null)
            {
                Object.DestroyImmediate(world.Hud.gameObject);
            }

            if (world.HudPanelSettings != null)
            {
                Object.DestroyImmediate(world.HudPanelSettings);
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
