using System.Collections.Generic;
using System.IO;
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
