using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 仮の形を作っていたコンポーネント (<see cref="DebrisPool"/>・<see cref="ThrownGrenade"/>・<see cref="GuardianLeaderMarker"/>・
    /// <see cref="TargetDummy"/>) が、プレハブを渡されればその見た目で出し、空なら今までの仮の形で出すことを確かめる。
    /// プレハブの代わりに、当たり判定の付いた Cube を子に持つシーンの GameObject を使う (Instantiate は同じく複製する)。
    /// </summary>
    public sealed class PlaceholderPrefabSlotTests
    {
        private const string ModelName = "Model";
        private const float Tolerance = 1e-4f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void DebrisUsesThePrefabScaledToTheSizeOnTheDebrisLayer()
        {
            var pool = Track(new GameObject("Pool")).AddComponent<DebrisPool>();
            var prefab = Prefab("Rock");
            pool.DebrisPrefab = prefab;
            var size = new Vector3(0.3f, 0.2f, 0.4f);

            var debris = pool.Spawn(new Vector3(1f, 2f, 3f), Quaternion.identity, size, null, Vector3.zero);

            Assert.That(debris.name, Is.EqualTo(DebrisPool.DebrisName));
            Assert.That(debris.transform.Find(ModelName), Is.Not.Null, "the prefab's look is used");
            Assert.That(debris.GetComponent<MeshFilter>(), Is.Null, "no placeholder cube on the root");
            Assert.That(debris.transform.localScale, Is.EqualTo(size));
            Assert.That(debris.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(debris.GetComponentsInChildren<Collider>(), Is.Not.Empty, "the prefab's collider stays so the debris lands");
            foreach (var part in debris.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(part.gameObject.layer, Is.EqualTo(DebrisPool.DebrisLayer), part.name);
            }

            pool.Clear();
        }

        [Test]
        public void DebrisPrefabWithoutAColliderGetsABox()
        {
            var pool = Track(new GameObject("Pool")).AddComponent<DebrisPool>();
            var prefab = Prefab("Shard");
            Object.DestroyImmediate(prefab.GetComponentInChildren<Collider>());
            pool.DebrisPrefab = prefab;

            var debris = pool.Spawn(Vector3.zero, Quaternion.identity, Vector3.one * 0.2f, null, Vector3.zero);

            Assert.That(debris.GetComponent<BoxCollider>(), Is.Not.Null);
            pool.Clear();
        }

        [Test]
        public void DebrisWithoutAPrefabIsTheTintedPlaceholderCube()
        {
            var pool = Track(new GameObject("Pool")).AddComponent<DebrisPool>();
            var material = Track(new Material(Prefab("Source").GetComponentInChildren<Renderer>().sharedMaterial) { name = "Wall" });

            var debris = pool.Spawn(Vector3.zero, Quaternion.identity, Vector3.one * 0.2f, material, Vector3.zero);

            Assert.That(debris.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(debris.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(debris.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(debris.layer, Is.EqualTo(DebrisPool.DebrisLayer));
            pool.Clear();
        }

        [Test]
        public void GrenadeShowsThePrefabButBouncesAsTheSameSphere()
        {
            var prefab = Prefab("Frag");

            var grenade = Spawn(prefab);

            var visual = grenade.transform.Find(ThrownGrenade.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Find(ModelName), Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "the look does not change how it bounces");
            Assert.That(grenade.transform.localScale, Is.EqualTo(Vector3.one));
            var sphere = grenade.GetComponent<SphereCollider>();
            Assert.That(sphere, Is.Not.Null);
            Assert.That(sphere.radius * grenade.transform.lossyScale.x, Is.EqualTo(ThrownGrenade.BodyRadius).Within(Tolerance));
            Assert.That(sphere.sharedMaterial, Is.SameAs(ThrownGrenade.SharedPhysicsMaterial()));
            Assert.That(grenade.GetComponent<MeshRenderer>(), Is.Null);
        }

        [Test]
        public void GrenadeWithoutAPrefabIsThePlaceholderSphere()
        {
            var grenade = Spawn(null);

            Assert.That(grenade.transform.Find(ThrownGrenade.VisualName), Is.Null);
            Assert.That(grenade.GetComponent<MeshRenderer>(), Is.Not.Null);
            var sphere = grenade.GetComponent<SphereCollider>();
            Assert.That(sphere.radius * grenade.transform.lossyScale.x, Is.EqualTo(ThrownGrenade.BodyRadius).Within(Tolerance));
        }

        [Test]
        public void ThrowerThrowsItsGrenadePrefab()
        {
            var prefab = Prefab("Frag");
            var body = Track(new GameObject("Thrower"));
            body.AddComponent<Damageable>().Initialize("Thrower", 100);
            var thrower = body.AddComponent<GrenadeThrower>();
            thrower.Configure(GameRules.Default.WithActions(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(1)));
            thrower.GrenadePrefab = prefab;
            ThrownGrenade thrown = null;
            thrower.Thrown += grenade =>
            {
                thrown = grenade;
                Track(grenade.gameObject);
            };

            Assert.That(thrower.TryThrow(), Is.True);

            Assert.That(thrown, Is.Not.Null);
            Assert.That(thrown.transform.Find(ThrownGrenade.VisualName), Is.Not.Null);
        }

        [Test]
        public void LeaderMarkerUsesThePrefabPaintedInTheTeamColour()
        {
            var prefab = Prefab("Crown");
            var prefabMaterial = prefab.GetComponentInChildren<Renderer>().sharedMaterial;
            var prefabColor = prefabMaterial.color;
            var leader = Member("Bot-1-1", 1, Vector3.zero);

            var marker = GuardianLeaderMarker.Create(leader, prefab);
            Track(marker.gameObject);

            Assert.That(marker.IsPlaceholder, Is.False);
            Assert.That(marker.transform.Find(ModelName), Is.Not.Null);
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty, "the marker does not block shots");
            Assert.That(marker.Renderers, Is.Not.Empty);
            Assert.That(marker.IsShown, Is.True);
            Assert.That(marker.Renderer.sharedMaterial.color, Is.EqualTo(GuardianLeaderMarker.ColorOf(Side.Bravo)));
            Assert.That(marker.Renderer.sharedMaterial, Is.Not.SameAs(prefabMaterial));
            Assert.That(prefabMaterial.color, Is.EqualTo(prefabColor), "the prefab's own material is not repainted");

            // 当たり判定の無い相手は足元から FallbackHeadHeight を頭とみなす。プレハブは原点を下端として置く。
            var expectedY = GuardianLeaderMarker.FallbackHeadHeight + GuardianLeaderMarker.DefaultHeightAboveHead;
            Assert.That(marker.transform.position.y, Is.EqualTo(expectedY).Within(Tolerance));

            marker.SetShown(false);
            Assert.That(marker.IsShown, Is.False);
            foreach (var markerRenderer in marker.Renderers)
            {
                Assert.That(markerRenderer.enabled, Is.False);
            }

            var copy = marker.Renderer.sharedMaterial;
            marker.Remove();
            Assert.That(copy == null, Is.True, "the copied material goes away with the marker");
        }

        [Test]
        public void LeaderMarkerWithoutAPrefabIsTheTiltedPlaceholderCube()
        {
            var leader = Member("Bot-1-1", 1, Vector3.zero);

            var marker = GuardianLeaderMarker.Create(leader);
            Track(marker.gameObject);

            Assert.That(marker.IsPlaceholder, Is.True);
            Assert.That(marker.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(marker.GetComponent<Collider>() == null, Is.True);
            Assert.That(Quaternion.Angle(marker.transform.rotation, Quaternion.Euler(45f, 0f, 45f)), Is.LessThan(0.01f));
            var expectedY = GuardianLeaderMarker.FallbackHeadHeight + GuardianLeaderMarker.DefaultHeightAboveHead
                + (GuardianLeaderMarker.DefaultSize * 0.5f);
            Assert.That(marker.transform.position.y, Is.EqualTo(expectedY).Within(Tolerance));
            marker.Remove();
        }

        [Test]
        public void GuardianRunnerPassesItsMarkerPrefab()
        {
            var runner = Track(new GameObject("Runner")).AddComponent<GuardianRunner>();
            runner.LeaderSeed = 7;
            runner.LeaderMarkerPrefab = Prefab("Crown");
            var player = Member("Player", 0, Vector3.zero);
            var enemy = Member("Bot-1-1", 1, new Vector3(0f, 0f, 30f));
            try
            {
                runner.BeginGuardian(new[] { player, enemy }, new SideSpawnPoint[0], player);

                var marker = runner.MarkerOf(1);
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.IsPlaceholder, Is.False);
                Assert.That(marker.transform.Find(ModelName), Is.Not.Null);
            }
            finally
            {
                runner.ClearMarkers();
                runner.Stop();
            }
        }

        [Test]
        public void TargetDummyShowsThePrefabAndKeepsInvisibleHitZones()
        {
            var prefab = Prefab("Mannequin");

            var dummy = TargetDummy.Create("dummy", Vector3.zero, Quaternion.identity, null, 50, 1f, prefab);
            Track(dummy.gameObject);

            var visual = dummy.transform.Find(TargetDummy.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Find(ModelName), Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "shots land on the hit zones, not on the look");

            var body = dummy.transform.Find(TargetDummy.BodyName);
            var head = dummy.transform.Find(TargetDummy.HeadName);
            Assert.That(body.GetComponent<Renderer>(), Is.Null);
            Assert.That(head.GetComponent<Renderer>(), Is.Null);
            Assert.That(body.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(head.GetComponent<SphereCollider>(), Is.Not.Null);
            Assert.That(body.GetComponent<Hitbox>().Zone, Is.EqualTo(HitZone.Body));
            Assert.That(head.GetComponent<Hitbox>().Zone, Is.EqualTo(HitZone.Head));

            // 当たり判定の大きさは仮の形と同じ (Capsule は高さ 2m・半径 0.5m を BodyScale 倍)。
            Physics.SyncTransforms();
            var bodyBounds = body.GetComponent<Collider>().bounds;
            Assert.That(bodyBounds.max.y, Is.EqualTo(TargetDummy.BodyLocalPosition.y + TargetDummy.BodyScale.y).Within(Tolerance));
            var headBounds = head.GetComponent<Collider>().bounds;
            Assert.That(headBounds.size.y, Is.EqualTo(TargetDummy.HeadScale.y).Within(Tolerance));

            var model = visual.GetComponentInChildren<Renderer>();
            dummy.Damageable.Kill();
            Assert.That(model.enabled, Is.False, "the look hides while waiting to respawn");
            dummy.Respawn();
            Assert.That(model.enabled, Is.True);
        }

        [Test]
        public void TargetDummyWithoutAPrefabIsThePlaceholderCapsuleAndSphere()
        {
            var dummy = TargetDummy.Create("dummy", Vector3.zero, Quaternion.identity);
            Track(dummy.gameObject);

            Assert.That(dummy.transform.Find(TargetDummy.VisualName), Is.Null);
            Assert.That(dummy.transform.Find(TargetDummy.BodyName).GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(dummy.transform.Find(TargetDummy.HeadName).GetComponent<MeshRenderer>(), Is.Not.Null);
        }

        private ThrownGrenade Spawn(GameObject prefab)
        {
            var spec = GrenadeSpec.Default;
            var grenade = ThrownGrenade.Spawn(
                null, new Vector3(0f, 50f, 0f), Vector3.zero, spec.FuseSeconds, spec.Explosion, spec.StructureDamageAtCenter, prefab);
            Track(grenade.gameObject);
            return grenade;
        }

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        /// <summary>当たり判定と Renderer の付いた Cube (<see cref="ModelName"/>) を子に持つ、プレハブ代わりの GameObject。</summary>
        private GameObject Prefab(string name)
        {
            var root = Track(new GameObject(name));
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = ModelName;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            model.transform.localScale = Vector3.one * 0.5f;
            return root;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
