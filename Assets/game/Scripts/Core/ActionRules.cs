using System;

namespace Shooter.Core
{
    public sealed class ActionRules
    {
        public const float DefaultDownedBleedoutSeconds = 15f;

        public const float DefaultDownedHealthFraction = 0.3f;

        public const float DefaultReviveSeconds = 3f;

        // Downed が Off でも秒数と割合を入れておくのは、Downed だけを Revive に差し替えたときにそのまま使えるようにするため。
        public static readonly ActionRules Default = new ActionRules(
            Downed.Off,
            DefaultDownedBleedoutSeconds,
            DefaultDownedHealthFraction,
            DefaultReviveSeconds,
            Cover.Off,
            BlindFire.Off,
            ActiveReload.Off);

        public Downed Downed { get; }

        public float DownedBleedoutSeconds { get; }

        public float DownedHealthFraction { get; }

        public float ReviveSeconds { get; }

        public Cover Cover { get; }

        public BlindFire BlindFire { get; }

        public ActiveReload ActiveReload { get; }

        public Dodge Dodge { get; }

        public Vault Vault { get; }

        public Grenade Grenade { get; }

        public int GrenadeCount { get; }

        public Melee Melee { get; }

        public Stagger Stagger { get; }

        public Slide Slide { get; }

        public Mantle Mantle { get; }

        public Prone Prone { get; }

        public Ladder Ladder { get; }

        public Zipline Zipline { get; }

        public Parachute Parachute { get; }

        public Stealth Stealth { get; }

        public Suppression Suppression { get; }

        public Ping Ping { get; }

        public Abilities Abilities { get; }

        public SlowMotion SlowMotion { get; }

        public AimToFire AimToFire { get; }

        public Jump Jump { get; }

        public float ReviveRange { get; }

        public Emote Emote { get; }

        public Stamina Stamina { get; }

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
            SlowMotion slowMotion = SlowMotion.Off,
            AimToFire aimToFire = AimToFire.Off,
            Jump jump = Jump.On,
            float reviveRange = ReviveReach.DefaultRange,
            Emote emote = Emote.Off,
            Stamina stamina = Stamina.Off)
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
            RequireDefined(aimToFire, nameof(aimToFire));
            RequireDefined(jump, nameof(jump));

            if (!(reviveRange > 0f) || float.IsInfinity(reviveRange))
            {
                throw new ArgumentOutOfRangeException(nameof(reviveRange), reviveRange, "reviveRange must be positive and finite");
            }

            RequireDefined(emote, nameof(emote));
            RequireDefined(stamina, nameof(stamina));

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
            AimToFire = aimToFire;
            Jump = jump;
            ReviveRange = reviveRange;
            Emote = emote;
            Stamina = stamina;
        }

        public ActionRules WithDowned(Downed value) => Copy(downed: value);

        public ActionRules WithDownedBleedoutSeconds(float value) => Copy(downedBleedoutSeconds: value);

        public ActionRules WithDownedHealthFraction(float value) => Copy(downedHealthFraction: value);

        public ActionRules WithReviveSeconds(float value) => Copy(reviveSeconds: value);

        public ActionRules WithCover(Cover value) => Copy(cover: value);

        public ActionRules WithBlindFire(BlindFire value) => Copy(blindFire: value);

        public ActionRules WithActiveReload(ActiveReload value) => Copy(activeReload: value);

        public ActionRules WithDodge(Dodge value) => Copy(dodge: value);

        public ActionRules WithVault(Vault value) => Copy(vault: value);

        public ActionRules WithGrenade(Grenade value) => Copy(grenade: value);

        public ActionRules WithGrenadeCount(int value) => Copy(grenadeCount: value);

        public ActionRules WithMelee(Melee value) => Copy(melee: value);

        public ActionRules WithStagger(Stagger value) => Copy(stagger: value);

        public ActionRules WithSlide(Slide value) => Copy(slide: value);

        public ActionRules WithMantle(Mantle value) => Copy(mantle: value);

        public ActionRules WithProne(Prone value) => Copy(prone: value);

        public ActionRules WithLadder(Ladder value) => Copy(ladder: value);

        public ActionRules WithZipline(Zipline value) => Copy(zipline: value);

        public ActionRules WithParachute(Parachute value) => Copy(parachute: value);

        public ActionRules WithStealth(Stealth value) => Copy(stealth: value);

        public ActionRules WithSuppression(Suppression value) => Copy(suppression: value);

        public ActionRules WithPing(Ping value) => Copy(ping: value);

        public ActionRules WithAbilities(Abilities value) => Copy(abilities: value);

        public ActionRules WithSlowMotion(SlowMotion value) => Copy(slowMotion: value);

        public ActionRules WithAimToFire(AimToFire value) => Copy(aimToFire: value);

        public ActionRules WithJump(Jump value) => Copy(jump: value);

        public ActionRules WithReviveRange(float value) => Copy(reviveRange: value);

        public ActionRules WithEmote(Emote value) => Copy(emote: value);

        public ActionRules WithStamina(Stamina value) => Copy(stamina: value);

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
            SlowMotion? slowMotion = null,
            AimToFire? aimToFire = null,
            Jump? jump = null,
            float? reviveRange = null,
            Emote? emote = null,
            Stamina? stamina = null)
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
                slowMotion ?? SlowMotion,
                aimToFire ?? AimToFire,
                jump ?? Jump,
                reviveRange ?? ReviveRange,
                emote ?? Emote,
                stamina ?? Stamina);
        }
    }
}
