using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// NetDeathCamera の切り替え。ネットワークを起動しないので、LateUpdate が集める状態 (ルール・生死・試合の終わり・相手の目) を
    /// Step と Frame へ直接渡す。プレハブは Fps.Editor.NetSceneBuilder.Build の出力を使う。
    /// </summary>
    public sealed class NetDeathCameraTests
    {
        private static readonly Vector3 _bodyPosition = new Vector3(0f, 1f, 0f);

        private GameObject _instance;
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
                // Edit Mode では OnDestroy が走らないので、観戦用カメラの数を他のテストへ持ち越さない
                var player = _instance.GetComponent<NetPlayer>();
                if (player != null && player.HasLocalView)
                {
                    player.SetLocalView(false);
                }

                Object.DestroyImmediate(_instance);
            }

            Assert.That(NetSpectatorCamera.LocalViewCount, Is.EqualTo(_localViewsBefore));
        }

        [Test]
        public void KillerSwitchesOnDeathFacesTheKillerAndRestoresOnRespawn()
        {
            var player = InstantiateLocalPlayer(out var deathCam);
            var rig = player.GetComponent<ViewRig>();
            var look = player.GetComponent<FirstPersonLook>();
            var ads = player.GetComponent<AimDownSights>();
            var camera = rig.Camera;
            var mode = rig.Mode;

            // 実行時は ViewRig の LateUpdate が置く位置。戻したときもここに来る
            rig.UpdateCameraPosition();
            var localPosition = camera.transform.localPosition;
            var localRotation = camera.transform.localRotation;
            var eye = player.AimOrigin();

            Assert.That(deathCam.Step(DeathCamera.Killer, true, false), Is.EqualTo(DeathCamTransition.None));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] started on "));
            Assert.That(deathCam.Step(DeathCamera.Killer, false, false), Is.EqualTo(DeathCamTransition.Started));

            Assert.That(deathCam.IsActive, Is.True);
            Assert.That(player.IsWatchingDeathCamera, Is.True, "firing waits until the view is back");
            Assert.That(look.InputSuspended, Is.True, "the view cannot be turned while watching");
            Assert.That(look.enabled, Is.True, "keeps the cursor locked instead of disabling the look");
            Assert.That(ads.enabled, Is.False);
            Assert.That(rig.enabled, Is.False, "the rig must not pull the camera back to the eye");
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.ThirdPerson));

            // 相手は +X 側の同じ高さにいる
            var killerEye = eye + new Vector3(10f, 0f, 0f);
            deathCam.Frame(killerEye);

            var position = camera.transform.position;
            Assert.That(position.y, Is.GreaterThan(eye.y + 0.5f), "a little above where the player fell");
            Assert.That(position.x, Is.LessThan(eye.x), "backs away from the killer so the death spot stays in view");
            var toKiller = (killerEye - position).normalized;
            Assert.That(Vector3.Dot(camera.transform.forward, toKiller), Is.GreaterThan(0.999f), "faces the killer");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] stopped on .*\(Respawned\)"));
            Assert.That(deathCam.Step(DeathCamera.Killer, true, false), Is.EqualTo(DeathCamTransition.Respawned));

            Assert.That(deathCam.IsActive, Is.False);
            Assert.That(player.IsWatchingDeathCamera, Is.False);
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, localRotation), Is.LessThan(1e-3f));
            Assert.That(look.InputSuspended, Is.False);
            Assert.That(ads.enabled, Is.True);
            Assert.That(rig.enabled, Is.True);
            Assert.That(rig.Mode, Is.EqualTo(mode));
        }

        [Test]
        public void WithoutKillerLooksDownAtTheDeathSpot()
        {
            var player = InstantiateLocalPlayer(out var deathCam);
            var camera = player.GetComponent<ViewRig>().Camera;
            var eye = player.AimOrigin();

            deathCam.Step(DeathCamera.Killer, true, false);
            deathCam.Step(DeathCamera.Killer, false, false);
            deathCam.Frame(null);

            var position = camera.transform.position;
            Assert.That(position.y, Is.GreaterThan(eye.y + 0.5f));
            Assert.That(Vector3.Dot(camera.transform.forward, (eye - position).normalized), Is.GreaterThan(0.999f),
                "looks at where the player fell");
            Assert.That(camera.transform.forward.y, Is.LessThan(-0.3f), "looking down");
        }

        [Test]
        public void NoneKeepsTheOwnView()
        {
            var player = InstantiateLocalPlayer(out var deathCam);
            var camera = player.GetComponent<ViewRig>().Camera;
            var localPosition = camera.transform.localPosition;

            deathCam.Step(DeathCamera.None, true, false);

            Assert.That(deathCam.Step(DeathCamera.None, false, false), Is.EqualTo(DeathCamTransition.None));
            deathCam.Frame(new Vector3(10f, 1.6f, 0f));

            Assert.That(deathCam.IsActive, Is.False);
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(player.GetComponent<FirstPersonLook>().InputSuspended, Is.False);
            Assert.That(player.GetComponent<ViewRig>().enabled, Is.True);
        }

        [Test]
        public void MatchEndYieldsToTheSpectatorView()
        {
            var player = InstantiateLocalPlayer(out var deathCam);

            deathCam.Step(DeathCamera.Killer, true, false);
            deathCam.Step(DeathCamera.Killer, false, false);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] stopped on .*\(MatchEnded\)"));
            Assert.That(deathCam.Step(DeathCamera.Killer, false, true), Is.EqualTo(DeathCamTransition.MatchEnded));
            Assert.That(player.GetComponent<FirstPersonLook>().InputSuspended, Is.False);
        }

        [Test]
        public void LosingTheViewRestoresTheCameraWithoutReenablingTheView()
        {
            var player = InstantiateLocalPlayer(out var deathCam);
            var rig = player.GetComponent<ViewRig>();
            var camera = rig.Camera;
            var localPosition = camera.transform.localPosition;

            deathCam.Step(DeathCamera.Killer, true, false);
            deathCam.Step(DeathCamera.Killer, false, false);
            deathCam.Frame(null);
            player.SetLocalView(false);

            Assert.That(deathCam.Step(DeathCamera.Killer, false, false), Is.EqualTo(DeathCamTransition.ViewLost));

            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(rig.enabled, Is.False, "a despawning player's view stays off");
            Assert.That(player.GetComponent<AimDownSights>().enabled, Is.False);
        }

        [Test]
        public void NonOwnerNeverSwitches()
        {
            var player = InstantiatePlayer(out var deathCam);

            deathCam.Step(DeathCamera.Killer, true, false);

            Assert.That(deathCam.Step(DeathCamera.Killer, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(player.IsWatchingDeathCamera, Is.False);
        }

        [Test]
        public void NoMatchMeansNoKiller()
        {
            Assert.That(NetDeathCamera.FindKillerId(null, 1), Is.Null);
        }

        private NetPlayer InstantiateLocalPlayer(out NetDeathCamera deathCam)
        {
            var player = InstantiatePlayer(out deathCam);

            // Edit Mode では Awake が走らないので、実行時に Awake が解決する参照をここで解決する
            Assert.That(player.GetComponent<ViewRig>().TryInitializeFromHierarchy(), Is.True);
            player.SetLocalView(true);
            Assert.That(player.HasLocalView, Is.True);
            return player;
        }

        private NetPlayer InstantiatePlayer(out NetDeathCamera deathCam)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetSceneBuilder.PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null, NetSceneBuilder.PlayerPrefabPath + " is missing; run Fps.Editor.NetSceneBuilder.Build");

            _instance = Object.Instantiate(prefab);
            _instance.transform.SetPositionAndRotation(_bodyPosition, Quaternion.identity);
            var player = _instance.GetComponent<NetPlayer>();
            Assert.That(player, Is.Not.Null);

            // プレハブに付いているかは NetTestSceneTests が見る。ここでは切り替えだけを確かめる
            deathCam = _instance.GetComponent<NetDeathCamera>();
            if (deathCam == null)
            {
                deathCam = _instance.AddComponent<NetDeathCamera>();
            }

            return player;
        }
    }
}
