using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// プレイヤーの今のアクションを 1 つにまとめた不変の値。デバッグ表示と自機モックが読む。
    /// 移動 (ダッシュ・しゃがみ・回避・乗り越え・接地)、構えと射撃 (カバー・ブラインドファイア・ADS・射撃・リロード)、
    /// ダウン・肩・視点・武器名・ばらつきを持つ。生成時に値を検査する。
    /// <c>default</c> は「何もしていない・一人称・右肩・武器なし」。
    /// </summary>
    public readonly struct PlayerActionState : IEquatable<PlayerActionState>
    {
        /// <summary>定義済みのフラグをすべて立てた値。</summary>
        public const PlayerActionConditions AllFlags =
            PlayerActionConditions.Sprinting
            | PlayerActionConditions.Crouching
            | PlayerActionConditions.Dodging
            | PlayerActionConditions.Vaulting
            | PlayerActionConditions.Grounded
            | PlayerActionConditions.InCover
            | PlayerActionConditions.BlindFiring
            | PlayerActionConditions.Aiming
            | PlayerActionConditions.Firing
            | PlayerActionConditions.Reloading
            | PlayerActionConditions.Downed;

        /// <summary>
        /// 1 発撃ってから射撃中と見なし続ける最短の秒数。連射間隔がこれより短い (またはセミオートを 1 回だけ撃った) ときも、
        /// 表示や見た目が 1 フレームで消えないようにする。
        /// </summary>
        public const float MinFiringSeconds = 0.1f;

        /// <summary><see cref="SpreadDegrees"/> の上限 (度)。<see cref="WeaponSpec"/> の散らばりの角度の上限と揃える。</summary>
        public const float MaxSpreadDegrees = 90f;

        private readonly string _weaponName;

        /// <summary>立っている状態の組み合わせ。</summary>
        public PlayerActionConditions Flags { get; }

        /// <summary>リロードの進み。0〜1。リロード中でなければ 0。</summary>
        public float ReloadProgress { get; }

        /// <summary>
        /// 今の (または直前に終えた) リロードでのアクティブリロードの結果。まだ判定していなければ null。
        /// 成功するとその場でリロードが終わるので、<see cref="IsReloading"/> が false でも Success が残る (次のリロードを始めるまで)。
        /// </summary>
        public ActiveReloadResult? ActiveReload { get; }

        /// <summary>三人称のカメラを置く肩 (補間の目標)。</summary>
        public ShoulderSide Shoulder { get; }

        /// <summary>一人称か三人称か。</summary>
        public ViewMode ViewMode { get; }

        /// <summary>今持っている武器のデバッグ表示用の名前 (<see cref="WeaponSpec.LogName"/>)。持っていなければ空文字。</summary>
        public string WeaponName => _weaponName ?? string.Empty;

        /// <summary>今撃ったときに弾が狙いから散らばる最大の角度 (度)。0 以上 <see cref="MaxSpreadDegrees"/> 以下。</summary>
        public float SpreadDegrees { get; }

        public bool IsSprinting => Has(PlayerActionConditions.Sprinting);
        public bool IsCrouching => Has(PlayerActionConditions.Crouching);
        public bool IsDodging => Has(PlayerActionConditions.Dodging);
        public bool IsVaulting => Has(PlayerActionConditions.Vaulting);
        public bool IsGrounded => Has(PlayerActionConditions.Grounded);
        public bool IsInCover => Has(PlayerActionConditions.InCover);
        public bool IsBlindFiring => Has(PlayerActionConditions.BlindFiring);
        public bool IsAiming => Has(PlayerActionConditions.Aiming);
        public bool IsFiring => Has(PlayerActionConditions.Firing);
        public bool IsReloading => Has(PlayerActionConditions.Reloading);
        public bool IsDowned => Has(PlayerActionConditions.Downed);

        /// <param name="flags">立っている状態。<see cref="AllFlags"/> の外のビットは渡せない。</param>
        /// <param name="reloadProgress">リロードの進み。0 以上 1 以下。<see cref="PlayerActionConditions.Reloading"/> が無ければ 0。</param>
        /// <param name="activeReload">アクティブリロードの結果。未判定なら null。</param>
        /// <param name="shoulder">三人称のカメラの肩。定義済みの値。</param>
        /// <param name="viewMode">視点。定義済みの値。</param>
        /// <param name="weaponName">武器のデバッグ表示用の名前。null なら空文字。</param>
        /// <param name="spreadDegrees">ばらつき (度)。0 以上 <see cref="MaxSpreadDegrees"/> 以下。</param>
        public PlayerActionState(
            PlayerActionConditions flags,
            float reloadProgress,
            ActiveReloadResult? activeReload,
            ShoulderSide shoulder,
            ViewMode viewMode,
            string weaponName,
            float spreadDegrees)
        {
            if ((flags & ~AllFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flags), flags, "flags contains undefined bits");
            }

            if (!(reloadProgress >= 0f && reloadProgress <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadProgress), reloadProgress, "reloadProgress must be between 0 and 1");
            }

            if ((flags & PlayerActionConditions.Reloading) == 0 && reloadProgress != 0f)
            {
                throw new ArgumentException("reloadProgress must be 0 while not reloading", nameof(reloadProgress));
            }

            if (activeReload.HasValue && !Enum.IsDefined(typeof(ActiveReloadResult), activeReload.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReload), activeReload, "activeReload is not a defined ActiveReloadResult");
            }

            if (!Enum.IsDefined(typeof(ShoulderSide), shoulder))
            {
                throw new ArgumentOutOfRangeException(nameof(shoulder), shoulder, "shoulder is not a defined ShoulderSide");
            }

            if (!Enum.IsDefined(typeof(ViewMode), viewMode))
            {
                throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, "viewMode is not a defined ViewMode");
            }

            if (!(spreadDegrees >= 0f && spreadDegrees <= MaxSpreadDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(spreadDegrees), spreadDegrees, "spreadDegrees must be between 0 and 90");
            }

            Flags = flags;
            ReloadProgress = reloadProgress;
            ActiveReload = activeReload;
            Shoulder = shoulder;
            ViewMode = viewMode;
            _weaponName = weaponName ?? string.Empty;
            SpreadDegrees = spreadDegrees;
        }

        /// <summary>
        /// 最後の 1 発から secondsSinceShot 秒経った今も射撃中と見なすか。連射間隔 fireInterval と <see cref="MinFiringSeconds"/> の長い方まで true。
        /// まだ撃っていなければ secondsSinceShot に <see cref="float.PositiveInfinity"/> を渡す (false になる)。
        /// </summary>
        /// <param name="secondsSinceShot">最後の 1 発からの秒数。0 以上 (無限大可)。</param>
        /// <param name="fireInterval">今の武器の連射間隔 (秒)。0 以上の有限値。</param>
        public static bool IsFiringAfter(float secondsSinceShot, float fireInterval)
        {
            if (!(secondsSinceShot >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsSinceShot), secondsSinceShot, "secondsSinceShot must not be negative");
            }

            if (!(fireInterval >= 0f) || float.IsInfinity(fireInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(fireInterval), fireInterval, "fireInterval must be a non-negative finite number");
            }

            return secondsSinceShot <= Math.Max(fireInterval, MinFiringSeconds);
        }

        public bool Equals(PlayerActionState other)
        {
            return Flags == other.Flags
                && ReloadProgress.Equals(other.ReloadProgress)
                && ActiveReload == other.ActiveReload
                && Shoulder == other.Shoulder
                && ViewMode == other.ViewMode
                && string.Equals(WeaponName, other.WeaponName, StringComparison.Ordinal)
                && SpreadDegrees.Equals(other.SpreadDegrees);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerActionState other && Equals(other);
        }

        public static bool operator ==(PlayerActionState left, PlayerActionState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerActionState left, PlayerActionState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Flags, ReloadProgress, ActiveReload, (int)Shoulder, (int)ViewMode, WeaponName, SpreadDegrees);
        }

        /// <summary>例: <c>flags=Grounded, Aiming reload=0.00 activeReload=- shoulder=Right view=ThirdPerson weapon=Rifle spread=0.0</c>。</summary>
        public override string ToString()
        {
            var activeReload = ActiveReload.HasValue ? ActiveReload.Value.ToString() : "-";
            return string.Format(
                CultureInfo.InvariantCulture,
                "flags={0} reload={1:0.00} activeReload={2} shoulder={3} view={4} weapon={5} spread={6:0.0}",
                Flags,
                ReloadProgress,
                activeReload,
                Shoulder,
                ViewMode,
                WeaponName,
                SpreadDegrees);
        }

        private bool Has(PlayerActionConditions flag)
        {
            return (Flags & flag) != 0;
        }
    }
}
