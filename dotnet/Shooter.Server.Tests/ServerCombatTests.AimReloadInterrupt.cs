using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        [Test]
        public void StartingToAimCancelsTheReloadWhenAimIsInReloadInterruptedBy()
        {
            var rules = Rules();
            Start(rules.WithActions(rules.Actions.WithReloadInterruptedBy(new[] { ReloadInterrupt.Melee, ReloadInterrupt.Grenade, ReloadInterrupt.Aim })));
            StartReloadingAfterAShot();

            Walk(1, Vector2.Zero, 1, InputButtons.Aim);

            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Combat] reload-cancelled player=1 by=Aim"));
        }

        [Test]
        public void AReloadRequestedWhileAlreadyAimingGoesOn()
        {
            var rules = Rules();
            Start(rules.WithActions(rules.Actions.WithReloadInterruptedBy(new[] { ReloadInterrupt.Aim })));
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            Walk(1, Vector2.Zero, 1, InputButtons.Aim);

            RequestReload(1);
            Walk(1, Vector2.Zero, 3, InputButtons.Aim);

            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.True, "only the press that starts the aim cancels a reload");
            Assert.That(_log.ToString(), Does.Not.Contain("reload-cancelled"));
        }

        [Test]
        public void WithoutAimInReloadInterruptedByAimingLeavesTheReloadAlone()
        {
            Start(Rules());
            StartReloadingAfterAShot();

            Walk(1, Vector2.Zero, 1, InputButtons.Aim);

            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.True);
            Assert.That(_log.ToString(), Does.Not.Contain("reload-cancelled"));
        }

        private void StartReloadingAfterAShot()
        {
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            RequestReload(1);
            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.True, _log.ToString());
        }
    }
}
