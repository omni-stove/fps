using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="GameRulesJsonImporter"/> を一時フォルダに走らせて、書いた JSON がそのルールの値になることと、
    /// 失敗したときに何も保存しないことを確かめる。
    /// </summary>
    public sealed class GameRulesJsonImporterTests
    {
        private string _folder;
        private string _jsonFolder;

        [SetUp]
        public void SetUp()
        {
            var id = Guid.NewGuid().ToString("N");
            _folder = "Assets/__GameRulesJsonImporterTests_" + id;

            // Assets の外に置き、テスト用の JSON を Unity に取り込ませない。
            _jsonFolder = Path.Combine(Path.GetTempPath(), "GameRulesJsonImporterTests_" + id).Replace('\\', '/');
            Directory.CreateDirectory(_jsonFolder);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }

            if (Directory.Exists(_jsonFolder))
            {
                Directory.Delete(_jsonFolder, true);
            }
        }

        [Test]
        public void ImportingWrittenRulesFillsTheAssetLikeConfigure()
        {
            var rules = GameRules.Default.WithActions(ActionRules.Default.WithCover(Cover.On));
            var imported = GameRulesJsonImporter.ImportFrom(WriteJson("Written.json", GameRulesJson.Write(rules)), _folder)[0];
            var configured = ScriptableObject.CreateInstance<GameRulesDefinition>();
            configured.name = imported.name;
            try
            {
                GameRulesAssets.Configure(configured, rules);

                Assert.That(EditorJsonUtility.ToJson(imported), Is.EqualTo(EditorJsonUtility.ToJson(configured)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(configured);
            }
        }

        [Test]
        public void ReimportKeepsTheGuidAndUpdatesTheValues()
        {
            var path = WriteJson("Arena.json", "{\"regenPerSecond\": 10}");
            GameRulesJsonImporter.ImportFrom(path, _folder);
            var assetPath = _folder + "/Arena.asset";
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            WriteJson("Arena.json", "{\"regenPerSecond\": 30, \"minPlayers\": 6}");
            var updated = GameRulesJsonImporter.ImportFrom(path, _folder)[0];

            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(guid));
            Assert.That(updated.RegenPerSecond, Is.EqualTo(30f));
            Assert.That(updated.MinPlayers, Is.EqualTo(6));
            Assert.That(updated.RegenDelaySeconds, Is.EqualTo(GameRules.Default.RegenDelaySeconds));
        }

        [Test]
        public void WrittenRulesWithEveryNewActionOnSurviveTheAsset()
        {
            var rules = GameRules.Default.WithActions(ActionRules.Default
                .WithMelee(Melee.On)
                .WithStagger(Stagger.On)
                .WithSlide(Slide.On)
                .WithMantle(Mantle.On)
                .WithProne(Prone.On)
                .WithLadder(Ladder.On)
                .WithZipline(Zipline.On)
                .WithParachute(Parachute.On)
                .WithStealth(Stealth.On)
                .WithSuppression(Suppression.On)
                .WithPing(Fps.Core.Ping.On)
                .WithAbilities(Abilities.On)
                .WithSlowMotion(SlowMotion.On));
            var path = WriteJson("Actions.json", GameRulesJson.Write(rules));

            var imported = GameRulesJsonImporter.ImportFrom(path, _folder)[0];

            Assert.That(imported.Melee, Is.EqualTo(Melee.On));
            Assert.That(imported.SlowMotion, Is.EqualTo(SlowMotion.On));
            AssertSameRules(imported.ToRules(), rules, imported.name);
        }

        [Test]
        public void OutOfRangeValueSavesNothingAndNamesTheFileAndItem()
        {
            WriteJson("A.json", "{}");
            var bad = WriteJson("B.json", "{\"actions\": {\"downedHealthFraction\": 2}}");

            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJsonImporter.ImportFrom(_jsonFolder, _folder));

            Assert.That(e.Message, Does.Contain(bad));
            Assert.That(e.Message, Does.Contain("actions.downedHealthFraction"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void UnknownItemSavesNothingAndNamesTheFileAndItem()
        {
            var bad = WriteJson("A.json", "{\"respawnDelay\": 3}");
            WriteJson("B.json", "{}");

            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJsonImporter.ImportFrom(_jsonFolder, _folder));

            Assert.That(e.Message, Does.Contain(bad));
            Assert.That(e.Message, Does.Contain("respawnDelay"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void FailedImportLeavesTheExistingAssetUnchanged()
        {
            WriteJson("Arena.json", "{\"regenPerSecond\": 10}");
            GameRulesJsonImporter.ImportFrom(_jsonFolder, _folder);

            WriteJson("Arena.json", "{\"regenPerSecond\": 30}");
            WriteJson("Broken.json", "{\"minPlayers\": -1}");
            Assert.Throws<GameRulesJsonException>(() => GameRulesJsonImporter.ImportFrom(_jsonFolder, _folder));

            var existing = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(_folder + "/Arena.asset");
            Assert.That(existing.RegenPerSecond, Is.EqualTo(10f));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/Broken.asset"), Is.Null);
        }

        [Test]
        public void AssetOfAnotherTypeAtTheDestinationSavesNothing()
        {
            WriteJson("A.json", "{}");
            WriteJson("B.json", "{}");
            SceneBuildHelpers.EnsureFolder(_folder);
            var other = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(other, _folder + "/B.asset");

            Assert.Throws<InvalidOperationException>(() => GameRulesJsonImporter.ImportFrom(_jsonFolder, _folder));

            Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/A.asset"), Is.Null);
        }

        [Test]
        public void FolderImportReadsOnlyJsonFilesInNameOrder()
        {
            WriteJson("b.json", "{}");
            WriteJson("a.json", "{}");
            File.WriteAllText(Path.Combine(_jsonFolder, "notes.txt"), "not rules");

            var files = GameRulesJsonImporter.FindJsonFiles(_jsonFolder);

            Assert.That(files, Is.EqualTo(new[] { _jsonFolder + "/a.json", _jsonFolder + "/b.json" }));
        }

        [Test]
        public void MissingOrEmptySourceIsRejected()
        {
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.FindJsonFiles(_jsonFolder));
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.FindJsonFiles(_jsonFolder + "/missing"));
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.FindJsonFiles(" "));

            var text = Path.Combine(_jsonFolder, "rules.txt");
            File.WriteAllText(text, "{}");
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.FindJsonFiles(text));
        }

        [TestCase("Packages/com.omnistove.fps/Rules")]
        [TestCase("Rules")]
        [TestCase("Assets/../Rules")]
        public void OutputOutsideAssetsIsRejected(string folder)
        {
            var path = WriteJson("A.json", "{}");

            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.ImportFrom(path, folder));
        }

        [Test]
        public void ArgumentsGiveTheSourceAndOutput()
        {
            var (source, output) = GameRulesJsonImporter.ResolveArguments(
                new[] { "unity", "-rulesJson", "Rules/Json", "-rulesOut", "Assets/Scratch/Rules" },
                () => throw new AssertionException("the default output must not be read when -rulesOut is given"));

            Assert.That(source, Is.EqualTo("Rules/Json"));
            Assert.That(output, Is.EqualTo("Assets/Scratch/Rules"));
        }

        [Test]
        public void OmittedOutputUsesTheDefault()
        {
            var (_, output) = GameRulesJsonImporter.ResolveArguments(new[] { "-rulesJson", "a.json" }, () => "Assets/game/Rules");

            Assert.That(output, Is.EqualTo("Assets/game/Rules"));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Func<string> fallback = () => "Assets/game/Rules";

            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.ResolveArguments(new[] { "unity" }, fallback));
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.ResolveArguments(new[] { "-rulesJson" }, fallback));
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.ResolveArguments(new[] { "-rulesJson", "-rulesOut", "Assets/x" }, fallback));
            Assert.Throws<ArgumentException>(
                () => GameRulesJsonImporter.ResolveArguments(new[] { "-rulesJson", "a.json", "-rulesJson", "b.json" }, fallback));
            Assert.Throws<ArgumentException>(() => GameRulesJsonImporter.ResolveArguments(new[] { "-rulesJson", "a.json", "-rulesOut" }, fallback));
        }

        private string WriteJson(string fileName, string json)
        {
            var path = _jsonFolder + "/" + fileName;
            File.WriteAllText(path, json);
            return path;
        }

        private static void AssertSameRules(GameRules actual, GameRules expected, string name)
        {
            Assert.That(actual.FriendlyFire, Is.EqualTo(expected.FriendlyFire), name + " friendlyFire");
            Assert.That(actual.RespawnMode, Is.EqualTo(expected.RespawnMode), name + " respawnMode");
            Assert.That(actual.WaveIntervalSeconds, Is.EqualTo(expected.WaveIntervalSeconds), name + " waveIntervalSeconds");
            Assert.That(actual.RegenDelaySeconds, Is.EqualTo(expected.RegenDelaySeconds), name + " regenDelaySeconds");
            Assert.That(actual.RegenPerSecond, Is.EqualTo(expected.RegenPerSecond), name + " regenPerSecond");
            Assert.That(actual.DeathBody, Is.EqualTo(expected.DeathBody), name + " deathBody");
            Assert.That(actual.DeathCamera, Is.EqualTo(expected.DeathCamera), name + " deathCamera");
            Assert.That(actual.InfiniteReserve, Is.EqualTo(expected.InfiniteReserve), name + " infiniteReserve");
            Assert.That(actual.ReserveAmmoMultiplier, Is.EqualTo(expected.ReserveAmmoMultiplier), name + " reserveAmmoMultiplier");
            Assert.That(actual.Destruction, Is.EqualTo(expected.Destruction), name + " destruction");
            Assert.That(actual.SpawnProtectionSeconds, Is.EqualTo(expected.SpawnProtectionSeconds), name + " spawnProtectionSeconds");
            Assert.That(actual.FallDamage, Is.EqualTo(expected.FallDamage), name + " fallDamage");
            Assert.That(actual.HudDensity, Is.EqualTo(expected.HudDensity), name + " hudDensity");
            Assert.That(actual.Objective, Is.EqualTo(expected.Objective), name + " objective");
            Assert.That(actual.MinPlayers, Is.EqualTo(expected.MinPlayers), name + " minPlayers");
            Assert.That(actual.IdleKickSeconds, Is.EqualTo(expected.IdleKickSeconds), name + " idleKickSeconds");

            var a = actual.Actions;
            var b = expected.Actions;
            Assert.That(a.Downed, Is.EqualTo(b.Downed), name + " actions.downed");
            Assert.That(a.DownedBleedoutSeconds, Is.EqualTo(b.DownedBleedoutSeconds), name + " actions.downedBleedoutSeconds");
            Assert.That(a.DownedHealthFraction, Is.EqualTo(b.DownedHealthFraction), name + " actions.downedHealthFraction");
            Assert.That(a.ReviveSeconds, Is.EqualTo(b.ReviveSeconds), name + " actions.reviveSeconds");
            Assert.That(a.Cover, Is.EqualTo(b.Cover), name + " actions.cover");
            Assert.That(a.BlindFire, Is.EqualTo(b.BlindFire), name + " actions.blindFire");
            Assert.That(a.ActiveReload, Is.EqualTo(b.ActiveReload), name + " actions.activeReload");
            Assert.That(a.Dodge, Is.EqualTo(b.Dodge), name + " actions.dodge");
            Assert.That(a.Vault, Is.EqualTo(b.Vault), name + " actions.vault");
            Assert.That(a.Grenade, Is.EqualTo(b.Grenade), name + " actions.grenade");
            Assert.That(a.GrenadeCount, Is.EqualTo(b.GrenadeCount), name + " actions.grenadeCount");
            Assert.That(a.Melee, Is.EqualTo(b.Melee), name + " actions.melee");
            Assert.That(a.Stagger, Is.EqualTo(b.Stagger), name + " actions.stagger");
            Assert.That(a.Slide, Is.EqualTo(b.Slide), name + " actions.slide");
            Assert.That(a.Mantle, Is.EqualTo(b.Mantle), name + " actions.mantle");
            Assert.That(a.Prone, Is.EqualTo(b.Prone), name + " actions.prone");
            Assert.That(a.Ladder, Is.EqualTo(b.Ladder), name + " actions.ladder");
            Assert.That(a.Zipline, Is.EqualTo(b.Zipline), name + " actions.zipline");
            Assert.That(a.Parachute, Is.EqualTo(b.Parachute), name + " actions.parachute");
            Assert.That(a.Stealth, Is.EqualTo(b.Stealth), name + " actions.stealth");
            Assert.That(a.Suppression, Is.EqualTo(b.Suppression), name + " actions.suppression");
            Assert.That(a.Ping, Is.EqualTo(b.Ping), name + " actions.ping");
            Assert.That(a.Abilities, Is.EqualTo(b.Abilities), name + " actions.abilities");
            Assert.That(a.SlowMotion, Is.EqualTo(b.SlowMotion), name + " actions.slowMotion");
        }
    }
}
