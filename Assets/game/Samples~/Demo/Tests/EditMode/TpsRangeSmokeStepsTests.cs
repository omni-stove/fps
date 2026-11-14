using System.Linq;
using Fps.Editor;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary>smoke の足された手順 (<see cref="TpsRangeSmokeRun.ExtraSteps"/>) が決まりを守っていることを確かめる。</summary>
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
