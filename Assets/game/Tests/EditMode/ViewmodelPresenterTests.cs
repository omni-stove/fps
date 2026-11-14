using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class ViewmodelPresenterTests
    {
        private const string RifleId = "rifle-id";
        private const string HandgunId = "handgun-id";
        private const float Frame = 1f / 60f;

        private readonly List<string> _logs = new List<string>();
        private GameObject _body;
        private Camera _camera;
        private ViewRig _rig;
        private ViewmodelPresenter _presenter;
        private ViewmodelCatalog _catalog;
        private GameObject _riflePrefab;
        private GameObject _handgunPrefab;
        private string _held;

        [SetUp]
        public void SetUp()
        {
            ViewmodelPresenter.ResetOnPlay();
            _body = new GameObject("Body");
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(eye, false);
            _rig = _body.AddComponent<ViewRig>();
            _rig.Initialize(eye, _camera);
            _rig.ApplyBodyVisibility(true);

            _riflePrefab = Template("RifleModel", 0.6f);
            _handgunPrefab = Template("HandgunModel", 0.2f);
            _catalog = ScriptableObject.CreateInstance<ViewmodelCatalog>();
            _catalog.name = "TestViewmodelCatalog";
            _catalog.SetWeapons(new[]
            {
                new ViewmodelEntry(RifleId, _riflePrefab, new Vector3(0.1f, -0.1f, 0.2f), new ViewmodelSwayParameters()),
                new ViewmodelEntry(HandgunId, _handgunPrefab, new Vector3(0.1f, -0.1f, 0.25f), new ViewmodelSwayParameters()),
            });

            _presenter = _body.AddComponent<ViewmodelPresenter>();
            _presenter.Catalog = _catalog;
            _held = RifleId;
            _presenter.WeaponIdSource = () => _held;

            _logs.Clear();
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= Capture;
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_riflePrefab);
            Object.DestroyImmediate(_handgunPrefab);
            Object.DestroyImmediate(_catalog);
            ViewmodelPresenter.ResetOnPlay();
        }

        private void Capture(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith(ViewmodelPresenter.LogTag, System.StringComparison.Ordinal))
            {
                _logs.Add(condition);
            }
        }

        private static GameObject Template(string name, float length)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = new Vector3(0.05f, 0.08f, length);
            var muzzle = new GameObject(WeaponSockets.MuzzleName).transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.5f);
            return root;
        }

        [Test]
        public void FirstPersonSpawnsTheHeldWeaponUnderTheViewmodelRoot()
        {
            _presenter.Refresh();

            var instance = _presenter.Instance;
            Assert.That(instance, Is.Not.Null);
            Assert.That(_presenter.CurrentWeaponId, Is.EqualTo(RifleId));
            Assert.That(instance.transform.parent, Is.SameAs(_presenter.Motion.Pivot));
            Assert.That(_presenter.Motion.Pivot.parent, Is.SameAs(_rig.ViewmodelRoot));
            Assert.That(instance.transform.localPosition, Is.EqualTo(new Vector3(0.1f, -0.1f, 0.2f)));
            Assert.That(_logs, Is.EqualTo(new[] { ViewmodelPresenter.FormatSpawnLog(RifleId, "RifleModel", true) }));
        }

        [Test]
        public void TheMuzzleSocketIsFoundForTheWeaponPresentation()
        {
            var presentation = _body.AddComponent<WeaponPresentation>();

            _presenter.Refresh();

            Assert.That(_presenter.Sockets.Muzzle, Is.Not.Null);
            Assert.That(_presenter.Sockets.Muzzle.name, Is.EqualTo(WeaponSockets.MuzzleName));
            Assert.That(_presenter.Sockets.Muzzle.IsChildOf(_rig.ViewmodelRoot), Is.True);
            Assert.That(presentation.WeaponModel, Is.SameAs(_presenter.Instance.transform));
            Assert.That(presentation.Sockets.Muzzle, Is.SameAs(_presenter.Sockets.Muzzle));
        }

        [Test]
        public void TheModelHasNoCollidersAndCastsNoShadows()
        {
            _presenter.Refresh();

            Assert.That(_presenter.Instance.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var renderer in _presenter.Instance.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            }
        }

        [Test]
        public void ApplyingBodyVisibilityLeavesTheViewmodelDrawn()
        {
            _presenter.Refresh();

            _rig.ApplyBodyVisibility(true);

            Assert.That(_presenter.Instance.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void SwitchingWeaponsSwapsTheModelAndLogsIt()
        {
            _presenter.Refresh();
            var first = _presenter.Instance;

            _held = HandgunId;
            _presenter.Refresh();

            Assert.That(first == null, Is.True, "the old model is destroyed");
            Assert.That(_presenter.CurrentWeaponId, Is.EqualTo(HandgunId));
            Assert.That(_presenter.Instance.name, Is.EqualTo("HandgunModel"));
            Assert.That(_rig.ViewmodelRoot.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(1));
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(ViewmodelPresenter.FormatSwapLog(RifleId, HandgunId, "HandgunModel", true)));
        }

        [Test]
        public void RefreshingWithTheSameWeaponKeepsTheModel()
        {
            _presenter.Refresh();
            var first = _presenter.Instance;

            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.SameAs(first));
            Assert.That(_logs.Count, Is.EqualTo(1));
        }

        [Test]
        public void ThirdPersonShowsNothingAndLogsNoSpawn()
        {
            _rig.Mode = ViewMode.ThirdPerson;

            _presenter.Refresh();
            _held = HandgunId;
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_logs, Is.Empty);
        }

        [Test]
        public void SwitchingToThirdPersonRemovesTheModel()
        {
            _presenter.Refresh();

            _rig.Mode = ViewMode.ThirdPerson;
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_presenter.Sockets, Is.SameAs(WeaponSockets.None));
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(ViewmodelPresenter.FormatRemoveLog(RifleId, ViewmodelPresenter.ReasonThirdPerson)));
        }

        [Test]
        public void SomeoneElsesViewShowsNothing()
        {
            _rig.ApplyBodyVisibility(false);

            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
        }

        [Test]
        public void ADeadPlayerShowsNothing()
        {
            var damageable = _body.AddComponent<Damageable>();
            damageable.Initialize("Player", 10);
            _presenter.Refresh();
            Assert.That(_presenter.Instance, Is.Not.Null);

            damageable.TakeDamage(10);
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(ViewmodelPresenter.FormatRemoveLog(RifleId, ViewmodelPresenter.ReasonDead)));
        }

        [Test]
        public void UnarmedShowsNothing()
        {
            _presenter.Refresh();

            _held = null;
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo(ViewmodelPresenter.FormatRemoveLog(RifleId, ViewmodelPresenter.ReasonUnarmed)));
        }

        [Test]
        public void AWeaponMissingFromTheCatalogWarnsOnceAndShowsNothing()
        {
            _held = "unknown-id";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("weapon 'unknown-id' has no viewmodel prefab"));

            _presenter.Refresh();
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_logs.Count, Is.EqualTo(1), "warned only once");
        }

        [Test]
        public void NoCatalogWarnsOnceAndCarriesOn()
        {
            _presenter.Catalog = null;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no ViewmodelCatalog at Resources/" + ViewmodelCatalog.ResourcePath));

            _presenter.Refresh();
            _held = HandgunId;
            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Null);
            Assert.That(_presenter.Sockets, Is.SameAs(WeaponSockets.None));
            Assert.That(_logs.Count, Is.EqualTo(1), "warned only once");
        }

        [Test]
        public void InvalidSwayValuesFallBackToTheDefaults()
        {
            var broken = new ViewmodelSwayParameters();
            typeof(ViewmodelSwayParameters)
                .GetField("_bobReferenceSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(broken, 0f);
            _catalog.SetWeapons(new[] { new ViewmodelEntry(RifleId, _riflePrefab, Vector3.zero, broken) });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("invalid sway values"));

            _presenter.Refresh();

            Assert.That(_presenter.Instance, Is.Not.Null);
            Assert.That(_presenter.Motion.Settings, Is.SameAs(ViewmodelSwaySettings.Default));
        }

        [Test]
        public void MotionIsStillWhileStandingAndBobsWhileWalking()
        {
            _presenter.Refresh();
            var motion = _presenter.Motion;
            var speed = 0f;
            motion.HorizontalSpeedSource = () => speed;
            motion.AdsProgressSource = () => 0f;

            Step(motion, 60);
            Assert.That(motion.Pivot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(motion.Pivot.localRotation, Is.EqualTo(Quaternion.identity));

            speed = 5f;
            var moved = 0f;
            for (var i = 0; i < 60; i++)
            {
                motion.Step(Frame);
                moved = Mathf.Max(moved, motion.Pivot.localPosition.magnitude);
            }

            Assert.That(moved, Is.GreaterThan(0.005f));

            speed = 0f;
            Step(motion, 300);
            Assert.That(motion.Pivot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(motion.Sway.IsAtRest, Is.True);
        }

        [Test]
        public void TurningTheViewMakesTheModelLagBehind()
        {
            _presenter.Refresh();
            var motion = _presenter.Motion;
            motion.HorizontalSpeedSource = () => 0f;
            motion.AdsProgressSource = () => 0f;
            motion.Step(Frame);

            for (var i = 0; i < 10; i++)
            {
                _body.transform.Rotate(0f, 1.5f, 0f);
                motion.Step(Frame);
            }

            Assert.That(motion.Sway.YawRightDegrees, Is.LessThan(0f));
            Assert.That(Mathf.DeltaAngle(0f, motion.Pivot.localEulerAngles.y), Is.LessThan(0f));

            Step(motion, 300);
            Assert.That(motion.Sway.IsAtRest, Is.True);
        }

        [Test]
        public void AJumpInTheViewIsNotTakenAsATurn()
        {
            _presenter.Refresh();
            var motion = _presenter.Motion;
            motion.HorizontalSpeedSource = () => 0f;
            motion.Step(Frame);

            _body.transform.Rotate(0f, 170f, 0f);
            motion.Step(Frame);

            Assert.That(motion.Sway.IsAtRest, Is.True);
        }

        [Test]
        public void FiringKicksTheModelUpAndBackThenItReturns()
        {
            _presenter.Refresh();
            var motion = _presenter.Motion;
            motion.HorizontalSpeedSource = () => 0f;
            motion.AdsProgressSource = () => 0f;

            motion.Kick();

            Assert.That(motion.Pivot.localPosition.z, Is.LessThan(0f));
            Assert.That(Mathf.DeltaAngle(0f, motion.Pivot.localEulerAngles.x), Is.LessThan(0f), "the muzzle tips up");

            Step(motion, 120);
            Assert.That(motion.Pivot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(motion.Pivot.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void OnlyShotsFromThisBodyCount()
        {
            var motion = _presenter.Motion;
            var stranger = new GameObject("Stranger");
            try
            {
                Assert.That(motion.IsOwnShot(_body), Is.True);
                Assert.That(motion.IsOwnShot(_camera.gameObject), Is.True);
                Assert.That(motion.IsOwnShot(stranger), Is.False);
                Assert.That(motion.IsOwnShot(null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void AimingDownSightsDampensTheBob()
        {
            _presenter.Refresh();
            var motion = _presenter.Motion;
            var ads = 0f;
            motion.HorizontalSpeedSource = () => 5f;
            motion.AdsProgressSource = () => ads;

            var hip = MaxOffset(motion, 60);
            motion.ResetMotion();
            ads = 1f;
            var aimed = MaxOffset(motion, 60);

            Assert.That(aimed, Is.GreaterThan(0f));
            Assert.That(aimed, Is.LessThan(hip * 0.5f));
        }

        [Test]
        public void MotionTouchesOnlyThePivotUnderTheViewmodelRoot()
        {
            _presenter.Refresh();
            var motion = _presenter.Motion;
            motion.HorizontalSpeedSource = () => 5f;
            var cameraPosition = _camera.transform.localPosition;
            var rootPosition = _rig.ViewmodelRoot.localPosition;
            var shakePosition = _rig.ShakeRoot.localPosition;

            Step(motion, 30);
            motion.Kick();

            Assert.That(_camera.transform.localPosition, Is.EqualTo(cameraPosition));
            Assert.That(_rig.ViewmodelRoot.localPosition, Is.EqualTo(rootPosition));
            Assert.That(_rig.ViewmodelRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_rig.ShakeRoot.localPosition, Is.EqualTo(shakePosition));
            Assert.That(motion.Pivot.localPosition, Is.Not.EqualTo(Vector3.zero));
        }

        private static void Step(ViewmodelMotion motion, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                motion.Step(Frame);
            }
        }

        private static float MaxOffset(ViewmodelMotion motion, int frames)
        {
            var max = 0f;
            for (var i = 0; i < frames; i++)
            {
                motion.Step(Frame);
                max = Mathf.Max(max, motion.Pivot.localPosition.magnitude);
            }

            return max;
        }
    }
}
