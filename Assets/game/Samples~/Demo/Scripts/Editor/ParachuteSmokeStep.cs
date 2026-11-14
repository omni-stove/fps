using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) で、パラシュートを確かめる手順。高い台 (<see cref="ParachuteTowerFixture"/>) と組で使う。
    /// <list type="number">
    /// <item>射撃場のルールでパラシュートが Off なら、空中でなくても開こうとして <c>[Parachute] open=RuleOff</c> で断られることを見てから、
    /// この手順の間だけプレイヤーの <see cref="ParachuteMovement.Rule"/> を On にする。落下ダメージ (<see cref="PlayerController.FallDamage"/>) も On にする。</item>
    /// <item>地面に立ったまま開こうとして、低すぎる (<c>[Parachute] open=TooLow</c>) と断られる。</item>
    /// <item>台の上に移して +Z へ歩かせ、端から落ちて台の上面より下がったところで開く (<c>[Parachute] opened</c>)。開いている間は
    /// <see cref="PlayerActionConditions.Parachuting"/> が立ち、下向きの速さは <see cref="MotorSpec.SafeFallSpeed"/> を超えない。</item>
    /// <item>着地すると閉じ (<c>[Parachute] closed reason=Landed</c>)、落下ダメージのログ (<c>[Player] fall damage=</c>) は出ず、体力も減っていない。
    /// 降りるのに、開かずに同じ高さを落ちる時間より長くかかっている。</item>
    /// </list>
    /// batchmode には入力が無いので、Jump の代わりに <see cref="ParachuteMovement.TryOpen"/>、歩く入力の代わりに <see cref="PlayerController.MoveFilter"/> への差し込みを使う。
    /// 変えたルール・落下ダメージ・差し込みは <see cref="Cleanup"/> で戻す。
    /// </summary>
    public sealed class ParachuteSmokeStep : ITpsActionSmokeStep
    {
        public const string StepName = "parachute";

        /// <summary>台の端から歩いて落ちたと見なす、足元が台の上面より下がった量 (m)。端の角で開こうとして台の上面を地面と測らないため。</summary>
        public const float DropBelowTopToOpen = 0.5f;

        /// <summary>
        /// 開いてから着地するまでの時間の、開かずに同じ高さを落ちる時間に対する比の下限。
        /// 既定の値 (降りる速さ 4 m/s・重力 20 m/s²) で台の高さからなら 2 倍を超えるので、余裕を見て 1.5 にする。
        /// </summary>
        public const float MinSlowdownRatio = 1.5f;

        /// <summary>台の上に移すとき、足元を上面からどれだけ浮かせるか (m)。上面にめり込まずに置くため。</summary>
        public const float PlaceAboveTop = 0.05f;

        private static readonly string _ruleOffLog = $"{ParachuteMovement.LogTag} open={ParachuteOpenVerdict.RuleOff} ";
        private static readonly string _tooLowLog = $"{ParachuteMovement.LogTag} open={ParachuteOpenVerdict.TooLow} ";
        private static readonly string _openedLog = $"{ParachuteMovement.LogTag} opened ";
        private static readonly string _landedLog = ParachuteMovement.FormatClosedLog(ParachuteCloseReason.Landed);
        private static readonly string _fallDamageLog = PlayerController.LogTag + " fall damage=";

        private Phase _phase;
        private ParachuteMovement _parachute;
        private PlayerController _mover;
        private ForwardFilter _filter;
        private Parachute _savedRule;
        private FallDamage _savedFallDamage;
        private bool _changedSettings;
        private bool _sawRuleOff;
        private bool _sawTooLow;
        private bool _sawOpened;
        private bool _sawLanded;
        private bool _sawFallDamage;
        private bool _closedByLanding;
        private ParachuteCloseReason? _closeReason;
        private int _healthAtOpen;
        private float _heightAtOpen;
        private double _openedAt;
        private double _landedAt;
        private float _fastestDescent;

        private enum Phase
        {
            Prepare,
            TryOnGround,
            PlaceOnTower,
            WalkOff,
            Descend,
            Done,
        }

        public int Order => 0;

        public string Name => StepName;

        public float TimeoutSeconds => 20f;

        public void Reset()
        {
            _phase = Phase.Prepare;
            _parachute = null;
            _mover = null;
            _filter = null;
            _changedSettings = false;
            _sawRuleOff = false;
            _sawTooLow = false;
            _sawOpened = false;
            _sawLanded = false;
            _sawFallDamage = false;
            _closedByLanding = false;
            _closeReason = null;
            _healthAtOpen = 0;
            _heightAtOpen = 0f;
            _openedAt = -1d;
            _landedAt = -1d;
            _fastestDescent = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition.StartsWith(_ruleOffLog, StringComparison.Ordinal))
            {
                _sawRuleOff = true;
            }
            else if (condition.StartsWith(_tooLowLog, StringComparison.Ordinal))
            {
                _sawTooLow = true;
            }
            else if (condition.StartsWith(_openedLog, StringComparison.Ordinal))
            {
                _sawOpened = true;
            }
            else if (string.Equals(condition, _landedLog, StringComparison.Ordinal))
            {
                _sawLanded = true;
            }
            else if (condition.StartsWith(_fallDamageLog, StringComparison.Ordinal))
            {
                _sawFallDamage = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Prepare:
                    problem = Prepare(world);
                    return false;

                case Phase.TryOnGround:
                    problem = TryOnGround();
                    return false;

                case Phase.PlaceOnTower:
                    problem = PlaceOnTower(world);
                    return false;

                case Phase.WalkOff:
                    problem = WalkOff(world);
                    return false;

                case Phase.Descend:
                    problem = Descend(world);
                    return _phase == Phase.Done && problem == null;

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_parachute != null)
            {
                _parachute.Closed -= OnClosed;
            }

            if (_mover != null && _filter != null && ReferenceEquals(_mover.MoveFilter, _filter))
            {
                _mover.MoveFilter = _filter.Inner;
            }

            if (_changedSettings)
            {
                if (_parachute != null)
                {
                    _parachute.Rule = _savedRule;
                }

                if (_mover != null)
                {
                    _mover.FallDamage = _savedFallDamage;
                }
            }

            _filter = null;
            _changedSettings = false;
        }

        private string Prepare(BootstrapWorld world)
        {
            _mover = world.Mover;
            _parachute = world.Player.GetComponent<ParachuteMovement>();
            if (_parachute == null || !_parachute.IsInitialized || !_parachute.enabled)
            {
                return $"the player has no initialized, enabled ParachuteMovement; is {nameof(ParachuteActionInstaller)} registered? (rules: {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            if (_parachute.Rule != world.Rules.Actions.Parachute)
            {
                return $"the player's parachute rule is {_parachute.Rule}, not the range's {world.Rules.Actions.Parachute}";
            }

            if (GameObject.Find(ParachuteTowerFixture.TowerName) == null)
            {
                return $"the scene has no '{ParachuteTowerFixture.TowerName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            // 移した直後は CharacterController の接地が前の位置のままなので、床に立ってから試す。
            if (!_mover.IsGrounded || _mover.IsMovementOverridden)
            {
                return null;
            }

            if (_parachute.Rule != Parachute.On)
            {
                var verdict = _parachute.TryOpen();
                if (verdict != ParachuteOpenVerdict.RuleOff || _parachute.IsOpen)
                {
                    return $"with the parachute rule {_parachute.Rule}, TryOpen gave {verdict} (open={_parachute.IsOpen})";
                }

                if (!_sawRuleOff)
                {
                    return $"refusing to open with the rule off logged no '{_ruleOffLog}...' line";
                }
            }

            _savedRule = _parachute.Rule;
            _savedFallDamage = _mover.FallDamage;
            _changedSettings = true;
            _parachute.Rule = Parachute.On;
            _mover.FallDamage = FallDamage.On;
            _parachute.Closed += OnClosed;
            _phase = Phase.TryOnGround;
            return null;
        }

        private string TryOnGround()
        {
            var height = _parachute.MeasureHeightAboveGround();
            if (height >= _parachute.Motion.MinOpenHeight)
            {
                return $"the player stands {Format(height)}m above the ground, not low enough to test refusing";
            }

            var verdict = _parachute.TryOpen();
            if (verdict != ParachuteOpenVerdict.TooLow || _parachute.IsOpen)
            {
                return $"opening {Format(height)}m above the ground gave {verdict} (open={_parachute.IsOpen}), not {ParachuteOpenVerdict.TooLow}";
            }

            if (!_sawTooLow)
            {
                return $"refusing to open near the ground logged no '{_tooLowLog}...' line";
            }

            _phase = Phase.PlaceOnTower;
            return null;
        }

        private string PlaceOnTower(BootstrapWorld world)
        {
            var tower = GameObject.Find(ParachuteTowerFixture.TowerName);
            var collider = tower != null ? tower.GetComponent<Collider>() : null;
            if (collider == null)
            {
                return $"the scene has no '{ParachuteTowerFixture.TowerName}' with a collider; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var bounds = collider.bounds;
            var controller = world.Controller;
            var feetToPivot = (controller.height / 2f) - controller.center.y;
            var position = new Vector3(bounds.center.x, bounds.max.y + feetToPivot + PlaceAboveTop, bounds.center.z);
            Teleport(controller, position);
            FaceForward(world);

            _filter = new ForwardFilter(_mover.MoveFilter, bounds.max.y);
            _mover.MoveFilter = _filter;
            _phase = Phase.WalkOff;
            return null;
        }

        private string WalkOff(BootstrapWorld world)
        {
            if (_filter == null || !ReferenceEquals(_mover.MoveFilter, _filter))
            {
                return "something replaced the move filter while walking off the tower";
            }

            var feet = FeetY(world.Controller);
            if (_mover.IsGrounded && feet < _filter.TopY - DropBelowTopToOpen)
            {
                return $"the player landed at {Format(feet)}m without opening the parachute";
            }

            if (_mover.IsGrounded || feet > _filter.TopY - DropBelowTopToOpen)
            {
                return null;
            }

            _healthAtOpen = world.PlayerDamageable.Health.Current;
            var verdict = _parachute.TryOpen();
            if (verdict != ParachuteOpenVerdict.Open || !_parachute.IsOpen)
            {
                return $"opening after walking off the tower ({Format(_parachute.MeasureHeightAboveGround())}m above the ground) gave {verdict}";
            }

            if (!_sawOpened)
            {
                return $"opening logged no '{_openedLog}...' line";
            }

            _heightAtOpen = feet;
            _openedAt = EditorApplication.timeSinceStartup;
            _phase = Phase.Descend;
            return null;
        }

        private string Descend(BootstrapWorld world)
        {
            if (_parachute.IsOpen)
            {
                if (_mover.IsMovementOverridden && !ReferenceEquals(_mover.ActiveMovementOverride, _parachute))
                {
                    return $"{_mover.ActiveMovementOverride.GetType().Name} took over the movement while the parachute was open";
                }

                if (ReferenceEquals(_mover.ActiveMovementOverride, _parachute)
                    && (_mover.MovementOverrideConditions & PlayerActionConditions.Parachuting) == 0)
                {
                    return $"the open parachute set {_mover.MovementOverrideConditions}, not {PlayerActionConditions.Parachuting}";
                }

                _fastestDescent = Mathf.Max(_fastestDescent, -_mover.Motor.Velocity.Y);
                return null;
            }

            if (!_closedByLanding)
            {
                return $"the parachute closed with reason {(_closeReason.HasValue ? _closeReason.Value.ToString() : "?")}, not {ParachuteCloseReason.Landed}";
            }

            if (!_sawLanded)
            {
                return $"the parachute closed on landing but no '{_landedLog}' line was logged";
            }

            if (_sawFallDamage)
            {
                return "the player took fall damage landing with the parachute open";
            }

            var health = world.PlayerDamageable.Health.Current;
            if (health < _healthAtOpen)
            {
                return $"the player's health dropped from {_healthAtOpen} to {health} while descending";
            }

            var safe = _mover.Motor.Spec.SafeFallSpeed;
            if (_fastestDescent > safe + 0.01f)
            {
                return $"the player fell at {Format(_fastestDescent)} m/s with the parachute open (safe {Format(safe)} m/s)";
            }

            var seconds = (float)(_landedAt - _openedAt);
            var freeFall = Mathf.Sqrt(2f * Mathf.Max(0f, _heightAtOpen) / _mover.Motor.Spec.Gravity);
            if (seconds < freeFall * MinSlowdownRatio)
            {
                return $"the descent took {Format(seconds)}s, not much slower than falling {Format(_heightAtOpen)}m ({Format(freeFall)}s)";
            }

            _mover.MoveFilter = _filter.Inner;
            _filter = null;
            _phase = Phase.Done;
            return null;
        }

        // 閉じたフレームのうちに Editor の更新が来るとは限らないので、閉じた瞬間に理由と時刻を受ける。
        private void OnClosed(ParachuteCloseReason reason)
        {
            _closeReason = reason;
            _closedByLanding = reason == ParachuteCloseReason.Landed;
            _landedAt = EditorApplication.timeSinceStartup;
        }

        private static float FeetY(CharacterController controller)
        {
            return controller.transform.position.y + controller.center.y - (controller.height / 2f);
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

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 台から歩いて降りるまで、入力の代わりに前進だけを押し続ける差し込み。開いた後も前へ流れて台の側面から離れる。
        /// 先に差し込まれていたもの (カバー) は後から呼び、その制限を上書きしない。
        /// </summary>
        private sealed class ForwardFilter : IPlayerMoveFilter
        {
            public ForwardFilter(IPlayerMoveFilter inner, float topY)
            {
                Inner = inner;
                TopY = topY;
            }

            public IPlayerMoveFilter Inner { get; }

            /// <summary>台の上面の高さ (ワールドの y)。</summary>
            public float TopY { get; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Vector2.up;
                sprint = false;
                crouch = false;
                jumpPressed = false;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
