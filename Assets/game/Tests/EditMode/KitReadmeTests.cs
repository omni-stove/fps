using System.IO;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class KitReadmeTests
    {
        private static string ReadReadme()
        {
            return File.ReadAllText(ShooterKitRoot.PhysicalPath(ShooterKitRoot.BundledPath("README.md")));
        }

        [Test]
        public void TheReadmePointsAtTheGameSignalsSourceInsteadOfListingThem()
        {
            var readme = ReadReadme();
            const string source = "Scripts/Runtime/GameSignals.cs";

            Assert.That(readme, Does.Contain("`" + source + "`"));
            Assert.That(File.Exists(ShooterKitRoot.PhysicalPath(ShooterKitRoot.BundledPath(source))), Is.True);
            Assert.That(readme, Does.Not.Contain("| `" + nameof(GameSignals.LocalPlayer) + "` |"));
        }

        [Test]
        public void TheReadmeNamesTheSubscriptionMembers()
        {
            var readme = ReadReadme();

            Assert.That(readme, Does.Contain("`" + nameof(GameSignal<object>.Value) + "`"));
            Assert.That(readme, Does.Contain("`" + nameof(GameSignal<object>.HasValue) + "`"));
            Assert.That(readme, Does.Contain("`" + nameof(GameSignal<object>.Changed) + "`"));
            Assert.That(readme, Does.Contain("`ModeStatus." + nameof(Shooter.Core.ModeStatus.Detail) + "`"));
            Assert.That(readme, Does.Contain("GameSignals.HudModel.Changed += Bind;"));
        }
    }
}
