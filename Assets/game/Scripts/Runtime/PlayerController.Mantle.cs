using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        private Mantle _mantleRule = Mantle.Off;
        private VaultPath _mantlePath;
        private float _mantleElapsed;

        public event Action<Vector3> MantleStarted;

        public event Action MantleEnded;

        public bool IsMantling => _mantlePath != null;

        public VaultPath MantlePath => _mantlePath;

        public VaultProbeResult LastMantleProbe { get; private set; }

        public Mantle MantleRule
        {
            get => _mantleRule;
            set
            {
                if (!Enum.IsDefined(typeof(Mantle), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Mantle");
                }

                _mantleRule = value;
                if (value != Mantle.On)
                {
                    CancelMantle();
                }
            }
        }

        public static string FormatMantlingLog(bool mantling)
        {
            return $"{LogTag} mantling={(mantling ? "true" : "false")}";
        }

        public static string FormatMantleProbeLog(VaultProbeResult result)
        {
            var depth = float.IsPositiveInfinity(result.Depth) ? "inf" : result.Depth.ToString("F2", CultureInfo.InvariantCulture);
            return $"{LogTag} mantle={result.Verdict} height={result.Height.ToString("F2", CultureInfo.InvariantCulture)} depth={depth}";
        }

        public VaultProbeResult ProbeMantle()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            LastMantleProbe = MeasureMantle();
            return LastMantleProbe;
        }

        public bool TryStartMantle()
        {
            return StartMantleIfAble(requireGrounded: true);
        }

        // グラップルなどで壁に掴まって止まっている (接地していない) 所から上るときに使う。接地を問わないほかは TryStartMantle と同じ。
        public bool TryStartMantleWhileHanging()
        {
            return StartMantleIfAble(requireGrounded: false);
        }

        private bool StartMantleIfAble(bool requireGrounded)
        {
            if (_mantleRule != Mantle.On || !IsInitialized || (requireGrounded && !IsGrounded) || _currentStance != PlayerStance.Standing || IsVaulting || IsMantling || IsDodging || IsMovementOverridden)
            {
                return false;
            }

            var result = ProbeMantle();
            if (result.Verdict != VaultVerdict.NoObstacle)
            {
                Debug.Log(FormatMantleProbeLog(result), this);
            }

            if (!result.CanMantle)
            {
                return false;
            }

            _mantlePath = result.Path;
            _mantleElapsed = 0f;
            Debug.Log(FormatMantlingLog(true), this);
            MantleStarted?.Invoke(result.Direction.ToUnity());
            return true;
        }

        public bool CancelMantle()
        {
            if (!IsMantling)
            {
                return false;
            }

            FinishMantle();
            return true;
        }

        private void FilterInputForMantle(ref bool sprint, ref bool crouch, ref bool jumpPressed)
        {
            if (!IsMantling)
            {
                return;
            }

            sprint = false;
            crouch = false;
            jumpPressed = false;
        }

        private void TryOverrideMoveForMantle(bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement)
        {
            if (!IsMantling && (!jumpPressed || !TryStartMantle()))
            {
                return;
            }

            var before = _mantleElapsed;
            var after = Mathf.Min(_mantlePath.Duration, before + deltaSeconds);
            _mantleElapsed = after;
            displacement = _mantlePath.Displacement(before, after).ToUnity();
            overridden = true;

            // 射撃のばらつきなど速度を読む側のために、たどっている速度を入れておく。
            if (deltaSeconds > 0f)
            {
                _motor.SetVelocity((displacement / deltaSeconds).ToNumerics());
            }

            if (after >= _mantlePath.Duration)
            {
                FinishMantle();
            }
        }

        // 上がる勢いを持ち越すと、縁の上で跳ね上がるので速度を 0 から始め直す。
        private void FinishMantle()
        {
            _mantlePath = null;
            _mantleElapsed = 0f;
            _motor?.ResetVelocity();
            Debug.Log(FormatMantlingLog(false), this);
            MantleEnded?.Invoke();
        }

        // 前方の壁の当て方と上端の測り方は乗り越え (MeasureVault) と同じ。Ray は崖のぼりの高さの上限の手前まで並べる。奥行きだけは、上に立てる広さを確かめるので測る先を変える。
        private VaultProbeResult MeasureMantle()
        {
            var body = transform;
            var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
            {
                return VaultProbeResult.None;
            }

            forward.Normalize();
            var feetY = VaultFeetY();
            var spec = _motor.Spec;
            if (!TryFindVaultFace(feetY, forward, spec.MantleMaxHeight, spec.VaultReach, out var hit))
            {
                return VaultProbeResult.None;
            }

            var radius = VaultBodyRadius();
            var into = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var topHeight = MeasureVaultTop(hit, into, feetY, spec.MantleMaxHeight);
            var depth = into == Vector3.zero ? float.PositiveInfinity : MeasureMantleDepth(hit, into, VaultProbe.MantleFootingDepth(radius));
            var width = MeasureVaultFaceWidth(hit, into, radius);
            var sample = new VaultObstacleSample(hit.distance * Vector3.Dot(forward, into), hit.normal.ToNumerics(), topHeight, depth, width);
            return VaultProbe.EvaluateMantle(sample, forward.ToNumerics(), spec, radius, IsVaultPathClear);
        }

        // 手前の面から立つのに要る奥行きより先から、当たったコライダーへ戻る Ray で向こうの面を探す。始点が中 (もっと奥まである) なら当たらないので無限大とする。
        private static float MeasureMantleDepth(RaycastHit hit, Vector3 into, float footingDepth)
        {
            var reach = footingDepth + VaultDepthSlack;
            var ray = new Ray(hit.point + (into * reach), -into);
            return hit.collider.Raycast(ray, out var back, reach) ? Mathf.Max(0f, reach - back.distance) : float.PositiveInfinity;
        }
    }
}
