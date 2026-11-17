using System;

namespace Shooter.Core
{
    /// <summary>
    /// トリガーの入力 (押されているか・今フレームに押されたか) から、撃とうとするかを <see cref="FireMode"/> で決める。
    /// <see cref="FireMode.Auto"/> は押している間、<see cref="FireMode.Semi"/> は押した瞬間だけ撃つ。
    /// 弾数は見ない (弾倉・リロードは <see cref="Weapon"/> やサーバーの <see cref="ServerWeapon"/> が判断する)。
    /// </summary>
    /// <remarks>
    /// <see cref="Weapon"/> を手元に持つ側 (オフライン) は連射間隔を Weapon が見るので <see cref="Wants"/> だけを使う。
    /// 弾数をサーバーが持つ側 (オンラインの持ち主) は、インスタンスを作って <see cref="Pull"/> で連射間隔も見る
    /// (サーバーの <see cref="ShotBudget"/> に弾かれる速さで射撃を送らないため)。時間は <see cref="Tick"/> で外から進める。
    /// </remarks>
    public sealed class FireTrigger
    {
        private readonly ITriggerModel _model;
        private float _unspentSeconds;
        private bool _held;

        /// <summary>射撃モード。</summary>
        public FireMode Mode { get; }

        /// <summary><see cref="Pull"/> で撃ってから次に撃てるまでの秒数。</summary>
        public float FireInterval { get; }

        public ITriggerModel Model => _model;

        /// <param name="mode">射撃モード。Auto か Semi。</param>
        /// <param name="fireInterval">連射間隔 (秒)。0 以上の有限値。</param>
        public FireTrigger(FireMode mode, float fireInterval)
        {
            ValidateMode(mode);
            _model = TriggerModels.For(mode, fireInterval);
            Mode = mode;
            FireInterval = fireInterval;
        }

        /// <summary>spec の射撃モードと連射間隔で作る。</summary>
        public FireTrigger(WeaponSpec spec)
        {
            _model = TriggerModels.For(spec ?? throw new ArgumentNullException(nameof(spec)));
            Mode = spec.FireMode;
            FireInterval = spec.FireInterval;
        }

        /// <summary>
        /// mode の武器で、この入力なら撃とうとするか。Auto は isPressed、Semi は wasPressedThisFrame
        /// (同じフレームに押して離した場合も 1 発撃つ)。連射間隔は見ない。
        /// </summary>
        public static bool Wants(FireMode mode, bool isPressed, bool wasPressedThisFrame)
        {
            ValidateMode(mode);
            return mode == FireMode.Auto ? isPressed : wasPressedThisFrame;
        }

        /// <summary>
        /// この入力で撃とうとし (<see cref="Wants"/>)、前回 true を返してから連射間隔が経っていれば true を返して間隔の待ちを始める。
        /// それ以外は何もせず false。Semi で間隔の待ち中に押した分は、押し続けても後から撃たない。
        /// </summary>
        public bool Pull(bool isPressed, bool wasPressedThisFrame)
        {
            var shots = 0;
            if (_held && (!isPressed || wasPressedThisFrame))
            {
                shots += _model.Release();
                _held = false;
            }

            // 離したかを先に伝えてから時間を進めるのは、離した後のフレームで押しっぱなしの撃ち方が撃たないようにするため。
            shots += _model.Tick(_unspentSeconds);
            _unspentSeconds = 0f;

            // Auto はフレーム内に押して離した入力を押していないと見なす (Wants と同じ)。
            var starts = Mode == FireMode.Auto ? isPressed && !_held : wasPressedThisFrame;
            if (starts)
            {
                shots += _model.Press();
                _held = true;
            }

            if (_held && !isPressed)
            {
                shots += _model.Release();
                _held = false;
            }

            return shots > 0;
        }

        /// <summary>連射間隔の待ちを deltaSeconds 進める。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            // 次の Pull まで溜めるのは、押しっぱなしの撃ち方が Tick の時点で撃つと、その間に離した入力を見られないため。
            _unspentSeconds += deltaSeconds;
        }

        private static void ValidateMode(FireMode mode)
        {
            if (mode != FireMode.Auto && mode != FireMode.Semi)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode must be Auto or Semi");
            }
        }
    }
}
