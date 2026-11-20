using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class GameRulesJson
    {
        public const string ActionsItem = "actions";

        private const string _kind = "game rules JSON";

        // ルールの JSON は列挙の誤りに受け付ける名前を並べて知らせてきたので、その文のまま読む。
        private static readonly ServerDataJson.Wording _wording = new ServerDataJson.Wording("known items here are", true);

        public const string FriendlyFireItem = "friendlyFire";
        public const string RespawnModeItem = "respawnMode";
        public const string WaveIntervalSecondsItem = "waveIntervalSeconds";
        public const string RegenDelaySecondsItem = "regenDelaySeconds";
        public const string RegenPerSecondItem = "regenPerSecond";
        public const string DeathBodyItem = "deathBody";
        public const string DeathCameraItem = "deathCamera";
        public const string InfiniteReserveItem = "infiniteReserve";
        public const string ReserveAmmoMultiplierItem = "reserveAmmoMultiplier";
        public const string DestructionItem = "destruction";
        public const string SpawnProtectionSecondsItem = "spawnProtectionSeconds";
        public const string FallDamageItem = "fallDamage";
        public const string HudDensityItem = "hudDensity";
        public const string ObjectiveItem = "objective";
        public const string MinPlayersItem = "minPlayers";
        public const string IdleKickSecondsItem = "idleKickSeconds";
        public const string IdleWarningSecondsItem = "idleWarningSeconds";
        public const string MaxWeaponsItem = "maxWeapons";
        public const string ReloadModeItem = "reloadMode";
        public const string ViewModeItem = "viewMode";
        public const string AdsEyeViewItem = "adsEyeView";
        public const string PlayerMaxHealthItem = "playerMaxHealth";
        public const string ZoneDamageItem = "zoneDamage";
        public const string StreaksItem = "streaks";
        public const string TextChatItem = "textChat";
        public const string SpectatingItem = "spectating";
        public const string RespawnAnchorItem = "respawnAnchor";

        public const string HeadItem = "head";
        public const string BodyItem = "body";
        public const string LimbItem = "limb";

        public const string DownedItem = "downed";
        public const string DownedBleedoutSecondsItem = "downedBleedoutSeconds";
        public const string DownedHealthFractionItem = "downedHealthFraction";
        public const string ReviveSecondsItem = "reviveSeconds";
        public const string CoverItem = "cover";
        public const string BlindFireItem = "blindFire";
        public const string ActiveReloadItem = "activeReload";
        public const string DodgeItem = "dodge";
        public const string VaultItem = "vault";
        public const string GrenadeItem = "grenade";
        public const string GrenadeCountItem = "grenadeCount";
        public const string MeleeItem = "melee";
        public const string StaggerItem = "stagger";
        public const string SlideItem = "slide";
        public const string MantleItem = "mantle";
        public const string ProneItem = "prone";
        public const string LadderItem = "ladder";
        public const string ZiplineItem = "zipline";
        public const string ParachuteItem = "parachute";
        public const string StealthItem = "stealth";
        public const string SuppressionItem = "suppression";
        public const string PingItem = "ping";
        public const string AbilitiesItem = "abilities";
        public const string SlowMotionItem = "slowMotion";
        public const string AimToFireItem = "aimToFire";
        public const string JumpItem = "jump";
        public const string ReviveRangeItem = "reviveRange";
        public const string EmoteItem = "emote";
        public const string StaminaItem = "stamina";
        public const string ReloadInterruptedByItem = "reloadInterruptedBy";
        public const string AimWhileReloadingItem = "aimWhileReloading";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            FriendlyFireItem,
            RespawnModeItem,
            WaveIntervalSecondsItem,
            RegenDelaySecondsItem,
            RegenPerSecondItem,
            DeathBodyItem,
            DeathCameraItem,
            InfiniteReserveItem,
            ReserveAmmoMultiplierItem,
            DestructionItem,
            ActionsItem,
            SpawnProtectionSecondsItem,
            FallDamageItem,
            HudDensityItem,
            ObjectiveItem,
            MinPlayersItem,
            IdleKickSecondsItem,
            IdleWarningSecondsItem,
            MaxWeaponsItem,
            ReloadModeItem,
            ViewModeItem,
            AdsEyeViewItem,
            PlayerMaxHealthItem,
            ZoneDamageItem,
            StreaksItem,
            TextChatItem,
            SpectatingItem,
            RespawnAnchorItem,
        };

        public static IReadOnlyList<string> ActionItemNames { get; } = new[]
        {
            DownedItem,
            DownedBleedoutSecondsItem,
            DownedHealthFractionItem,
            ReviveSecondsItem,
            CoverItem,
            BlindFireItem,
            ActiveReloadItem,
            DodgeItem,
            VaultItem,
            GrenadeItem,
            GrenadeCountItem,
            MeleeItem,
            StaggerItem,
            SlideItem,
            MantleItem,
            ProneItem,
            LadderItem,
            ZiplineItem,
            ParachuteItem,
            StealthItem,
            SuppressionItem,
            PingItem,
            AbilitiesItem,
            SlowMotionItem,
            AimToFireItem,
            JumpItem,
            ReviveRangeItem,
            EmoteItem,
            StaminaItem,
            ReloadInterruptedByItem,
            AimWhileReloadingItem,
        };

        // 弱点の倍率はルールで変えるものではないので書けるようにせず、土台のルールの値のままにする。
        public static IReadOnlyList<string> ZoneDamageItemNames { get; } = new[]
        {
            HeadItem,
            BodyItem,
            LimbItem,
        };

        public static GameRules Parse(string text, string sourceName)
        {
            return Parse(text, sourceName, GameRules.Default);
        }

        public static GameRules Parse(string text, string sourceName, GameRules baseRules)
        {
            if (baseRules == null)
            {
                throw new ArgumentNullException(nameof(baseRules));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var source = ServerDataJson.SourceOf(sourceName, _kind);
            return Rethrown(() => Read(ServerDataJson.ParseDocument(text, source, _kind), source, string.Empty, baseRules));
        }

        // path は root を置いた項目の名前 (試合のファイルなら "rules") で、エラーの項目名の頭に付ける。
        internal static GameRules FromNode(JsonNode root, string source, string path)
        {
            return Rethrown(() => Read(root, source, path, GameRules.Default));
        }

        // 呼ぶ側 (GameRulesOverride・MutatorJson・MissionDefinition) は GameRulesJsonException を待つので、共通の読み手の例外を包み直す。
        private static T Rethrown<T>(Func<T> read)
        {
            return ServerDataJson.Rethrown(read, e => new GameRulesJsonException(e.SourceName, e.Item, e.Message, e.InnerException ?? e));
        }

        private static GameRules Read(JsonNode root, string source, string path, GameRules defaults)
        {
            var top = new ServerDataJson.Fields(source, _kind, root, path, ItemNames, _wording);

            var actions = ReadActions(top, defaults.Actions);
            var zoneDamage = ReadZoneDamage(top, defaults.ZoneDamage);

            try
            {
                return new GameRules(
                    top.ReadEnum(FriendlyFireItem, defaults.FriendlyFire),
                    top.ReadEnum(RespawnModeItem, defaults.RespawnMode),
                    top.ReadFloat(WaveIntervalSecondsItem, defaults.WaveIntervalSeconds),
                    top.ReadFloat(RegenDelaySecondsItem, defaults.RegenDelaySeconds),
                    top.ReadFloat(RegenPerSecondItem, defaults.RegenPerSecond),
                    top.ReadEnum(DeathBodyItem, defaults.DeathBody),
                    top.ReadEnum(DeathCameraItem, defaults.DeathCamera),
                    top.ReadBool(InfiniteReserveItem, defaults.InfiniteReserve),
                    top.ReadFloat(ReserveAmmoMultiplierItem, defaults.ReserveAmmoMultiplier),
                    top.ReadEnum(DestructionItem, defaults.Destruction),
                    actions,
                    top.ReadFloat(SpawnProtectionSecondsItem, defaults.SpawnProtectionSeconds),
                    top.ReadEnum(FallDamageItem, defaults.FallDamage),
                    top.ReadEnum(HudDensityItem, defaults.HudDensity),
                    top.ReadEnum(ObjectiveItem, defaults.Objective),
                    top.ReadInt(MinPlayersItem, defaults.MinPlayers),
                    top.ReadFloat(IdleKickSecondsItem, defaults.IdleKickSeconds),
                    top.ReadInt(MaxWeaponsItem, defaults.MaxWeapons),
                    top.ReadEnum(ReloadModeItem, defaults.ReloadMode),
                    top.ReadEnum(ViewModeItem, defaults.ViewMode),
                    top.ReadInt(PlayerMaxHealthItem, defaults.PlayerMaxHealth),
                    zoneDamage,
                    top.ReadEnum(StreaksItem, defaults.Streaks),
                    top.ReadEnum(TextChatItem, defaults.TextChat),
                    top.ReadEnum(SpectatingItem, defaults.Spectating),
                    top.ReadEnumArray(RespawnAnchorItem, defaults.RespawnAnchors),
                    top.ReadFloat(IdleWarningSecondsItem, defaults.IdleWarningSeconds),
                    top.ReadEnum(AdsEyeViewItem, defaults.AdsEyeView));
            }
            catch (ArgumentException e)
            {
                throw Rejected(top, e);
            }
        }

        public static string Write(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var actions = rules.Actions;
            var writer = new JsonWriter();
            writer.Enum(FriendlyFireItem, rules.FriendlyFire);
            writer.Enum(RespawnModeItem, rules.RespawnMode);
            writer.Float(WaveIntervalSecondsItem, rules.WaveIntervalSeconds);
            writer.Float(RegenDelaySecondsItem, rules.RegenDelaySeconds);
            writer.Float(RegenPerSecondItem, rules.RegenPerSecond);
            writer.Enum(DeathBodyItem, rules.DeathBody);
            writer.Enum(DeathCameraItem, rules.DeathCamera);
            writer.Bool(InfiniteReserveItem, rules.InfiniteReserve);
            writer.Float(ReserveAmmoMultiplierItem, rules.ReserveAmmoMultiplier);
            writer.Enum(DestructionItem, rules.Destruction);
            writer.BeginObject(ActionsItem);
            writer.Enum(DownedItem, actions.Downed);
            writer.Float(DownedBleedoutSecondsItem, actions.DownedBleedoutSeconds);
            writer.Float(DownedHealthFractionItem, actions.DownedHealthFraction);
            writer.Float(ReviveSecondsItem, actions.ReviveSeconds);
            writer.Enum(CoverItem, actions.Cover);
            writer.Enum(BlindFireItem, actions.BlindFire);
            writer.Enum(ActiveReloadItem, actions.ActiveReload);
            writer.Enum(DodgeItem, actions.Dodge);
            writer.Enum(VaultItem, actions.Vault);
            writer.Enum(GrenadeItem, actions.Grenade);
            writer.Int(GrenadeCountItem, actions.GrenadeCount);
            writer.Enum(MeleeItem, actions.Melee);
            writer.Enum(StaggerItem, actions.Stagger);
            writer.Enum(SlideItem, actions.Slide);
            writer.Enum(MantleItem, actions.Mantle);
            writer.Enum(ProneItem, actions.Prone);
            writer.Enum(LadderItem, actions.Ladder);
            writer.Enum(ZiplineItem, actions.Zipline);
            writer.Enum(ParachuteItem, actions.Parachute);
            writer.Enum(StealthItem, actions.Stealth);
            writer.Enum(SuppressionItem, actions.Suppression);
            writer.Enum(PingItem, actions.Ping);
            writer.Enum(AbilitiesItem, actions.Abilities);
            writer.Enum(SlowMotionItem, actions.SlowMotion);
            writer.Enum(AimToFireItem, actions.AimToFire);
            writer.Enum(JumpItem, actions.Jump);
            writer.Float(ReviveRangeItem, actions.ReviveRange);
            writer.Enum(EmoteItem, actions.Emote);
            writer.Enum(StaminaItem, actions.Stamina);
            writer.EnumArray(ReloadInterruptedByItem, actions.ReloadInterruptedBy);
            writer.Enum(AimWhileReloadingItem, actions.AimWhileReloading);
            writer.EndObject();
            writer.Float(SpawnProtectionSecondsItem, rules.SpawnProtectionSeconds);
            writer.Enum(FallDamageItem, rules.FallDamage);
            writer.Enum(HudDensityItem, rules.HudDensity);
            writer.Enum(ObjectiveItem, rules.Objective);
            writer.Int(MinPlayersItem, rules.MinPlayers);
            writer.Float(IdleKickSecondsItem, rules.IdleKickSeconds);
            writer.Float(IdleWarningSecondsItem, rules.IdleWarningSeconds);
            writer.Int(MaxWeaponsItem, rules.MaxWeapons);
            writer.Enum(ReloadModeItem, rules.ReloadMode);
            writer.Enum(ViewModeItem, rules.ViewMode);
            writer.Enum(AdsEyeViewItem, rules.AdsEyeView);
            writer.Int(PlayerMaxHealthItem, rules.PlayerMaxHealth);
            writer.BeginObject(ZoneDamageItem);
            writer.Double(HeadItem, rules.ZoneDamage.Head);
            writer.Double(BodyItem, rules.ZoneDamage.Body);
            writer.Double(LimbItem, rules.ZoneDamage.Limb);
            writer.EndObject();
            writer.Enum(StreaksItem, rules.Streaks);
            writer.Enum(TextChatItem, rules.TextChat);
            writer.Enum(SpectatingItem, rules.Spectating);
            writer.EnumArray(RespawnAnchorItem, rules.RespawnAnchors);
            return writer.Finish();
        }

        private static ActionRules ReadActions(ServerDataJson.Fields top, ActionRules defaults)
        {
            var fields = top.Object(ActionsItem, ActionItemNames);
            if (fields == null)
            {
                return defaults;
            }

            try
            {
                return new ActionRules(
                    fields.ReadEnum(DownedItem, defaults.Downed),
                    fields.ReadFloat(DownedBleedoutSecondsItem, defaults.DownedBleedoutSeconds),
                    fields.ReadFloat(DownedHealthFractionItem, defaults.DownedHealthFraction),
                    fields.ReadFloat(ReviveSecondsItem, defaults.ReviveSeconds),
                    fields.ReadEnum(CoverItem, defaults.Cover),
                    fields.ReadEnum(BlindFireItem, defaults.BlindFire),
                    fields.ReadEnum(ActiveReloadItem, defaults.ActiveReload),
                    fields.ReadEnum(DodgeItem, defaults.Dodge),
                    fields.ReadEnum(VaultItem, defaults.Vault),
                    fields.ReadEnum(GrenadeItem, defaults.Grenade),
                    fields.ReadInt(GrenadeCountItem, defaults.GrenadeCount),
                    fields.ReadEnum(MeleeItem, defaults.Melee),
                    fields.ReadEnum(StaggerItem, defaults.Stagger),
                    fields.ReadEnum(SlideItem, defaults.Slide),
                    fields.ReadEnum(MantleItem, defaults.Mantle),
                    fields.ReadEnum(ProneItem, defaults.Prone),
                    fields.ReadEnum(LadderItem, defaults.Ladder),
                    fields.ReadEnum(ZiplineItem, defaults.Zipline),
                    fields.ReadEnum(ParachuteItem, defaults.Parachute),
                    fields.ReadEnum(StealthItem, defaults.Stealth),
                    fields.ReadEnum(SuppressionItem, defaults.Suppression),
                    fields.ReadEnum(PingItem, defaults.Ping),
                    fields.ReadEnum(AbilitiesItem, defaults.Abilities),
                    fields.ReadEnum(SlowMotionItem, defaults.SlowMotion),
                    fields.ReadEnum(AimToFireItem, defaults.AimToFire),
                    fields.ReadEnum(JumpItem, defaults.Jump),
                    fields.ReadFloat(ReviveRangeItem, defaults.ReviveRange),
                    fields.ReadEnum(EmoteItem, defaults.Emote),
                    fields.ReadEnum(StaminaItem, defaults.Stamina),
                    fields.ReadEnumArray(ReloadInterruptedByItem, defaults.ReloadInterruptedBy),
                    fields.ReadEnum(AimWhileReloadingItem, defaults.AimWhileReloading));
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, e);
            }
        }

        private static ZoneDamageMultipliers ReadZoneDamage(ServerDataJson.Fields top, ZoneDamageMultipliers defaults)
        {
            var fields = top.Object(ZoneDamageItem, ZoneDamageItemNames);
            if (fields == null)
            {
                return defaults;
            }

            try
            {
                return new ZoneDamageMultipliers(
                    fields.ReadDouble(HeadItem, defaults.Head),
                    fields.ReadDouble(BodyItem, defaults.Body),
                    fields.ReadDouble(LimbItem, defaults.Limb),
                    defaults.WeakPoint);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, e);
            }
        }

        // GameRules と ActionRules と ZoneDamageMultipliers は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static ServerDataJsonException Rejected(ServerDataJson.Fields fields, ArgumentException e)
        {
            return fields.Rejected(string.IsNullOrEmpty(e.ParamName) ? null : fields.ItemPath(e.ParamName), e.Message, e);
        }

    }

    public sealed class GameRulesJsonException : Exception
    {
        public GameRulesJsonException()
        {
        }

        public GameRulesJsonException(string message)
            : base(message)
        {
        }

        public GameRulesJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public GameRulesJsonException(string sourceName, string item, string message, Exception innerException = null)
            : base(message, innerException)
        {
            SourceName = sourceName;
            Item = item;
        }

        public string SourceName { get; }

        public string Item { get; }
    }
}
