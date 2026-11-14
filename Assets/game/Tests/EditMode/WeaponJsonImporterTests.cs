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
    /// <see cref="WeaponJsonImporter"/> を一時フォルダに走らせて、同梱のサンプル JSON が今の武器と同じ値になることと、
    /// 不正な JSON では何も保存しないことを確かめる。リポジトリの Assets/game/Weapons は書き換えない。
    /// </summary>
    public sealed class WeaponJsonImporterTests
    {
        private string _folder;
        private string _jsonFolder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponJsonImporterTests_" + Guid.NewGuid().ToString("N");
            // Assets の外に置き、テスト用の JSON を Unity に取り込ませない
            _jsonFolder = Path.Combine(Path.GetTempPath(), "WeaponJsonImporterTests_" + Guid.NewGuid().ToString("N"));
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
        public void TheSamplesLiveUnderTheBundledWeaponsFolder()
        {
            Assert.That(WeaponJsonImporter.SampleFolder, Is.EqualTo("Assets/game/Weapons/Json"));
            Assert.That(WeaponJsonImporter.DefaultOutputFolder, Is.EqualTo(WeaponAssetBuilder.Folder));
        }

        [Test]
        public void ImportingTheSamplesReproducesTheBuiltInWeapons()
        {
            var imported = WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, null);

            Assert.That(imported, Has.Count.EqualTo(3));
            AssertSameSpec(Load("Rifle").ToSpec(), WeaponAssetBuilder.RifleSpec());
            AssertSameSpec(Load("Handgun").ToSpec(), WeaponAssetBuilder.HandgunSpec());
            AssertSameSpec(Load("Launcher").ToSpec(), WeaponAssetBuilder.LauncherSpec());
        }

        [Test]
        public void ImportingTheSamplesMatchesTheCommittedAssetFields()
        {
            var committedRifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.RiflePath);
            var committedHandgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.HandgunPath);
            Assume.That(committedRifle, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build first");
            Assume.That(committedHandgun, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build first");

            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, null);

            AssertSameFields(Load("Rifle"), committedRifle);
            AssertSameFields(Load("Handgun"), committedHandgun);
        }

        [Test]
        public void ReimportingKeepsTheGuidAndRestoresTheValues()
        {
            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, null);
            var path = _folder + "/Rifle.asset";
            var guid = AssetDatabase.AssetPathToGUID(path);
            var rifle = Load("Rifle");
            rifle.Damage = 1;

            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder + "/Rifle.json", _folder, null);

            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(Load("Rifle"), Is.SameAs(rifle));
            Assert.That(rifle.Damage, Is.EqualTo(25));
        }

        [Test]
        public void ImportsOverTheBuilderOutputWithoutChangingIt()
        {
            WeaponAssetBuilder.BuildAt(
                _folder + "/Rifle.asset", _folder + "/Handgun.asset", _folder + "/WeaponCatalog.asset", _folder + "/DefaultLoadout.asset");
            var rifleGuid = AssetDatabase.AssetPathToGUID(_folder + "/Rifle.asset");

            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, null);

            Assert.That(AssetDatabase.AssetPathToGUID(_folder + "/Rifle.asset"), Is.EqualTo(rifleGuid));
            AssertSameSpec(Load("Rifle").ToSpec(), WeaponAssetBuilder.RifleSpec());
        }

        [Test]
        public void AddsTheImportedWeaponsToTheCatalogOnceById()
        {
            var catalogPath = _folder + "/WeaponCatalog.asset";
            WeaponAssetBuilder.BuildAt(_folder + "/Rifle.asset", _folder + "/Handgun.asset", catalogPath, _folder + "/DefaultLoadout.asset");

            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, catalogPath);
            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, catalogPath);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(catalogPath);
            Assert.That(catalog.Weapons, Is.EqualTo(new[] { Load("Rifle"), Load("Handgun"), Load("Launcher") }));
            Assert.That(catalog.ToCatalog().Count, Is.EqualTo(3));
        }

        [Test]
        public void ReplacesACatalogEntryThatHasTheSameIdButAnotherAsset()
        {
            var otherFolder = _folder + "/Other";
            var catalogPath = otherFolder + "/WeaponCatalog.asset";
            WeaponAssetBuilder.BuildAt(
                otherFolder + "/Rifle.asset", otherFolder + "/Handgun.asset", catalogPath, otherFolder + "/DefaultLoadout.asset");
            var otherHandgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(otherFolder + "/Handgun.asset");
            var importFolder = _folder + "/Imported";

            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(WeaponAssetBuilder.RifleId)));
            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder + "/Rifle.json", importFolder, catalogPath);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(catalogPath);
            var imported = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(importFolder + "/Rifle.asset");
            Assert.That(catalog.Weapons, Is.EqualTo(new[] { imported, otherHandgun }));
        }

        [Test]
        public void AMissingCatalogStopsBeforeSavingAnything()
        {
            Assert.Throws<InvalidOperationException>(
                () => WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder, _folder, _folder + "/Missing.asset"));

            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void AnInvalidValueNamesTheFileAndFieldAndSavesNothing()
        {
            File.Copy(WeaponJsonImporter.SampleFolder + "/Rifle.json", Path.Combine(_jsonFolder, "A_Rifle.json"));
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

            AssertSameSpec(spec, WeaponAssetBuilder.LauncherSpec());
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
            WriteJson("B.json", SampleText("Rifle").Replace("\"displayName\": \"Rifle\"", "\"displayName\": \"Copy\""));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("id"));
            Assert.That(e.Message, Does.Contain("B.json").And.Contain("A.json"));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void AnIdThatAnotherAssetInTheOutputAlreadyUsesIsRejected()
        {
            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder + "/Rifle.json", _folder, null);
            WriteJson("Carbine.json", SampleText("Rifle"));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("id"));
            Assert.That(e.Message, Does.Contain(_folder + "/Rifle.asset"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/Carbine.asset"), Is.Null);
        }

        [Test]
        public void ChangingTheIdOfAnExistingAssetIsRejected()
        {
            WeaponJsonImporter.ImportFrom(WeaponJsonImporter.SampleFolder + "/Rifle.json", _folder, null);
            WriteJson("Rifle.json", SampleText("Rifle").Replace(WeaponAssetBuilder.RifleId, "11111111-2222-4333-8444-555555555555"));

            var e = Assert.Throws<WeaponJsonException>(() => WeaponJsonImporter.ImportFrom(_jsonFolder, _folder, null));

            Assert.That(e.Field, Is.EqualTo("id"));
            Assert.That(Load("Rifle").Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
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
            return File.ReadAllText(WeaponJsonImporter.SampleFolder + "/" + name + ".json");
        }

        private static void AssertSameSpec(WeaponSpec actual, WeaponSpec expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName));
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
        }

        // 変換後の WeaponSpec だけでなく、Inspector に出るアセットの値そのものが一致することを確かめる
        private static void AssertSameFields(WeaponDefinition actual, WeaponDefinition expected)
        {
            Assert.That(EditorJsonUtility.ToJson(actual), Is.EqualTo(EditorJsonUtility.ToJson(expected)), expected.name);
        }
    }
}
