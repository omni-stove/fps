using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TeamAssignerTests
    {
        [Test]
        public void AlternatesStartingWithAlpha()
        {
            var assigner = new TeamAssigner();

            Assert.That(assigner.Assign(), Is.EqualTo(Team.Alpha));
            Assert.That(assigner.Assign(), Is.EqualTo(Team.Bravo));
            Assert.That(assigner.Assign(), Is.EqualTo(Team.Alpha));
            Assert.That(assigner.Assign(), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void PeekDoesNotAdvance()
        {
            var assigner = new TeamAssigner();

            Assert.That(assigner.Peek, Is.EqualTo(Team.Alpha));
            Assert.That(assigner.Peek, Is.EqualTo(Team.Alpha));
            assigner.Assign();
            Assert.That(assigner.Peek, Is.EqualTo(Team.Bravo));
        }
    }
}
