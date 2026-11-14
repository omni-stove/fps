using System;
using NUnit.Framework;

namespace Fps.EosSdk.Tests
{
    // SDK を置いた手元でも置いていない CI でも同じテストが通るよう、どちらのビルドかで期待を分ける
    public sealed class EosSdkBuildTests
    {
        [Test]
        public void IncludedMatchesWhetherTheSdkTypesAreCompiledIn()
        {
            var sdkType = typeof(EosSdkBuild).Assembly.GetType("Epic.OnlineServices.Common");

            Assert.That(sdkType != null, Is.EqualTo(EosSdkBuild.Included));
        }

        [Test]
        public void WithoutTheSdkTheLogLineSaysItIsMissing()
        {
            if (EosSdkBuild.Included)
            {
                Assert.Pass("this build includes the EOS SDK");
            }

            Assert.That(EosSdkBuild.NativeLibraryFile, Is.Null);
            Assert.That(EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory), Is.False);
            Assert.That(EosSdkBuild.LogLine(AppContext.BaseDirectory), Does.StartWith("[Eos] sdk=missing"));
            Assert.Throws<InvalidOperationException>(() => EosSdkBuild.NativeVersion());
        }

        [Test]
        public void WithTheSdkTheNativeLibraryAnswersItsVersion()
        {
            if (!EosSdkBuild.Included)
            {
                Assert.Pass("this build does not include the EOS SDK");
            }

            Assert.That(EosSdkBuild.NativeLibraryFile, Is.Not.Empty);
            if (!EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory))
            {
                Assert.That(EosSdkBuild.LogLine(AppContext.BaseDirectory), Does.Contain("(missing;"));
                return;
            }

            Assert.That(EosSdkBuild.NativeVersion(), Does.Match(@"^\d+\.\d+"));
            Assert.That(EosSdkBuild.LogLine(AppContext.BaseDirectory), Does.Contain(" version=" + EosSdkBuild.NativeVersion()));
        }

        [Test]
        public void HasNativeLibraryRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => EosSdkBuild.HasNativeLibrary(null));
        }
    }
}
