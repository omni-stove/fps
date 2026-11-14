using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainChangeSpecTests
    {
        [Test]
        public void ChangeSpecSwapsTheSettingsAndKeepsTheState()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2));
            brain.SetPatrolRoute(new[] { new System.Numerics.Vector3(1f, 0f, 0f) });
            brain.RecordShot();
            var faster = new EnemySpec(30f, 15f, 1f, 2f, 0.1f, 2f, 0f, 4);

            brain.ChangeSpec(faster);

            Assert.That(brain.Spec, Is.SameAs(faster));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(brain.ShotsInAttack, Is.EqualTo(1));
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(faster.SpreadDegreesForShot(1)));
        }

        [Test]
        public void ChangeSpecRefusesNull()
        {
            var brain = new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0.5f));

            Assert.Throws<ArgumentNullException>(() => brain.ChangeSpec(null));
        }
    }
}
