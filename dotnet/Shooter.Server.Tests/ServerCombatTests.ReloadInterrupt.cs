using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        [Test]
        public void ThrowingAGrenadeCancelsTheReloadAndHoldsOffANewOneUntilTheThrowEnds()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            RequestReload(1);
            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.True, _log.ToString());

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });

            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Combat] reload-cancelled player=1 by=Grenade"));
            RequestReload(1);
            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.False, "no reload while throwing");

            Step(GrenadeSpec.DefaultThrowSeconds + 0.1f);
            RequestReload(1);
            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.True);
        }

        [Test]
        public void WithoutGrenadeInReloadInterruptedByTheReloadGoesOnThroughTheThrow()
        {
            var rules = Rules();
            Start(rules.WithActions(rules.Actions.WithReloadInterruptedBy(new[] { ReloadInterrupt.Melee })));
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            RequestReload(1);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(1));
            Assert.That(Snapshot(1).Owner.Weapon.IsReloading, Is.True);
            Assert.That(_log.ToString(), Does.Not.Contain("reload-cancelled"));
        }

        private void RequestReload(int id)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new WeaponActionRequestMessage { Action = WeaponAction.Reload });
        }
    }
}
