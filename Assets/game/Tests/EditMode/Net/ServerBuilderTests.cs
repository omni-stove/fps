using System;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Net.Tests
{
    /// <summary>実際のビルドは重いので <c>make build-server</c> に任せ、ここではターゲットの対応だけを見る。</summary>
    public sealed class ServerBuilderTests
    {
        [TestCase(ServerPlatform.MacOS, BuildTarget.StandaloneOSX)]
        [TestCase(ServerPlatform.Linux, BuildTarget.StandaloneLinux64)]
        public void MapsPlatformToStandaloneTarget(ServerPlatform platform, BuildTarget expected)
        {
            Assert.That(ServerBuilder.ToBuildTarget(platform), Is.EqualTo(expected));
        }

        [Test]
        public void RejectsUnknownPlatform()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerBuilder.ToBuildTarget((ServerPlatform)99));
            Assert.Throws<ArgumentNullException>(() => ServerBuilder.BuildFor(null));
        }
    }
}
