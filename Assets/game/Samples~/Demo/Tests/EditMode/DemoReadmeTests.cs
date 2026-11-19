using System.IO;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class DemoReadmeTests
    {
        private static string ReadReadme()
        {
            return File.ReadAllText(ShooterKitRoot.PhysicalPath(ShooterKitRoot.BundledPath("README.md")));
        }

        [TestCase(nameof(DefaultHudFactory))]
        [TestCase(nameof(ModeHudFormatters))]
        [TestCase(nameof(ModeHudFormatterBase))]
        [TestCase(nameof(Localizer))]
        [TestCase(nameof(LanguagePreferenceStore))]
        [TestCase("Shooter.Language")]
        public void TheKitReadmeLeavesTheSampleUiToTheSample(string sampleName)
        {
            Assert.That(ReadReadme(), Does.Not.Contain(sampleName));
        }

        [Test]
        public void TheKitReadmeNamesHowToImportTheSample()
        {
            var readme = ReadReadme();

            Assert.That(readme, Does.Contain("**Demo Game**"));
            Assert.That(readme, Does.Contain("`Samples~/Demo`"));
        }
    }
}
