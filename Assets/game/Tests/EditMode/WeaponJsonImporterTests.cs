using System;
using System.IO;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="WeaponJsonImporter"/> を一時フォルダに走らせて、テストの JSON (<see cref="TestWeaponJson"/>) が同じ値の武器になることと、
    /// 不正な JSON では何も保存しないことを確かめる。
    /// </summary>
    public sealed class WeaponJsonImporterTests
    {
        private string _folder;
        private string _jsonFolder;
        private string _samplesFolder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponJsonImporterTests_" + Guid.NewGuid().ToString("N");
            // Assets の外に置き、テスト用の JSON を Unity に取り込ませない
            _jsonFolder = Path.Combine(Path.GetTempPath(), "WeaponJsonImporterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_jsonFolder);
            _samplesFolder = Path.Combine(Path.GetTempPath(), "WeaponJsonImporterTests_Samples_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory(_samplesFolder);
            foreach (var name in TestWeaponJson.Names)
            {
                File.WriteAllText(Path.Combine(_samplesFolder, name + ".json"), TestWeaponJson.Text(name));
            }
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

            if (Directory.Exists(_samplesFolder))
            {
                Directory.Delete(_samplesFolder, true);
            }
        }

        [Test]
        public void TheDefaultOutputIsTheWeaponsFolderOfTheOutputRoot()
        {
            Assert.That(WeaponJsonImporter.DefaultOutputFolder, Is.EqualTo(WeaponAssets.Folder));
        }

        [Test]
        public void ImportingTheTestJsonReproducesTheTestWeapons()
        {
            var imported = WeaponJsonImporter.ImportFrom(_samplesFolder, _folder, null);

            Assert.That(imported, Has.Count.EqualTo(3));
            AssertSameSpec(Load("Rifle").ToSpec(), TestWeapons.RifleSpec());
            AssertSameSpec(Load("Handgun").ToSpec(), TestWeapons.HandgunSpec());
            AssertSameSpec(Load("Launcher").ToSpec(), TestWeapons.LauncherSpec());
        }

        [Test]
        public void ReimportingKeepsTheGuidAndRestoresTheValues()
        {
            WeaponJsonImporter.ImportFrom(_samplesFolder, _folder, null);
            var path = _folder + "/Rifle.asset";
            var guid = AssetDatabase.AssetPathToGUID(path);
            var rifle = Load("Rifle");
            rifle.Damage = 1;

            WeaponJsonImporter.ImportFrom(_samplesFolder + "/Rifle.json", _folder, null);

            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(Load("Rifle"), Is.SameAs(rifle));
            Assert.That(rifle.Damage, Is.EqualTo(25));
        }

        [Test]
        public void ImportsOverTheBuilderOutputWithoutChangingIt()
        {
            BuildTestWeapons(_folder + "/Rifle.asset", _folder + "/Handgun.asset", _folder + "/WeaponCatalog.asset");
            var rifleGuid = AssetDatabase.AssetPathToGUID(_folder + "/Rifle.asset");

            WeaponJsonImporter.ImportFrom(_samplesFolder, _folder, null);

            Assert.That(AssetDatabase.AssetPathToGUID(_folder + "/Rifle.asset"), Is.EqualTo(rifleGuid));
            AssertSameSpec(Load("Rifle").ToSpec(), TestWeapons.RifleSpec());
        }

        [Test]
        public void AddsTheImportedWeaponsToTheCatalogOnceById()
        {
            var catalogPath = _folder + "/WeaponCatalog.asset";
            BuildTestWeapons(_folder + "/Rifle.asset", _folder + "/Handgun.asset", catalogPath);

            WeaponJsonImporter.ImportFrom(_samplesFolder, _folder, catalogPath);
            WeaponJsonImporter.ImportFrom(_samplesFolder, _folder, catalogPath);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(catalogPath);
            Assert.That(catalog.Weapons, Is.EqualTo(new[] { Load("Rifle"), Load("Handgun"), Load("Launcher") }));
            Assert.That(catalog.ToCatalog().Count, Is.EqualTo(3));
        }

        [Test]
        public void ReplacesACatalogEntryThatHasTheSameIdButAnotherAsset()
        {
            var otherFolder = _folder + "/Other";
            var catalogPath = otherFolder + "/WeaponCatalog.asset";
            BuildTestWeapons(otherFolder + "/Rifle.asset", otherFolder + "/Handgun.asset", catalogPath);
            var otherHandgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(otherFolder + "/Handgun.asset");
            var importFolder = _folder + "/Imported";

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(TestWeapons.RifleId)));
            WeaponJsonImporter.ImportFrom(_samplesFolder + "/Rifle.json", importFolder, catalogPath);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(catalogPath);
            var imported = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(importFolder + "/Rifle.asset");
            Assert.That(catalog.Weapons, Is.EqualTo(new[] { imported, otherHandgun }));
        }

        [Test]
        public void AMissingCatalogStopsBeforeSavingAnything()
        {
            Assert.Throws<InvalidOperationException>(
                () => WeaponJsonImporter.ImportFrom(_samplesFolder, _folder, _folder + "/Missing.asset"));

            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void AnInvalidValueNamesTheFileAndFieldAndSavesNothing()
        {
            WriteJson("A_Rifle.json", SampleText("Rifle"));
            WriteJson("B_Broken.json", SampleText("Handgun").Replace("\"magazineSize\": 12", "\"magazineSize\": 0"));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("magazineSize"));
            Assert.That(e.File, Does.EndWith("B_Broken.json"));
            Assert.That(e.Message, Does.Contain("B_Broken.json").And.Contain("magazineSize"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False, "the valid rifle before it is not saved either");
        }

        [Test]
        public void AMissingFieldIsNamed()
        {
            var e = Assert.Throws<WeaponJsonException>(
                () => WeaponJsonImporter.ParseSpec(SampleText("Rifle").Replace("\"damage\": 25,", string.Empty), "Rifle.json"));

            Assert.That(e.Field, Is.EqualTo("damage"));
            Assert.That(e.Message, Does.Contain("Rifle.json").And.Contain("damage").And.Contain("missing"));
        }

        [Test]
        public void AnOmittedOptionalFieldTakesTheWeaponSpecDefault()
        {
            var spec = WeaponJsonImporter.ParseSpec(
                SampleText("Launcher").Replace("\"activeReloadFailSeconds\": 0.5,", string.Empty).Replace("\"adsFieldOfView\": null,", string.Empty),
                "Launcher.json");

            AssertSameSpec(spec, TestWeapons.LauncherSpec());
        }

        [Test]
        public void TheRecoilRecoveryDelayIsReadAndDefaultsWhenOmitted()
        {
            var text = SampleText("Rifle");
            Assume.That(text, Does.Not.Contain(WeaponJsonImporter.RecoilRecoveryDelaySecondsField));
            Assume.That(text, Does.Contain("\"recoilRecoveryPerSecond\": 10,"));

            Assert.That(
                WeaponJsonImporter.ParseSpec(text, "Rifle.json").RecoilRecoveryDelaySeconds,
                Is.EqualTo(WeaponSpec.DefaultRecoilRecoveryDelaySeconds));

            var slow = WeaponJsonImporter.ParseSpec(
                text.Replace("\"recoilRecoveryPerSecond\": 10,", "\"recoilRecoveryPerSecond\": 10, \"recoilRecoveryDelaySeconds\": 0.6,"),
                "Rifle.json");
            Assert.That(slow.RecoilRecoveryDelaySeconds, Is.EqualTo(0.6f));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ParseSpec(
                text.Replace("\"recoilRecoveryPerSecond\": 10,", "\"recoilRecoveryPerSecond\": 10, \"recoilRecoveryDelaySeconds\": -1,"),
                "Rifle.json"));
            Assert.That(e.Field, Is.EqualTo(WeaponJsonImporter.RecoilRecoveryDelaySecondsField));
        }

        [Test]
        public void TheImportedNameIsReadFromTheTableByItsKey()
        {
            WeaponJsonImporter.ImportFrom(_samplesFolder + "/Launcher.json", _folder, null);

            var launcher = Load("Launcher");
            Assert.That(launcher.NameKey, Is.EqualTo(TestWeapons.LauncherNameKey), "the asset stores the key, not the text");
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Resolve(launcher.ToSpec().Name), Is.EqualTo("Launcher"));
        }

        [TestCase("\"\"", "")]
        [TestCase("null", "")]
        [TestCase("\"  weapon.carbine \"", "weapon.carbine")]
        public void TheNameKeyIsTrimmedAndMayBeEmpty(string value, string expected)
        {
            var spec = WeaponJsonImporter.ParseSpec(
                SampleText("Rifle").Replace("\"nameKey\": \"weapon.rifle\"", "\"nameKey\": " + value),
                "Rifle.json");

            Assert.That(spec.NameKey, Is.EqualTo(expected));
            Assert.That(spec.Name, Is.EqualTo(expected.Length > 0 ? new LocalizedText(expected) : LocalizedText.Literal(spec.Id)));
        }

        [Test]
        public void TheOldDisplayNameFieldAsksForANameKey()
        {
            var e = Assert.Throws<WeaponJsonException>(
                () => WeaponJsonImporter.ParseSpec(SampleText("Rifle").Replace("\"nameKey\": \"weapon.rifle\"", "\"displayName\": \"Rifle\""), "Rifle.json"));

            Assert.That(e.Field, Is.EqualTo(WeaponJsonImporter.LegacyDisplayNameField));
            Assert.That(e.Message, Does.Contain("Rifle.json").And.Contain(WeaponJsonImporter.NameKeyField));
        }

        [Test]
        public void AnUnknownFieldIsNamed()
        {
            var e = Assert.Throws<WeaponJsonException>(
                () => WeaponJsonImporter.ParseSpec(SampleText("Rifle").Replace("\"damage\":", "\"damgae\":"), "Rifle.json"));

            Assert.That(e.Field, Is.EqualTo("damgae"));
        }

        [TestCase("\"magazineSize\": 30", "\"magazineSize\": 30.5", "magazineSize")]
        [TestCase("\"magazineSize\": 30", "\"magazineSize\": \"30\"", "magazineSize")]
        [TestCase("\"magazineSize\": 30", "\"magazineSize\": 99999999999", "magazineSize")]
        [TestCase("\"category\": \"AssaultRifle\"", "\"category\": \"assaultrifle\"", "category")]
        [TestCase("\"category\": \"AssaultRifle\"", "\"category\": \"0\"", "category")]
        [TestCase("\"fireMode\": \"Auto\"", "\"fireMode\": 0", "fireMode")]
        [TestCase("\"range\": 100", "\"range\": 0", "range")]
        [TestCase("\"range\": 100", "\"range\": 1e39", "range")]
        [TestCase("\"id\": \"6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83\"", "\"id\": \"RIFLE\"", "id")]
        [TestCase("\"id\": \"6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83\"", "\"id\": null", "id")]
        [TestCase("\"minMultiplier\": 0.5", "\"minMultiplier\": 2", "falloff.minMultiplier")]
        [TestCase("\"minMultiplier\": 0.5", "\"minMultiplier\": 0.5, \"curve\": 1", "falloff.curve")]
        [TestCase("\"recoilPitchDegrees\": 0.5", "\"recoilPitchDegrees\": 91", "recoilPitchDegrees")]
        [TestCase("\"damage\": 25", "\"damage\": 25, \"damage\": 26", "damage")]
        [TestCase("\"nameKey\": \"weapon.rifle\"", "\"nameKey\": 1", "nameKey")]
        public void AnInvalidFieldIsNamed(string original, string replacement, string field)
        {
            var text = SampleText("Rifle");
            Assume.That(text, Does.Contain(original));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ParseSpec(text.Replace(original, replacement), "Rifle.json"));

            Assert.That(e.Message, Does.Contain(field));
            if (e.Field != null)
            {
                Assert.That(e.Field, Is.EqualTo(field));
            }
        }

        [Test]
        public void FalloffNoneIsTheOnlyAcceptedString()
        {
            Assert.That(WeaponJsonImporter.ParseSpec(SampleText("Launcher"), "Launcher.json").Falloff, Is.SameAs(DamageFalloff.None));

            var e = Assert.Throws<WeaponJsonException>(
                () => WeaponJsonImporter.ParseSpec(SampleText("Launcher").Replace("\"None\"", "\"none\""), "Launcher.json"));
            Assert.That(e.Field, Is.EqualTo("falloff"));
        }

        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{\"id\": ")]
        [TestCase("{} {}")]
        public void TextThatIsNotAWeaponObjectIsRejected(string json)
        {
            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ParseSpec(json, "Bad.json"));

            Assert.That(e.Message, Does.Contain("Bad.json"));
        }

        [Test]
        public void ADuplicateIdAcrossFilesSavesNothing()
        {
            WriteJson("A.json", SampleText("Rifle"));
            WriteJson("B.json", SampleText("Rifle").Replace("\"nameKey\": \"weapon.rifle\"", "\"nameKey\": \"weapon.copy\""));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("id"));
            Assert.That(e.Message, Does.Contain("B.json").And.Contain("A.json"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void AnIdThatAnotherAssetInTheOutputAlreadyUsesIsRejected()
        {
            WeaponJsonImporter.ImportFrom(_samplesFolder + "/Rifle.json", _folder, null);
            WriteJson("Carbine.json", SampleText("Rifle"));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("id"));
            Assert.That(e.Message, Does.Contain(_folder + "/Rifle.asset"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/Carbine.asset"), Is.Null);
        }

        [Test]
        public void ChangingTheIdOfAnExistingAssetIsRejected()
        {
            WeaponJsonImporter.ImportFrom(_samplesFolder + "/Rifle.json", _folder, null);
            WriteJson("Rifle.json", SampleText("Rifle").Replace(TestWeapons.RifleId, "11111111-2222-4333-8444-555555555555"));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("id"));
            Assert.That(Load("Rifle").Id, Is.EqualTo(TestWeapons.RifleId));
        }

        [Test]
        public void AFolderWithoutJsonIsRejected()
        {
            Assert.Throws<FileNotFoundException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));
            Assert.Throws<FileNotFoundException>(
                () => WeaponJsonImporter.ImportFrom(Path.Combine(_jsonFolder, "missing.json"), _folder, null));
        }

        [TestCase("Packages/com.example/Weapons")]
        [TestCase("Assets/../Weapons")]
        [TestCase("")]
        public void TheOutputMustBeUnderAssets(string folder)
        {
            Assert.Throws<ArgumentException>(() => WeaponJsonImporter.ValidateOutputFolder(folder));
        }

        [Test]
        public void ParsesTheArguments()
        {
            var options = WeaponJsonImporter.ParseArguments(new[]
            {
                "unity", "-batchmode", "-weaponJson", "Assets/game/Weapons/Json", "-weaponOut", "Assets\\Scratch\\Weapons/",
                "-weaponCatalog", "Assets/game/Weapons/WeaponCatalog.asset",
            });

            Assert.That(options.Source, Is.EqualTo("Assets/game/Weapons/Json"));
            Assert.That(options.OutputFolder, Is.EqualTo("Assets/Scratch/Weapons"));
            Assert.That(options.CatalogPath, Is.EqualTo("Assets/game/Weapons/WeaponCatalog.asset"));

            var defaults = WeaponJsonImporter.ParseArguments(new[] { "-weaponJson", "weapons.json" });
            Assert.That(defaults.OutputFolder, Is.Null);
            Assert.That(defaults.CatalogPath, Is.Null);
        }

        [Test]
        public void RejectsMissingOrRepeatedArguments()
        {
            Assert.Throws<ArgumentException>(() => WeaponJsonImporter.ParseArguments(new[] { "-batchmode" }));
            Assert.Throws<ArgumentException>(() => WeaponJsonImporter.ParseArguments(new[] { "-weaponJson" }));
            Assert.Throws<ArgumentException>(() => WeaponJsonImporter.ParseArguments(new[] { "-weaponJson", "-weaponOut", "Assets/X" }));
            Assert.Throws<ArgumentException>(() => WeaponJsonImporter.ParseArguments(new[] { "-weaponJson", "a", "-weaponJson", "b" }));
            Assert.Throws<ArgumentException>(
                () => WeaponJsonImporter.ParseArguments(new[] { "-weaponJson", "a", "-weaponCatalog", "Assets/Catalog.json" }));
        }

        private WeaponDefinition Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_folder + "/" + name + ".asset");
        }

        private void WriteJson(string fileName, string text)
        {
            File.WriteAllText(Path.Combine(_jsonFolder, fileName), text);
        }

        private static string SampleText(string name)
        {
            return TestWeaponJson.Text(name);
        }

        /// <summary>rifle・handgun の定義と、それを並べたカタログを作る。</summary>
        private static void BuildTestWeapons(string riflePath, string handgunPath, string catalogPath)
        {
            var rifleSpec = TestWeapons.RifleSpec();
            var handgunSpec = TestWeapons.HandgunSpec();
            var rifle = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(riflePath, d => WeaponAssets.Configure(d, rifleSpec));
            var handgun = SceneBuildHelpers.CreateOrUpdateAsset<WeaponDefinition>(handgunPath, d => WeaponAssets.Configure(d, handgunSpec));
            SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(catalogPath, c => WeaponAssets.Configure(c, new[] { rifle, handgun }));
        }

        private static void AssertSameSpec(WeaponSpec actual, WeaponSpec expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.NameKey, Is.EqualTo(expected.NameKey));
            Assert.That(actual.Category, Is.EqualTo(expected.Category));
            Assert.That(actual.FireMode, Is.EqualTo(expected.FireMode));
            Assert.That(actual.MagazineSize, Is.EqualTo(expected.MagazineSize));
            Assert.That(actual.ReserveAmmo, Is.EqualTo(expected.ReserveAmmo));
            Assert.That(actual.FireInterval, Is.EqualTo(expected.FireInterval));
            Assert.That(actual.ReloadSeconds, Is.EqualTo(expected.ReloadSeconds));
            Assert.That(actual.Damage, Is.EqualTo(expected.Damage));
            Assert.That(actual.Range, Is.EqualTo(expected.Range));
            Assert.That(actual.Falloff.StartDistance, Is.EqualTo(expected.Falloff.StartDistance));
            Assert.That(actual.Falloff.EndDistance, Is.EqualTo(expected.Falloff.EndDistance));
            Assert.That(actual.Falloff.MinMultiplier, Is.EqualTo(expected.Falloff.MinMultiplier));
            Assert.That(actual.ExplosionRadius, Is.EqualTo(expected.ExplosionRadius));
            Assert.That(actual.AdsFieldOfView, Is.EqualTo(expected.AdsFieldOfView));
            Assert.That(actual.BlindFireSpreadDegrees, Is.EqualTo(expected.BlindFireSpreadDegrees));
            Assert.That(actual.ActiveReloadWindowStart, Is.EqualTo(expected.ActiveReloadWindowStart));
            Assert.That(actual.ActiveReloadWindowLength, Is.EqualTo(expected.ActiveReloadWindowLength));
            Assert.That(actual.ActiveReloadFailSeconds, Is.EqualTo(expected.ActiveReloadFailSeconds));
            Assert.That(actual.RecoilPitchDegrees, Is.EqualTo(expected.RecoilPitchDegrees));
            Assert.That(actual.RecoilYawDegrees, Is.EqualTo(expected.RecoilYawDegrees));
            Assert.That(actual.RecoilRecoveryPerSecond, Is.EqualTo(expected.RecoilRecoveryPerSecond));
            Assert.That(actual.HipSpreadDegrees, Is.EqualTo(expected.HipSpreadDegrees));
            Assert.That(actual.AdsSpreadDegrees, Is.EqualTo(expected.AdsSpreadDegrees));
            Assert.That(actual.MoveSpreadDegrees, Is.EqualTo(expected.MoveSpreadDegrees));
            Assert.That(actual.RecoilRecoveryDelaySeconds, Is.EqualTo(expected.RecoilRecoveryDelaySeconds));
        }

        // 変換後の WeaponSpec だけでなく、Inspector に出るアセットの値そのものが一致することを確かめる
        private static void AssertSameFields(WeaponDefinition actual, WeaponDefinition expected)
        {
            Assert.That(EditorJsonUtility.ToJson(actual), Is.EqualTo(EditorJsonUtility.ToJson(expected)), expected.name);
        }
    }
}
