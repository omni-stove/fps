using System.Linq;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class TpsRangeSmokeStepsTests
    {
        [Test]
        public void TheSmokeStepsHaveNamesAndTimeouts()
        {
            var steps = TpsRangeSmokeRun.ExtraSteps();

            foreach (var step in steps)
            {
                Assert.That(step.Name, Is.Not.Empty, step.GetType().FullName);
                Assert.That(step.TimeoutSeconds, Is.GreaterThan(0f), step.Name);
            }

            Assert.That(steps.Select(s => s.Order), Is.Ordered);
        }
    }
}
