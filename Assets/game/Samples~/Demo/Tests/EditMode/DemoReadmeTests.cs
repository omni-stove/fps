using System.IO;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    public sealed class DemoReadmeTests
    {
        private static string ReadReadme()
        {
            return File.ReadAllText(FpsKitRoot.PhysicalPath(FpsKitRoot.BundledPath("README.md")));
        }

        [Test]
        public void TheReadmeNamesTheSampleUiEntryPoints()
        {
            var readme = ReadReadme();

            Assert.That(readme, Does.Contain(nameof(DefaultHudFactory) + "." + nameof(DefaultHudFactory.Enabled) + " = false;"));
            Assert.That(readme, Does.Contain(nameof(ModeHudFormatters) + "." + nameof(ModeHudFormatters.Register) + "<TDetail>(formatter)"));
            Assert.That(readme, Does.Contain(nameof(ModeHudFormatters) + "." + nameof(ModeHudFormatters.Default)));
            Assert.That(readme, Does.Contain(nameof(ModeHudFormatterBase)));
        }

        [Test]
        public void TheReadmeNamesWhereTheChosenLanguageIsSaved()
        {
            var readme = ReadReadme();

            Assert.That(LanguagePreferenceStore.DefaultKey, Is.EqualTo("Fps.Language"));
            Assert.That(readme, Does.Contain("`" + LanguagePreferenceStore.DefaultKey + "`"));
            Assert.That(readme, Does.Contain("Resources/" + Localizer.LanguageListFolder + "/zz-mygame.json"));
            Assert.That(readme, Does.Contain("Resources/" + Localizer.ResourceFolder + "/<言語コード>.json"));
        }
    }
}
