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
