using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦のグレネード。サーバーの数と間隔 (<see cref="ServerGrenadeSupply"/>)、持ち主が要求を送るかの見込み、
    /// 爆発の距離 (<see cref="NetTeamDeathmatch.DistanceToCapsule"/>)・投げる位置と初速、プレハブの検査と登録、キルフィードの名前とログを確かめる。
    /// ネットワークは起動しない。
    /// </summary>
    public sealed class NetGrenadeTests
    {
        private const double _now = 100d;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private static ActionRules Grenades(int count)
        {
            return ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(count);
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        [Test]
        public void ASupplyIsDisabledUntilItIsRefilled()
        {
            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);

            Assert.That(supply.Enabled, Is.False);
            Assert.That(supply.State, Is.EqualTo(NetGrenadeState.None));
            Assert.That(supply.Check(_now), Is.EqualTo(GrenadeThrowRejection.Disabled));
        }

        [Test]
        public void RefillingFillsTheSupplyToTheRuleCount()
        {
            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);

            supply.Refill(Grenades(2));

            Assert.That(supply.Enabled, Is.True);
            Assert.That(supply.Capacity, Is.EqualTo(2));
            Assert.That(supply.State, Is.EqualTo(new NetGrenadeState(true, 2)));
            Assert.That(supply.Check(_now), Is.EqualTo(GrenadeThrowRejection.None));
        }

        [Test]
        public void TheRuleTurnedOffGivesNoGrenadesEvenWithACount()
        {
            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);

            supply.Refill(ActionRules.Default.WithGrenade(Grenade.Off).WithGrenadeCount(3));

            Assert.That(supply.State, Is.EqualTo(new NetGrenadeState(false, 0)));
            Assert.That(supply.Take(_now), Is.EqualTo(GrenadeThrowRejection.Disabled));
        }

        [Test]
        public void CheckingDoesNotTakeAGrenade()
        {
            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);
            supply.Refill(Grenades(1));

            supply.Check(_now);
            supply.Check(_now);

            Assert.That(supply.Remaining, Is.EqualTo(1));
        }

        [Test]
        public void TakingWaitsTheThrowTimeBeforeTheNextOne()
        {
            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);
            supply.Refill(Grenades(3));

            Assert.That(supply.Take(_now), Is.EqualTo(GrenadeThrowRejection.None));
            Assert.That(supply.Remaining, Is.EqualTo(2));

            Assert.That(supply.Take(_now + (GrenadeSpec.Default.ThrowSeconds * 0.5f)), Is.EqualTo(GrenadeThrowRejection.TooSoon));
            Assert.That(supply.Remaining, Is.EqualTo(2), "a rejected throw keeps the grenade");

            Assert.That(supply.Take(_now + GrenadeSpec.Default.ThrowSeconds), Is.EqualTo(GrenadeThrowRejection.None));
            Assert.That(supply.Remaining, Is.EqualTo(1));
        }

        [Test]
        public void AnEmptySupplyRejectsUntilTheNextRefill()
        {
            var supply = new ServerGrenadeSupply(0f);
            supply.Refill(Grenades(1));
            supply.Take(_now);

            Assert.That(supply.Take(_now + 10d), Is.EqualTo(GrenadeThrowRejection.Empty));
            Assert.That(supply.State, Is.EqualTo(new NetGrenadeState(true, 0)));

            supply.Refill(Grenades(1));
            Assert.That(supply.Take(_now + 10d), Is.EqualTo(GrenadeThrowRejection.None));
        }

        [Test]
        public void RefillingClearsTheWaitForTheNextThrow()
        {
            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);
            supply.Refill(Grenades(2));
            supply.Take(_now);

            supply.Refill(Grenades(2));

            Assert.That(supply.Check(_now), Is.EqualTo(GrenadeThrowRejection.None));
        }

        [Test]
        public void ASupplyRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerGrenadeSupply(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerGrenadeSupply(float.PositiveInfinity));

            var supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds);
            Assert.Throws<ArgumentNullException>(() => supply.Refill(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => supply.Check(double.NaN));
        }

        [Test]
        public void TheStateComparesByValue()
        {
            Assert.That(new NetGrenadeState(true, 2), Is.EqualTo(new NetGrenadeState(true, 2)));
            Assert.That(new NetGrenadeState(true, 2), Is.Not.EqualTo(new NetGrenadeState(true, 1)));
            Assert.That(new NetGrenadeState(true, 2), Is.Not.EqualTo(new NetGrenadeState(false, 2)));
            Assert.That(new NetGrenadeState(true, 1).CanThrow, Is.True);
            Assert.That(new NetGrenadeState(true, 0).CanThrow, Is.False);
            Assert.That(new NetGrenadeState(false, 3).CanThrow, Is.False);
        }

        [TestCase(true, false, false, true, 2, 0f, ExpectedResult = true)]
        [TestCase(false, false, false, true, 2, 0f, ExpectedResult = false)]
        [TestCase(true, true, false, true, 2, 0f, ExpectedResult = false)]
        [TestCase(true, false, true, true, 2, 0f, ExpectedResult = false)]
        [TestCase(true, false, false, false, 2, 0f, ExpectedResult = false)]
        [TestCase(true, false, false, true, 0, 0f, ExpectedResult = false)]
        [TestCase(true, false, false, true, 2, 0.1f, ExpectedResult = false)]
        public bool TheOwnerOnlyRequestsAThrowItExpectsTheServerToAccept(
            bool alive, bool downed, bool watchingDeathCamera, bool enabled, int remaining, float cooldown)
        {
            return NetPlayer.CanRequestGrenade(alive, downed, watchingDeathCamera, new NetGrenadeState(enabled, remaining), cooldown);
        }

        [Test]
        public void TheDistanceToACapsuleIsMeasuredFromItsSurface()
        {
            var bottom = new Vector3(0f, -1f, 0f);
            var top = new Vector3(0f, 1f, 0f);
            const float radius = 0.5f;

            Assert.That(NetTeamDeathmatch.DistanceToCapsule(new Vector3(3f, 0f, 0f), bottom, top, radius), Is.EqualTo(2.5f).Within(1e-4f), "beside the body");
            Assert.That(NetTeamDeathmatch.DistanceToCapsule(new Vector3(0f, 4f, 0f), bottom, top, radius), Is.EqualTo(3f).Within(1e-4f), "above the head");
            Assert.That(NetTeamDeathmatch.DistanceToCapsule(new Vector3(0f, -3f, 0f), bottom, top, radius), Is.EqualTo(2f).Within(1e-4f), "below the feet");
            Assert.That(NetTeamDeathmatch.DistanceToCapsule(new Vector3(0.2f, 0.3f, 0f), bottom, top, radius), Is.EqualTo(0f), "inside the body");
        }

        [Test]
        public void AShortCapsuleIsMeasuredAsASphere()
        {
            var bottom = new Vector3(0f, 0f, 0f);
            var top = new Vector3(0f, 0.6f, 0f);

            var distance = NetTeamDeathmatch.DistanceToCapsule(new Vector3(0f, 0.3f, 2f), bottom, top, 0.5f);

            Assert.That(distance, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => NetTeamDeathmatch.DistanceToCapsule(Vector3.zero, bottom, top, -1f));
        }

        [Test]
        public void TheExplosionReachesAPlayerWhoseBodyIsInsideTheRadiusEvenIfTheirCenterIsNot()
        {
            // 体の中心は半径の外でも、近い面が半径の内なら当たる (オフラインの ThrownGrenade.DistanceTo と同じ考え方)
            var bottom = new Vector3(GrenadeSpec.Default.DamageRadius + 0.3f, -1f, 0f);
            var top = new Vector3(GrenadeSpec.Default.DamageRadius + 0.3f, 1f, 0f);

            var distance = NetTeamDeathmatch.DistanceToCapsule(Vector3.zero, bottom, top, 0.5f);

            Assert.That(GrenadeSpec.Default.Explosion.Reaches(distance), Is.True);
            Assert.That(GrenadeSpec.Default.Explosion.At(distance, obstructed: false), Is.GreaterThan(0));
        }

        [Test]
        public void TheThrowVelocityMatchesTheOfflineThrow()
        {
            var velocity = NetTeamDeathmatch.GrenadeVelocity(Vector3.forward, GrenadeSpec.Default);

            Assert.That(velocity.z, Is.EqualTo(GrenadeSpec.Default.ThrowSpeed).Within(1e-4f));
            Assert.That(velocity.y, Is.EqualTo(GrenadeSpec.Default.ThrowLiftSpeed).Within(1e-4f));
        }

        [Test]
        public void TheThrowVelocityFollowsTheGivenSpec()
        {
            var velocity = NetTeamDeathmatch.GrenadeVelocity(Vector3.forward, new GrenadeSpec(throwSpeed: 20f, throwLiftSpeed: 1f));

            Assert.That(velocity.z, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(velocity.y, Is.EqualTo(1f).Within(1e-4f));
            Assert.Throws<ArgumentNullException>(() => NetTeamDeathmatch.GrenadeVelocity(Vector3.forward, null));
        }

        [Test]
        public void TheMatchUsesTheTunedGrenadeUntilReplaced()
        {
            var go = Create("Match");
            go.AddComponent<NetworkObject>();
            var match = go.AddComponent<NetTeamDeathmatch>();

            Assert.That(match.GrenadeSpec, Is.SameAs(KitTuning.Grenade));
            var spec = new GrenadeSpec(fuseSeconds: 1f);
            match.GrenadeSpec = spec;
            Assert.That(match.GrenadeSpec, Is.SameAs(spec));
            Assert.Throws<ArgumentNullException>(() => match.GrenadeSpec = null);
        }

        [Test]
        public void TheGrenadeIsReleasedInFrontOfTheEyeWithNothingInTheWay()
        {
            var eye = new Vector3(500f, 500f, 500f);

            var position = NetTeamDeathmatch.GrenadeReleasePoint(eye, Vector3.forward, null);

            Assert.That(Vector3.Distance(position, eye + (Vector3.forward * GrenadeThrower.ReleaseDistance)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TheGrenadeIsReleasedShortOfAWall()
        {
            var eye = new Vector3(500f, 500f, 500f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = eye + (Vector3.forward * 0.5f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();

            var position = NetTeamDeathmatch.GrenadeReleasePoint(eye, Vector3.forward, null);

            var wallFace = 0.5f - 0.1f;
            Assert.That(position.z - eye.z, Is.EqualTo(wallFace - ThrownGrenade.BodyRadius).Within(1e-3f));
        }

        [Test]
        public void TheThrowersOwnCollidersDoNotStopTheRelease()
        {
            var eye = new Vector3(600f, 500f, 500f);
            var thrower = Create("Thrower");
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(arm);
            arm.transform.SetParent(thrower.transform, false);
            arm.transform.position = eye + (Vector3.forward * 0.3f);
            arm.transform.localScale = new Vector3(0.3f, 0.3f, 0.1f);
            Physics.SyncTransforms();

            var position = NetTeamDeathmatch.GrenadeReleasePoint(eye, Vector3.forward, thrower.transform);

            Assert.That(position.z - eye.z, Is.EqualTo(GrenadeThrower.ReleaseDistance).Within(1e-4f));
        }

        [Test]
        public void AWallBetweenTheExplosionAndTheBodyBlocksIt()
        {
            var center = new Vector3(700f, 500f, 500f);
            var body = center + (Vector3.forward * 3f);
            var head = body + (Vector3.up * 0.8f);
            Assert.That(NetTeamDeathmatch.IsExplosionObstructed(center, body, head), Is.False, "open ground");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = center + (Vector3.forward * 1.5f);
            wall.transform.localScale = new Vector3(4f, 6f, 0.2f);
            Physics.SyncTransforms();

            Assert.That(NetTeamDeathmatch.IsExplosionObstructed(center, body, head), Is.True);
        }

        [Test]
        public void ALowWallDoesNotShieldTheHead()
        {
            var center = new Vector3(800f, 500f, 500f);
            var body = center + (Vector3.forward * 3f);
            var head = body + (Vector3.up * 2f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = center + (Vector3.forward * 1.5f);
            wall.transform.localScale = new Vector3(4f, 1f, 0.2f);
            Physics.SyncTransforms();

            Assert.That(NetTeamDeathmatch.IsExplosionObstructed(center, body, head), Is.False);
        }

        [Test]
        public void AGrenadePrefabNeedsANetworkObjectAndANetGrenade()
        {
            Assert.That(NetTeamDeathmatch.TryValidateGrenadePrefab(null, out var missing), Is.False);
            Assert.That(missing, Does.Contain("no grenade prefab"));

            var bare = Create("Bare");
            Assert.That(NetTeamDeathmatch.TryValidateGrenadePrefab(bare, out var noNetworkObject), Is.False);
            Assert.That(noNetworkObject, Does.Contain("NetworkObject"));

            var withoutGrenade = Create("WithoutGrenade");
            withoutGrenade.AddComponent<NetworkObject>();
            Assert.That(NetTeamDeathmatch.TryValidateGrenadePrefab(withoutGrenade, out var noGrenade), Is.False);
            Assert.That(noGrenade, Does.Contain("NetGrenade"));

            Assert.That(NetTeamDeathmatch.TryValidateGrenadePrefab(CreateGrenadePrefab(), out var error), Is.True, error);
        }

        [Test]
        public void TheBootstrapRegistersTheMatchesGrenadePrefab()
        {
            var grenade = CreateGrenadePrefab();
            var matchObject = Create("Match");
            matchObject.AddComponent<NetworkObject>();
            matchObject.AddComponent<NetTeamDeathmatch>().GrenadePrefab = grenade;

            Assert.That(NetBootstrap.SpawnablePrefabs(matchObject), Is.EqualTo(new[] { matchObject, grenade }));
        }

        [Test]
        public void TheBootstrapSkipsAnUnusableGrenadePrefabWithAWarning()
        {
            var matchObject = Create("Match");
            matchObject.AddComponent<NetworkObject>();
            matchObject.AddComponent<NetTeamDeathmatch>();

            LogAssert.Expect(LogType.Warning, new Regex("grenades cannot be thrown online"));
            Assert.That(NetBootstrap.SpawnablePrefabs(matchObject), Is.EqualTo(new[] { matchObject }));
        }

        [Test]
        public void TheKillFeedNamesTheGrenadeWithOrWithoutALoadout()
        {
            var withoutMatch = NetKillFeedNames.For(null, 0);
            var line = withoutMatch.Describe(new KillFeedEntry(0, 1, GrenadeThrower.WeaponId, false, false));
            Assert.That(line.WeaponName, Is.EqualTo(GrenadeThrower.Name));
            Assert.That(GrenadeThrower.Name, Is.EqualTo(new LocalizedText("weapon.grenade")));

            var names = NetKillFeedNames.WeaponNames(null, null);
            Assert.That(names(GrenadeThrower.WeaponId), Is.EqualTo(GrenadeThrower.Name));
            Assert.That(names("rifle").IsEmpty, Is.True);
        }

        [Test]
        public void AGrenadeKillCarriesTheGrenadeIntoTheKillFeed()
        {
            var shot = new NetTeamDeathmatch.KillShot(1, 2, GrenadeThrower.WeaponId, HitZone.Body);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, friendly: false, shot);

            Assert.That(entry.WeaponId, Is.EqualTo(GrenadeThrower.WeaponId));
            Assert.That(entry.Headshot, Is.False);
        }

        [Test]
        public void BlowingYourselfUpCarriesTheGrenadeIntoTheKillFeed()
        {
            var shot = new NetTeamDeathmatch.KillShot(1, 1, GrenadeThrower.WeaponId, HitZone.Body);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 1, friendly: false, shot);

            Assert.That(entry.IsSuicide, Is.True);
            Assert.That(entry.WeaponId, Is.EqualTo(GrenadeThrower.WeaponId));
        }

        [Test]
        public void TheLogsUseInvariantNumbers()
        {
            Assert.That(
                NetGrenade.ThrownLog(1, 2, new Vector3(1f, 0.6f, -21.3f), new Vector3(0f, 3f, 14f)),
                Is.EqualTo("[Grenade] net thrown player=1 remaining=2 at=(1.0, 0.6, -21.3) velocity=(0.0, 3.0, 14.0)"));
            Assert.That(
                NetGrenade.HitLog(1, 2, 150, 0.84f, DamageOutcome.Killed),
                Is.EqualTo("[Grenade] net hit attacker=1 victim=2 dmg=150 distance=0.8 outcome=Killed"));
            Assert.That(
                NetGrenade.ExplodedLog(1, new GrenadeExplosion(new Vector3(1f, -0.9f, 9.5f), 2, 1, 0)),
                Is.EqualTo("[Grenade] net exploded thrower=1 at=(1.0, -0.9, 9.5) damaged=2 killed=1 structures=0"));
            Assert.That(NetGrenade.RejectedLog(3, GrenadeThrowRejection.Empty), Is.EqualTo("[Grenade] net rejected player=3 reason=Empty"));
            Assert.That(
                NetCombatFeed.ExplosionLog(GrenadeThrower.WeaponId, new Vector3(1f, -0.92f, 9.5f), 1),
                Is.EqualTo("[Combat] Explosion weapon=grenade at=(1.00, -0.92, 9.50) thrower=1"));
            Assert.That(
                NetCombatFeed.ExplosionLog(GrenadeThrower.WeaponId, Vector3.zero, null),
                Does.EndWith("thrower=none"));
        }

        private GameObject CreateGrenadePrefab()
        {
            var grenade = Create("NetGrenade");
            grenade.AddComponent<NetworkObject>();
            grenade.AddComponent<NetGrenade>();
            return grenade;
        }
    }
}
