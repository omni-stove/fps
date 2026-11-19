using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // NetV2 の自分の体に一人称の武器モデルを出し、着けている迷彩で塗る。武器の Id と隠す理由はスナップショットの代わりに差し込む。
    public sealed class NetV2ViewmodelTests
    {
        private const int LocalPlayerId = 4;
        private const string RifleId = "rifle-id";
        private const string HandgunId = "handgun-id";
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private NetV2LocalPlayer _local;
        private ViewmodelCatalog _catalog;
        private GameObject _riflePrefab;
        private GameObject _handgunPrefab;
        private string _held;
        private string _hidden;

        [SetUp]
        public void SetUp()
        {
            ViewmodelPresenter.ResetOnPlay();
            _riflePrefab = Track(Template("RifleModel"));
            _handgunPrefab = Track(Template("HandgunModel"));
            _catalog = Track(ScriptableObject.CreateInstance<ViewmodelCatalog>());
            _catalog.SetWeapons(new[]
            {
                new ViewmodelEntry(RifleId, _riflePrefab, new Vector3(0.1f, -0.1f, 0.2f), new ViewmodelSwayParameters()),
                new ViewmodelEntry(HandgunId, _handgunPrefab, new Vector3(0.1f, -0.1f, 0.25f), new ViewmodelSwayParameters()),
            });
            _local = NetV2LocalPlayer.Create(_stance, MotorSpec.Default, () => true, Vector3.zero, 0f);
            Track(_local.gameObject);
            _held = RifleId;
            _hidden = null;
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
            ViewmodelPresenter.ResetOnPlay();
        }

        [Test]
        public void TheHeldWeaponIsDrawnInFrontOfTheCamera()
        {
            var presenter = Attach();

            presenter.Refresh();

            var rig = _local.GetComponent<ViewRig>();
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(rig.Camera, Is.SameAs(_local.Camera));
            Assert.That(rig.ViewmodelRoot.parent, Is.SameAs(_local.Camera.transform));
            Assert.That(presenter.CurrentWeaponId, Is.EqualTo(RifleId));
            Assert.That(presenter.Instance.transform.IsChildOf(rig.ViewmodelRoot), Is.True);
            Assert.That(
                _local.GetComponent<WeaponPresentation>().WeaponModel,
                Is.SameAs(presenter.Instance.transform),
                "muzzle flashes come out of the first-person model");

            _held = HandgunId;
            presenter.Refresh();

            Assert.That(presenter.CurrentWeaponId, Is.EqualTo(HandgunId));
            Assert.That(presenter.Instance.name, Is.EqualTo("HandgunModel"));
        }

        [Test]
        public void TheWornCamoPaintsTheNetworkedWeapon()
        {
            var tiger = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Tiger" });
            var presenter = Attach();
            Material worn = tiger;
            presenter.PaintSource = () => worn;

            presenter.Refresh();

            Assert.That(presenter.Instance.GetComponent<Renderer>().sharedMaterial, Is.SameAs(tiger));

            _held = HandgunId;
            presenter.Refresh();

            Assert.That(presenter.Instance.GetComponent<Renderer>().sharedMaterial, Is.SameAs(tiger), "the next weapon is painted too");

            worn = null;
            presenter.Refresh();

            Assert.That(
                presenter.Instance.GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(_handgunPrefab.GetComponent<Renderer>().sharedMaterial));
        }

        [Test]
        public void AttachingTwiceKeepsOneRigAndOnePresenter()
        {
            var first = _local.AttachViewmodel();
            var second = _local.AttachViewmodel();

            Assert.That(second, Is.SameAs(first));
            Assert.That(_local.GetComponents<ViewRig>().Length, Is.EqualTo(1));
            Assert.That(_local.Camera.transform.Find(ViewRig.ViewmodelRootName), Is.Not.Null);
        }

        [Test]
        public void TheWeaponIsPutAwayWhileTheSnapshotSaysSo()
        {
            var presenter = Attach();
            presenter.Refresh();
            Assert.That(presenter.Instance, Is.Not.Null);

            _hidden = ViewmodelPresenter.ReasonDead;
            presenter.Refresh();
            Assert.That(presenter.Instance, Is.Null);

            _hidden = null;
            presenter.Refresh();
            Assert.That(presenter.CurrentWeaponId, Is.EqualTo(RifleId), "respawning draws the weapon again");
        }

        [Test]
        public void ALivePlayerOnFootKeepsTheWeapon()
        {
            var snapshot = Snapshot(100, false);

            Assert.That(NetV2Viewmodel.HiddenReason(snapshot, LocalPlayerId, Standing(), false), Is.Null);
        }

        [Test]
        public void ADeadPlayerHidesTheWeapon()
        {
            Assert.That(
                NetV2Viewmodel.HiddenReason(Snapshot(0, false), LocalPlayerId, Standing(), false),
                Is.EqualTo(ViewmodelPresenter.ReasonDead));
        }

        [Test]
        public void ADownedPlayerHidesTheWeaponBecauseTheyCannotFire()
        {
            Assert.That(
                NetV2Viewmodel.HiddenReason(Snapshot(0, true), LocalPlayerId, Standing(), false),
                Is.EqualTo(ViewmodelPresenter.ReasonDowned));
            Assert.That(
                NetV2Viewmodel.HiddenReason(Snapshot(30, true), LocalPlayerId, Standing(), false),
                Is.EqualTo(ViewmodelPresenter.ReasonDowned));
        }

        [Test]
        public void ASeatThatCannotFireTheOwnWeaponHidesIt()
        {
            Assert.That(
                NetV2Viewmodel.HiddenReason(Snapshot(100, false), LocalPlayerId, Standing(), true),
                Is.EqualTo(ViewmodelPresenter.ReasonSeated));
        }

        [Test]
        public void ClimbingALadderHolstersTheWeapon()
        {
            var climbing = new CharacterMotorState(
                System.Numerics.Vector3.Zero,
                System.Numerics.Vector3.Zero,
                false,
                PlayerStance.Standing,
                CharacterCoverState.Out,
                DodgeSnapshot.Ready,
                CharacterVaultState.None,
                CharacterProneState.None,
                CharacterLadderState.Climbing(0, LadderEntry.Front, 1f, false, 0));

            Assert.That(
                NetV2Viewmodel.HiddenReason(Snapshot(100, false), LocalPlayerId, climbing, false),
                Is.EqualTo(ViewmodelPresenter.ReasonHolstered));
        }

        [Test]
        public void WithoutTheOwnRowOnlyTheMotionDecides()
        {
            Assert.That(NetV2Viewmodel.HiddenReason(null, LocalPlayerId, Standing(), false), Is.Null);
            Assert.That(NetV2Viewmodel.HiddenReason(new SnapshotMessage(), LocalPlayerId, Standing(), false), Is.Null);
            Assert.That(
                NetV2Viewmodel.HiddenReason(Snapshot(0, false), LocalPlayerId + 1, Standing(), false),
                Is.Null,
                "another player's death does not hide this player's weapon");
        }

        [Test]
        public void TheBobFollowsTheGroundSpeedAndStopsInTheAir()
        {
            var walking = new CharacterMotorState(System.Numerics.Vector3.Zero, new System.Numerics.Vector3(3f, -1f, 4f), true, false);
            var falling = new CharacterMotorState(System.Numerics.Vector3.Zero, new System.Numerics.Vector3(3f, -1f, 4f), false, false);

            Assert.That(NetV2Viewmodel.HorizontalSpeed(walking), Is.EqualTo(5f).Within(1e-5f), "the fall speed is left out");
            Assert.That(NetV2Viewmodel.HorizontalSpeed(falling), Is.EqualTo(0f));
        }

        private ViewmodelPresenter Attach()
        {
            var presenter = _local.AttachViewmodel();
            presenter.Catalog = _catalog;
            presenter.PaintSource = () => null;
            presenter.WeaponIdSource = () => _held;
            presenter.HiddenReasonSource = () => _hidden;
            return presenter;
        }

        private static CharacterMotorState Standing()
        {
            return new CharacterMotorState(System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, true, false);
        }

        private static SnapshotMessage Snapshot(int health, bool downed)
        {
            var snapshot = new SnapshotMessage();
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = LocalPlayerId;
            own.Team = Team.Alpha;
            own.Health = health;
            own.Downed = new DownedSync(downed, downed ? 10 : 0, 0);
            return snapshot;
        }

        private static GameObject Template(string name)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(0.05f, 0.08f, 0.4f);
            var muzzle = new GameObject(WeaponSockets.MuzzleName).transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.5f);
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
