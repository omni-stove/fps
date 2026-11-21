using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GettingStartedGuideTests
    {
        private const string GuidePath = "docs/getting-started.md";
        private const string TasksPath = ".mise/tasks.toml";

        private static readonly string[] _gameContentLabels =
        {
            "Rules",
            "Motor",
            "Enemies",
            "Weapon Catalog",
            "Loadout",
            "Ability Catalog",
            "Missions",
            "Menu Scenes",
            "Character Rig",
            "Grenade Tuning",
            "Mutators Tuning",
            "Playlists Tuning",
        };

        private static readonly string[] _bootstrapLabels =
        {
            "Create Floor",
            "Rules",
        };

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        // 導入ガイドはキットのリポジトリにだけあり、サンプルを入れたゲームのプロジェクトには無いので、そこでは読まない。
        private static string ReadGuide()
        {
            var path = Path.Combine(ProjectRoot, GuidePath);
            if (!File.Exists(path))
            {
                Assert.Ignore(GuidePath + " is not in this project; it ships only with the kit repository");
            }

            return File.ReadAllText(path);
        }

        [Test]
        public void TheGuideNamesTheMenusAsTheEditorShowsThem()
        {
            var guide = ReadGuide();
            var menus = new[]
            {
                SceneBuildSteps.MenuPath,
                WeaponAssetBuilder.MenuPath,
                WeaponJsonImporter.MenuPath,
                GameRulesJsonImporter.MenuPath,
                GameRulesAssetBuilder.MenuPath,
                GameContentAssets.MenuPath,
                ReplacementAudit.MenuPath,
                MapCollisionExporter.MenuPath,
            };

            foreach (var menu in menus)
            {
                var shown = menu.Replace("/", " > ");
                Assert.That(guide, Does.Contain(shown), menu + " is not written as '" + shown + "'");
            }
        }

        [Test]
        public void EveryTaskTheGuideRunsIsAMiseTask()
        {
            var guide = ReadGuide();
            var tasksPath = Path.Combine(ProjectRoot, TasksPath);
            Assert.That(File.Exists(tasksPath), Is.True, TasksPath + " is missing");
            var tasks = new HashSet<string>(
                Regex.Matches(File.ReadAllText(tasksPath), @"^\[([a-z0-9-]+)\]", RegexOptions.Multiline).Select(m => m.Groups[1].Value),
                StringComparer.Ordinal);

            var named = Regex.Matches(guide, @"(?:make|mise run) ([a-z][a-z0-9-]+)").Select(m => m.Groups[1].Value).Distinct().ToList();

            Assert.That(named, Is.SupersetOf(new[] { "build-scenes", "import-weapons", "import-rules", "game-content", "audit-replacements", "export-server-data", "export-server-rules", "server-dotnet", "build" }));
            foreach (var task in named)
            {
                Assert.That(tasks, Does.Contain(task), "the guide runs '" + task + "', which is not in " + TasksPath);
            }
        }

        [TestCase("CompactPistol")]
        [TestCase("Carbine556")]
        public void TheWeaponsTheGuideEditsAreTheOnesTheRangePlayerHolds(string name)
        {
            var guide = ReadGuide();
            var file = DemoSample.Root + "/Weapons/ReferenceJson/" + name + ".json";
            var physical = ShooterKitRoot.PhysicalPath(file);
            Assert.That(File.Exists(physical), Is.True, file + " is missing");

            var spec = WeaponJsonImporter.ParseSpec(File.ReadAllText(physical), file);

            Assert.That(BootstrapSceneBuilder.PlayerWeaponIds, Does.Contain(spec.Id));
            Assert.That(guide, Does.Contain("<Demo>/Weapons/ReferenceJson/" + name + ".json"));
        }

        [Test]
        public void TheEditedPistolIsSavedWhereTheGuideSays()
        {
            var guide = ReadGuide();

            Assert.That(WeaponAssetBuilder.ReferenceFolder, Is.EqualTo(DemoSample.Root + "/Weapons/Reference"));
            Assert.That(guide, Does.Contain("<Demo>/Weapons/Reference/CompactPistol.asset"));
            Assert.That(guide, Does.Contain("--out \"<Demo>/Weapons/Reference\""));
        }

        [Test]
        public void TheEditedPistolStartsSemiAutomaticWithATwentyRoundMagazine()
        {
            var file = DemoSample.Root + "/Weapons/ReferenceJson/CompactPistol.json";
            var spec = WeaponJsonImporter.ParseSpec(File.ReadAllText(ShooterKitRoot.PhysicalPath(file)), file);

            Assert.That(spec.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(spec.MagazineSize, Is.EqualTo(20));
        }

        [Test]
        public void TheGuideNamesTheKeysThatFireAndSwitchWeapons()
        {
            var guide = ReadGuide();
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "the project-wide input actions are not set");

            Assert.That(actions.FindAction(InputActionPaths.Attack).bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Mouse>/leftButton"));
            Assert.That(actions.FindAction(InputActionPaths.Next).bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/2"));
            Assert.That(actions.FindAction(InputActionPaths.Previous).bindings, Has.Some.Matches<InputBinding>(b => b.path == "<Keyboard>/1"));
            Assert.That(guide, Does.Contain("左クリックで撃ち、`2` と `1` で武器を持ち替え"));
        }

        [Test]
        public void TheGuideListsEveryBundledTuningFile()
        {
            var guide = ReadGuide();
            var folder = ShooterKitRoot.PhysicalPath(ShooterKitRoot.BundledPath("Resources/Tuning"));
            var files = Directory.GetFiles(folder, "*.json").Select(Path.GetFileName).ToList();

            Assert.That(files, Does.Contain("grenade.json"));
            foreach (var file in files)
            {
                Assert.That(guide, Does.Contain("`" + file + "`"), file + " is not in the guide's list of data");
            }
        }

        [Test]
        public void TheGuideGivesTheAddressQuickMatchFallsBackTo()
        {
            var guide = ReadGuide();

            Assert.That(NetClientLaunch.DefaultServer().Endpoint, Is.EqualTo("127.0.0.1:7777"));
            Assert.That(guide, Does.Contain("EOS が無ければ `127.0.0.1:7777` につなぐ"));
        }

        [Test]
        public void TheGameContentFieldsTheGuideNamesAreInTheInspector()
        {
            var guide = ReadGuide();
            var content = ScriptableObject.CreateInstance<GameContent>();
            try
            {
                var labels = DisplayNames(new SerializedObject(content));
                foreach (var label in _gameContentLabels)
                {
                    Assert.That(labels, Does.Contain(label), "GameContent has no field shown as '" + label + "'");
                    Assert.That(guide, Does.Contain("**" + label + "**"));
                }
            }
            finally
            {
                Object.DestroyImmediate(content);
            }
        }

        [Test]
        public void TheGameBootstrapFieldsTheGuideNamesAreInTheInspector()
        {
            var guide = ReadGuide();
            var host = new GameObject("GettingStartedGuideTests");
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                var labels = DisplayNames(new SerializedObject(bootstrap));
                foreach (var label in _bootstrapLabels)
                {
                    Assert.That(labels, Does.Contain(label), "GameBootstrap has no field shown as '" + label + "'");
                    Assert.That(guide, Does.Contain("**" + label + "**"));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TheGuideNamesTheResourceFolderOfGameContent()
        {
            var guide = ReadGuide();

            Assert.That(GameContent.ResourceFolder, Is.EqualTo("ShooterKit"));
            Assert.That(guide, Does.Contain("`Resources/" + GameContent.ResourceFolder + "/`"));
            Assert.That(guide, Does.Contain("`Resources/" + MenuSceneSettings.ResourcePath + "`"));
        }

        private static List<string> DisplayNames(SerializedObject serialized)
        {
            var names = new List<string>();
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                names.Add(property.displayName);
                enterChildren = false;
            }

            return names;
        }
    }
}
