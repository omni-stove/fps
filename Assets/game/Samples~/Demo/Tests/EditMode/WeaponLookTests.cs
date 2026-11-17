using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class WeaponLookTests
    {
        private static Dictionary<string, WeaponLook> Looks()
        {
            return WeaponAssetBuilder.RangeWeaponSources().ToDictionary(source => source.Name, source => WeaponLook.For(source.Spec));
        }

        [Test]
        public void EveryRangeWeaponLooksDifferent()
        {
            var looks = Looks();

            Assert.That(looks.Count, Is.EqualTo(21));
            var duplicates = looks.GroupBy(pair => pair.Value.Signature()).Where(group => group.Count() > 1).Select(group => string.Join(", ", group.Select(pair => pair.Key)));
            Assert.That(duplicates, Is.Empty);
        }

        [Test]
        public void EachMechanismAddsItsOwnPart()
        {
            var looks = Looks();

            Assert.That(looks["Minigun"].HasPrefix(WeaponLook.RotaryBarrelPrefix), Is.True);
            Assert.That(looks["Minigun"].Has(WeaponLook.AmmoBoxName), Is.True);
            Assert.That(looks["Minigun"].Has(WeaponLook.BarrelName), Is.False);
            Assert.That(looks["Rifle"].HasPrefix(WeaponLook.RotaryBarrelPrefix), Is.False);
            Assert.That(looks["Rifle"].Has(WeaponLook.MagazineName), Is.True);
            Assert.That(looks["Rifle"].Has(WeaponLook.StockName), Is.True);
            Assert.That(looks["Rifle"].Has(WeaponLook.ScopeName), Is.False);
            Assert.That(looks["BurstRifle"].Has(WeaponLook.ForegripName), Is.True);
            Assert.That(looks["Dmr"].Has(WeaponLook.ScopeName), Is.True);
            Assert.That(looks["ScopedRifle"].Has(WeaponLook.LongScopeName), Is.True);
            Assert.That(looks["BoltSniper"].Has(WeaponLook.LongScopeName), Is.True);
            Assert.That(looks["BoltSniper"].Has(WeaponLook.BoltHandleName), Is.True);
            Assert.That(looks["AntiMaterial"].Has(WeaponLook.MuzzleBrakeName), Is.True);
            Assert.That(looks["AntiMaterial"].HasPrefix(WeaponLook.BipodPrefix), Is.True);
            Assert.That(looks["ChargeRifle"].HasPrefix(WeaponLook.CoilPrefix), Is.True);
            Assert.That(looks["Shotgun"].Has(WeaponLook.TubeMagazineName), Is.True);
            Assert.That(looks["Shotgun"].Has(WeaponLook.PumpName), Is.True);
            Assert.That(looks["SlugCannon"].Has(WeaponLook.TubeMagazineName), Is.False);
            Assert.That(looks["SlugCannon"].Has(WeaponLook.MagazineName), Is.True);
            Assert.That(looks["Crossbow"].Has(WeaponLook.BowLimbsName), Is.True);
            Assert.That(looks["Crossbow"].Has(WeaponLook.BowStringName), Is.True);
            Assert.That(looks["Flamethrower"].Has(WeaponLook.FuelTankName), Is.True);
            Assert.That(looks["Flamethrower"].Has(WeaponLook.NozzleName), Is.True);
            Assert.That(looks["Laser"].Has(WeaponLook.EmitterName), Is.True);
            Assert.That(looks["Laser"].HasPrefix(WeaponLook.CoolingRingPrefix), Is.True);
            Assert.That(looks["EnergyRifle"].HasPrefix(WeaponLook.CoolingRingPrefix), Is.True);
            Assert.That(looks["EnergyRifle"].Has(WeaponLook.EmitterName), Is.False);
            Assert.That(looks["Launcher"].Has(WeaponLook.TubeName), Is.True);
            Assert.That(looks["Launcher"].Has(WeaponLook.WarheadName), Is.False);
            Assert.That(looks["RocketLauncher"].Has(WeaponLook.WarheadName), Is.True);
            Assert.That(looks["GuidedLauncher"].Has(WeaponLook.WarheadName), Is.True);
            Assert.That(looks["GuidedLauncher"].Has(WeaponLook.SeekerName), Is.True);
            Assert.That(looks["GrenadeLauncher"].Has(WeaponLook.DrumName), Is.True);
            Assert.That(looks["GrenadeLauncher"].Has(WeaponLook.TubeName), Is.False);
            Assert.That(looks["RicochetPistol"].Has(WeaponLook.CompensatorName), Is.True);
            Assert.That(looks["Handgun"].Has(WeaponLook.CompensatorName), Is.False);
            Assert.That(looks["Handgun"].Has(WeaponLook.StockName), Is.False);
            Assert.That(looks["Handgun"].ForeGrip, Is.Null);
        }

        [Test]
        public void TheAccentColorShowsWhatTheRoundDoes()
        {
            var looks = Looks();

            Assert.That(looks["Rifle"].AccentColor, Is.Null);
            Assert.That(looks["Rifle"].Has(WeaponLook.BandName), Is.False);
            Assert.That(looks["IncendiaryRifle"].AccentColor, Is.EqualTo(WeaponLook.BurnAccent));
            Assert.That(looks["IncendiaryRifle"].Has(WeaponLook.BandName), Is.True);
            Assert.That(looks["Flamethrower"].AccentColor, Is.EqualTo(WeaponLook.BurnAccent));
            Assert.That(looks["Laser"].AccentColor, Is.EqualTo(WeaponLook.BeamAccent));
            Assert.That(looks["EnergyRifle"].AccentColor, Is.EqualTo(WeaponLook.HeatAccent));
            Assert.That(looks["ChargeRifle"].AccentColor, Is.EqualTo(WeaponLook.ChargeAccent));
            Assert.That(looks["GuidedLauncher"].AccentColor, Is.EqualTo(WeaponLook.HomingAccent));
            Assert.That(looks["RocketLauncher"].AccentColor, Is.EqualTo(WeaponLook.ExplosiveAccent));
            Assert.That(looks["RicochetPistol"].AccentColor, Is.EqualTo(WeaponLook.RicochetAccent));
        }

        [Test]
        public void TheBodyAndFurnitureColorsFollowTheCategory()
        {
            var looks = Looks();

            Assert.That(looks["Rifle"].BodyColor, Is.EqualTo(WeaponLook.MetalColor));
            Assert.That(looks["Laser"].BodyColor, Is.EqualTo(WeaponLook.EnergyBodyColor));
            Assert.That(looks["EnergyRifle"].BodyColor, Is.EqualTo(WeaponLook.EnergyBodyColor));
            Assert.That(looks["RocketLauncher"].BodyColor, Is.EqualTo(WeaponLook.LauncherBodyColor));
            Assert.That(looks["Shotgun"].FurnitureColor, Is.EqualTo(WeaponLook.WoodColor));
            Assert.That(looks["BoltSniper"].FurnitureColor, Is.EqualTo(WeaponLook.TanColor));
            Assert.That(looks["Dmr"].FurnitureColor, Is.EqualTo(WeaponLook.OliveColor));
            Assert.That(looks["Rifle"].FurnitureColor, Is.EqualTo(WeaponLook.PolymerColor));
        }

        [Test]
        public void TheMuzzleIsAtTheFrontAndTheSightAboveTheBore()
        {
            foreach (var pair in Looks())
            {
                var look = pair.Value;
                Assert.That(look.Muzzle.z, Is.GreaterThanOrEqualTo(look.Bounds.max.z - 1e-4f), pair.Key);
                Assert.That(look.Sight.y, Is.GreaterThan(look.Muzzle.y), pair.Key);
                Assert.That(look.Sight.z, Is.LessThan(look.Muzzle.z), pair.Key);
            }
        }

        [Test]
        public void BuildPlacesEveryPartWithoutColliders()
        {
            var look = WeaponLook.For(WeaponAssetBuilder.RifleSpec());
            var root = new GameObject("Gun");
            try
            {
                var painted = new List<(WeaponPaint, Color)>();
                look.Build(root.transform, (paint, color) =>
                {
                    painted.Add((paint, color));
                    return null;
                }, castShadows: false);

                Assert.That(root.transform.childCount, Is.EqualTo(look.Parts.Count));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(painted, Does.Contain((WeaponPaint.Furniture, WeaponLook.PolymerColor)));
                var barrel = root.transform.Find(WeaponLook.BarrelName);
                Assert.That(barrel.GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(barrel.up.z, Is.EqualTo(1f).Within(1e-4f), "a cylinder part points its axis forward");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
