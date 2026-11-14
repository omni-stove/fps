using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、崖のぼりの処理。
    /// <see cref="MantleRule"/> が <see cref="Mantle.On"/> で接地して立っているときに Jump を押し、乗り越えが始まらなければ (高すぎる・<see cref="VaultRule"/> が Off など)
    /// 前方の段を物理で調べ (<see cref="ProbeMantle"/>)、<see cref="VaultProbe.EvaluateMantle"/> がよじ登れると決めたらジャンプの代わりにその軌跡をたどって縁の上に立つ。
    /// 乗り越えと同じ入力で、高さが <see cref="MotorSpec.VaultMaxHeight"/> 以下なら乗り越え、それより高く <see cref="MotorSpec.MantleMaxHeight"/> 以下ならよじ登る。
    /// よじ登れなければ (上が狭い・頭上が塞がっているなど) ふつうにジャンプする。たどっている間は <see cref="TryOverrideMove"/> で移動を差し替え、
    /// しゃがみ・ダッシュ・ジャンプを止める。撃たせないのは <see cref="GameBootstrap"/> が武器に配線する。
    /// </summary>
    public sealed partial class PlayerController
    {
        private Mantle _mantleRule = Mantle.Off;
        private VaultPath _mantlePath;
        private float _mantleElapsed;

        /// <summary>よじ登り始めたときに、よじ登る向き (ワールドの水平の単位ベクトル) を渡して呼ばれる。</summary>
        public event Action<Vector3> MantleStarted;

        /// <summary>よじ登り終えたとき (途中でやめたときを含む) に呼ばれる。</summary>
        public event Action MantleEnded;

        /// <summary>よじ登っている最中か。</summary>
        public bool IsMantling => _mantlePath != null;

        /// <summary>今たどっている軌跡。よじ登っていなければ null。</summary>
        public VaultPath MantlePath => _mantlePath;

        /// <summary>最後に <see cref="ProbeMantle"/> で調べた結果。</summary>
        public VaultProbeResult LastMantleProbe { get; private set; }

        /// <summary>
        /// 崖のぼりを使うか。既定は <see cref="Mantle.Off"/> (今までどおり乗り越えだけ)。<see cref="PlayerActions.Attach"/> がルールの <see cref="ActionRules.Mantle"/> を入れる。
        /// Off にするとよじ登っている最中でもやめる。
        /// </summary>
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

        /// <summary>崖のぼりのログ。例: <c>[Player] mantling=true</c>。</summary>
        public static string FormatMantlingLog(bool mantling)
        {
            return $"{LogTag} mantling={(mantling ? "true" : "false")}";
        }

        /// <summary>段を調べた結果のログ。例: <c>[Player] mantle=TooNarrow height=1.80 depth=0.40</c>。</summary>
        public static string FormatMantleProbeLog(VaultProbeResult result)
        {
            var depth = float.IsPositiveInfinity(result.Depth) ? "inf" : result.Depth.ToString("F2", CultureInfo.InvariantCulture);
            return $"{LogTag} mantle={result.Verdict} height={result.Height.ToString("F2", CultureInfo.InvariantCulture)} depth={depth}";
        }

        /// <summary>
        /// 向いている方向 (この GameObject の水平の前) の段を物理で調べ、<see cref="VaultProbe.EvaluateMantle"/> でよじ登れるかを決める。
        /// 結果は <see cref="LastMantleProbe"/> にも残す。ルールや接地は問わない。
        /// </summary>
        public VaultProbeResult ProbeMantle()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PlayerController is not initialized");
            }

            LastMantleProbe = MeasureMantle();
            return LastMantleProbe;
        }

        /// <summary>
        /// 前方の段によじ登り始める。<see cref="MantleRule"/> が Off・未初期化・接地していない・しゃがんでいるか伏せている・乗り越え中かよじ登り中か回避中・
        /// 移動の横取り (<see cref="IsMovementOverridden"/>) の最中なら調べずに false。調べて段があれば結果を <see cref="FormatMantleProbeLog"/> でログに出し、よじ登れなければ false。
        /// 始めたら <c>[Player] mantling=true</c> をログに出して <see cref="MantleStarted"/> を呼ぶ。動くのは次の <see cref="Tick"/> から。
        /// </summary>
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

        /// <summary>よじ登っている最中ならやめる (その場から重力で落ちる)。やめたら true。</summary>
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

        /// <summary>
        /// よじ登りの最中か、このフレームの Jump でよじ登り始めたら、この <see cref="Tick"/> で動かす変位を displacement に入れ、overridden を true にする。
        /// 乗り越えの後・回避の前に呼ばれる (<see cref="TryOverrideMove"/>)。よじ登れなければ何もせず、Jump は <see cref="PlayerMotor"/> のジャンプになる。
        /// </summary>
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

        // 前方の壁の当て方と上端の測り方は乗り越え (MeasureVault) と同じ。奥行きだけは、上に立てる広さを確かめるので測る先を変える。
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
            var origin = new Vector3(body.position.x, feetY + VaultProbeHeight, body.position.z);
            if (!TryVaultCast(origin, forward, VaultProbe.MaxReach, out var hit))
            {
                return VaultProbeResult.None;
            }

            var spec = _motor.Spec;
            var radius = VaultBodyRadius();
            var into = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var topHeight = MeasureVaultTop(hit, into, feetY, spec.MantleMaxHeight);
            var depth = into == Vector3.zero ? float.PositiveInfinity : MeasureMantleDepth(hit, into, VaultProbe.MantleFootingDepth(radius));
            var sample = new VaultObstacleSample(hit.distance * Vector3.Dot(forward, into), hit.normal.ToNumerics(), topHeight, depth);
            return VaultProbe.EvaluateMantle(
                sample, forward.ToNumerics(), spec.VaultMaxHeight, spec.MantleMaxHeight, radius, spec.MantleSpeed, IsVaultPathClear);
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
