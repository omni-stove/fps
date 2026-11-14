using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では Update が走らず入力も無いので、Update が集める入力を Advance へ直接渡す。
    // UIDocument が木を作らないことがあるので、PauseMenuTests と同じく Hud.uxml を複製して Bind に渡す
    public sealed class PhotoModeTests
    {
        private const float _fieldOfView = 60f;

        private readonly List<Object> _objects = new List<Object>();
        private Hud _hud;
        private PauseMenu _pause;
        private PhotoModeView _photo;
        private VisualElement _root;
        private float _timeScaleBefore;

        [SetUp]
        public void SetUp()
        {
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 1f;
            _hud = Hud.Create("TestPhotoHud", null, null, null);
            _pause = _hud.PauseMenu;
            _photo = _hud.PhotoMode;
            _root = CloneLayout();
            _pause.Bind(_root);
            _photo.Bind(_root);
        }

        [TearDown]
        public void TearDown()
        {
            // Edit Mode では OnDisable が走らないので、借りたカメラと止めた時間を次のテストへ持ち越さない
            if (_photo != null)
            {
                _photo.Exit();
            }

            if (_pause != null)
            {
                _pause.Close();
            }

            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }

            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            Time.timeScale = _timeScaleBefore;
        }

        [Test]
        public void TheHudHasAPhotoModeWithAButtonInThePauseMenu()
        {
            Assert.That(_photo, Is.Not.Null);
            Assert.That(_photo.IsActive, Is.False);
            var element = _root.Q<VisualElement>(PauseMenu.UxmlName);
            Assert.That(element.Q<Button>(PhotoModeView.ButtonName), Is.Not.Null);
        }

        [Test]
        public void EnteringNeedsThePauseMenuOpen()
        {
            _photo.Target = Player(out _);

            LogAssert.Expect(LogType.Warning, "[PhotoMode] open the pause menu before entering the photo mode");
            Assert.That(_photo.Enter(), Is.False);
            Assert.That(_photo.IsActive, Is.False);
        }

        [Test]
        public void OfflineFreezesTimeHidesTheHudAndFreesTheCamera()
        {
            var rig = Player(out var damageable);
            _photo.Target = rig;
            var eye = rig.Eye.position;
            _pause.TimeMode = PauseTimeMode.Freeze;
            _pause.Open();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[PhotoMode\] began on Player around "));
            Assert.That(_photo.Enter(), Is.True);

            Assert.That(_photo.IsActive, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the pause keeps time frozen while photographing");
            Assert.That(HudRoot().style.display.value, Is.EqualTo(DisplayStyle.None), "the HUD and the pause menu are hidden");
            Assert.That(rig.enabled, Is.False, "the rig must not pull the camera back to the eye");
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.ThirdPerson), "the player's body is drawn");

            var camera = rig.Camera;
            var start = camera.transform.position;
            Assert.That(Vector3.Distance(start, eye), Is.GreaterThan(1f), "starts behind the player");
            Assert.That(Vector3.Distance(start, eye), Is.LessThanOrEqualTo(PhotoModeSpec.DefaultRadius));

            var yawBefore = camera.transform.eulerAngles.y;
            _photo.Advance(new Vector2(0f, 1f), new Vector2(30f, 0f), 0, 0f, false, 0.5f);

            Assert.That(Mathf.DeltaAngle(yawBefore, camera.transform.eulerAngles.y), Is.EqualTo(30f).Within(1e-2f), "Look turns the camera");
            var expected = start + (camera.transform.forward * (PhotoModeSpec.DefaultMoveSpeed * 0.5f));
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(1e-3f), "Move flies the way it faces");
            Assert.That(damageable.transform.position, Is.EqualTo(Vector3.zero), "the player itself does not move");
        }

        [Test]
        public void TheCameraStaysWithinTheRadiusAroundThePlayer()
        {
            var rig = Player(out _);
            _photo.Target = rig;
            _photo.Spec = new PhotoModeSpec(3f, 3f, 20f, 100f, 5f, 45f, 45f, 0.6f, 2.5f);
            var eye = rig.Eye.position;
            _pause.Open();
            _photo.Enter();

            for (var i = 0; i < 20; i++)
            {
                _photo.Advance(new Vector2(0.3f, 1f), Vector2.zero, 0, 0f, false, 1f);
                Assert.That(Vector3.Distance(rig.Camera.transform.position, eye), Is.LessThanOrEqualTo(3f + 1e-3f), "frame " + i);
            }

            Assert.That(Vector3.Distance(rig.Camera.transform.position, eye), Is.GreaterThan(2.9f), "flew out to the edge");
            Assert.That(_photo.Session.Center, Is.EqualTo(eye));
        }

        [Test]
        public void ZoomAndTiltChangeTheLensAndResetPutsItBack()
        {
            var rig = Player(out _);
            _photo.Target = rig;
            _pause.Open();
            _photo.Enter();
            var camera = rig.Camera;

            _photo.Advance(Vector2.zero, Vector2.zero, 2, 0f, false, 0f);
            Assert.That(camera.fieldOfView, Is.EqualTo(_fieldOfView - (2f * PhotoModeSpec.DefaultFieldOfViewStep)).Within(1e-3f), "Next zooms in");

            _photo.Advance(Vector2.zero, Vector2.zero, -100, 0f, false, 0f);
            Assert.That(camera.fieldOfView, Is.EqualTo(PhotoModeSpec.DefaultMaxFieldOfView).Within(1e-3f), "zooming out stops at the widest");

            _photo.Advance(Vector2.zero, Vector2.zero, 0, 1f, false, 0.5f);
            var roll = PhotoModeSpec.DefaultRollSpeed * 0.5f;
            Assert.That(_photo.Session.Roll, Is.EqualTo(roll).Within(1e-3f));
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z), Is.EqualTo(roll).Within(1e-2f), "the camera is tilted");

            _photo.Advance(Vector2.zero, Vector2.zero, 0, 1f, false, 100f);
            Assert.That(_photo.Session.Roll, Is.EqualTo(PhotoModeSpec.DefaultMaxRoll).Within(1e-3f), "tilting stops at the limit");

            _photo.Advance(Vector2.zero, Vector2.zero, 0, 0f, true, 0f);
            Assert.That(camera.fieldOfView, Is.EqualTo(_fieldOfView).Within(1e-3f));
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z), Is.EqualTo(0f).Within(1e-2f));
        }

        [Test]
        public void ClosingThePauseLeavesThePhotoModeAndRestoresTheView()
        {
            var rig = Player(out _);
            _photo.Target = rig;
            var camera = rig.Camera;
            var mode = rig.Mode;
            rig.UpdateCameraPosition();
            var localPosition = camera.transform.localPosition;
            var localRotation = camera.transform.localRotation;
            _pause.TimeMode = PauseTimeMode.Freeze;
            _pause.Open();
            _photo.Enter();
            _photo.Advance(new Vector2(1f, 1f), new Vector2(40f, 20f), 3, 1f, false, 0.5f);
            var exited = 0;
            _photo.Exited += () => exited++;

            LogAssert.Expect(LogType.Log, "[PhotoMode] ended on Player");
            _pause.Close();

            Assert.That(_photo.IsActive, Is.False);
            Assert.That(exited, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(HudRoot().style.display.value, Is.Not.EqualTo(DisplayStyle.None), "the HUD is back");
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, localRotation), Is.LessThan(1e-3f));
            Assert.That(camera.fieldOfView, Is.EqualTo(_fieldOfView).Within(1e-3f));
            Assert.That(rig.enabled, Is.True);
            Assert.That(rig.Mode, Is.EqualTo(mode));
        }

        [Test]
        public void ExitingKeepsThePauseMenuOpen()
        {
            _photo.Target = Player(out _);
            _pause.Open();
            _photo.Enter();

            LogAssert.Expect(LogType.Log, "[PhotoMode] exited to the pause menu");
            _photo.Exit();

            Assert.That(_pause.IsOpen, Is.True);
            Assert.That(HudRoot().style.display.value, Is.Not.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TakingAPhotoUsesTheReplacedCaptureAndLogsThePath()
        {
            _photo.Target = Player(out _);
            var capture = new FakeCapture("/photos/shot.png");
            _photo.PhotoCapture = capture;
            string taken = null;
            _photo.PhotoTaken += path => taken = path;
            _pause.Open();
            _photo.Enter();

            LogAssert.Expect(LogType.Log, "[PhotoMode] saved /photos/shot.png");
            var saved = _photo.TakePhoto();

            Assert.That(saved, Is.EqualTo("/photos/shot.png"));
            Assert.That(taken, Is.EqualTo(saved));
            Assert.That(capture.Calls, Is.EqualTo(1));
            Assert.That(HudRoot().style.display.value, Is.EqualTo(DisplayStyle.None), "the HUD stays hidden while the photo is taken");
        }

        [Test]
        public void AFailedCaptureIsLoggedAndReturnsNoPath()
        {
            _photo.Target = Player(out _);
            _photo.PhotoCapture = new FakeCapture(new IOException("disk full"));
            _pause.Open();
            _photo.Enter();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[PhotoMode\] could not save the photo with FakeCapture: disk full"));
            LogAssert.Expect(LogType.Exception, new Regex("disk full"));
            Assert.That(_photo.TakePhoto(), Is.Null);
            Assert.That(_photo.IsActive, Is.True, "a failed photo does not end the photo mode");
        }

        [Test]
        public void TakingAPhotoOutsideThePhotoModeDoesNothing()
        {
            var capture = new FakeCapture("/photos/shot.png");
            _photo.PhotoCapture = capture;

            LogAssert.Expect(LogType.Warning, "[PhotoMode] enter the photo mode before taking a photo");
            Assert.That(_photo.TakePhoto(), Is.Null);
            Assert.That(capture.Calls, Is.EqualTo(0));
        }

        [Test]
        public void ADeadPlayerCannotEnter()
        {
            var rig = Player(out var damageable);
            _photo.Target = rig;
            damageable.Kill();
            _pause.Open();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[PhotoMode\] Player is dead"));
            Assert.That(_photo.Enter(), Is.False);
            Assert.That(rig.enabled, Is.True);
        }

        [Test]
        public void TheDeathCameraTakesTheCameraBackAndThePhotoModeYieldsToIt()
        {
            var rig = Player(out _);
            _photo.Target = rig;
            var camera = rig.Camera;
            _pause.Open();
            _photo.Enter();
            _photo.Advance(Vector2.zero, Vector2.zero, 4, 0f, false, 0f);

            var deathCam = new DeathCamView(rig, "test");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[PhotoMode\] the death camera took over Player's camera"));
            deathCam.Begin(rig.Eye.position, 0f);

            Assert.That(_photo.Session, Is.Null, "the photo session ended");
            Assert.That(camera.fieldOfView, Is.EqualTo(_fieldOfView).Within(1e-3f), "the lens is put back before the death camera remembers it");
            Assert.That(DeathCamView.IsBorrowed(camera), Is.True);

            _photo.Exit();
            Assert.That(HudRoot().style.display.value, Is.Not.EqualTo(DisplayStyle.None));

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[PhotoMode\] Player's camera is in use"));
            Assert.That(_photo.Enter(), Is.False, "does not take the camera from the death camera");

            deathCam.Restore(true);
            Assert.That(DeathCamView.IsBorrowed(camera), Is.False);
            Assert.That(rig.enabled, Is.True);
        }

        [Test]
        public void TheSpecCannotChangeWhileActive()
        {
            _photo.Target = Player(out _);
            _pause.Open();
            _photo.Enter();

            Assert.Throws<InvalidOperationException>(() => _photo.Spec = PhotoModeSpec.Default);
            Assert.Throws<ArgumentNullException>(() => _photo.Spec = null);
        }

        [Test]
        public void ScreenshotsAreNamedByTimeAndNeverOverwrite()
        {
            var time = new DateTime(2026, 11, 14, 9, 30, 5, 120);
            Assert.That(ScreenshotPhotoCapture.FileNameFor(time), Is.EqualTo("photo_20261114_093005_120.png"));

            var directory = Path.Combine(Path.GetTempPath(), "fps-photo-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var first = ScreenshotPhotoCapture.UniquePath(directory, "photo.png");
                Assert.That(first, Is.EqualTo(Path.Combine(directory, "photo.png")));

                File.WriteAllBytes(first, new byte[] { 0 });
                Assert.That(ScreenshotPhotoCapture.UniquePath(directory, "photo.png"), Is.EqualTo(Path.Combine(directory, "photo_2.png")));
            }
            finally
            {
                Directory.Delete(directory, true);
            }

            Assert.That(new ScreenshotPhotoCapture().OutputDirectory, Is.EqualTo(ScreenshotPhotoCapture.DefaultDirectory));
            Assert.That(ScreenshotPhotoCapture.DefaultDirectory, Does.EndWith(ScreenshotPhotoCapture.FolderName));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenshotPhotoCapture(null, 0));
        }

        private VisualElement HudRoot()
        {
            return _root.Q<VisualElement>(PhotoModeView.HudRootName);
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        private ViewRig Player(out Damageable damageable)
        {
            var go = new GameObject("Player");
            _objects.Add(go);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            damageable = go.AddComponent<Damageable>();
            damageable.Initialize("Player", Damageable.DefaultMaxHealth);

            var eye = new GameObject(GameBootstrap.EyeName).transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = GameBootstrap.EyeOffset;
            var cameraObject = new GameObject(GameBootstrap.CameraName);
            cameraObject.transform.SetParent(eye, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = _fieldOfView;

            var rig = go.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            return rig;
        }

        private sealed class FakeCapture : IPhotoCapture
        {
            private readonly string _path;
            private readonly Exception _error;

            public FakeCapture(string path)
            {
                _path = path;
            }

            public FakeCapture(Exception error)
            {
                _error = error;
            }

            public int Calls { get; private set; }

            public string Capture()
            {
                Calls++;
                if (_error != null)
                {
                    throw _error;
                }

                return _path;
            }
        }
    }
}
