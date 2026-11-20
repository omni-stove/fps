using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Net.Tests
{
    public sealed class NetV2DeployablesPartTests
    {
        private readonly List<IMessage> _sent = new List<IMessage>();
        private readonly List<string> _logs = new List<string>();
        private readonly List<Object> _created = new List<Object>();
        private ShieldDeployDefinition _shield;
        private NetV2DeployablesPart _part;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _shield = ScriptableObject.CreateInstance<ShieldDeployDefinition>();
            _shield.Id = "deploy-shield";
            _shield.Size = new Vector3(3f, 2f, 0.5f);
            _created.Add(_shield);
            _part = new NetV2DeployablesPart(Send, () => 1, new DeployAbilityDefinition[] { _shield }, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ADefinitionWithoutAPrefabIsShownAsABoxOfItsSizeWithoutColliders()
        {
            var view = Show(DeployableKind.Shield, "deploy-shield", new System.Numerics.Vector3(2f, 0.5f, -1f), 90f);

            var box = view.Visual.transform.Find("Box");
            Assert.That(box, Is.Not.Null);
            Assert.That(box.localScale, Is.EqualTo(new Vector3(3f, 2f, 0.5f)));
            Assert.That(box.localPosition, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(view.Visual.transform.position, Is.EqualTo(new Vector3(2f, 0.5f, -1f)));
            Assert.That(view.Visual.transform.eulerAngles.y, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(view.Visual.GetComponentInChildren<Collider>().enabled, Is.False, "the server's movement does not collide with it");
            Assert.That(_logs, Does.Contain("[Deploy] net look kind=deploy-shield role=Shield prefab=False"));
        }

        [Test]
        public void TheReplaceablePrefabIsShownWithItsOwnBehavioursAndCollidersStopped()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var turret = prefab.AddComponent<Turret>();
            _created.Add(prefab);
            _shield.Prefab = prefab;

            var view = Show(DeployableKind.Shield, "deploy-shield", System.Numerics.Vector3.Zero, 0f);

            Assert.That(view.Visual, Is.Not.SameAs(prefab));
            Assert.That(view.Visual.GetComponent<SphereCollider>().enabled, Is.False);
            Assert.That(view.Visual.GetComponent<Turret>().enabled, Is.False);
            Assert.That(view.Visual.GetComponent<Deployable>().enabled, Is.False);
            Assert.That(turret.enabled, Is.True, "the prefab itself is left as it is");
            Assert.That(_logs, Does.Contain("[Deploy] net look kind=deploy-shield role=Shield prefab=True"));
        }

        [Test]
        public void ATypeWithoutADefinitionIsShownAsTheDefaultBoxOfItsKind()
        {
            var view = Show(DeployableKind.Turret, "deploy-unknown", System.Numerics.Vector3.Zero, 0f);

            Assert.That(view.Visual.transform.Find("Box").localScale, Is.EqualTo(new Vector3(TurretSpec.DefaultWidth, TurretSpec.DefaultHeight, TurretSpec.DefaultDepth)));
        }

        [Test]
        public void ATurretWithoutAPrefabIsShownAsABaseAndAHeadAndOnlyTheHeadTurns()
        {
            var view = ShowTurret(90f);

            var root = view.Visual.transform;
            Assert.That(root.Find(TurretDeployDefinition.BaseName), Is.Not.Null);
            var head = root.Find(Turret.HeadName);
            Assert.That(head, Is.Not.Null);
            Assert.That(root.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(head.eulerAngles.y, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(root.Find(TurretDeployDefinition.BaseName).eulerAngles.y, Is.EqualTo(0f).Within(1e-3f));

            view.Move(System.Numerics.Vector3.Zero, 30f);

            Assert.That(root.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(head.eulerAngles.y, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void ATurretPrefabWithoutAHeadTurnsAsAWhole()
        {
            var prefab = new GameObject("TurretPrefab");
            _created.Add(prefab);
            var turret = ScriptableObject.CreateInstance<TurretDeployDefinition>();
            turret.Id = "deploy-turret";
            turret.Prefab = prefab;
            _created.Add(turret);
            _part.Dispose();
            _part = new NetV2DeployablesPart(Send, () => 1, new DeployAbilityDefinition[] { turret }, _logs.Add);

            var view = (NetV2DeployablesPart.View)_part.Show(DeployableKind.Turret, "deploy-turret", Team.Alpha, true, System.Numerics.Vector3.Zero, 90f);
            _created.Add(view.Visual);

            Assert.That(view.Visual.transform.Find(Turret.HeadName), Is.Null);
            Assert.That(view.Visual.transform.eulerAngles.y, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void ABeaconWithoutAPrefabIsShownAsItsPoleAndLampWithoutCollidersOrItsOfflineParts()
        {
            var beacon = ScriptableObject.CreateInstance<BeaconDeployDefinition>();
            beacon.Id = "deploy-beacon";
            _created.Add(beacon);
            _part.Dispose();
            _part = new NetV2DeployablesPart(Send, () => 1, new DeployAbilityDefinition[] { beacon }, _logs.Add);

            var view = (NetV2DeployablesPart.View)_part.Show(
                DeployableKind.Beacon, "deploy-beacon", Team.Alpha, true, new System.Numerics.Vector3(-3f, 0.5f, 0f), 90f);
            _created.Add(view.Visual);

            var root = view.Visual.transform;
            Assert.That(root.Find(BeaconDeployDefinition.PoleName), Is.Not.Null);
            Assert.That(root.Find(BeaconDeployDefinition.LampName), Is.Not.Null);
            Assert.That(root.position, Is.EqualTo(new Vector3(-3f, 0.5f, 0f)));
            Assert.That(root.eulerAngles.y, Is.EqualTo(90f).Within(1e-3f), "a beacon turns as a whole");
            Assert.That(view.Visual.GetComponentsInChildren<Collider>(), Has.All.Matches<Collider>(c => !c.enabled));
            Assert.That(view.Visual.GetComponentInChildren<SpawnBeacon>(), Is.Null, "the server counts the uses, not the client");
            Assert.That(_logs, Does.Contain("[Deploy] net look kind=deploy-beacon role=Beacon prefab=False"));
        }

        [Test]
        public void ABeaconWithoutADefinitionIsShownAsTheBeaconBox()
        {
            var view = Show(DeployableKind.Beacon, "deploy-unknown-beacon", System.Numerics.Vector3.Zero, 0f);

            Assert.That(
                view.Visual.transform.Find("Box").localScale,
                Is.EqualTo(new Vector3(BeaconSpec.DefaultWidth, BeaconSpec.DefaultHeight, BeaconSpec.DefaultDepth)));
        }

        [Test]
        public void ASnapshotShowsAndRemovesWhatTheServerLists()
        {
            var snapshot = new SnapshotMessage();
            ref var row = ref snapshot.Deployables.Add();
            row.DeployableId = 4;
            row.Present = DeployableFields.All;
            row.Kind = DeployableKind.Shield;
            row.TypeId = "deploy-shield";
            row.Team = Team.Alpha;
            row.HasOwner = true;
            row.OwnerId = 1;
            row.Health = 300;

            _part.Apply(snapshot);

            Assert.That(_part.Deployables.Count, Is.EqualTo(1));
            Assert.That(_logs, Does.Contain("[Deploy] net shown id=4 kind=deploy-shield own=True"));

            _part.Apply(new SnapshotMessage());

            Assert.That(_part.Deployables.Count, Is.EqualTo(0));
            Assert.That(_logs, Does.Contain("[Deploy] net gone id=4 kind=deploy-shield"));
        }

        [Test]
        public void DeployingAnUnknownTypeSendsNothing()
        {
            var owner = new GameObject("Owner");
            _created.Add(owner);

            Assert.That(_part.Deploy("deploy-missing", owner.transform, Vector3.zero, Vector3.forward), Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Does.Contain("[Deploy] net request kind=deploy-missing not sent: no deploy definition has this id"));
        }

        [Test]
        public void DeployingWhereThereIsNoGroundSendsNothing()
        {
            var owner = new GameObject("Owner");
            owner.transform.position = new Vector3(0f, 500f, 0f);
            _created.Add(owner);

            Assert.That(_part.Deploy("deploy-shield", owner.transform, owner.transform.position, Vector3.forward), Is.False);
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Does.Contain("[Deploy] net request kind=deploy-shield not sent verdict=NoGround"));
        }

        private NetV2DeployablesPart.View Show(DeployableKind kind, string typeId, System.Numerics.Vector3 position, float yaw)
        {
            var view = (NetV2DeployablesPart.View)_part.Show(kind, typeId, Team.Alpha, true, position, yaw);
            _created.Add(view.Visual);
            return view;
        }

        private NetV2DeployablesPart.View ShowTurret(float yaw)
        {
            var turret = ScriptableObject.CreateInstance<TurretDeployDefinition>();
            turret.Id = "deploy-turret";
            _created.Add(turret);
            _part.Dispose();
            _part = new NetV2DeployablesPart(Send, () => 1, new DeployAbilityDefinition[] { turret }, _logs.Add);
            var view = (NetV2DeployablesPart.View)_part.Show(DeployableKind.Turret, "deploy-turret", Team.Alpha, true, System.Numerics.Vector3.Zero, yaw);
            _created.Add(view.Visual);
            return view;
        }

        private bool Send(IMessage message)
        {
            _sent.Add(message);
            return true;
        }
    }
}
