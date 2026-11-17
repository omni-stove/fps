using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AirstrikeRewardTests
    {
        private const string WeaponId = "airstrike";

        private static readonly ExplosionSpec _spec = new ExplosionSpec(GrenadeSpec.Default.Explosion, 50);

        // 地面を探す線が届かないよう、何も無い高さで爆発させる。
        private static readonly Vector3 _sky = new Vector3(0f, 50f, 0f);

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<ExplosionInfo> _explosions = new List<ExplosionInfo>();
        private readonly List<KillFeedEntry> _kills = new List<KillFeedEntry>();
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            CombatEvents.Exploded += _explosions.Add;
            CombatEvents.Killed += _kills.Add;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            _explosions.Clear();
            _kills.Clear();
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void DetonatesTheGivenNumberOfStrikesOneIntervalApart()
        {
            var airstrike = User();

            LogAssert.Expect(LogType.Log, "[Streak] airstrike called user=User at=(0.0, 50.0, 0.0) strikes=3 interval=0.50 spread=4.0");
            airstrike.Call(_sky, 3, 0.5f, 4f, _spec, WeaponId, new System.Random(7));

            Assert.That(_explosions, Is.Empty, "the first strike waits one interval");
            Assert.That(airstrike.PendingCount, Is.EqualTo(3));

            Assert.That(airstrike.Tick(0.49f), Is.Zero);
            Assert.That(airstrike.Tick(0.5f), Is.EqualTo(1));
            Assert.That(_explosions.Count, Is.EqualTo(1));
            Assert.That(airstrike.Tick(0.99f), Is.Zero);
            Assert.That(airstrike.Tick(1f), Is.EqualTo(1));
            Assert.That(airstrike.Tick(1.5f), Is.EqualTo(1));

            Assert.That(_explosions.Count, Is.EqualTo(3));
            Assert.That(airstrike.PendingCount, Is.Zero);
            Assert.That(_explosions.TrueForAll(e => e.WeaponId == WeaponId), Is.True);
            Assert.That(airstrike.Tick(10f), Is.Zero, "nothing is left to detonate");
        }

        [Test]
        public void ALateTickDetonatesEveryStrikeThatIsDueInOrder()
        {
            var airstrike = User();
            airstrike.Call(_sky, 4, 1f, 4f, _spec, WeaponId, new System.Random(3));

            Assert.That(airstrike.Tick(2.5f), Is.EqualTo(2));
            Assert.That(airstrike.PendingCount, Is.EqualTo(2));

            var expected = Scatter(_sky, 4f, new System.Random(3), 4);
            Assert.That(_explosions[0].Center, Is.EqualTo(expected[0]));
            Assert.That(_explosions[1].Center, Is.EqualTo(expected[1]));
        }

        [Test]
        public void TheStrikesLandInsideTheSpreadWhereTheRandomPutsThem()
        {
            var airstrike = User();
            const float spread = 6f;
            const int strikes = 8;

            airstrike.Call(_sky, strikes, 0f, spread, _spec, WeaponId, new System.Random(42));

            var expected = Scatter(_sky, spread, new System.Random(42), strikes);
            Assert.That(_explosions.Count, Is.EqualTo(strikes), "with no interval every strike lands at once");
            for (var i = 0; i < strikes; i++)
            {
                var center = _explosions[i].Center;
                Assert.That(center, Is.EqualTo(expected[i]), "the same seed picks the same spots");
                Assert.That(center.y, Is.EqualTo(_sky.y));
                Assert.That(new Vector2(center.x - _sky.x, center.z - _sky.z).magnitude, Is.LessThanOrEqualTo(spread + 1e-4f));
            }
        }

        [Test]
        public void ScatterPointStaysInTheCircleAndFollowsTheSeed()
        {
            var first = new System.Random(11);
            var second = new System.Random(11);
            var center = new Vector3(3f, 2f, -4f);

            for (var i = 0; i < 100; i++)
            {
                var point = AirstrikeReward.ScatterPoint(center, 5f, first);
                Assert.That(point, Is.EqualTo(AirstrikeReward.ScatterPoint(center, 5f, second)));
                Assert.That(point.y, Is.EqualTo(center.y));
                Assert.That(new Vector2(point.x - center.x, point.z - center.z).magnitude, Is.LessThanOrEqualTo(5f + 1e-4f));
            }

            Assert.That(AirstrikeReward.ScatterPoint(center, 0f, first), Is.EqualTo(center));
            Assert.Throws<ArgumentNullException>(() => AirstrikeReward.ScatterPoint(center, 1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => AirstrikeReward.ScatterPoint(center, -1f, first));
        }

        [Test]
        public void KillsAreCreditedToTheUser()
        {
            var airstrike = User();
            var target = Target("Target", new Vector3(0f, 1f, 1.5f));

            LogAssert.Expect(LogType.Log, "[Streak] airstrike strike=1/1 user=User at=(0.0, 0.1, 0.0) damaged=1 killed=1");
            airstrike.Call(new Vector3(0f, 0.1f, 0f), 1, 0f, 0f, _spec, WeaponId, new System.Random(1));

            Assert.That(target.IsDead, Is.True);
            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(airstrike.gameObject)));
            Assert.That(_kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(target.gameObject)));
            Assert.That(_kills[0].WeaponId, Is.EqualTo(WeaponId));
        }

        [Test]
        public void AStrikeIsPutOnTheGroundUnderIt()
        {
            var airstrike = User();
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();

            airstrike.Call(new Vector3(0f, 0.5f, 0f), 1, 0f, 0f, _spec, WeaponId, new System.Random(1));

            var onGround = ImpactResolver.DetonationPoint(System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitY).ToUnity();
            Assert.That(Vector3.Distance(_explosions[0].Center, onGround), Is.LessThan(1e-4f));
        }

        [Test]
        public void CallAtAimCentresOnWhatTheAimHits()
        {
            var airstrike = User();
            airstrike.transform.position = new Vector3(0f, 1f, 0f);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1f, 10f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Physics.SyncTransforms();

            var center = airstrike.CallAtAim(100f, 1, 5f, 0f, _spec, WeaponId, new System.Random(1));

            Assert.That(center.z, Is.EqualTo(9.75f).Within(1e-3f), "the face of the wall");
            Assert.That(airstrike.PendingCount, Is.EqualTo(1));
            Assert.That(airstrike.Cancel(), Is.EqualTo(1));
            Assert.That(airstrike.Tick(10f), Is.Zero, "a cancelled strike does not land");
        }

        [Test]
        public void CallAtAimCentresAtTheRangeWhenNothingIsHit()
        {
            var airstrike = User();
            airstrike.transform.position = new Vector3(0f, 1f, 0f);

            var center = airstrike.CallAtAim(80f, 1, 5f, 0f, _spec, WeaponId, new System.Random(1));

            Assert.That(Vector3.Distance(center, new Vector3(0f, 1f, 80f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var airstrike = User();
            var random = new System.Random(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(_sky, 0, 1f, 1f, _spec, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(_sky, 1, -1f, 1f, _spec, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(_sky, 1, float.NaN, 1f, _spec, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(_sky, 1, 1f, -1f, _spec, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => airstrike.Call(new Vector3(float.NaN, 0f, 0f), 1, 1f, 1f, _spec, WeaponId, random));
            Assert.Throws<ArgumentNullException>(() => airstrike.Call(_sky, 1, 1f, 1f, null, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.CallAtAim(0f, 1, 1f, 1f, _spec, WeaponId, random));
            Assert.That(airstrike.PendingCount, Is.Zero);
        }

        private static Vector3[] Scatter(Vector3 center, float spread, System.Random random, int count)
        {
            var points = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                points[i] = AirstrikeReward.ScatterPoint(center, spread, random);
            }

            return points;
        }

        private AirstrikeReward User()
        {
            var go = Track(new GameObject("User"));
            var airstrike = go.AddComponent<AirstrikeReward>();
            airstrike.Clock = () => _now;
            return airstrike;
        }

        private Damageable Target(string name, Vector3 position)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            go.name = name;
            go.transform.position = position;
            var damageable = go.AddComponent<Damageable>();
            damageable.Initialize(name, Damageable.DefaultMaxHealth);
            Physics.SyncTransforms();
            return damageable;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
