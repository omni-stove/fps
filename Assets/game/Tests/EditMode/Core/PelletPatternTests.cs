using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PelletPatternTests
    {
        private static readonly RoundSpec _shotgun = new RoundSpec(range: 40f, pelletCount: 8, pelletSpreadDegrees: 5f);

        [Test]
        public void CountIsThePelletCount()
        {
            Assert.That(PelletPattern.Count(_shotgun), Is.EqualTo(8));
            Assert.That(PelletPattern.Count(RoundSpec.Default), Is.EqualTo(1));
        }

        [Test]
        public void TheSameSeedGivesTheSameDirection()
        {
            var center = Vector3.Normalize(new Vector3(0.3f, -0.2f, 1f));

            for (var shot = 0; shot < 20; shot++)
            {
                for (var pellet = 0; pellet < 8; pellet++)
                {
                    Assert.That(
                        PelletPattern.Direction(_shotgun, center, 42, shot, pellet),
                        Is.EqualTo(PelletPattern.Direction(new RoundSpec(pelletCount: 8, pelletSpreadDegrees: 5f), center, 42, shot, pellet)));
                }
            }
        }

        [Test]
        public void PelletsOfOneShotGoDifferentWays()
        {
            var directions = new HashSet<Vector3>();
            for (var pellet = 0; pellet < 8; pellet++)
            {
                directions.Add(PelletPattern.Direction(_shotgun, Vector3.UnitZ, 1, 0, pellet));
            }

            Assert.That(directions, Has.Count.EqualTo(8));
        }

        [Test]
        public void TheShooterAndTheShotChangeThePattern()
        {
            var baseline = PelletPattern.Direction(_shotgun, Vector3.UnitZ, 1, 0, 3);

            Assert.That(PelletPattern.Direction(_shotgun, Vector3.UnitZ, 2, 0, 3), Is.Not.EqualTo(baseline));
            Assert.That(PelletPattern.Direction(_shotgun, Vector3.UnitZ, 1, 1, 3), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void EveryPelletStaysInsideTheConeAroundTheCenter()
        {
            var center = new Vector3(1f, 0.5f, -2f);

            for (var shot = 0; shot < 200; shot++)
            {
                for (var pellet = 0; pellet < 8; pellet++)
                {
                    var direction = PelletPattern.Direction(_shotgun, center, 7, shot, pellet);

                    Assert.That(direction.Length(), Is.EqualTo(1f).Within(1e-5f));
                    Assert.That(AngleDegrees(center, direction), Is.LessThanOrEqualTo(5f + 1e-3f));
                }
            }
        }

        [Test]
        public void ThePelletConeDoesNotRepeatTheSpreadOfTheCenter()
        {
            // 中心の散らばりと同じ値の系列を使うと、ペレットが中心のずれと同じ向きへ揃ってずれる。
            var aim = Vector3.UnitZ;
            var single = new RoundSpec(pelletCount: 1, pelletSpreadDegrees: 5f);

            for (var shot = 0; shot < 20; shot++)
            {
                Assert.That(
                    PelletPattern.Direction(single, aim, 3, shot, 0),
                    Is.Not.EqualTo(SpreadPattern.Direction(aim, 5f, 3, shot)));
            }
        }

        [Test]
        public void NoPelletSpreadKeepsTheCenterAsIs()
        {
            var center = new Vector3(0f, 0f, 3f);
            var tight = new RoundSpec(pelletCount: 4, pelletSpreadDegrees: 0f);

            for (var pellet = 0; pellet < 4; pellet++)
            {
                Assert.That(PelletPattern.Direction(tight, center, 1, 2, pellet), Is.EqualTo(center));
            }
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => PelletPattern.Count(null));
            Assert.Throws<ArgumentNullException>(() => PelletPattern.Direction(null, Vector3.UnitZ, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PelletPattern.Direction(_shotgun, Vector3.UnitZ, 0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PelletPattern.Direction(_shotgun, Vector3.UnitZ, 0, 0, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => PelletPattern.Direction(_shotgun, Vector3.Zero, 0, 0, 0));
        }

        private static float AngleDegrees(Vector3 a, Vector3 b)
        {
            var cos = Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b));
            return (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * 180.0 / Math.PI);
        }
    }
}
