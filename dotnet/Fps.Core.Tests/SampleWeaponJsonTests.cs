using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // サンプルの武器の JSON は後の武器を足すときの手本になるので、Write が書く部品の形のまま置かれていることを確かめる。
    public sealed class SampleWeaponJsonTests
    {
        [Test]
        public void SampleWeaponJsonReadsAndIsWrittenTheSameWay()
        {
            var files = Directory.GetFiles(SampleWeaponJsonFolder(), "*.json");
            Assert.That(files, Is.Not.Empty);

            var ids = new HashSet<string>();
            foreach (var path in files)
            {
                var text = File.ReadAllText(path);
                var spec = WeaponSpecJson.Parse(text, path);

                Assert.That(WeaponSpecJson.Write(spec), Is.EqualTo(text), path);
                Assert.That(ids.Add(spec.Id), Is.True, path + " reuses the id " + spec.Id);
            }
        }

        [Test]
        public void TheCrossbowShootsASlowDroppingBoltThatSticksInTheWall()
        {
            var path = Path.Combine(SampleWeaponJsonFolder(), "Crossbow.json");
            var spec = WeaponSpecJson.Parse(File.ReadAllText(path), path);
            var projectile = spec.Round.Projectile;

            Assert.That(spec.NameKey, Is.EqualTo("weapon.crossbow"));
            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Projectile));
            Assert.That(projectile.OnWorldHit, Is.EqualTo(WorldHitResponse.Stick));
            Assert.That(projectile.GravityMultiplier, Is.GreaterThan(0f));

            var world = new FakeWorldCollision();
            world.AddBox(new Vector3(-5f, -50f, 40f), new Vector3(5f, 50f, 41f));
            var flight = new BallisticFlight(projectile, null, world, ~0, Vector3.Zero, Vector3.UnitZ, new Vector3(0f, -9.81f, 0f));
            while (!flight.IsDone)
            {
                flight.Step(1f / 60f);
            }

            Assert.That(flight.State, Is.EqualTo(BallisticState.Stuck));
            Assert.That(flight.Position.Y, Is.LessThan(-1f), "a target 40 m away needs the aim held above it");
        }

        [Test]
        public void TheGuidedLauncherLocksOnAndItsRocketChasesATargetItWouldMissFlyingStraight()
        {
            var path = Path.Combine(SampleWeaponJsonFolder(), "GuidedLauncher.json");
            var spec = WeaponSpecJson.Parse(File.ReadAllText(path), path);
            var homing = spec.Round.Homing;

            Assert.That(spec.NameKey, Is.EqualTo("weapon.guidedLauncher"));
            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Projectile));
            Assert.That(homing, Is.Not.Null);
            Assert.That(homing.LockOnSeconds, Is.GreaterThan(0f));
            Assert.That(spec.Impact.Explosion, Is.Not.Null);

            // 正面から 10 度ずれて横へ歩く的。まっすぐ飛ぶと外れる
            var target = new Vector3(5f, 0f, 30f);
            var flight = new BallisticFlight(spec.Round.Projectile, homing, new FakeWorldCollision(), ~0, Vector3.Zero, Vector3.UnitZ, new Vector3(0f, -9.81f, 0f));
            var closest = float.MaxValue;
            while (!flight.IsDone)
            {
                target += new Vector3(3f / 60f, 0f, 0f);
                flight.Step(1f / 60f, target);
                closest = System.Math.Min(closest, Vector3.Distance(flight.Position, target));
            }

            Assert.That(closest, Is.LessThan(0.5f));
        }

        [Test]
        public void TheShotgunLoadsOneShellAtATime()
        {
            var path = Path.Combine(SampleWeaponJsonFolder(), "Shotgun.json");
            var spec = WeaponSpecJson.Parse(File.ReadAllText(path), path);
            var weapon = new Weapon(spec, 0, spec.ReserveAmmo);

            Assert.That(weapon.Ammo, Is.TypeOf<PerRoundReload>());
            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(spec.Ammo.ReloadSecondsPerRound);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(1));
            Assert.That(weapon.TryFire(), Is.True, "a loaded shell fires mid-reload");
            Assert.That(weapon.IsReloading, Is.False);
        }

        [Test]
        public void TheFlamethrowerSpraysAShortConeThatBurnsAndIsManagedByHeat()
        {
            var path = Path.Combine(SampleWeaponJsonFolder(), "Flamethrower.json");
            var spec = WeaponSpecJson.Parse(File.ReadAllText(path), path);
            var spray = spec.Round.Spray;

            Assert.That(spec.NameKey, Is.EqualTo("weapon.flamethrower"));
            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Spray));
            Assert.That(spec.Ammo.Kind, Is.EqualTo(AmmoKind.Heat));
            Assert.That(spec.Impact.StatusEffect.KindId, Is.EqualTo(StatusEffectKinds.Burn));
            Assert.That(spec.Round.Range, Is.LessThanOrEqualTo(10f), "a short range");

            var origin = Vector3.Zero;
            Assert.That(SprayCone.Contains(spray, spec.Round.Range, origin, Vector3.UnitZ, new Vector3(0f, 0f, spec.Round.Range * 0.5f)), Is.True);
            Assert.That(SprayCone.Contains(spray, spec.Round.Range, origin, Vector3.UnitZ, new Vector3(0f, 0f, spec.Round.Range + 1f)), Is.False);

            // 押しっぱなしで数秒噴くと熱で止まる
            var weapon = new Weapon(spec);
            var ticks = 0;
            weapon.Trigger.Press();
            while (weapon.TryFire() && ticks < 1000)
            {
                ticks++;
                weapon.Tick(spray.TickSeconds);
                weapon.Trigger.Tick(spray.TickSeconds);
            }

            Assert.That(ticks * spray.TickSeconds, Is.InRange(2f, 8f), "seconds of flame before overheating");
        }

        private static string SampleWeaponJsonFolder()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "Assets", "game", "Samples~", "Demo", "Weapons", "Json");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            Assert.Fail("Assets/game/Samples~/Demo/Weapons/Json is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
