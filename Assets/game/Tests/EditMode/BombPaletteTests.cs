using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BombPaletteTests
    {
        // 材質に書いた色は、読み戻すと丸めで最下位の桁がずれることがあるので、ぴったりでは比べない。
        private const float ColorTolerance = 1e-3f;

        private readonly List<Object> _created = new List<Object>();
        private ControlSettings _settingsBefore;
        private float _volumeBefore;

        [SetUp]
        public void SetUp()
        {
            _settingsBefore = AppliedControlSettings.Current;
            _volumeBefore = AudioListener.volume;
            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.Standard));
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            AppliedControlSettings.Apply(_settingsBefore);
            AudioListener.volume = _volumeBefore;
        }

        [Test]
        public void TheBombTakesItsColorsFromThePalette()
        {
            var palette = TeamPalette.RedGreen;
            var objective = TeamColors.ToColor(palette.Objective);

            AssertColor(BombObject.ColorOf(BombState.Dropped, palette), objective);
            AssertColor(
                BombObject.ColorOf(BombState.Carried, palette),
                new Color(objective.r * BombObject.CarriedBrightness, objective.g * BombObject.CarriedBrightness, objective.b * BombObject.CarriedBrightness));
            AssertColor(BombObject.ColorOf(BombState.Planted, palette), TeamColors.ToColor(palette.Danger));
            AssertColor(BombObject.ColorOf(BombState.Detonated, palette), TeamColors.ToColor(palette.Danger));
            AssertColor(BombObject.ColorOf(BombState.Defused, palette), TeamColors.ToColor(palette.Safe));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BombObject.ColorOf((BombState)99, palette));
            Assert.Throws<System.ArgumentNullException>(() => BombObject.ColorOf(BombState.Dropped, null));
        }

        [Test]
        public void TheBombIsRepaintedWhenThePaletteChanges()
        {
            var bomb = NewBomb();
            bomb.ShowOnGround(Vector3.zero, BombState.Planted);
            var material = TrackMaterial(bomb.gameObject);

            AssertColor(material.color, BombObject.ColorOf(BombState.Planted, TeamPalette.Standard));

            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.RedGreen));

            Assert.That(bomb.ShownState, Is.EqualTo(BombState.Planted));
            AssertColor(material.color, BombObject.ColorOf(BombState.Planted, TeamPalette.RedGreen));

            bomb.ShowOnGround(Vector3.zero, BombState.Defused);
            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.BlueYellow));

            AssertColor(material.color, BombObject.ColorOf(BombState.Defused, TeamPalette.BlueYellow));
        }

        [Test]
        public void AHiddenBombIsRepaintedAndStaysHidden()
        {
            var bomb = NewBomb();
            var carrier = Track(new GameObject("Carrier")).transform;
            bomb.ShowCarried(carrier);
            var material = TrackMaterial(bomb.gameObject);
            bomb.Hide();

            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.RedGreen));

            Assert.That(bomb.IsVisible, Is.False);
            AssertColor(material.color, BombObject.ColorOf(BombState.Carried, TeamPalette.RedGreen));
        }

        [Test]
        public void TheSiteMarkerTakesItsColorsFromThePalette()
        {
            foreach (var palette in TeamPalette.All)
            {
                AssertColor(BombSite.MarkerColorOf(false, palette), TeamColors.ToColor(palette.Objective));
                AssertColor(BombSite.MarkerColorOf(true, palette), TeamColors.ToColor(palette.Danger));
            }

            Assert.Throws<System.ArgumentNullException>(() => BombSite.MarkerColorOf(false, null));
        }

        [Test]
        public void TheSiteMarkerIsRepaintedWhenThePaletteChanges()
        {
            var site = Track(new GameObject("Site")).AddComponent<BombSite>();
            site.Radius = 2f;
            site.RefreshMarker();
            var material = TrackMaterial(site.Marker);

            AssertColor(material.color, BombSite.MarkerColorOf(false, TeamPalette.Standard));

            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.RedGreen));

            AssertColor(material.color, BombSite.MarkerColorOf(false, TeamPalette.RedGreen));

            site.ShowPlanted(true);

            AssertColor(material.color, BombSite.MarkerColorOf(true, TeamPalette.RedGreen));

            AppliedControlSettings.Apply(_settingsBefore.WithPalette(ColorPalette.BlueYellow));

            AssertColor(material.color, BombSite.MarkerColorOf(true, TeamPalette.BlueYellow));
        }

        // 1 型・2 型色覚で見分けられるよう、RedGreen では今までの赤・橙・緑と違う色にする
        [Test]
        public void RedGreenPaintsTheBombAndSiteOtherwiseThanStandard()
        {
            foreach (BombState state in System.Enum.GetValues(typeof(BombState)))
            {
                Assert.That(BombObject.ColorOf(state, TeamPalette.RedGreen), Is.Not.EqualTo(BombObject.ColorOf(state, TeamPalette.Standard)), state.ToString());
            }

            Assert.That(BombSite.MarkerColorOf(false, TeamPalette.RedGreen), Is.Not.EqualTo(BombSite.MarkerColorOf(false, TeamPalette.Standard)));
            Assert.That(BombSite.MarkerColorOf(true, TeamPalette.RedGreen), Is.Not.EqualTo(BombSite.MarkerColorOf(true, TeamPalette.Standard)));
        }

        private BombObject NewBomb()
        {
            var bomb = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            bomb.name = "Bomb";
            Object.DestroyImmediate(bomb.GetComponent<Collider>());
            return bomb.AddComponent<BombObject>();
        }

        // Edit Mode では OnDestroy が呼ばれないので、複製された材質も後で消す。
        private Material TrackMaterial(GameObject root)
        {
            var material = root.GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(material, Is.Not.Null);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Track(renderer.sharedMaterial);
            }

            return material;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColorTolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColorTolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColorTolerance));
        }
    }
}
