using System;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class UnlocksTests
    {
        private Func<string, bool> _installed;

        [SetUp]
        public void SetUp()
        {
            // 本体のテストは進行のアセンブリを引かないので、Play から残った門 (進行の記録係が入れたものなど) は門ごと外す。
            Unlocks.Uninstall(Unlocks.Gate);
            _installed = null;
        }

        [TearDown]
        public void TearDown()
        {
            Unlocks.Uninstall(_installed);
        }

        [Test]
        public void WithoutAGateEverythingIsUnlocked()
        {
            Assert.That(Unlocks.Gate, Is.Null);
            Assert.That(Unlocks.IsUnlocked("scope"), Is.True);
            Assert.That(Unlocks.IsUnlocked("rifle"), Is.True);
        }

        [Test]
        public void TheInstalledGateAnswersEveryQuestion()
        {
            var asked = 0;
            Install(id =>
            {
                asked++;
                return id != "scope";
            });

            Assert.That(Unlocks.Gate, Is.SameAs(_installed));
            Assert.That(Unlocks.IsUnlocked("scope"), Is.False);
            Assert.That(Unlocks.IsUnlocked("rifle"), Is.True);
            Assert.That(asked, Is.EqualTo(2), "the answer is not remembered, so each question reaches the gate");
        }

        [Test]
        public void InstallingAnotherGateReplacesTheFirst()
        {
            Func<string, bool> first = _ => false;
            Unlocks.Install(first);
            Install(_ => true);

            Assert.That(Unlocks.Gate, Is.SameAs(_installed));
            Assert.That(Unlocks.IsUnlocked("scope"), Is.True);
        }

        [Test]
        public void UninstallTakesOutOnlyTheGateItIsGiven()
        {
            Func<string, bool> earlier = _ => false;
            Install(id => id != "scope");

            Unlocks.Uninstall(earlier);
            Unlocks.Uninstall(null);

            Assert.That(Unlocks.Gate, Is.SameAs(_installed), "someone else's gate stays");
            Assert.That(Unlocks.IsUnlocked("scope"), Is.False);

            Unlocks.Uninstall(_installed);

            Assert.That(Unlocks.Gate, Is.Null);
            Assert.That(Unlocks.IsUnlocked("scope"), Is.True);
        }

        [Test]
        public void RejectsAMissingGateAndAnEmptyId()
        {
            Assert.Throws<ArgumentNullException>(() => Unlocks.Install(null));
            Assert.Throws<ArgumentException>(() => Unlocks.IsUnlocked(""));
            Assert.Throws<ArgumentException>(() => Unlocks.IsUnlocked(null));
            Assert.That(Unlocks.Gate, Is.Null);
        }

        private void Install(Func<string, bool> gate)
        {
            _installed = gate;
            Unlocks.Install(gate);
        }
    }
}
