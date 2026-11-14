using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// サンプルの武器の JSON (<see cref="SamplesFolder"/>) を一時フォルダに取り込むと、<see cref="WeaponAssetBuilder"/> の武器と
    /// コミットしてある武器のアセットと同じ値になることを確かめる。
    /// </summary>
    public sealed class WeaponJsonSamplesTests
    {
        private string _folder;

        private static string SamplesFolder => DemoSample.Root + "/Weapons/Json";

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponJsonSamplesTests_" + Guid.NewGuid().ToString("N");
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
        public void ImportingTheSamplesReproducesTheBuiltInWeapons()
        {
            var imported = WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

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

            WeaponJsonImporter.ImportFrom(SamplesFolder, _folder, null);

            AssertSameFields(Load("Rifle"), committedRifle);
            AssertSameFields(Load("Handgun"), committedHandgun);
        }

        private WeaponDefinition Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_folder + "/" + name + ".asset");
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

        private static void AssertSameFields(WeaponDefinition actual, WeaponDefinition expected)
        {
            Assert.That(EditorJsonUtility.ToJson(actual), Is.EqualTo(EditorJsonUtility.ToJson(expected)), expected.name);
        }
    }
}
