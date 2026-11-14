using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // プレハブの代わりにシーン上の GameObject を渡す。Instantiate はどちらでも同じく複製する。
    public sealed class EnemyVisualPrefabTests
    {
        private static readonly Vector3 Origin = new Vector3(800f, 0f, 800f);

        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _created.Add(_rifle);
        }

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
        public void TheBomberShowsItsSignalPrefabUnderItselfWithoutColliders()
        {
            var agent = CreateAgent("bomber-1", EnemyArchetype.Bomber);
            var bomber = agent.gameObject.AddComponent<EnemyBomber>();
            var prefab = CreatePrefab("CustomSignal", new Vector3(0f, 2f, 0f));
            bomber.SignalPrefab = prefab;

            bomber.Tick(0f);

            AssertInstanceOf(bomber.Signal, prefab);
            Assert.That(bomber.Signal.parent, Is.SameAs(agent.transform));
            Assert.That(bomber.Signal.localPosition, Is.EqualTo(new Vector3(0f, 2f, 0f)), "the prefab's own offset is kept");
            Assert.That(bomber.IsSignalVisible, Is.False, "it stays hidden until the fuse is lit");
        }

        [Test]
        public void WithoutAPrefabTheBomberMakesThePlaceholderSphere()
        {
            var bomber = CreateAgent("bomber-1", EnemyArchetype.Bomber).gameObject.AddComponent<EnemyBomber>();

            bomber.Tick(0f);

            Assert.That(bomber.Signal.name, Is.EqualTo(EnemyBomber.SignalPlaceholderName));
            Assert.That(bomber.Signal.parent, Is.SameAs(bomber.transform));
            Assert.That(bomber.Signal.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void AWiredSignalIsKeptOverThePrefab()
        {
            var bomber = CreateAgent("bomber-1", EnemyArchetype.Bomber).gameObject.AddComponent<EnemyBomber>();
            var wired = Track(new GameObject("Wired")).transform;
            wired.SetParent(bomber.transform, false);
            bomber.Signal = wired;
            bomber.SignalPrefab = CreatePrefab("CustomSignal", Vector3.zero);

            bomber.Tick(0f);

            Assert.That(bomber.Signal, Is.SameAs(wired));
        }

        [Test]
        public void TheChargerShowsItsTellPrefabUnderItselfWithoutColliders()
        {
            var agent = CreateAgent("charger-1", EnemyArchetype.Charger);
            var charger = agent.gameObject.AddComponent<EnemyCharger>();
            var prefab = CreatePrefab("CustomTell", new Vector3(0f, 1f, 0.5f));
            charger.TellPrefab = prefab;

            charger.Tick(0f);

            AssertInstanceOf(charger.Tell, prefab);
            Assert.That(charger.Tell.parent, Is.SameAs(agent.transform));
            Assert.That(charger.Tell.localPosition, Is.EqualTo(new Vector3(0f, 1f, 0.5f)));
            Assert.That(charger.IsTellVisible, Is.False);
        }

        [Test]
        public void TheSniperShowsItsBeamPrefabOutsideItselfWithoutColliders()
        {
            var agent = CreateAgent("sniper-1", EnemyArchetype.Sniper);
            var sniper = agent.gameObject.AddComponent<EnemySniper>();
            var prefab = CreatePrefab("CustomBeam", Vector3.zero);
            sniper.BeamPrefab = prefab;

            sniper.Tick();
            Track(sniper.AimBeam.gameObject);

            AssertInstanceOf(sniper.AimBeam, prefab);
            Assert.That(sniper.AimBeam.IsChildOf(agent.transform), Is.False, "the beam does not turn with the sniper");
            Assert.That(sniper.IsBeamVisible, Is.False);
        }

        [Test]
        public void WithoutAPrefabTheSniperMakesThePlaceholderBeam()
        {
            var sniper = CreateAgent("sniper-1", EnemyArchetype.Sniper).gameObject.AddComponent<EnemySniper>();

            sniper.Tick();
            Track(sniper.AimBeam.gameObject);

            Assert.That(sniper.AimBeam.name, Is.EqualTo(EnemySniper.BeamPlaceholderName));
            Assert.That(sniper.AimBeam.IsChildOf(sniper.transform), Is.False);
            Assert.That(sniper.AimBeam.GetComponent<Collider>(), Is.Null, "the beam does not stop bullets or sight");
            Assert.That(sniper.IsBeamVisible, Is.False);
        }

        [Test]
        public void TheGunPrefabIsHeldAndItsMuzzleFeedsTheFlash()
        {
            var agent = CreateAgent("enemy-1", EnemyArchetype.Rifleman);
            var presentation = agent.gameObject.AddComponent<WeaponPresentation>();
            var view = agent.gameObject.AddComponent<EnemyWeaponView>();
            var prefab = CreatePrefab("CustomGun", Vector3.zero);
            var muzzle = new GameObject(EnemyWeaponView.MuzzleName).transform;
            muzzle.SetParent(prefab.transform.GetChild(0), false);
            muzzle.localPosition = new Vector3(0f, 0.1f, 0.8f);
            view.ModelPrefab = prefab;

            view.Refresh(0.016f);

            Assert.That(view.IsShown, Is.True);
            var model = view.Root.Find(prefab.name);
            AssertInstanceOf(model, prefab);
            Assert.That(view.Root.Find(EnemyWeaponView.ModelName), Is.Null, "no placeholder box is made");
            Assert.That(view.Muzzle.IsChildOf(model), Is.True, "the prefab's own muzzle is used");
            Assert.That(view.Muzzle.localPosition, Is.EqualTo(new Vector3(0f, 0.1f, 0.8f)));
            Assert.That(presentation.Muzzle, Is.SameAs(view.Muzzle));
        }

        [Test]
        public void AGunPrefabWithoutAMuzzleGetsTheDefaultOne()
        {
            var view = CreateAgent("enemy-1", EnemyArchetype.Rifleman).gameObject.AddComponent<EnemyWeaponView>();
            view.ModelPrefab = CreatePrefab("CustomGun", Vector3.zero);

            view.Refresh(0.016f);

            Assert.That(view.Muzzle.parent, Is.SameAs(view.Root));
            Assert.That(view.Muzzle.localPosition, Is.EqualTo(new Vector3(0f, 0f, PlayerMockPose.GunSize.z / 2f)));
        }

        [Test]
        public void ShowingTheGunAgainLeavesTheRenderersThePrefabTurnedOff()
        {
            var agent = CreateAgent("enemy-1", EnemyArchetype.Rifleman);
            var view = agent.gameObject.AddComponent<EnemyWeaponView>();
            var prefab = CreatePrefab("CustomGun", Vector3.zero);
            var spare = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spare.name = "Spare";
            spare.transform.SetParent(prefab.transform, false);
            spare.GetComponent<Renderer>().enabled = false;
            view.ModelPrefab = prefab;
            view.Refresh(0.016f);

            var model = view.Root.Find(prefab.name);
            foreach (var gunRenderer in model.GetComponentsInChildren<Renderer>(true))
            {
                gunRenderer.enabled = false;
            }

            agent.Disarm();
            view.Refresh(0.016f);
            agent.EquipWeapon(_rifle);
            view.Refresh(0.016f);

            Assert.That(model.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(model.Find("Spare").GetComponent<Renderer>().enabled, Is.False);
        }

        [Test]
        public void WithoutAPrefabTheGunIsThePlaceholderBox()
        {
            var view = CreateAgent("enemy-1", EnemyArchetype.Rifleman).gameObject.AddComponent<EnemyWeaponView>();

            view.Refresh(0.016f);

            var model = view.Root.Find(EnemyWeaponView.ModelName);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.localScale, Is.EqualTo(PlayerMockPose.GunSize));
            Assert.That(view.Root.GetComponentInChildren<Collider>(true), Is.Null);
        }

        private static void AssertInstanceOf(Transform instance, GameObject prefab)
        {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.gameObject, Is.Not.SameAs(prefab), "the prefab itself is not moved into place");
            Assert.That(instance.name, Is.EqualTo(prefab.name));
            Assert.That(instance.GetComponentInChildren<Collider>(true), Is.Null, "it must not block shots, sight or the agent");
            Assert.That(prefab.GetComponentInChildren<Collider>(true), Is.Not.Null, "the prefab keeps its colliders");
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        // 子にも当たり判定を持たせ、子孫まで外すかを見る。
        private GameObject CreatePrefab(string prefabName, Vector3 localPosition)
        {
            var prefab = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            prefab.name = prefabName;
            prefab.transform.position = localPosition;
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Part";
            part.transform.SetParent(prefab.transform, false);
            return prefab;
        }

        private EnemyAgent CreateAgent(string id, EnemyArchetype archetype)
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = id;
            host.transform.SetPositionAndRotation(Origin + Vector3.up, Quaternion.identity);
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = id;
            host.GetComponent<Damageable>().Initialize(id, 100);
            agent.ApplySpec(new EnemySpec(10f, 6f, 1f, 2f, 0.5f, archetype: archetype));
            Assert.That(agent.TryInitialize(), Is.True);
            return agent;
        }
    }
}
