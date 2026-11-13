using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// NetPlayer の一人称視点の切り替えと、NetworkPlayerMotor が入力に乗せる向き。
    /// プレハブは Fps.Editor.NetSceneBuilder.Build の出力をそのまま使う。
    /// </summary>
    public sealed class NetPlayerViewTests
    {
        private GameObject _instance;
        private GameObject _body;
        private InputAction _lookAction;
        private int _localViewsBefore;

        [SetUp]
        public void SetUp()
        {
            _localViewsBefore = NetSpectatorCamera.LocalViewCount;
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                // Edit Mode では OnDestroy が走らないので、途中で失敗しても観戦用カメラの数を他のテストへ持ち越さない
                var player = _instance.GetComponent<NetPlayer>();
                if (player != null && player.HasLocalView)
                {
                    player.SetLocalView(false);
                }

                Object.DestroyImmediate(_instance);
            }

            if (_body != null)
            {
                Object.DestroyImmediate(_body);
            }

            _lookAction?.Dispose();
            Assert.That(NetSpectatorCamera.LocalViewCount, Is.EqualTo(_localViewsBefore));
        }

        [Test]
        public void NonOwnerKeepsCameraListenerAndLookDisabled()
        {
            var player = InstantiatePlayer();
            var camera = player.View.GetComponentInChildren<Camera>(true);
            var listener = camera.GetComponent<AudioListener>();
            var look = player.GetComponent<FirstPersonLook>();
            var rig = player.GetComponent<ViewRig>();

            // 何かの拍子に有効になっていても、オーナーでなければ止める
            camera.enabled = true;
            listener.enabled = true;
            look.enabled = true;
            rig.enabled = true;

            player.SetLocalView(NetStartup.ShouldEnableFirstPersonView(false, false, false));

            Assert.That(camera.enabled, Is.False);
            Assert.That(listener.enabled, Is.False);
            Assert.That(look.enabled, Is.False);
            Assert.That(rig.enabled, Is.False);
            Assert.That(player.HasLocalView, Is.False);
            Assert.That(NetSpectatorCamera.LocalViewCount, Is.EqualTo(_localViewsBefore));
        }

        [Test]
        public void OwnerViewEnablesCameraAndHidesSpectatorUntilDisabled()
        {
            var player = InstantiatePlayer();
            var camera = player.View.GetComponentInChildren<Camera>(true);
            var listener = camera.GetComponent<AudioListener>();
            var body = player.GetComponentInChildren<Renderer>(true);

            player.SetLocalView(true);

            Assert.That(camera.enabled, Is.True);
            Assert.That(listener.enabled, Is.True);
            Assert.That(player.GetComponent<FirstPersonLook>().enabled, Is.True);
            Assert.That(player.GetComponent<ViewRig>().enabled, Is.True);
            Assert.That(player.HasLocalView, Is.True);
            Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly), "own body must not block the view");
            Assert.That(NetSpectatorCamera.LocalViewCount, Is.EqualTo(_localViewsBefore + 1));
            Assert.That(NetSpectatorCamera.IsSpectating, Is.False);

            // 2 回目は数えない
            player.SetLocalView(true);
            Assert.That(NetSpectatorCamera.LocalViewCount, Is.EqualTo(_localViewsBefore + 1));

            player.SetLocalView(false);

            Assert.That(camera.enabled, Is.False);
            Assert.That(listener.enabled, Is.False);
            Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(NetSpectatorCamera.LocalViewCount, Is.EqualTo(_localViewsBefore));
        }

        [Test]
        public void AimsFromEyeAlongItsForwardIncludingPitch()
        {
            var player = InstantiatePlayer();
            player.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
            player.View.localRotation = Quaternion.Euler(-30f, 0f, 0f);

            var origin = player.AimOrigin();
            var direction = player.AimDirection();

            Assert.That(Vector3.Distance(origin, new Vector3(1f, 2f + NetPlayer.EyeHeight, 3f)), Is.LessThan(1e-4f),
                "the shot starts at the eye, which the server checks with NetPlayer.EyeHeight");
            Assert.That(Vector3.Distance(direction, player.View.forward), Is.LessThan(1e-4f));
            Assert.That(direction.y, Is.GreaterThan(0.4f), "looking up must aim up");
            Assert.That(direction.x, Is.GreaterThan(0.8f), "yaw 90 must aim toward +X");
        }

        [Test]
        public void CrouchLowersTheAimOriginWithTheEye()
        {
            var player = InstantiatePlayer();
            player.transform.position = new Vector3(1f, 2f, 3f);
            var stance = NetStance.FromHierarchy(player.GetComponent<CharacterController>(), PlayerController.DefaultCrouchHeight);

            stance.Apply(true);

            // 頭のてっぺんが 0.8m 下がるので、目 (射撃の始点) も 0.8m 下がる
            var crouchedEye = new Vector3(1f, 2f + NetPlayer.EyeHeight - 0.8f, 3f);
            Assert.That(Vector3.Distance(player.AimOrigin(), crouchedEye), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(stance.EyePosition, crouchedEye), Is.LessThan(1e-4f),
                "the server checks the origin from the same crouched eye height");

            stance.Apply(false);

            Assert.That(Vector3.Distance(player.AimOrigin(), new Vector3(1f, 2f + NetPlayer.EyeHeight, 3f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void ThirdPersonStillShowsTheBodyAndAimsFromTheEye()
        {
            var player = InstantiatePlayer();
            var rig = player.GetComponent<ViewRig>();
            Assert.That(rig.TryInitializeFromHierarchy(), Is.True);
            rig.Mode = ViewMode.ThirdPerson;
            rig.UpdateCameraPosition();
            player.transform.position = new Vector3(1f, 2f, 3f);

            player.SetLocalView(true);

            Assert.That(player.GetComponentInChildren<Renderer>(true).shadowCastingMode, Is.EqualTo(ShadowCastingMode.On),
                "the own body is what a third-person camera looks at");
            Assert.That(Vector3.Distance(player.AimOrigin(), new Vector3(1f, 2f + NetPlayer.EyeHeight, 3f)), Is.LessThan(1e-4f),
                "the shot must start at the eye, not at the camera, or the server rejects it and walls can be peeked");
            Assert.That(player.AimDirection().z, Is.GreaterThan(0.99f));
        }

        [Test]
        public void SampledYawComesFromFirstPersonLook()
        {
            var look = CreateLookBody();
            look.ApplyLookDelta(new Vector2(90f, 0f));

            // 本体が別の向きになっていても、入力の向きは視点操作の yaw
            _body.transform.rotation = Quaternion.Euler(0f, 10f, 0f);

            Assert.That(NetworkPlayerMotor.SampleYaw(look, _body.transform), Is.EqualTo(Mathf.PI / 2f).Within(1e-4f));
        }

        [Test]
        public void SampledYawFallsBackToBodyWithoutActiveLook()
        {
            var look = CreateLookBody();
            look.ApplyLookDelta(new Vector2(90f, 0f));
            _body.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            look.enabled = false;
            Assert.That(NetworkPlayerMotor.SampleYaw(look, _body.transform), Is.EqualTo(Mathf.PI).Within(1e-4f));
            Assert.That(NetworkPlayerMotor.SampleYaw(null, _body.transform), Is.EqualTo(Mathf.PI).Within(1e-4f));
            Assert.Throws<System.ArgumentNullException>(() => NetworkPlayerMotor.SampleYaw(look, null));
        }

        [Test]
        public void YawRotationTurnsBodyToTheSampledYaw()
        {
            var forward = NetworkPlayerMotor.YawRotation(Mathf.PI / 2f) * Vector3.forward;

            Assert.That(Vector3.Distance(forward, Vector3.right), Is.LessThan(1e-4f));
        }

        [Test]
        public void SpectatorShowsOnlyWithoutLocalViewOutsideDedicatedServer()
        {
            Assert.That(NetSpectatorCamera.ShouldShow(0, false), Is.True);
            Assert.That(NetSpectatorCamera.ShouldShow(1, false), Is.False);
            Assert.That(NetSpectatorCamera.ShouldShow(0, true), Is.False);
        }

        private NetPlayer InstantiatePlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null, NetSceneBuilder.PlayerPrefabPath + " is missing; run Fps.Editor.NetSceneBuilder.Build");

            _instance = Object.Instantiate(prefab);
            var player = _instance.GetComponent<NetPlayer>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.View, Is.Not.Null, "NetPlayer has no view; run Fps.Editor.NetSceneBuilder.Build");
            return player;
        }

        private FirstPersonLook CreateLookBody()
        {
            _body = new GameObject("Body");
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(_body.transform, false);
            _lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");

            var look = _body.AddComponent<FirstPersonLook>();
            look.Initialize(pivot, _lookAction);
            return look;
        }
    }
}
