using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) で、分隊への指示 (<see cref="SquadCommandController"/>) を確かめる手順。
    /// <list type="number">
    /// <item>陣営の無いプレイヤーが指示しても出ない (<c>[Squad] refused reason=NoSide</c>) ことを確かめ、プレイヤーに味方の bot
    /// (<see cref="SquadCommandRangeFixture.BotName"/>) と同じ陣営を持たせる。</item>
    /// <item><see cref="SquadCommandRangeFixture.StandPoint"/> から地面 (<see cref="SquadCommandRangeFixture.HoldPoint"/>) を指して指示すると、
    /// bot が受けて (<c>[Squad] accepted ... kind=Hold</c>) 応答の字幕を出し、そこへ移って着く (<c>[Squad] arrived</c>)。HUD に守る指示の印が出る。</item>
    /// <item>標的ダミーを <see cref="SquadCommandRangeFixture.TargetPoint"/> に置いて指して指示すると、bot がそのダミーを狙う (<c>[Squad] engage</c>)。</item>
    /// <item>長押し (<see cref="SquadCommandController.Recall"/>) で付いてこさせると (<c>[Squad] order kind=Follow</c>)、
    /// bot がプレイヤーの近く (<see cref="SquadOrderSpec.FollowDistance"/>) まで戻る。</item>
    /// </list>
    /// 入力の代わりに <see cref="FirstPersonLook.ApplyLookDelta"/> で照準を合わせ、<see cref="SquadCommandController.TryOrder"/> を呼ぶ。
    /// </summary>
    public sealed class SquadCommandSmokeStep : ITpsActionSmokeStep
    {
        /// <summary><see cref="Order"/>。</summary>
        public const int StepOrder = 510;

        // NavMesh を焼くのを待ち、守る場所まで歩き、ダミーを狙い、戻ってくるまで。
        private const float _timeoutSeconds = 45f;

        // 照準を合わせ直すフレーム数。肩越しのカメラは向きを変えると位置も動くので、何度か合わせ直して揃える。
        private const int _aimFrames = 3;

        // HUD に表示が付くまで待つ秒数。SquadCommandController は HUD を ViewSearchIntervalSeconds ごとに探す。
        private const float _viewWaitSeconds = SquadCommandController.ViewSearchIntervalSeconds * 3f;

        // 付いてきたとみなす距離の余裕 (m)。止まった後に押し合って少し離れる分。
        private const float _followTolerance = 1f;

        private readonly List<string> _logs = new List<string>();

        private Phase _phase;
        private BootstrapWorld _world;
        private SquadCommandController _controller;
        private EnemyAgent _bot;
        private SideMember _addedSide;
        private TargetDummy _dummy;
        private Vector3 _dummyPosition;
        private Quaternion _dummyRotation;
        private int _frames;
        private float _waitStartedAt;

        private enum Phase
        {
            Start,
            WaitBot,
            AimHold,
            WaitArrived,
            CheckMarker,
            StageDummy,
            AimAttack,
            WaitEngage,
            Recall,
            WaitFollow,
            Done,
        }

        public int Order => StepOrder;

        public string Name => "squad-command";

        public float TimeoutSeconds => _timeoutSeconds;

        public void Reset()
        {
            _logs.Clear();
            _phase = Phase.Start;
            _world = null;
            _controller = null;
            _bot = null;
            _addedSide = null;
            _dummy = null;
            _frames = 0;
            _waitStartedAt = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition != null && condition.StartsWith(SquadOrderLog.Tag + " ", System.StringComparison.Ordinal))
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
                case Phase.WaitBot:
                    problem = WaitBot(world);
                    break;
                case Phase.AimHold:
                    problem = AimAndHold(world);
                    break;
                case Phase.WaitArrived:
                    problem = CheckArrived();
                    break;
                case Phase.CheckMarker:
                    problem = CheckMarker(world);
                    break;
                case Phase.StageDummy:
                    problem = StageDummy(world);
                    break;
                case Phase.AimAttack:
                    problem = AimAndAttack(world);
                    break;
                case Phase.WaitEngage:
                    problem = CheckEngage();
                    break;
                case Phase.Recall:
                    problem = Recall(world);
                    break;
                case Phase.WaitFollow:
                    problem = CheckFollow(world);
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

            if (_bot != null)
            {
                _bot.ClearOrder("SmokeCleanup");
            }

            if (_addedSide != null)
            {
                Object.Destroy(_addedSide);
            }

            if (_controller != null)
            {
                _controller.Configure();
            }

            _dummy = null;
            _bot = null;
            _addedSide = null;
            _controller = null;
            _world = null;
        }

        private string Begin(BootstrapWorld world)
        {
            _world = world;
            _controller = world.Player.GetComponent<SquadCommandController>();
            if (_controller == null)
            {
                return $"the player has no SquadCommandController; {nameof(SquadCommandActionInstaller)} is not registered with {nameof(PlayerActions)}";
            }

            if (GameObject.Find(SquadCommandRangeFixture.RootName) == null)
            {
                return $"the scene has no '{SquadCommandRangeFixture.RootName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (SideMember.Of(world.Player) == null)
            {
                if (_controller.TryOrder(out _))
                {
                    return "a squad order was given by a player without a side";
                }

                if (_controller.LastRefusal != SquadOrderRefusal.NoSide)
                {
                    return $"an order from a player without a side was refused for {_controller.LastRefusal} instead of {SquadOrderRefusal.NoSide}";
                }

                _addedSide = world.Player.AddComponent<SideMember>();
                _addedSide.Side = SquadCommandRangeFixture.BotSide;
            }
            else if (SideMember.Of(world.Player).Side != SquadCommandRangeFixture.BotSide)
            {
                return $"the player is on {SideMember.Of(world.Player).Side}, not on the squad bot's {SquadCommandRangeFixture.BotSide}";
            }

            _phase = Phase.WaitBot;
            return null;
        }

        // NavMeshBakeActivator が焼き終えるまで bot は無効なので、GameObject.Find では見つからない。
        private string WaitBot(BootstrapWorld world)
        {
            if (_bot == null)
            {
                var root = GameObject.Find(SquadCommandRangeFixture.RootName);
                var child = root != null ? root.transform.Find(SquadCommandRangeFixture.BotName) : null;
                _bot = child != null ? child.GetComponent<EnemyAgent>() : null;
                if (_bot == null)
                {
                    return $"'{SquadCommandRangeFixture.RootName}' has no '{SquadCommandRangeFixture.BotName}'; the range has no weapon for it";
                }
            }

            if (!_bot.isActiveAndEnabled || !_bot.IsInitialized || !_bot.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh)
            {
                return null;
            }

            Teleport(world.Controller, SquadCommandRangeFixture.StandPoint + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            AimAt(world, SquadCommandRangeFixture.HoldPoint);
            _frames = 0;
            _phase = Phase.AimHold;
            return null;
        }

        private string AimAndHold(BootstrapWorld world)
        {
            AimAt(world, SquadCommandRangeFixture.HoldPoint);
            if (++_frames < _aimFrames)
            {
                return null;
            }

            if (!_controller.TryOrder(out var recipients))
            {
                return $"pointing at the ground {Format(SquadCommandRangeFixture.HoldPoint)} gave no order ({_controller.LastRefusal})";
            }

            if (_controller.CurrentOrder != BotErrandKind.Hold || _bot.Order.Kind != BotErrandKind.Hold)
            {
                return $"pointing at the ground gave the order {_controller.CurrentOrder} and the bot took {_bot.Order.Kind} instead of {BotErrandKind.Hold}";
            }

            if (!HasLog($"{SquadOrderLog.Tag} order kind={BotErrandKind.Hold} ") || !HasLog($"{SquadOrderLog.Tag} accepted id={_bot.Id} kind={BotErrandKind.Hold} "))
            {
                return $"the hold order to {recipients} bot(s) did not log '{SquadOrderLog.Tag} order' and '{SquadOrderLog.Tag} accepted' (logs: {string.Join(" | ", _logs)})";
            }

            var reply = CheckReply(world, BotErrandKind.Hold);
            if (reply != null)
            {
                return reply;
            }

            _phase = Phase.WaitArrived;
            return null;
        }

        private string CheckArrived()
        {
            if (!HasLog($"{SquadOrderLog.Tag} arrived id={_bot.Id} "))
            {
                return null;
            }

            if (!_bot.HasArrivedForOrder)
            {
                return $"the bot logged that it arrived but does not hold its place (order {_bot.Order.Kind})";
            }

            _waitStartedAt = Time.realtimeSinceStartup;
            _phase = Phase.CheckMarker;
            return null;
        }

        private string CheckMarker(BootstrapWorld world)
        {
            if (!GameUi.IsAlive(world.Hud))
            {
                Debug.Log($"{SquadOrderLog.Tag} smoke: the TPS range has no HUD, so the order marker is not checked");
                _phase = Phase.StageDummy;
                return null;
            }

            var view = world.Hud.gameObject.GetComponent<SquadOrderMarkerView>();
            if (view == null || view.ShownKind != BotErrandKind.Hold)
            {
                return Time.realtimeSinceStartup - _waitStartedAt > _viewWaitSeconds
                    ? $"the HUD shows no hold marker (view={(view != null ? "attached, shows " + view.ShownKind : "missing")}, logs: {string.Join(" | ", _logs)})"
                    : null;
            }

            if (!HasLog($"{SquadOrderLog.Tag} screen kind={BotErrandKind.Hold} "))
            {
                return $"showing the hold marker did not log '{SquadOrderLog.Tag} screen'";
            }

            _phase = Phase.StageDummy;
            return null;
        }

        private string StageDummy(BootstrapWorld world)
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
            _dummy.transform.SetPositionAndRotation(SquadCommandRangeFixture.TargetPoint, Quaternion.identity);
            Physics.SyncTransforms();
            AimAt(world, _dummy.Damageable.AimPoint);
            _frames = 0;
            _phase = Phase.AimAttack;
            return null;
        }

        private string AimAndAttack(BootstrapWorld world)
        {
            AimAt(world, _dummy.Damageable.AimPoint);
            if (++_frames < _aimFrames)
            {
                return null;
            }

            if (!_controller.TryOrder(out _))
            {
                return $"pointing at the target dummy '{_dummy.Damageable.Id}' gave no order ({_controller.LastRefusal})";
            }

            if (_controller.CurrentOrder != BotErrandKind.Attack || _controller.CurrentTarget != _dummy.Damageable)
            {
                var target = _controller.CurrentTarget;
                return $"pointing at '{_dummy.Damageable.Id}' gave the order {_controller.CurrentOrder} on '{(target != null ? target.Id : "none")}'";
            }

            if (_bot.OrderTarget != _dummy.Damageable)
            {
                return $"the bot did not take the attack order on '{_dummy.Damageable.Id}' (order {_bot.Order.Kind})";
            }

            _phase = Phase.WaitEngage;
            return null;
        }

        private string CheckEngage()
        {
            if (!HasLog($"{SquadOrderLog.Tag} engage id={_bot.Id} target={_dummy.Damageable.Id}"))
            {
                return null;
            }

            _phase = Phase.Recall;
            return null;
        }

        private string Recall(BootstrapWorld world)
        {
            if (!_controller.Recall(out _))
            {
                return $"holding the button did not call the bot back ({_controller.LastRefusal})";
            }

            if (_bot.Order.Kind != BotErrandKind.Follow || _controller.CurrentOrder != BotErrandKind.None)
            {
                return $"after the recall the bot follows {_bot.Order.Kind} and the HUD still shows {_controller.CurrentOrder}";
            }

            if (!HasLog($"{SquadOrderLog.Tag} order kind={BotErrandKind.Follow} "))
            {
                return $"the recall did not log '{SquadOrderLog.Tag} order kind={BotErrandKind.Follow}'";
            }

            var reply = CheckReply(world, BotErrandKind.Follow);
            if (reply != null)
            {
                return reply;
            }

            _phase = Phase.WaitFollow;
            return null;
        }

        private string CheckFollow(BootstrapWorld world)
        {
            var player = SideMember.Of(world.Player);
            var distance = Flat(_bot.GroundPosition - player.GroundPosition).magnitude;
            if (distance > _controller.Spec.FollowDistance + _followTolerance)
            {
                return null;
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} smoke: the bot came back to {1:0.0}m from the player",
                SquadOrderLog.Tag,
                distance));
            _phase = Phase.Done;
            return null;
        }

        // 字幕は設定で隠していても出す決まりなので、HUD があれば必ず行がある。
        private string CheckReply(BootstrapWorld world, BotErrandKind kind)
        {
            if (!GameUi.IsAlive(world.Hud))
            {
                return null;
            }

            var subtitles = world.Hud.gameObject.GetComponent<SubtitleView>();
            if (subtitles == null)
            {
                return null;
            }

            var expected = SquadOrderText.Reply(_bot.Id, kind);
            foreach (var line in subtitles.Lines)
            {
                if (line.IsSpeech && line.Speaker == _bot.Id && line.Text == expected)
                {
                    return null;
                }
            }

            return $"the bot '{_bot.Id}' did not reply '{expected}' in the subtitles";
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

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }
    }
}
