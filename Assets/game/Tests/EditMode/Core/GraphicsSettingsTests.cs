using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GraphicsSettingsTests
    {
        private static readonly GraphicsSettings _chosen = new GraphicsSettings(new ScreenResolution(1920, 1080), 2, true, 144);

        private static readonly string[] _rootProblems = { "the root must be an object" };

        [Test]
        public void TheDefaultKeepsTheDisplayTheProjectLevelAndNoLimit()
        {
            var settings = GraphicsSettings.Default;

            Assert.That(settings.Resolution, Is.EqualTo(ScreenResolution.Native));
            Assert.That(settings.Resolution.IsNative, Is.True);
            Assert.That(settings.QualityLevel, Is.EqualTo(GraphicsSettings.ProjectQualityLevel));
            Assert.That(settings.VSync, Is.False);
            Assert.That(settings.FrameRateLimit, Is.EqualTo(GraphicsSettings.NoFrameRateLimit));
            Assert.That(settings.HasFrameRateLimit, Is.False);
        }

        [TestCase(0, true)]
        [TestCase(14, false)]
        [TestCase(15, true)]
        [TestCase(60, true)]
        [TestCase(1000, true)]
        [TestCase(1001, false)]
        [TestCase(-1, false)]
        public void TheFrameRateLimitIsNoneOrFifteenToAThousand(int limit, bool valid)
        {
            Assert.That(GraphicsSettings.IsValidFrameRateLimit(limit), Is.EqualTo(valid));
            if (valid)
            {
                Assert.That(GraphicsSettings.Default.WithFrameRateLimit(limit).FrameRateLimit, Is.EqualTo(limit));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => GraphicsSettings.Default.WithFrameRateLimit(limit));
            }
        }

        [TestCase(-1, true)]
        [TestCase(-2, false)]
        [TestCase(0, true)]
        [TestCase(63, true)]
        [TestCase(64, false)]
        public void TheQualityLevelIsTheProjectsOrZeroToSixtyThree(int level, bool valid)
        {
            Assert.That(GraphicsSettings.IsValidQualityLevel(level), Is.EqualTo(valid));
            if (valid)
            {
                Assert.That(GraphicsSettings.Default.WithQualityLevel(level).QualityLevel, Is.EqualTo(level));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => GraphicsSettings.Default.WithQualityLevel(level));
            }
        }

        [TestCase(0, 0, true)]
        [TestCase(320, 320, true)]
        [TestCase(16384, 16384, true)]
        [TestCase(319, 240, false)]
        [TestCase(1920, 0, false)]
        [TestCase(0, 1080, false)]
        [TestCase(16385, 1080, false)]
        [TestCase(-1920, 1080, false)]
        public void AResolutionIsTheDisplaysOrBothSidesInRange(int width, int height, bool valid)
        {
            Assert.That(ScreenResolution.IsValid(width, height), Is.EqualTo(valid));
            if (valid)
            {
                var resolution = new ScreenResolution(width, height);
                Assert.That(resolution.Width, Is.EqualTo(width));
                Assert.That(resolution.Height, Is.EqualTo(height));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenResolution(width, height));
            }
        }

        [Test]
        public void TheWithMethodsChangeOnlyTheirValue()
        {
            var settings = GraphicsSettings.Default
                .WithResolution(new ScreenResolution(1280, 720))
                .WithQualityLevel(1)
                .WithVSync(true)
                .WithFrameRateLimit(60);

            Assert.That(settings, Is.EqualTo(new GraphicsSettings(new ScreenResolution(1280, 720), 1, true, 60)));
            Assert.That(settings.WithVSync(true), Is.SameAs(settings), "an unchanged value keeps the same object");
            Assert.That(GraphicsSettings.Default, Is.EqualTo(new GraphicsSettings(ScreenResolution.Native, -1, false, 0)), "the default is not changed");
        }

        [Test]
        public void EqualSettingsHaveTheSameHash()
        {
            var copy = new GraphicsSettings(new ScreenResolution(1920, 1080), 2, true, 144);

            Assert.That(copy, Is.EqualTo(_chosen));
            Assert.That(copy.GetHashCode(), Is.EqualTo(_chosen.GetHashCode()));
            Assert.That(copy.WithVSync(false), Is.Not.EqualTo(_chosen));
            Assert.That(_chosen.Equals(null), Is.False);
        }

        [Test]
        public void JsonRoundTrips()
        {
            var json = _chosen.ToJson();

            Assert.That(
                json,
                Is.EqualTo("{\"version\":1,\"resolutionWidth\":1920,\"resolutionHeight\":1080,\"qualityLevel\":2,\"vSync\":true,\"frameRateLimit\":144}"));
            var read = GraphicsSettings.FromJson(json, GraphicsSettings.Default, out var problems);
            Assert.That(read, Is.EqualTo(_chosen));
            Assert.That(problems, Is.Empty);

            var defaults = GraphicsSettings.FromJson(GraphicsSettings.Default.ToJson(), _chosen, out problems);
            Assert.That(defaults, Is.EqualTo(GraphicsSettings.Default), "saved values win over the defaults");
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void BrokenJsonGivesTheDefaultsPassedIn()
        {
            Assert.That(GraphicsSettings.FromJson("{not json", _chosen, out var problems), Is.SameAs(_chosen));
            Assert.That(problems[0], Does.StartWith("not valid JSON"));

            Assert.That(GraphicsSettings.FromJson("[]", _chosen, out problems), Is.SameAs(_chosen));
            Assert.That(problems, Is.EqualTo(_rootProblems));

            Assert.That(GraphicsSettings.FromJson("{\"version\":2,\"vSync\":false}", _chosen, out problems), Is.SameAs(_chosen));
            Assert.That(problems[0], Does.StartWith("unsupported version"));

            Assert.Throws<ArgumentNullException>(() => GraphicsSettings.FromJson(null, _chosen, out _));
            Assert.Throws<ArgumentNullException>(() => GraphicsSettings.FromJson("{}", null, out _));
        }

        [Test]
        public void MissingValuesAreFilledFromTheDefaults()
        {
            var read = GraphicsSettings.FromJson("{\"version\":1,\"vSync\":false}", _chosen, out var problems);

            Assert.That(read, Is.EqualTo(new GraphicsSettings(_chosen.Resolution, _chosen.QualityLevel, false, _chosen.FrameRateLimit)));
            Assert.That(problems, Is.EqualTo(new List<string>
            {
                "missing 'resolutionWidth' and 'resolutionHeight'",
                "missing 'qualityLevel'",
                "missing 'frameRateLimit'",
            }));
        }

        [Test]
        public void AFrameRateOutOfRangeIsMovedToTheNearestLimit()
        {
            var high = GraphicsSettings.FromJson(Json(0, 0, -1, false, 5000), GraphicsSettings.Default, out var problems);
            Assert.That(high.FrameRateLimit, Is.EqualTo(1000));
            Assert.That(problems[0], Does.Contain("'frameRateLimit' 5000").And.EndWith("using 1000"));

            var low = GraphicsSettings.FromJson(Json(0, 0, -1, false, 5), GraphicsSettings.Default, out problems);
            Assert.That(low.FrameRateLimit, Is.EqualTo(15), "a too-small limit stays a limit rather than turning into no limit");
            Assert.That(problems, Has.Count.EqualTo(1));

            var negative = GraphicsSettings.FromJson(Json(0, 0, -1, false, -30), GraphicsSettings.Default, out problems);
            Assert.That(negative.FrameRateLimit, Is.EqualTo(GraphicsSettings.NoFrameRateLimit));
            Assert.That(problems, Has.Count.EqualTo(1));
        }

        [Test]
        public void ABadResolutionOrQualityIsReported()
        {
            var resolution = GraphicsSettings.FromJson(Json(100, 100, 1, true, 60), _chosen, out var problems);
            Assert.That(resolution.Resolution, Is.EqualTo(ScreenResolution.Native));
            Assert.That(resolution.QualityLevel, Is.EqualTo(1));
            Assert.That(problems[0], Does.StartWith("resolution 100x100"));

            var quality = GraphicsSettings.FromJson(Json(0, 0, 99, true, 60), _chosen, out problems);
            Assert.That(quality.QualityLevel, Is.EqualTo(_chosen.QualityLevel));
            Assert.That(problems[0], Does.StartWith("'qualityLevel' 99"));

            var types = GraphicsSettings.FromJson(
                "{\"version\":1,\"resolutionWidth\":\"wide\",\"resolutionHeight\":1080,\"qualityLevel\":true,\"vSync\":1,\"frameRateLimit\":\"60\",\"echo\":1}",
                _chosen,
                out problems);
            Assert.That(types, Is.EqualTo(_chosen));
            Assert.That(problems, Is.EqualTo(new List<string>
            {
                "ignored unknown key 'echo'",
                "'resolutionWidth' and 'resolutionHeight' must both be integers",
                "'qualityLevel' must be an integer",
                "'vSync' must be true or false",
                "'frameRateLimit' must be an integer",
            }));
        }

        private static string Json(int width, int height, int quality, bool vSync, int frameRate)
        {
            return "{\"version\":1,\"resolutionWidth\":" + Format(width) + ",\"resolutionHeight\":" + Format(height)
                + ",\"qualityLevel\":" + Format(quality) + ",\"vSync\":" + (vSync ? "true" : "false")
                + ",\"frameRateLimit\":" + Format(frameRate) + "}";
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
