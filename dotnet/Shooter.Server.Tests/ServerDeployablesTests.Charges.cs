using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 置く要求を、種類の能力の値 (回数・回数の戻り・次に置けるまで) でサーバーが数え直して断ること・通すことを確かめる
    public sealed partial class ServerDeployablesTests
    {
        // 盾は幅 2 m を z に沿わせて (90 度) 並べるので、z を 2.5 m ずつ離せば重ならない
        private static readonly Vector3 _shieldLeft = new Vector3(-3f, 0.5f, -2.5f);
        private static readonly Vector3 _shieldRight = new Vector3(-3f, 0.5f, 2.5f);
        private static readonly Vector3 _shieldMiddle = new Vector3(-3f, 0.5f, 0f);

        [Test]
        public void WithNoChargeLeftAPlacementIsRefusedUntilTheChargeComesBack()
        {
            Start(Catalog(shieldAbility: new AbilitySpec(1, 2f, 0.5f, 0f)));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _shieldId, _shieldLeft, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.NoCharges));
            Assert.That(_deployables.Count, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield net refused reason=NoCharges charges=0/1 wait=2.00"));

            Step(1.5f);
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.NoCharges), "half a second is still left");

            Step(0.6f);
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Assert.That(_deployables.Count, Is.EqualTo(2));
        }

        [Test]
        public void APlacementInsideTheCooldownIsRefusedButOneArrivingJustEarlyIsLetThrough()
        {
            Start(Catalog(shieldAbility: new AbilitySpec(3, 0f, 1f, 0f)));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _shieldId, _shieldLeft, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.CoolingDown));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield net refused reason=CoolingDown charges=2/3 wait=1.00"));

            Step(0.5f);
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.CoolingDown));

            // 残りは 0.15 秒ほどで、届くまでの揺れとして許す幅 (UseArrivalToleranceSeconds) の内
            Assert.That(ServerDeployables.UseArrivalToleranceSeconds, Is.EqualTo(0.2f));
            Step(0.35f);
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());

            // 間隔は置けた時から数え直す
            Assert.That(_deployables.Place(1, _shieldId, _shieldMiddle, 90f), Is.EqualTo(ServerDeployRefusal.CoolingDown));
            Assert.That(_deployables.Count, Is.EqualTo(2));
        }

        [Test]
        public void ARefusedPlacementKeepsTheChargeAndRespawningFillsTheCharges()
        {
            Start(Catalog(shieldAbility: new AbilitySpec(1, 0f, 0f, 0f)));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _shieldId, new Vector3(5f, 0.5f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.OutOfReach));
            Assert.That(_deployables.Place(1, _shieldId, _shieldLeft, 90f), Is.EqualTo(ServerDeployRefusal.None), "the refused request did not use the charge");

            // 断った理由の行は 1 秒に 1 度なので、次の理由が行に出るまで待つ。回数は戻らない枠なので待っても戻らない
            Step(1.1f);
            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.NoCharges));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield net refused reason=NoCharges charges=0/1 wait=never"));

            Assert.That(_deployables.Place(2, _shieldId, _bravoFront, 90f), Is.EqualTo(ServerDeployRefusal.None), "each player has their own charges");

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(NetMatchSettings.DefaultRespawnSeconds + 0.2f);
            Assert.That(_rules.Session.IsAlive(1), Is.True, _log.ToString());

            Assert.That(_deployables.Place(1, _shieldId, _shieldRight, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
        }
    }
}
