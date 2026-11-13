using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SideSpawnSelectorTests
    {
        private static readonly Side _red = new Side(1);
        private static readonly Side _blue = new Side(2);

        private static readonly SideSpawn[] _spawns =
        {
            new SideSpawn(Side.Alpha, new Vector3(0f, 0f, 0f)),
            new SideSpawn(_red, new Vector3(10f, 0f, 0f)),
            new SideSpawn(_red, new Vector3(-10f, 0f, 0f)),
            new SideSpawn(_blue, new Vector3(0f, 0f, 10f)),
        };

        [Test]
        public void PicksOnlyPointsOfTheSide()
        {
            Assert.That(SideSpawnSelector.Select(_spawns, Side.Alpha, Array.Empty<Vector3>()), Is.Zero);
            Assert.That(SideSpawnSelector.Select(_spawns, _blue, Array.Empty<Vector3>()), Is.EqualTo(3));
        }

        [Test]
        public void WithoutEnemiesTheFirstPointOfTheSideWins()
        {
            Assert.That(SideSpawnSelector.Select(_spawns, _red, Array.Empty<Vector3>()), Is.EqualTo(1));
        }

        [Test]
        public void PicksThePointFarthestFromTheNearestEnemy()
        {
            var enemies = new[] { new Vector3(8f, 0f, 0f), new Vector3(0f, 0f, 40f) };

            Assert.That(SideSpawnSelector.Select(_spawns, _red, enemies), Is.EqualTo(2));
        }

        [Test]
        public void ReturnsMinusOneWhenTheSideHasNoPoint()
        {
            Assert.That(SideSpawnSelector.Select(_spawns, new Side(3), Array.Empty<Vector3>()), Is.EqualTo(-1));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => SideSpawnSelector.Select(null, _red, Array.Empty<Vector3>()));
            Assert.Throws<ArgumentNullException>(() => SideSpawnSelector.Select(_spawns, _red, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SideSpawn(_red, new Vector3(0f, float.NaN, 0f)));
        }
    }
}
