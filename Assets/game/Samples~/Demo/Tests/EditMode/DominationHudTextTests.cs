using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class DominationHudTextTests
    {
        [Test]
        public void PointOwnerNamesThePointAndItsOwner()
        {
            Assert.That(GameText.Resolve(DominationHudText.PointOwner(0, Team.Bravo)), Is.EqualTo("A BRAVO"));
            Assert.That(GameText.Resolve(DominationHudText.PointOwner(1, null)), Is.EqualTo("B --"));
            Assert.That(GameText.Resolve(DominationHudText.PointOwner(26, null)), Is.EqualTo("P27 --"));
        }

        [Test]
        public void TeamScoreFloorsThePoints()
        {
            Assert.That(GameText.Resolve(DominationHudText.TeamScore(Team.Alpha, 12.9f)), Is.EqualTo("ALPHA 12"));
            Assert.That(GameText.Resolve(DominationHudText.TeamScore(Team.Bravo, -1f)), Is.EqualTo("BRAVO 0"));
        }
    }
}
