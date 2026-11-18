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

        // 落ちる先を探す線が届かないよう、何も無い高さで爆発させる。
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

            LogAssert.Expect(LogType.Log, "[Streak] airstrike called user=User at=(0.0, 50.0, 0.0) heading=0.0 strikes=3 interval=0.50 line=8.0");
            var center = airstrike.Call(_sky, 0f, Spec(3, 0.5f, spread: 4f, lineLength: 8f), WeaponId, new System.Random(7));

            Assert.That(center, Is.EqualTo(_sky), "nothing is under the sky to land on");
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
            var spec = Spec(4, 1f, spread: 4f, lineLength: 12f);
            airstrike.Call(_sky, 30f, spec, WeaponId, new System.Random(3));

            Assert.That(airstrike.Tick(2.5f), Is.EqualTo(2));
            Assert.That(airstrike.PendingCount, Is.EqualTo(2));

            var expected = Shells(_sky, 30f, spec, new System.Random(3));
            Assert.That(_explosions[0].Center, Is.EqualTo(expected[0]));
            Assert.That(_explosions[1].Center, Is.EqualTo(expected[1]));
        }

        [Test]
        public void TheStrikesFallAlongTheHeadingFromTheBackEnd()
        {
            var airstrike = User();
            const int strikes = 5;

            airstrike.Call(_sky, 90f, Spec(strikes, 0f, spread: 0f, lineLength: 20f), WeaponId, new System.Random(42));

            Assert.That(_explosions.Count, Is.EqualTo(strikes), "with no interval every strike lands at once");
            for (var i = 0; i < strikes; i++)
            {
                var center = _explosions[i].Center;
                Assert.That(center.x, Is.EqualTo(-10f + (i * 5f)).Within(1e-4f), "heading 90 flies toward +X, so the first strike lands at the -X end");
                Assert.That(center.y, Is.EqualTo(_sky.y));
                Assert.That(center.z, Is.EqualTo(_sky.z).Within(1e-4f));
            }
        }

        [Test]
        public void TheSpreadMovesEachStrikeOffTheLineByTheSeed()
        {
            var airstrike = User();
            const float spread = 3f;
            var spec = Spec(6, 0f, spread: spread, lineLength: 25f);

            airstrike.Call(_sky, 0f, spec, WeaponId, new System.Random(42));

            var expected = Shells(_sky, 0f, spec, new System.Random(42));
            for (var i = 0; i < expected.Length; i++)
            {
                var onLine = AirstrikeLine.PointOnLine(_sky.ToNumerics(), 0f, 25f, i, 6).ToUnity();
                Assert.That(_explosions[i].Center, Is.EqualTo(expected[i]), "the same seed picks the same spots");
                Assert.That(Vector3.Distance(_explosions[i].Center, onLine), Is.LessThanOrEqualTo(spread + 1e-4f));
            }
        }

        [Test]
        public void KillsAreCreditedToTheUser()
        {
            var airstrike = User();
            var target = Target("Target", new Vector3(0f, 1f, 1.5f));

            LogAssert.Expect(LogType.Log, "[Streak] airstrike strike=1/1 user=User at=(0.0, 0.1, 0.0) damaged=1 killed=1");
            airstrike.Call(new Vector3(0f, 0.1f, 0f), 0f, Spec(1, 0f, dropHeight: 0.5f), WeaponId, new System.Random(1));

            Assert.That(target.IsDead, Is.True);
            Assert.That(_kills.Count, Is.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(airstrike.gameObject)));
            Assert.That(_kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(target.gameObject)));
            Assert.That(_kills[0].WeaponId, Is.EqualTo(WeaponId));
        }

        [Test]
        public void AStrikeIsPutOnTheGroundUnderTheChosenSpot()
        {
            var airstrike = User();
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();

            var center = airstrike.Call(new Vector3(0f, 3f, 0f), 0f, Spec(1, 0f, dropHeight: 10f), WeaponId, new System.Random(1));

            var onGround = ImpactResolver.DetonationPoint(System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitY).ToUnity();
            Assert.That(Vector3.Distance(center, onGround), Is.LessThan(1e-4f), "the height chosen on the map is replaced by the ground");
            Assert.That(Vector3.Distance(_explosions[0].Center, onGround), Is.LessThan(1e-4f));
        }

        [Test]
        public void AStrikeStopsOnARoofOverTheChosenSpot()
        {
            var airstrike = User();
            var roof = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            roof.transform.position = new Vector3(0f, 4.5f, 0f);
            roof.transform.localScale = new Vector3(6f, 1f, 6f);
            Physics.SyncTransforms();

            airstrike.Call(new Vector3(0f, 0f, 0f), 0f, Spec(1, 0f, dropHeight: 20f), WeaponId, new System.Random(1));

            var onRoof = ImpactResolver.DetonationPoint(new System.Numerics.Vector3(0f, 5f, 0f), System.Numerics.Vector3.UnitY).ToUnity();
            Assert.That(Vector3.Distance(_explosions[0].Center, onRoof), Is.LessThan(1e-4f), "bombs from above hit the roof first");
        }

        [Test]
        public void AStrikeSearchesFromTheUsersHeightWithinTheViewHeight()
        {
            var airstrike = User();
            airstrike.transform.position = new Vector3(0f, 60f, 0f);
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -50.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();

            var center = airstrike.Call(new Vector3(0f, 0f, 0f), 0f, Spec(1, 0f, dropHeight: 40f), WeaponId, new System.Random(1), new StreakTargetingSpec(viewHeight: 150f));

            var onGround = ImpactResolver.DetonationPoint(new System.Numerics.Vector3(0f, -50f, 0f), System.Numerics.Vector3.UnitY).ToUnity();
            Assert.That(Vector3.Distance(center, onGround), Is.LessThan(1e-4f), "ground further than dropHeight below the chosen height is still found");
        }

        [Test]
        public void ACancelledStrikeDoesNotLand()
        {
            var airstrike = User();
            airstrike.Call(_sky, 0f, Spec(2, 5f), WeaponId, new System.Random(1));

            Assert.That(airstrike.Cancel(), Is.EqualTo(2));
            Assert.That(airstrike.Tick(20f), Is.Zero);
            Assert.That(_explosions, Is.Empty);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            var airstrike = User();
            var random = new System.Random(1);
            var spec = Spec(1, 1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(new Vector3(float.NaN, 0f, 0f), 0f, spec, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(_sky, float.NaN, spec, WeaponId, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => airstrike.Call(_sky, float.PositiveInfinity, spec, WeaponId, random));
            Assert.Throws<ArgumentNullException>(() => airstrike.Call(_sky, 0f, null, WeaponId, random));
            Assert.That(airstrike.PendingCount, Is.Zero);
        }

        private static StreakAirstrikeSpec Spec(int strikes, float intervalSeconds, float spread = 0f, float lineLength = 0f, float dropHeight = 1f)
        {
            return new StreakAirstrikeSpec(strikes, intervalSeconds, spread, _spec, StreakAirstrikeSpec.DefaultRange, lineLength, dropHeight);
        }

        private static Vector3[] Shells(Vector3 center, float heading, StreakAirstrikeSpec spec, System.Random random)
        {
            var points = new Vector3[spec.ShellCount];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = AirstrikeLine.ShellPoint(center.ToNumerics(), heading, spec, i, random).ToUnity();
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
