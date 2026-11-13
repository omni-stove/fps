using System;

namespace Fps.Core
{
    /// <summary>
    /// TPS のアクション (ダウンと蘇生、カバー、ブラインドファイア、アクティブリロード、回避、乗り越え、グレネード) のルール。
    /// 生成時に値を検査し、以後は変わらない。<see cref="GameRules.Actions"/> として持つ。
    /// </summary>
    public sealed class ActionRules
    {
        /// <summary><see cref="Default"/> の <see cref="DownedBleedoutSeconds"/>。</summary>
        public const float DefaultDownedBleedoutSeconds = 15f;

        /// <summary><see cref="Default"/> の <see cref="DownedHealthFraction"/>。</summary>
        public const float DefaultDownedHealthFraction = 0.3f;

        /// <summary><see cref="Default"/> の <see cref="ReviveSeconds"/>。</summary>
        public const float DefaultReviveSeconds = 3f;

        /// <summary>
        /// 今の挙動。どのアクションも使わない。ダウンの値は <see cref="Core.Downed.Revive"/> にしたときのために、
        /// 出血 <see cref="DefaultDownedBleedoutSeconds"/> 秒、体力の割合 <see cref="DefaultDownedHealthFraction"/>、
        /// 蘇生 <see cref="DefaultReviveSeconds"/> 秒を入れておく。グレネードの数は 0。
        /// </summary>
        public static readonly ActionRules Default = new ActionRules(
            Downed.Off,
            DefaultDownedBleedoutSeconds,
            DefaultDownedHealthFraction,
            DefaultReviveSeconds,
            Cover.Off,
            BlindFire.Off,
            ActiveReload.Off);

        /// <summary>体力が 0 になったプレイヤーをダウンさせるか。</summary>
        public Downed Downed { get; }

        /// <summary>ダウンしてから起こされずに倒れるまでの秒数。0 より大きい有限値。</summary>
        public float DownedBleedoutSeconds { get; }

        /// <summary>ダウン中の体力と、起こされた後の体力の、最大体力に対する割合。0 より大きく 1 以下。</summary>
        public float DownedHealthFraction { get; }

        /// <summary>味方を起こすのにかかる秒数。0 より大きい有限値。</summary>
        public float ReviveSeconds { get; }

        /// <summary>遮蔽物に張り付けるか。</summary>
        public Cover Cover { get; }

        /// <summary>隠れたまま撃てるか。<see cref="Core.BlindFire.On"/> なら <see cref="Cover"/> も <see cref="Core.Cover.On"/>。</summary>
        public BlindFire BlindFire { get; }

        /// <summary>アクティブリロードを使うか。</summary>
        public ActiveReload ActiveReload { get; }

        /// <summary>素早く飛び退けるか。</summary>
        public Dodge Dodge { get; }

        /// <summary>低い障害物を乗り越えられるか。</summary>
        public Vault Vault { get; }

        /// <summary>グレネードを投げられるか。</summary>
        public Grenade Grenade { get; }

        /// <summary>出撃ごとに持つグレネードの数。0 以上。<see cref="Core.Grenade.Off"/> なら使わない。</summary>
        public int GrenadeCount { get; }

        /// <param name="downed">ダウンの扱い。定義済みの値。</param>
        /// <param name="downedBleedoutSeconds">ダウンから倒れるまでの秒数。0 より大きい有限値。</param>
        /// <param name="downedHealthFraction">ダウン中と起こされた後の体力の割合。0 より大きく 1 以下。</param>
        /// <param name="reviveSeconds">蘇生にかかる秒数。0 より大きい有限値。</param>
        /// <param name="cover">カバー。定義済みの値。</param>
        /// <param name="blindFire">ブラインドファイア。定義済みの値。On なら cover も On であること。</param>
        /// <param name="activeReload">アクティブリロード。定義済みの値。</param>
        /// <param name="dodge">回避。定義済みの値。</param>
        /// <param name="vault">乗り越え。定義済みの値。</param>
        /// <param name="grenade">グレネード。定義済みの値。</param>
        /// <param name="grenadeCount">出撃ごとのグレネードの数。0 以上。</param>
        public ActionRules(
            Downed downed,
            float downedBleedoutSeconds,
            float downedHealthFraction,
            float reviveSeconds,
            Cover cover,
            BlindFire blindFire,
            ActiveReload activeReload,
            Dodge dodge = Dodge.Off,
            Vault vault = Vault.Off,
            Grenade grenade = Grenade.Off,
            int grenadeCount = 0)
        {
            if (!Enum.IsDefined(typeof(Downed), downed))
            {
                throw new ArgumentOutOfRangeException(nameof(downed), downed, "downed is not a defined Downed");
            }

            // Off でも検査するのは、後から Downed だけ差し替えた値でも秒数と割合が必ず使える状態にしておくため。
            if (!(downedBleedoutSeconds > 0f) || float.IsInfinity(downedBleedoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(downedBleedoutSeconds), downedBleedoutSeconds, "downedBleedoutSeconds must be positive and finite");
            }

            if (!(downedHealthFraction > 0f && downedHealthFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(downedHealthFraction), downedHealthFraction, "downedHealthFraction must be greater than 0 and at most 1");
            }

            if (!(reviveSeconds > 0f) || float.IsInfinity(reviveSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reviveSeconds), reviveSeconds, "reviveSeconds must be positive and finite");
            }

            if (!Enum.IsDefined(typeof(Cover), cover))
            {
                throw new ArgumentOutOfRangeException(nameof(cover), cover, "cover is not a defined Cover");
            }

            if (!Enum.IsDefined(typeof(BlindFire), blindFire))
            {
                throw new ArgumentOutOfRangeException(nameof(blindFire), blindFire, "blindFire is not a defined BlindFire");
            }

            if (blindFire == BlindFire.On && cover != Cover.On)
            {
                throw new ArgumentException("blindFire can be On only when cover is On", nameof(blindFire));
            }

            if (!Enum.IsDefined(typeof(ActiveReload), activeReload))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReload), activeReload, "activeReload is not a defined ActiveReload");
            }

            if (!Enum.IsDefined(typeof(Dodge), dodge))
            {
                throw new ArgumentOutOfRangeException(nameof(dodge), dodge, "dodge is not a defined Dodge");
            }

            if (!Enum.IsDefined(typeof(Vault), vault))
            {
                throw new ArgumentOutOfRangeException(nameof(vault), vault, "vault is not a defined Vault");
            }

            if (!Enum.IsDefined(typeof(Grenade), grenade))
            {
                throw new ArgumentOutOfRangeException(nameof(grenade), grenade, "grenade is not a defined Grenade");
            }

            if (grenadeCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(grenadeCount), grenadeCount, "grenadeCount must not be negative");
            }

            Downed = downed;
            DownedBleedoutSeconds = downedBleedoutSeconds;
            DownedHealthFraction = downedHealthFraction;
            ReviveSeconds = reviveSeconds;
            Cover = cover;
            BlindFire = blindFire;
            ActiveReload = activeReload;
            Dodge = dodge;
            Vault = vault;
            Grenade = grenade;
            GrenadeCount = grenadeCount;
        }

        /// <summary><see cref="Downed"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithDowned(Downed value) => Copy(downed: value);

        /// <summary><see cref="DownedBleedoutSeconds"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithDownedBleedoutSeconds(float value) => Copy(downedBleedoutSeconds: value);

        /// <summary><see cref="DownedHealthFraction"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithDownedHealthFraction(float value) => Copy(downedHealthFraction: value);

        /// <summary><see cref="ReviveSeconds"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithReviveSeconds(float value) => Copy(reviveSeconds: value);

        /// <summary>
        /// <see cref="Cover"/> だけを value にした複製。値の検査はコンストラクターと同じで、
        /// <see cref="BlindFire"/> が On のまま Off にすると <see cref="ArgumentException"/> (先に <see cref="WithBlindFire"/> で切る)。
        /// </summary>
        public ActionRules WithCover(Cover value) => Copy(cover: value);

        /// <summary>
        /// <see cref="BlindFire"/> だけを value にした複製。値の検査はコンストラクターと同じで、
        /// <see cref="Cover"/> が Off のまま On にすると <see cref="ArgumentException"/> (先に <see cref="WithCover"/> で入れる)。
        /// </summary>
        public ActionRules WithBlindFire(BlindFire value) => Copy(blindFire: value);

        /// <summary><see cref="ActiveReload"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithActiveReload(ActiveReload value) => Copy(activeReload: value);

        /// <summary><see cref="Dodge"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithDodge(Dodge value) => Copy(dodge: value);

        /// <summary><see cref="Vault"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithVault(Vault value) => Copy(vault: value);

        /// <summary><see cref="Grenade"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithGrenade(Grenade value) => Copy(grenade: value);

        /// <summary><see cref="GrenadeCount"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithGrenadeCount(int value) => Copy(grenadeCount: value);

        private ActionRules Copy(
            Downed? downed = null,
            float? downedBleedoutSeconds = null,
            float? downedHealthFraction = null,
            float? reviveSeconds = null,
            Cover? cover = null,
            BlindFire? blindFire = null,
            ActiveReload? activeReload = null,
            Dodge? dodge = null,
            Vault? vault = null,
            Grenade? grenade = null,
            int? grenadeCount = null)
        {
            return new ActionRules(
                downed ?? Downed,
                downedBleedoutSeconds ?? DownedBleedoutSeconds,
                downedHealthFraction ?? DownedHealthFraction,
                reviveSeconds ?? ReviveSeconds,
                cover ?? Cover,
                blindFire ?? BlindFire,
                activeReload ?? ActiveReload,
                dodge ?? Dodge,
                vault ?? Vault,
                grenade ?? Grenade,
                grenadeCount ?? GrenadeCount);
        }
    }
}
