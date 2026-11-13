using System;
using System.IO;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    /// <summary>実際のビルドは <c>make build</c> / <c>make build-server</c> に任せ、版の決め方と bundleVersion を戻すことだけを見る。</summary>
    public sealed class BuildVersionStamperTests
    {
        private string _original;

        [SetUp]
        public void SetUp()
        {
            _original = PlayerSettings.bundleVersion;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerSettings.bundleVersion = _original;
        }

        [Test]
        public void ResolvePrefersArgument()
        {
            var version = BuildVersionStamper.Resolve(new[] { "Unity", "-batchmode", "-buildVersion", "v0.2.0" });

            Assert.That(version, Is.EqualTo("0.2.0"));
        }

        [Test]
        public void ResolveRejectsInvalidArgument()
        {
            Assert.Throws<ArgumentException>(() => BuildVersionStamper.Resolve(new[] { "-buildVersion" }));
            Assert.Throws<ArgumentException>(() => BuildVersionStamper.Resolve(new[] { "-buildVersion", "1.0+build" }));
            Assert.Throws<ArgumentNullException>(() => BuildVersionStamper.Resolve(null));
        }

        [Test]
        public void StampsDuringBuildAndRestoresAfter()
        {
            var seen = BuildVersionStamper.WithVersion("v9.8.7-test", () => PlayerSettings.bundleVersion);

            Assert.That(seen, Is.EqualTo("9.8.7-test"));
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(_original));
        }

        [Test]
        public void RestoresWhenBuildThrows()
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildVersionStamper.WithVersion<int>("9.8.7-test", () => throw new InvalidOperationException("build failed")));

            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(_original));
        }

        [Test]
        public void RejectsInvalidVersionWithoutTouchingSettings()
        {
            var called = false;
            Assert.Throws<ArgumentException>(() => BuildVersionStamper.WithVersion("", () => called = true));

            Assert.That(called, Is.False);
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(_original));
            Assert.Throws<ArgumentNullException>(() => BuildVersionStamper.WithVersion<int>("1.0", null));
        }

        [Test]
        public void DescribeFailsOutsideARepository()
        {
            var directory = Path.Combine(Path.GetTempPath(), "fps-build-version-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                // git が無い環境でも、リポジトリでない場所でも、どちらも InvalidOperationException になる
                Assert.Throws<InvalidOperationException>(() => BuildVersionStamper.DescribeWithGit(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }

            Assert.Throws<ArgumentException>(() => BuildVersionStamper.DescribeWithGit(""));
        }
    }
}
