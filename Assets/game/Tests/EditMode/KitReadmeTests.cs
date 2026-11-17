using System.IO;
using System.Linq;
using System.Reflection;
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
        public void TheReadmeListsEveryGameSignal()
        {
            var readme = ReadReadme();
            var signals = typeof(GameSignals)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(GameSignal<>))
                .Select(p => p.Name)
                .ToList();

            Assert.That(signals, Has.Count.EqualTo(20));
            foreach (var name in signals)
            {
                Assert.That(readme, Does.Contain("| `" + name + "` |"), name);
            }
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
