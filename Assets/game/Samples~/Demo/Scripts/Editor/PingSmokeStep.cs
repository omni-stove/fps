using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) で、印 (<see cref="PingController"/>) を確かめる手順。
    /// <list type="number">
    /// <item>射撃場のルールで Ping が Off なら、付けようとしても付かない (<c>[Ping] refused reason=RuleOff</c>) ことを確かめ、
    /// プレイヤーの <see cref="PingController"/> だけを Ping を On にしたルールで設定し直す。</item>
    /// <item>標的ダミーを <see cref="PingRangeFixture.ShowPoint"/> に置き、<see cref="PingRangeFixture.StandPoint"/> から照準を合わせて付ける
    /// (<c>[Ping] placed ... kind=Enemy</c>)。</item>
    /// <item>ダミーを壁の裏 (<see cref="PingRangeFixture.HiddenPoint"/>) へ動かすと見失い (<c>[Ping] lost</c>)、印は最後に見た位置に残る。</item>
    /// <item>右を向くと、印は HUD (<see cref="PingMarkersView"/>) で画面の左端に寄って向きが出る (<c>[Ping] screen ... onScreen=false edge=Left</c>)。</item>
    /// <item>付けてから <see cref="PingSpec.LifetimeSeconds"/> 秒で消える (<c>[Ping] expired</c>)。</item>
    /// </list>
    /// 入力の代わりに <see cref="FirstPersonLook.ApplyLookDelta"/> で照準を合わせ、<see cref="PingController.TryPing"/> を呼ぶ。
    /// </summary>
    public sealed class PingSmokeStep : ITpsActionSmokeStep
    {
        /// <summary><see cref="Order"/>。</summary>
        public const int StepOrder = 500;

        // 印を付けてから消えるまでを待つので、寿命に置き直しと振り向きの分を足す。
        private const float _extraSeconds = 20f;

        // 照準を合わせ直すフレーム数。肩越しのカメラは向きを変えると位置も動くので、何度か合わせ直して揃える。
        private const int _aimFrames = 3;

        // HUD に表示が付くまで待つ秒数。PingController は HUD を PingController.ViewSearchIntervalSeconds ごとに探す。
        private const float _viewWaitSeconds = PingController.ViewSearchIntervalSeconds * 3f;

        // 見失った後の印の位置と、最後に見た位置のずれの許容 (m)。
        private const float _positionTolerance = 0.01f;

        // 寿命より早く消えていないかの許容 (秒)。フレームの刻みの分。
        private const float _lifetimeTolerance = 0.1f;

        private readonly List<string> _logs = new List<string>();

        private Phase _phase;
        private BootstrapWorld _world;
        private PingController _controller;
        private bool _reconfigured;
        private TargetDummy _dummy;
        private Vector3 _dummyPosition;
        private Quaternion _dummyRotation;
        private int _frames;
        private int _markerId;
        private float _placedAt;
        private Vector3 _lastSeen;
        private float _waitStartedAt;

        private enum Phase
        {
            Start,
            Stage,
            Aim,
            Hide,
            WaitLost,
            TurnAway,
            WaitOffScreen,
            WaitExpired,
            Done,
        }

        public int Order => StepOrder;

        public string Name => "ping";

        public float TimeoutSeconds => PingSpec.DefaultLifetimeSeconds + _extraSeconds;

        public void Reset()
        {
            _logs.Clear();
            _phase = Phase.Start;
            _world = null;
            _controller = null;
            _reconfigured = false;
            _dummy = null;
            _frames = 0;
            _markerId = 0;
            _placedAt = 0f;
            _lastSeen = Vector3.zero;
            _waitStartedAt = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition != null && condition.StartsWith(PingLog.Tag + " ", System.StringComparison.Ordinal))
            {
                _logs.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Start:
                    problem = Begin(world);
                    break;
                case Phase.Stage:
                    problem = Stage(world);
                    break;
                case Phase.Aim:
                    problem = AimAndPing(world);
                    break;
                case Phase.Hide:
                    problem = Hide();
                    break;
                case Phase.WaitLost:
                    problem = CheckLost();
                    break;
                case Phase.TurnAway:
                    TurnTo(world, 90f);
                    _waitStartedAt = Time.realtimeSinceStartup;
                    _phase = Phase.WaitOffScreen;
                    break;
                case Phase.WaitOffScreen:
                    problem = CheckOffScreen(world);
                    break;
                case Phase.WaitExpired:
                    problem = CheckExpired();
                    break;
            }

            return problem == null && _phase == Phase.Done;
        }

        public void Cleanup()
        {
            if (_dummy != null)
            {
                _dummy.transform.SetPositionAndRotation(_dummyPosition, _dummyRotation);
                Physics.SyncTransforms();
            }

            if (_reconfigured && _controller != null && _world != null)
            {
                _controller.Configure(_world.Rules);
            }

            _dummy = null;
            _controller = null;
            _world = null;
            _reconfigured = false;
        }

        private string Begin(BootstrapWorld world)
        {
            _world = world;
            _controller = world.Player.GetComponent<PingController>();
            if (_controller == null)
            {
                return $"the player has no PingController; {nameof(PingActionInstaller)} is not registered with {nameof(PlayerActions)}";
            }

            if (GameObject.Find(PingRangeFixture.FixtureName + "/" + PingRangeFixture.BlindName) == null)
            {
                return $"the scene has no '{PingRangeFixture.FixtureName}/{PingRangeFixture.BlindName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var actions = world.Rules.Actions;
            if (actions.Ping == Fps.Core.Ping.Off)
            {
                if (_controller.TryPing(out var unexpected))
                {
                    return $"a ping was placed while the rule was Off ({PingLog.Describe(unexpected)})";
                }

                if (_controller.LastRefusal != PingRefusal.RuleOff)
                {
                    return $"a ping with the rule Off was refused for {_controller.LastRefusal} instead of {PingRefusal.RuleOff}";
                }

                _controller.Configure(world.Rules.WithActions(actions.WithPing(Fps.Core.Ping.On)));
                _reconfigured = true;
            }

            if (!_controller.UsesPing)
            {
                return $"the PingController did not turn on (rule {_controller.Rule})";
            }

            _phase = Phase.Stage;
            return null;
        }

        private string Stage(BootstrapWorld world)
        {
            // 前の手順で倒されたダミーは戻るまで待つ (戻るまでの時間は手順の上限に収まる)。
            foreach (var target in world.Targets)
            {
                if (target != null && target.Damageable != null && !target.Damageable.IsDead)
                {
                    _dummy = target;
                    break;
                }
            }

            if (_dummy == null)
            {
                return null;
            }

            _dummyPosition = _dummy.transform.position;
            _dummyRotation = _dummy.transform.rotation;
            MoveDummy(PingRangeFixture.ShowPoint);
            Teleport(world.Controller, PingRangeFixture.StandPoint + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            AimAt(world, _dummy.Damageable.AimPoint);
            _frames = 0;
            _phase = Phase.Aim;
            return null;
        }

        private string AimAndPing(BootstrapWorld world)
        {
            AimAt(world, _dummy.Damageable.AimPoint);
            if (++_frames < _aimFrames)
            {
                return null;
            }

            if (!_controller.TryPing(out var marker))
            {
                return $"pinging the target dummy '{_dummy.Damageable.Id}' at {Format(PingRangeFixture.ShowPoint)} was refused ({_controller.LastRefusal})";
            }

            if (marker.Kind != PingKind.Enemy || _controller.TargetOf(marker.Id) != _dummy.Damageable)
            {
                var target = _controller.TargetOf(marker.Id);
                return $"aiming at '{_dummy.Damageable.Id}' marked {marker.Kind} '{(target != null ? target.Id : "none")}' ({PingLog.Describe(marker)})";
            }

            if (!HasLog($"{PingLog.Tag} placed id={Id(marker.Id)} kind={PingKind.Enemy}"))
            {
                return $"placing ping {Id(marker.Id)} did not log '{PingLog.Tag} placed'";
            }

            _markerId = marker.Id;
            _placedAt = marker.PlacedAt;
            _phase = Phase.Hide;
            return null;
        }

        // 付けた次のフレームまでに PingController が一度は追っているので、そこで最後に見た位置を覚えてから隠す。
        private string Hide()
        {
            if (!_controller.TryGetMarker(_markerId, out var marker))
            {
                return $"ping {Id(_markerId)} disappeared before the dummy was hidden";
            }

            if (!marker.IsTracking)
            {
                return $"ping {Id(_markerId)} lost the dummy while it was still in sight ({PingLog.Describe(marker)})";
            }

            _lastSeen = marker.Position.ToUnity();
            MoveDummy(PingRangeFixture.HiddenPoint);
            _phase = Phase.WaitLost;
            return null;
        }

        private string CheckLost()
        {
            if (!HasLog($"{PingLog.Tag} lost id={Id(_markerId)} "))
            {
                return null;
            }

            if (!_controller.TryGetMarker(_markerId, out var marker))
            {
                return $"ping {Id(_markerId)} was removed when the dummy hid instead of staying where it was last seen";
            }

            var position = marker.Position.ToUnity();
            if (marker.IsTracking || Vector3.Distance(position, _lastSeen) > _positionTolerance)
            {
                return $"ping {Id(_markerId)} did not stay where the dummy was last seen {Format(_lastSeen)} ({PingLog.Describe(marker)})";
            }

            _phase = Phase.TurnAway;
            return null;
        }

        private string CheckOffScreen(BootstrapWorld world)
        {
            if (world.Hud == null)
            {
                Debug.Log($"{PingLog.Tag} smoke: the TPS range has no HUD, so the screen edge is not checked");
                _phase = Phase.WaitExpired;
                return null;
            }

            var view = world.Hud.gameObject.GetComponent<PingMarkersView>();
            if (view == null || !view.TryGetPlacement(_markerId, out var placement) || placement.IsOnScreen)
            {
                return Time.realtimeSinceStartup - _waitStartedAt > _viewWaitSeconds
                    ? $"ping {Id(_markerId)} was not moved to the screen edge after turning away (view={(view != null ? "attached" : "missing")}, logs: {string.Join(" | ", _logs)})"
                    : null;
            }

            if (placement.Edge != PingScreenEdge.Left)
            {
                return $"turning right should put ping {Id(_markerId)} on the left edge ({PingLog.Screen(_markerId, placement)})";
            }

            if (!HasLog($"{PingLog.Tag} screen id={Id(_markerId)} onScreen=false edge={PingScreenEdge.Left}"))
            {
                return $"moving ping {Id(_markerId)} to the screen edge did not log '{PingLog.Tag} screen'";
            }

            _phase = Phase.WaitExpired;
            return null;
        }

        private string CheckExpired()
        {
            if (!HasLog($"{PingLog.Tag} expired id={Id(_markerId)} "))
            {
                return null;
            }

            if (_controller.TryGetMarker(_markerId, out _))
            {
                return $"ping {Id(_markerId)} logged that it expired but is still on the board";
            }

            var lived = Time.time - _placedAt;
            var lifetime = _controller.Spec.LifetimeSeconds;
            if (lived + _lifetimeTolerance < lifetime)
            {
                return $"ping {Id(_markerId)} expired after {lived.ToString("0.##", CultureInfo.InvariantCulture)}s, before its lifetime {lifetime.ToString("0.##", CultureInfo.InvariantCulture)}s";
            }

            _phase = Phase.Done;
            return null;
        }

        private bool HasLog(string prefix)
        {
            foreach (var log in _logs)
            {
                if (log.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveDummy(Vector3 groundPoint)
        {
            _dummy.transform.SetPositionAndRotation(groundPoint, Quaternion.identity);
            Physics.SyncTransforms();
        }

        // 肩越しのカメラの正面が point を通るよう、カメラの位置から見た向きに視点を回す。
        private static void AimAt(BootstrapWorld world, Vector3 point)
        {
            var camera = world.Camera != null ? world.Camera.transform : world.Player.transform;
            var toPoint = point - camera.position;
            if (toPoint.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var yaw = Mathf.Atan2(toPoint.x, toPoint.z) * Mathf.Rad2Deg;
            var pitch = -Mathf.Asin(Mathf.Clamp(toPoint.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), look.Pitch - pitch));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private static void TurnTo(BootstrapWorld world, float yaw)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), look.Pitch));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }
    }
}
