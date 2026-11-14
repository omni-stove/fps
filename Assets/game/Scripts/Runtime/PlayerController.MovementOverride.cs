using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、移動の横取り (<see cref="IMovementOverride"/>) の処理。
    /// <see cref="Tick"/> のたびに登録されたものから有効で優先度の最も高いものを選び、選ばれたものがあれば
    /// 姿勢と移動を <see cref="PlayerMotor"/>・回避・乗り越えの代わりにそれへ任せる。
    /// 選ばれた時点で回避と乗り越えをやめさせて伏せを解き、選ばれている間は始めさせない (<see cref="TryStartDodge"/> / <see cref="TryStartVault"/> / <see cref="UpdateProne"/>)。
    /// 選ばれたもの・外れたものは <c>[Player] override=.. active=..</c> でログに出す。
    /// </summary>
    public sealed partial class PlayerController
    {
        private readonly List<IMovementOverride> _movementOverrides = new List<IMovementOverride>();
        private IMovementOverride _activeMovementOverride;

        /// <summary>登録されている横取り。登録した順。</summary>
        public IReadOnlyList<IMovementOverride> MovementOverrides => _movementOverrides;

        /// <summary>今移動を任せている横取り。無ければ null。選び直すのは <see cref="Tick"/> のたび。</summary>
        public IMovementOverride ActiveMovementOverride => _activeMovementOverride;

        /// <summary>移動を横取りされているか。</summary>
        public bool IsMovementOverridden => _activeMovementOverride != null;

        /// <summary>今移動を任せている横取りが立てるビット (<see cref="IMovementOverride.Conditions"/>)。無ければ None。</summary>
        public PlayerActionConditions MovementOverrideConditions =>
            _activeMovementOverride != null ? _activeMovementOverride.Conditions : PlayerActionConditions.None;

        /// <summary>横取りが選ばれた・外れたときのログ。例: <c>[Player] override=LadderClimb active=true</c>。</summary>
        public static string FormatMovementOverrideLog(IMovementOverride movementOverride, bool active)
        {
            if (movementOverride == null)
            {
                throw new ArgumentNullException(nameof(movementOverride));
            }

            return $"{LogTag} override={movementOverride.GetType().Name} active={(active ? "true" : "false")}";
        }

        /// <summary>
        /// 横取りを登録する。選ばれるのは次の <see cref="Tick"/> から。既に登録されていれば何もせず false。
        /// </summary>
        public bool RegisterMovementOverride(IMovementOverride movementOverride)
        {
            if (movementOverride == null)
            {
                throw new ArgumentNullException(nameof(movementOverride));
            }

            if (_movementOverrides.Contains(movementOverride))
            {
                return false;
            }

            _movementOverrides.Add(movementOverride);
            return true;
        }

        /// <summary>
        /// 横取りの登録を外す。今選ばれていれば、その場で <see cref="IMovementOverride.End"/> を呼ぶ。登録されていなければ false。
        /// </summary>
        public bool UnregisterMovementOverride(IMovementOverride movementOverride)
        {
            if (movementOverride == null)
            {
                throw new ArgumentNullException(nameof(movementOverride));
            }

            if (!_movementOverrides.Remove(movementOverride))
            {
                return false;
            }

            if (ReferenceEquals(_activeMovementOverride, movementOverride))
            {
                EndMovementOverride();
            }

            return true;
        }

        // 選び直し、替わるなら前のものを終えてから次を始める。Begin の中で登録を外されることもあるので、最後に今のものを返す。
        private IMovementOverride SelectMovementOverride()
        {
            IMovementOverride best = null;
            for (var i = 0; i < _movementOverrides.Count; i++)
            {
                var candidate = _movementOverrides[i];
                if (candidate.IsActive && (best == null || candidate.Priority > best.Priority))
                {
                    best = candidate;
                }
            }

            if (ReferenceEquals(best, _activeMovementOverride))
            {
                return best;
            }

            EndMovementOverride();
            if (best != null)
            {
                BeginMovementOverride(best);
            }

            return _activeMovementOverride;
        }

        private void BeginMovementOverride(IMovementOverride movementOverride)
        {
            CancelDodge();
            CancelVault();
            ReleaseProne(ProneInput.ReasonOverride);
            _activeMovementOverride = movementOverride;
            Debug.Log(FormatMovementOverrideLog(movementOverride, true), this);
            movementOverride.Begin(this);
        }

        // End の中で登録し直されても二重に終えないよう、先に外してから呼ぶ。
        private void EndMovementOverride()
        {
            var ending = _activeMovementOverride;
            if (ending == null)
            {
                return;
            }

            _activeMovementOverride = null;
            Debug.Log(FormatMovementOverrideLog(ending, false), this);
            ending.End(this);
        }

        private Vector3 StepMovementOverride(
            IMovementOverride movementOverride, Vector2 move, bool sprint, bool crouch, bool jumpPressed, float deltaSeconds)
        {
            var input = new MovementOverrideInput(move, sprint, crouch, jumpPressed, _controller.isGrounded);
            var step = movementOverride.Step(this, input, deltaSeconds);

            // 回避は選ばれた時点でやめているので、ここで進むのは次の回避までの待ち時間だけ。
            _dodgeMotion?.Step(deltaSeconds);

            UpdateStance(step.Crouched ?? crouch, false);
            if (step.Position.HasValue)
            {
                PlaceAt(step.Position.Value);
            }

            if (step.YawDegrees.HasValue)
            {
                FaceYaw(step.YawDegrees.Value);
            }

            _motor.SetVelocity(step.Velocity.ToNumerics());
            return step.Displacement;
        }

        // CharacterController は有効な間 transform の位置を自分の位置で上書きするので、止めてから置く。
        private void PlaceAt(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
        }

        // FirstPersonLook が向きを覚えているので、初期化済みならその向きごと回す。そうでなければ本体を直接回す。
        private void FaceYaw(float yawDegrees)
        {
            if (_look.IsInitialized)
            {
                _look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(_look.Yaw, yawDegrees), 0f));
            }
            else
            {
                _look.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            }
        }
    }
}
