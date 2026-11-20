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
    }
}
