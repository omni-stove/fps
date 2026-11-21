using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ViewModeDebugToggleTests
    {
        private GameObject _body;
        private Transform _eye;
        private Camera _camera;
        private ViewRig _rig;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _eye = new GameObject("Eye").transform;
            _eye.SetParent(_body.transform, false);
            _eye.localPosition = new Vector3(0f, 0.6f, 0f);
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_eye, false);
            _rig = _body.AddComponent<ViewRig>();
            _rig.Initialize(_eye, _camera);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
        }

        [Test]
        public void ToggleKeyIsF2()
        {
            Assert.That(ViewModeDebugToggle.ToggleKey, Is.EqualTo(Key.F2));
        }

        [Test]
        public void TogglingTheLocalViewFlipsBetweenFirstAndThirdPerson()
        {
            _rig.Mode = ViewMode.FirstPerson;
            _rig.ApplyBodyVisibility(true);

            LogAssert.Expect(LogType.Log, "[ViewSwitch] view: FirstPerson -> ThirdPerson");
            Assert.That(ViewModeDebugToggle.Toggle(_rig), Is.EqualTo(ViewModeSwitch.Refusal.None));
            Assert.That(_rig.Mode, Is.EqualTo(ViewMode.ThirdPerson));

            LogAssert.Expect(LogType.Log, "[ViewSwitch] view: ThirdPerson -> FirstPerson");
            Assert.That(ViewModeDebugToggle.Toggle(_rig), Is.EqualTo(ViewModeSwitch.Refusal.None));
            Assert.That(_rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void TogglingMovesTheCameraOffTheEyeInThirdPerson()
        {
            _rig.Mode = ViewMode.FirstPerson;
            _rig.ApplyBodyVisibility(true);
            _rig.UpdateCameraPosition();
            Assert.That(_camera.transform.position, Is.EqualTo(_eye.position));

            LogAssert.Expect(LogType.Log, "[ViewSwitch] view: FirstPerson -> ThirdPerson");
            ViewModeDebugToggle.Toggle(_rig);
            _rig.UpdateCameraPosition();

            Assert.That(Vector3.Distance(_camera.transform.position, _eye.position), Is.GreaterThan(0.5f));
        }

        [Test]
        public void RefusesWithoutARig()
        {
            LogAssert.Expect(LogType.Log, "[ViewSwitch] refused: no local player view in this scene");
            Assert.That(ViewModeDebugToggle.Toggle(null), Is.EqualTo(ViewModeSwitch.Refusal.NoLocalView));
        }

        [Test]
        public void RefusesAViewThatIsNotLocal()
        {
            _rig.Mode = ViewMode.FirstPerson;
            _rig.ApplyBodyVisibility(false);

            LogAssert.Expect(LogType.Log, "[ViewSwitch] refused: no local player view in this scene");
            Assert.That(ViewModeDebugToggle.Toggle(_rig), Is.EqualTo(ViewModeSwitch.Refusal.NoLocalView));
            Assert.That(_rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void RefusesWhileAnotherCameraBorrowsTheView()
        {
            _rig.Mode = ViewMode.FirstPerson;
            // 死亡カメラ・乗り物のカメラと同じく、止めてから体を描き直す (無効にすると ViewRig は自分を他人の視点として描くため)。
            _rig.enabled = false;
            _rig.ApplyBodyVisibility(true);

            LogAssert.Expect(LogType.Log, "[ViewSwitch] refused: the view is borrowed by the death or vehicle camera");
            Assert.That(ViewModeDebugToggle.Toggle(_rig), Is.EqualTo(ViewModeSwitch.Refusal.ViewBorrowed));
            Assert.That(_rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void FindLocalRigSkipsRigsThatAreNotLocal()
        {
            _rig.ApplyBodyVisibility(false);
            Assert.That(ViewModeDebugToggle.FindLocalRig(), Is.Null);

            _rig.ApplyBodyVisibility(true);
            Assert.That(ViewModeDebugToggle.FindLocalRig(), Is.SameAs(_rig));
        }
    }
}
