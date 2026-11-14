using System;

namespace Fps.Core
{
    /// <summary>
    /// TPS のアクション (ダウンと蘇生、カバー、ブラインドファイア、アクティブリロード、回避、乗り越え、グレネード、
    /// 近接攻撃、怯み、スライディング、崖のぼり、伏せ、はしご、ジップライン、パラシュート、発見ゲージ、制圧射撃、印、能力、時間の速さ) のルール。
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

        /// <summary>近接攻撃を使えるか。</summary>
        public Melee Melee { get; }

        /// <summary>被弾したときに怯むか。</summary>
        public Stagger Stagger { get; }

        /// <summary>スライディングできるか。</summary>
        public Slide Slide { get; }

        /// <summary>段や崖の縁によじ登れるか。</summary>
        public Mantle Mantle { get; }

        /// <summary>伏せられるか。</summary>
        public Prone Prone { get; }

        /// <summary>はしごを上り下りできるか。</summary>
        public Ladder Ladder { get; }

        /// <summary>ジップラインを使えるか。</summary>
        public Zipline Zipline { get; }

        /// <summary>パラシュートを開けるか。</summary>
        public Parachute Parachute { get; }

        /// <summary>敵が発見ゲージで段階を踏んで気づくか。</summary>
        public Stealth Stealth { get; }

        /// <summary>近くを飛ぶ弾で制圧されるか。</summary>
        public Suppression Suppression { get; }

        /// <summary>敵や場所に印を付けられるか。</summary>
        public Ping Ping { get; }

        /// <summary>能力を使えるか。</summary>
        public Abilities Abilities { get; }

        /// <summary>時間の速さを変えられるか。オフラインに限る。</summary>
        public SlowMotion SlowMotion { get; }

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
        /// <param name="melee">近接攻撃。定義済みの値。</param>
        /// <param name="stagger">怯み。定義済みの値。</param>
        /// <param name="slide">スライディング。定義済みの値。</param>
        /// <param name="mantle">崖のぼり。定義済みの値。</param>
        /// <param name="prone">伏せ。定義済みの値。</param>
        /// <param name="ladder">はしご。定義済みの値。</param>
        /// <param name="zipline">ジップライン。定義済みの値。</param>
        /// <param name="parachute">パラシュート。定義済みの値。</param>
        /// <param name="stealth">発見ゲージ。定義済みの値。</param>
        /// <param name="suppression">制圧射撃。定義済みの値。</param>
        /// <param name="ping">印。定義済みの値。</param>
        /// <param name="abilities">能力。定義済みの値。</param>
        /// <param name="slowMotion">時間の速さ。定義済みの値。</param>
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
            int grenadeCount = 0,
            Melee melee = Melee.Off,
            Stagger stagger = Stagger.Off,
            Slide slide = Slide.Off,
            Mantle mantle = Mantle.Off,
            Prone prone = Prone.Off,
            Ladder ladder = Ladder.Off,
            Zipline zipline = Zipline.Off,
            Parachute parachute = Parachute.Off,
            Stealth stealth = Stealth.Off,
            Suppression suppression = Suppression.Off,
            Ping ping = Ping.Off,
            Abilities abilities = Abilities.Off,
            SlowMotion slowMotion = SlowMotion.Off)
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

            RequireDefined(melee, nameof(melee));
            RequireDefined(stagger, nameof(stagger));
            RequireDefined(slide, nameof(slide));
            RequireDefined(mantle, nameof(mantle));
            RequireDefined(prone, nameof(prone));
            RequireDefined(ladder, nameof(ladder));
            RequireDefined(zipline, nameof(zipline));
            RequireDefined(parachute, nameof(parachute));
            RequireDefined(stealth, nameof(stealth));
            RequireDefined(suppression, nameof(suppression));
            RequireDefined(ping, nameof(ping));
            RequireDefined(abilities, nameof(abilities));
            RequireDefined(slowMotion, nameof(slowMotion));

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
            Melee = melee;
            Stagger = stagger;
            Slide = slide;
            Mantle = mantle;
            Prone = prone;
            Ladder = ladder;
            Zipline = zipline;
            Parachute = parachute;
            Stealth = stealth;
            Suppression = suppression;
            Ping = ping;
            Abilities = abilities;
            SlowMotion = slowMotion;
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

        /// <summary><see cref="Melee"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithMelee(Melee value) => Copy(melee: value);

        /// <summary><see cref="Stagger"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithStagger(Stagger value) => Copy(stagger: value);

        /// <summary><see cref="Slide"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithSlide(Slide value) => Copy(slide: value);

        /// <summary><see cref="Mantle"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithMantle(Mantle value) => Copy(mantle: value);

        /// <summary><see cref="Prone"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithProne(Prone value) => Copy(prone: value);

        /// <summary><see cref="Ladder"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithLadder(Ladder value) => Copy(ladder: value);

        /// <summary><see cref="Zipline"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithZipline(Zipline value) => Copy(zipline: value);

        /// <summary><see cref="Parachute"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithParachute(Parachute value) => Copy(parachute: value);

        /// <summary><see cref="Stealth"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithStealth(Stealth value) => Copy(stealth: value);

        /// <summary><see cref="Suppression"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithSuppression(Suppression value) => Copy(suppression: value);

        /// <summary><see cref="Ping"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithPing(Ping value) => Copy(ping: value);

        /// <summary><see cref="Abilities"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithAbilities(Abilities value) => Copy(abilities: value);

        /// <summary><see cref="SlowMotion"/> だけを value にした複製。値の検査はコンストラクターと同じ。</summary>
        public ActionRules WithSlowMotion(SlowMotion value) => Copy(slowMotion: value);

        private static void RequireDefined<T>(T value, string name)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} is not a defined {typeof(T).Name}");
            }
        }

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
            int? grenadeCount = null,
            Melee? melee = null,
            Stagger? stagger = null,
            Slide? slide = null,
            Mantle? mantle = null,
            Prone? prone = null,
            Ladder? ladder = null,
            Zipline? zipline = null,
            Parachute? parachute = null,
            Stealth? stealth = null,
            Suppression? suppression = null,
            Ping? ping = null,
            Abilities? abilities = null,
            SlowMotion? slowMotion = null)
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
                grenadeCount ?? GrenadeCount,
                melee ?? Melee,
                stagger ?? Stagger,
                slide ?? Slide,
                mantle ?? Mantle,
                prone ?? Prone,
                ladder ?? Ladder,
                zipline ?? Zipline,
                parachute ?? Parachute,
                stealth ?? Stealth,
                suppression ?? Suppression,
                ping ?? Ping,
                abilities ?? Abilities,
                slowMotion ?? SlowMotion);
        }
    }
}
