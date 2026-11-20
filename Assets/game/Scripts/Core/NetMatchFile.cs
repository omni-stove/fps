using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // Unity の Editor が NetMatch.prefab とルールのアセットから書き出したものを読むので、Unity 抜きのサーバーの試合も Unity の専用サーバーと同じ値で動く。
    public sealed class NetMatchFile
    {
        public const string Extension = ".json";

        public const string NameItem = "name";
        public const string RulesItem = "rules";
        public const string MatchItem = "match";
        public const string LoadoutItem = "loadout";
        public const string CatalogItem = "catalog";
        public const string WeaponsItem = "weapons";
        public const string SwitchSecondsItem = "switchSeconds";
        public const string SlotsItem = "slots";
        public const string AbilitiesItem = "abilities";

        public const string ScoreLimitItem = "scoreLimit";
        public const string TimeLimitSecondsItem = "timeLimitSeconds";
        public const string RespawnSecondsItem = "respawnSeconds";
        public const string RestartDelaySecondsItem = "restartDelaySeconds";
        public const string MaxHealthItem = "maxHealth";
        public const string MinEnemySpawnDistanceItem = "minEnemySpawnDistance";
        public const string ReconnectGraceSecondsItem = "reconnectGraceSeconds";
        public const string MaxShotOriginOffsetItem = "maxShotOriginOffset";
        public const string DominationScoreLimitItem = "dominationScoreLimit";
        public const string CaptureSecondsItem = "captureSeconds";
        public const string PointsPerSecondItem = "pointsPerSecond";
        public const string MaxCapturersItem = "maxCapturers";
        public const string MaxPlayersItem = "maxPlayers";
        public const string MapPoolItem = "mapPool";
        public const string MapVoteSecondsItem = "mapVoteSeconds";
        public const string MaxTeamSizeDifferenceItem = "maxTeamSizeDifference";
        public const string PrivateMatchItem = "privateMatch";
        public const string JoinCodeItem = "joinCode";
        public const string ChatIntervalSecondsItem = "chatIntervalSeconds";
        public const string MaxChatLengthItem = "maxChatLength";
        public const string MaxDisplayNameLengthItem = "maxDisplayNameLength";
        public const string RoundWinsToWinItem = "roundWinsToWin";
        public const string RoundPreparationSecondsItem = "roundPreparationSeconds";
        public const string RoundTimeLimitSecondsItem = "roundTimeLimitSeconds";
        public const string WeatherIdItem = "weatherId";
        public const string TimeOfDayIdItem = "timeOfDayId";

        public const string CaptureTheFlagItem = "captureTheFlag";
        public const string PickupRadiusItem = "pickupRadius";
        public const string CaptureRadiusItem = "captureRadius";
        public const string ReturnSecondsItem = "returnSeconds";
        public const string PointsPerCaptureItem = "pointsPerCapture";

        public const string HardpointItem = "hardpoint";
        public const string RotateSecondsItem = "rotateSeconds";

        public const string SearchAndDestroyItem = "searchAndDestroy";
        public const string RoundSecondsItem = "roundSeconds";
        public const string PlantSecondsItem = "plantSeconds";
        public const string DefuseSecondsItem = "defuseSeconds";
        public const string FuseSecondsItem = "fuseSeconds";
        public const string WinsToWinItem = "winsToWin";
        public const string PreparationSecondsItem = "preparationSeconds";
        public const string SwapEveryRoundsItem = "swapEveryRounds";
        public const string BombPickupRadiusItem = "bombPickupRadius";
        public const string DefuseRadiusItem = "defuseRadius";

        public const string FreeForAllItem = "freeForAll";
        public const string PointsPerKillItem = "pointsPerKill";

        public const string KillConfirmedItem = "killConfirmed";
        public const string TagLifetimeSecondsItem = "tagLifetimeSeconds";
        public const string PointsPerConfirmItem = "pointsPerConfirm";
        public const string TagPickupRadiusItem = "tagPickupRadius";

        public const string GunGameItem = "gunGame";
        public const string TierWeaponIdsItem = "tierWeaponIds";
        public const string MeleeWeaponIdsItem = "meleeWeaponIds";
        public const string DemotesOnMeleeItem = "demotesOnMelee";

        public const string VoteKickItem = "voteKick";
        public const string EnabledItem = "enabled";
        public const string RequiredYesFractionItem = "requiredYesFraction";
        public const string VoteSecondsItem = "voteSeconds";
        public const string CooldownSecondsItem = "cooldownSeconds";

        public const string PayloadItem = "payload";
        public const string CartSpeedItem = "cartSpeed";
        public const string MinPushersItem = "minPushers";
        public const string PusherSpeedMultipliersItem = "pusherSpeedMultipliers";
        public const string RollbackDelaySecondsItem = "rollbackDelaySeconds";
        public const string RollbackSpeedItem = "rollbackSpeed";
        public const string CheckpointBonusSecondsItem = "checkpointBonusSeconds";

        public const string EconomyItem = "economy";
        public const string StartMoneyItem = "startMoney";
        public const string MaxMoneyItem = "maxMoney";
        public const string KillRewardItem = "killReward";
        public const string RoundWinRewardItem = "roundWinReward";
        public const string RoundLossRewardItem = "roundLossReward";
        public const string LossStreakBonusItem = "lossStreakBonus";
        public const string MaxLossRewardItem = "maxLossReward";

        public const string MotorItem = "motor";
        public const string WalkSpeedItem = "walkSpeed";
        public const string SprintSpeedItem = "sprintSpeed";
        public const string JumpHeightItem = "jumpHeight";
        public const string GravityItem = "gravity";
        public const string AirControlItem = "airControl";
        public const string CrouchSpeedItem = "crouchSpeed";
        public const string SafeFallSpeedItem = "safeFallSpeed";
        public const string LethalFallSpeedItem = "lethalFallSpeed";
        public const string DodgeDistanceItem = "dodgeDistance";
        public const string DodgeSecondsItem = "dodgeSeconds";
        public const string VaultMaxHeightItem = "vaultMaxHeight";
        public const string MantleMaxHeightItem = "mantleMaxHeight";
        public const string MantleSpeedItem = "mantleSpeed";
        public const string DodgeCooldownSecondsItem = "dodgeCooldownSeconds";
        public const string VaultSpeedItem = "vaultSpeed";
        public const string VaultReachItem = "vaultReach";
        public const string VaultMaxAngleDegreesItem = "vaultMaxAngleDegrees";
        public const string VaultMinHeightItem = "vaultMinHeight";
        public const string VaultMaxDepthItem = "vaultMaxDepth";
        public const string CoverMaxEnterDistanceItem = "coverMaxEnterDistance";
        public const string CoverMaxEnterAngleDegreesItem = "coverMaxEnterAngleDegrees";
        public const string CoverLowWallHeightItem = "coverLowWallHeight";
        public const string CoverPeekSideDistanceItem = "coverPeekSideDistance";
        public const string CoverEnterSecondsItem = "coverEnterSeconds";
        public const string CoverBackHoldSecondsItem = "coverBackHoldSeconds";
        public const string CoverPeekSlideSecondsItem = "coverPeekSlideSeconds";
        public const string CoverEnterSpeedItem = "coverEnterSpeed";
        public const string CoverMinWallHeightItem = "coverMinWallHeight";
        public const string LadderTopEntryDepthItem = "ladderTopEntryDepth";
        public const string LadderMinApproachDotItem = "ladderMinApproachDot";
        public const string LadderMinApproachSpeedItem = "ladderMinApproachSpeed";
        public const string WalkForwardScaleItem = "walkForwardScale";
        public const string WalkBackwardScaleItem = "walkBackwardScale";
        public const string WalkSideScaleItem = "walkSideScale";
        public const string SprintForwardScaleItem = "sprintForwardScale";
        public const string SprintBackwardScaleItem = "sprintBackwardScale";
        public const string SprintSideScaleItem = "sprintSideScale";
        public const string CarryWeightSourceItem = "carryWeightSource";
        public const string CarryMultiplierSourceItem = "carryMultiplierSource";
        public const string CarryFreeWeightItem = "carryFreeWeight";
        public const string CarrySpeedLossPerKgItem = "carrySpeedLossPerKg";
        public const string CarryMinWeightMultiplierItem = "carryMinWeightMultiplier";
        public const string CarryWalkItem = "carryWalk";
        public const string CarrySprintItem = "carrySprint";
        public const string CarryCrouchItem = "carryCrouch";
        public const string CarryProneItem = "carryProne";
        public const string StandingHeightItem = "standingHeight";
        public const string StandingCenterYItem = "standingCenterY";
        public const string StandingEyeYItem = "standingEyeY";
        public const string CrouchHeightItem = "crouchHeight";
        public const string RadiusItem = "radius";
        public const string SlopeLimitDegreesItem = "slopeLimitDegrees";
        public const string StepOffsetItem = "stepOffset";
        public const string SkinWidthItem = "skinWidth";
        public const string ProneHeightItem = "proneHeight";
        public const string ProneSpeedItem = "proneSpeed";
        public const string DiveDistanceItem = "diveDistance";
        public const string DiveSecondsItem = "diveSeconds";
        public const string LeanSideOffsetItem = "leanSideOffset";
        public const string LeanAngleDegreesItem = "leanAngleDegrees";
        public const string LeanSpeedItem = "leanSpeed";
        public const string AirJumpsItem = "airJumps";
        public const string AirJumpSpeedItem = "airJumpSpeed";
        public const string JumpGraceSecondsItem = "jumpGraceSeconds";
        public const string SwimSpeedItem = "swimSpeed";
        public const string SwimBuoyancyItem = "swimBuoyancy";
        public const string SwimBreathSecondsItem = "swimBreathSeconds";
        public const string WallRunEnabledItem = "wallRunEnabled";
        public const string WallRunMaxSecondsItem = "wallRunMaxSeconds";
        public const string WallRunMinSpeedItem = "wallRunMinSpeed";
        public const string WallRunGravityScaleItem = "wallRunGravityScale";
        public const string WallRunJumpOffSpeedItem = "wallRunJumpOffSpeed";
        public const string ParachuteMinOpenHeightItem = "parachuteMinOpenHeight";
        public const string ParachuteDescentSpeedItem = "parachuteDescentSpeed";
        public const string ParachuteHorizontalSpeedItem = "parachuteHorizontalSpeed";
        public const string ParachuteSteerAccelerationItem = "parachuteSteerAcceleration";

        private const string _kind = "match rules JSON";

        private static readonly string[] _topItems = { NameItem, RulesItem, MatchItem, MotorItem, LoadoutItem, CatalogItem, WeaponsItem };

        private static readonly string[] _motorItems =
        {
            WalkSpeedItem, SprintSpeedItem, JumpHeightItem, GravityItem, AirControlItem, CrouchSpeedItem, SafeFallSpeedItem, LethalFallSpeedItem,
            DodgeDistanceItem, DodgeSecondsItem, VaultMaxHeightItem, MantleMaxHeightItem, MantleSpeedItem,
            DodgeCooldownSecondsItem, VaultSpeedItem, VaultReachItem, VaultMaxAngleDegreesItem, VaultMinHeightItem, VaultMaxDepthItem,
            CoverMaxEnterDistanceItem, CoverMaxEnterAngleDegreesItem, CoverLowWallHeightItem, CoverPeekSideDistanceItem,
            CoverEnterSecondsItem, CoverBackHoldSecondsItem, CoverPeekSlideSecondsItem, CoverEnterSpeedItem,
            CoverMinWallHeightItem,
            LadderTopEntryDepthItem, LadderMinApproachDotItem, LadderMinApproachSpeedItem,
            WalkForwardScaleItem, WalkBackwardScaleItem, WalkSideScaleItem, SprintForwardScaleItem, SprintBackwardScaleItem, SprintSideScaleItem,
            CarryWeightSourceItem, CarryMultiplierSourceItem, CarryFreeWeightItem, CarrySpeedLossPerKgItem, CarryMinWeightMultiplierItem,
            CarryWalkItem, CarrySprintItem, CarryCrouchItem, CarryProneItem,
            StandingHeightItem, StandingCenterYItem,
            StandingEyeYItem, CrouchHeightItem, RadiusItem, SlopeLimitDegreesItem, StepOffsetItem, SkinWidthItem,
            ProneHeightItem, ProneSpeedItem, DiveDistanceItem, DiveSecondsItem,
            LeanSideOffsetItem, LeanAngleDegreesItem, LeanSpeedItem, AirJumpsItem, AirJumpSpeedItem, JumpGraceSecondsItem,
            SwimSpeedItem, SwimBuoyancyItem, SwimBreathSecondsItem,
            WallRunEnabledItem, WallRunMaxSecondsItem, WallRunMinSpeedItem, WallRunGravityScaleItem, WallRunJumpOffSpeedItem,
            ParachuteMinOpenHeightItem, ParachuteDescentSpeedItem, ParachuteHorizontalSpeedItem, ParachuteSteerAccelerationItem,
        };
        private static readonly string[] _loadoutItems = { NameItem, SwitchSecondsItem, SlotsItem, WeaponsItem, AbilitiesItem };
        private static readonly string[] _catalogItems = { NameItem, WeaponsItem };

        private static readonly string[] _matchItems =
        {
            ScoreLimitItem, TimeLimitSecondsItem, RespawnSecondsItem, RestartDelaySecondsItem, MaxHealthItem, MinEnemySpawnDistanceItem,
            ReconnectGraceSecondsItem, MaxShotOriginOffsetItem, DominationScoreLimitItem, CaptureSecondsItem, PointsPerSecondItem,
            MaxCapturersItem, MaxPlayersItem, MapPoolItem, MapVoteSecondsItem, MaxTeamSizeDifferenceItem, PrivateMatchItem, JoinCodeItem,
            ChatIntervalSecondsItem, MaxChatLengthItem, MaxDisplayNameLengthItem, RoundWinsToWinItem, RoundPreparationSecondsItem,
            RoundTimeLimitSecondsItem, WeatherIdItem, TimeOfDayIdItem,
            CaptureTheFlagItem, HardpointItem, SearchAndDestroyItem, FreeForAllItem, KillConfirmedItem, GunGameItem,
            VoteKickItem, PayloadItem, EconomyItem,
        };

        private static readonly string[] _captureTheFlagItems =
        {
            ScoreLimitItem, TimeLimitSecondsItem, RespawnSecondsItem, PickupRadiusItem, CaptureRadiusItem, ReturnSecondsItem, PointsPerCaptureItem,
        };

        // 拠点の数はマップで決まるので項目に持たない
        private static readonly string[] _hardpointItems =
        {
            RotateSecondsItem, PointsPerSecondItem, ScoreLimitItem, TimeLimitSecondsItem, RespawnSecondsItem,
        };

        // 爆破地点の数はマップで決まるので項目に持たない
        private static readonly string[] _searchAndDestroyItems =
        {
            RoundSecondsItem, PlantSecondsItem, DefuseSecondsItem, FuseSecondsItem, WinsToWinItem, PreparationSecondsItem, SwapEveryRoundsItem,
            BombPickupRadiusItem, DefuseRadiusItem,
        };

        private static readonly string[] _freeForAllItems = { ScoreLimitItem, TimeLimitSecondsItem, RespawnSecondsItem, PointsPerKillItem };

        private static readonly string[] _killConfirmedItems =
        {
            ScoreLimitItem, TimeLimitSecondsItem, RespawnSecondsItem, TagLifetimeSecondsItem, PointsPerConfirmItem, TagPickupRadiusItem,
        };

        private static readonly string[] _gunGameItems =
        {
            TimeLimitSecondsItem, RespawnSecondsItem, TierWeaponIdsItem, MeleeWeaponIdsItem, DemotesOnMeleeItem,
        };

        private static readonly string[] _voteKickItems = { EnabledItem, RequiredYesFractionItem, VoteSecondsItem, CooldownSecondsItem };

        // 道筋と中継点の位置はマップで決まるので項目に持たない
        private static readonly string[] _payloadItems =
        {
            CartSpeedItem, MinPushersItem, PusherSpeedMultipliersItem, RollbackDelaySecondsItem, RollbackSpeedItem, CheckpointBonusSecondsItem,
            TimeLimitSecondsItem,
        };

        private static readonly string[] _economyItems =
        {
            StartMoneyItem, MaxMoneyItem, KillRewardItem, RoundWinRewardItem, RoundLossRewardItem, LossStreakBonusItem, MaxLossRewardItem,
        };

        public NetMatchFile(
            string name,
            GameRules rules,
            NetMatchSettings settings,
            string loadoutName,
            LoadoutRule loadoutRule,
            IReadOnlyList<WeaponSpec> loadoutWeapons,
            float switchSeconds,
            string catalogName,
            WeaponCatalog catalog,
            NetMotorSettings motor = null,
            IReadOnlyList<string> loadoutAbilities = null)
        {
            Motor = motor ?? NetMotorSettings.Default;
            Name = name ?? string.Empty;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            LoadoutName = loadoutName ?? string.Empty;
            LoadoutRule = loadoutRule ?? throw new ArgumentNullException(nameof(loadoutRule));
            LoadoutWeapons = new List<WeaponSpec>(loadoutWeapons ?? throw new ArgumentNullException(nameof(loadoutWeapons))).AsReadOnly();
            SwitchSeconds = switchSeconds;
            CatalogName = catalog == null ? null : catalogName ?? string.Empty;
            Catalog = catalog;
            LoadoutAbilities = CopyAbilities(loadoutAbilities);

            var objectiveProblem = ObjectiveProblem(Rules, Settings);
            if (objectiveProblem != null)
            {
                throw new ArgumentException(objectiveProblem, nameof(settings));
            }

            // 作れないロードアウトを試合の開始まで持ち越さないよう、ここで一度組んで確かめる
            CreateLoadout();
        }

        public string Name { get; }

        public GameRules Rules { get; }

        public NetMatchSettings Settings { get; }

        public NetMotorSettings Motor { get; }

        public string LoadoutName { get; }

        public LoadoutRule LoadoutRule { get; }

        public IReadOnlyList<WeaponSpec> LoadoutWeapons { get; }

        public float SwitchSeconds { get; }

        public string CatalogName { get; }

        public WeaponCatalog Catalog { get; }

        // null と空の並びを分けるのは、項目の無いファイルではロードアウトのアセットの能力を使い、空なら能力を持たせないため。
        // DeployCatalog は別のファイル (deployables) から読むので、ここでは一覧にある Id かを確かめない
        public IReadOnlyList<string> LoadoutAbilities { get; }

        // 試合ごとに作り直すのは、ServerLoadout がプレイヤーごとの選んだ並びと連射の制限を持つため。
        public ServerLoadout CreateLoadout()
        {
            return new ServerLoadout(LoadoutRule, LoadoutWeapons, Catalog, SwitchSeconds);
        }

        public static NetMatchFile Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var rulesNode = top.Find(RulesItem);
            var rules = rulesNode == null ? GameRules.Default : GameRulesJson.FromNode(rulesNode, source, RulesItem + ".");
            var settings = ReadSettings(top.Object(MatchItem, _matchItems));
            var objectiveProblem = ObjectiveProblem(rules, settings);
            if (objectiveProblem != null)
            {
                var item = MatchItem + "." + GunGameItem + "." + TierWeaponIdsItem;
                throw top.Rejected(item, objectiveProblem);
            }

            var motor = ReadMotor(top.Object(MotorItem, _motorItems));
            var weapons = ReadWeapons(top);

            var loadout = top.Object(LoadoutItem, _loadoutItems)
                ?? throw new ServerDataJsonException(source, LoadoutItem, $"{_kind} '{source}' has no item '{LoadoutItem}'");
            var rule = ReadRule(loadout);
            var loadoutWeapons = Pick(loadout, weapons);
            var switchSeconds = loadout.ReadFloat(SwitchSecondsItem, 0f);
            var abilities = ReadAbilities(loadout);

            var catalogFields = top.Object(CatalogItem, _catalogItems);
            WeaponCatalog catalog = null;
            if (catalogFields != null)
            {
                try
                {
                    catalog = new WeaponCatalog(Pick(catalogFields, weapons));
                }
                catch (ArgumentException e)
                {
                    throw catalogFields.Rejected(CatalogItem, e.Message, e);
                }
            }

            try
            {
                return new NetMatchFile(
                    top.ReadString(NameItem, string.Empty),
                    rules,
                    settings,
                    loadout.ReadString(NameItem, string.Empty),
                    rule,
                    loadoutWeapons,
                    switchSeconds,
                    catalogFields?.ReadString(NameItem, string.Empty),
                    catalog,
                    motor,
                    abilities);
            }
            catch (ArgumentException e)
            {
                throw loadout.Rejected(LoadoutItem, e.Message, e);
            }
        }

        public static string Write(NetMatchFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var writer = new JsonWriter();
            writer.String(NameItem, file.Name);
            writer.Embed(RulesItem, GameRulesJson.Write(file.Rules));

            var s = file.Settings;
            writer.BeginObject(MatchItem);
            writer.Int(ScoreLimitItem, s.ScoreLimit);
            writer.Float(TimeLimitSecondsItem, s.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, s.RespawnSeconds);
            writer.Float(RestartDelaySecondsItem, s.RestartDelaySeconds);
            writer.Int(MaxHealthItem, s.MaxHealth);
            writer.Float(MinEnemySpawnDistanceItem, s.MinEnemySpawnDistance);
            writer.Float(ReconnectGraceSecondsItem, s.ReconnectGraceSeconds);
            writer.Float(MaxShotOriginOffsetItem, s.MaxShotOriginOffset);
            writer.Int(DominationScoreLimitItem, s.DominationScoreLimit);
            writer.Float(CaptureSecondsItem, s.CaptureSeconds);
            writer.Float(PointsPerSecondItem, s.PointsPerSecond);
            writer.Int(MaxCapturersItem, s.MaxCapturers);
            writer.Int(MaxPlayersItem, s.MaxPlayers);
            writer.StringArray(MapPoolItem, s.MapPool);
            writer.Float(MapVoteSecondsItem, s.MapVoteSeconds);
            writer.Int(MaxTeamSizeDifferenceItem, s.MaxTeamSizeDifference);
            writer.Bool(PrivateMatchItem, s.PrivateMatch);
            writer.String(JoinCodeItem, s.JoinCode ?? string.Empty);
            writer.Float(ChatIntervalSecondsItem, s.ChatIntervalSeconds);
            writer.Int(MaxChatLengthItem, s.MaxChatLength);

            // 既定値のときに書かないのは、項目を足す前に書き出したルールのファイルを書き出し直さずにそのまま使えるようにするため
            if (s.MaxDisplayNameLength != NetMatchSettings.DefaultMaxDisplayNameLength)
            {
                writer.Int(MaxDisplayNameLengthItem, s.MaxDisplayNameLength);
            }

            writer.Int(RoundWinsToWinItem, s.RoundWinsToWin);
            writer.Float(RoundPreparationSecondsItem, s.RoundPreparationSeconds);
            writer.Float(RoundTimeLimitSecondsItem, s.RoundTimeLimitSeconds);
            writer.String(WeatherIdItem, s.WeatherId ?? string.Empty);
            writer.String(TimeOfDayIdItem, s.TimeOfDayId ?? string.Empty);

            var ctf = s.CaptureTheFlag;
            writer.BeginObject(CaptureTheFlagItem);
            writer.Int(ScoreLimitItem, ctf.ScoreLimit);
            writer.Float(TimeLimitSecondsItem, ctf.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, ctf.RespawnSeconds);
            writer.Float(PickupRadiusItem, ctf.PickupRadius);
            writer.Float(CaptureRadiusItem, ctf.CaptureRadius);
            writer.Float(ReturnSecondsItem, ctf.ReturnSeconds);
            writer.Int(PointsPerCaptureItem, ctf.PointsPerCapture);
            writer.EndObject();

            var hardpoint = s.Hardpoint;
            writer.BeginObject(HardpointItem);
            writer.Float(RotateSecondsItem, hardpoint.RotateSeconds);
            writer.Float(PointsPerSecondItem, hardpoint.PointsPerSecond);
            writer.Int(ScoreLimitItem, hardpoint.ScoreLimit);
            writer.Float(TimeLimitSecondsItem, hardpoint.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, hardpoint.RespawnSeconds);
            writer.EndObject();

            var snd = s.SearchAndDestroy;
            writer.BeginObject(SearchAndDestroyItem);
            writer.Float(RoundSecondsItem, snd.RoundSeconds);
            writer.Float(PlantSecondsItem, snd.PlantSeconds);
            writer.Float(DefuseSecondsItem, snd.DefuseSeconds);
            writer.Float(FuseSecondsItem, snd.FuseSeconds);
            writer.Int(WinsToWinItem, snd.WinsToWin);
            writer.Float(PreparationSecondsItem, snd.PreparationSeconds);
            writer.Int(SwapEveryRoundsItem, snd.SwapEveryRounds);
            writer.Float(BombPickupRadiusItem, snd.BombPickupRadius);
            writer.Float(DefuseRadiusItem, snd.DefuseRadius);
            writer.EndObject();

            var ffa = s.FreeForAll;
            writer.BeginObject(FreeForAllItem);
            writer.Int(ScoreLimitItem, ffa.ScoreLimit);
            writer.Float(TimeLimitSecondsItem, ffa.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, ffa.RespawnSeconds);
            writer.Int(PointsPerKillItem, ffa.PointsPerKill);
            writer.EndObject();

            var kc = s.KillConfirmed;
            writer.BeginObject(KillConfirmedItem);
            writer.Int(ScoreLimitItem, kc.ScoreLimit);
            writer.Float(TimeLimitSecondsItem, kc.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, kc.RespawnSeconds);
            writer.Float(TagLifetimeSecondsItem, kc.TagLifetimeSeconds);
            writer.Int(PointsPerConfirmItem, kc.PointsPerConfirm);
            writer.Float(TagPickupRadiusItem, kc.TagPickupRadius);
            writer.EndObject();

            var gunGame = s.GunGame;
            writer.BeginObject(GunGameItem);
            writer.Float(TimeLimitSecondsItem, gunGame.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, gunGame.RespawnSeconds);
            writer.StringArray(TierWeaponIdsItem, gunGame.TierWeaponIds);
            writer.StringArray(MeleeWeaponIdsItem, gunGame.MeleeWeaponIds);
            writer.Bool(DemotesOnMeleeItem, gunGame.DemotesOnMelee);
            writer.EndObject();

            var voteKick = s.VoteKick;
            writer.BeginObject(VoteKickItem);
            writer.Bool(EnabledItem, voteKick.Enabled);
            writer.Float(RequiredYesFractionItem, voteKick.RequiredYesFraction);
            writer.Float(VoteSecondsItem, voteKick.VoteSeconds);
            writer.Float(CooldownSecondsItem, voteKick.CooldownSeconds);
            writer.EndObject();

            var payload = s.Payload;
            writer.BeginObject(PayloadItem);
            writer.Float(CartSpeedItem, payload.CartSpeed);
            writer.Int(MinPushersItem, payload.MinPushers);
            writer.FloatArray(PusherSpeedMultipliersItem, payload.PusherSpeedMultipliers);
            writer.Float(RollbackDelaySecondsItem, payload.RollbackDelaySeconds);
            writer.Float(RollbackSpeedItem, payload.RollbackSpeed);
            writer.Float(CheckpointBonusSecondsItem, payload.CheckpointBonusSeconds);
            writer.Float(TimeLimitSecondsItem, payload.TimeLimitSeconds);
            writer.EndObject();

            var economy = s.Economy;
            writer.BeginObject(EconomyItem);
            writer.Int(StartMoneyItem, economy.StartMoney);
            writer.Int(MaxMoneyItem, economy.MaxMoney);
            writer.Int(KillRewardItem, economy.KillReward);
            writer.Int(RoundWinRewardItem, economy.RoundWinReward);
            writer.Int(RoundLossRewardItem, economy.RoundLossReward);
            writer.Int(LossStreakBonusItem, economy.LossStreakBonus);
            writer.Int(MaxLossRewardItem, economy.MaxLossReward);
            writer.EndObject();
            writer.EndObject();

            var motor = file.Motor.Motor;
            var stance = file.Motor.Stance;
            var mover = file.Motor.Mover;
            writer.BeginObject(MotorItem);
            writer.Float(WalkSpeedItem, motor.WalkSpeed);
            writer.Float(SprintSpeedItem, motor.SprintSpeed);
            writer.Float(JumpHeightItem, motor.JumpHeight);
            writer.Float(GravityItem, motor.Gravity);
            writer.Float(AirControlItem, motor.AirControl);
            writer.Float(CrouchSpeedItem, motor.CrouchSpeed);
            writer.Float(SafeFallSpeedItem, motor.SafeFallSpeed);
            writer.Float(LethalFallSpeedItem, motor.LethalFallSpeed);
            writer.Float(DodgeDistanceItem, motor.DodgeDistance);
            writer.Float(DodgeSecondsItem, motor.DodgeSeconds);
            writer.Float(VaultMaxHeightItem, motor.VaultMaxHeight);
            writer.Float(MantleMaxHeightItem, motor.MantleMaxHeight);
            writer.Float(MantleSpeedItem, motor.MantleSpeed);
            writer.Float(DodgeCooldownSecondsItem, motor.DodgeCooldownSeconds);
            writer.Float(VaultSpeedItem, motor.VaultSpeed);
            writer.Float(VaultReachItem, motor.VaultReach);
            writer.Float(VaultMaxAngleDegreesItem, motor.VaultMaxAngleDegrees);
            writer.Float(VaultMinHeightItem, motor.VaultMinHeight);
            writer.Float(VaultMaxDepthItem, motor.VaultMaxDepth);
            writer.Float(CoverMaxEnterDistanceItem, motor.CoverMaxEnterDistance);
            writer.Float(CoverMaxEnterAngleDegreesItem, motor.CoverMaxEnterAngleDegrees);
            writer.Float(CoverLowWallHeightItem, motor.CoverLowWallHeight);
            writer.Float(CoverPeekSideDistanceItem, motor.CoverPeekSideDistance);
            writer.Float(CoverEnterSecondsItem, motor.CoverEnterSeconds);
            writer.Float(CoverBackHoldSecondsItem, motor.CoverBackHoldSeconds);
            writer.Float(CoverPeekSlideSecondsItem, motor.CoverPeekSlideSeconds);
            writer.Float(CoverEnterSpeedItem, motor.CoverEnterSpeed);
            writer.Float(CoverMinWallHeightItem, motor.CoverMinWallHeight);
            writer.Float(LadderTopEntryDepthItem, motor.LadderTopEntryDepth);
            writer.Float(LadderMinApproachDotItem, motor.LadderMinApproachDot);
            writer.Float(LadderMinApproachSpeedItem, motor.LadderMinApproachSpeed);
            writer.Float(WalkForwardScaleItem, motor.WalkDirection.Forward);
            writer.Float(WalkBackwardScaleItem, motor.WalkDirection.Backward);
            writer.Float(WalkSideScaleItem, motor.WalkDirection.Side);
            writer.Float(SprintForwardScaleItem, motor.SprintDirection.Forward);
            writer.Float(SprintBackwardScaleItem, motor.SprintDirection.Backward);
            writer.Float(SprintSideScaleItem, motor.SprintDirection.Side);
            var carry = motor.CarryLoad;
            writer.Enum(CarryWeightSourceItem, carry.WeightSource);
            writer.Enum(CarryMultiplierSourceItem, carry.MultiplierSource);
            writer.Float(CarryFreeWeightItem, carry.FreeWeight);
            writer.Float(CarrySpeedLossPerKgItem, carry.SpeedLossPerKg);
            writer.Float(CarryMinWeightMultiplierItem, carry.MinWeightMultiplier);
            writer.Bool(CarryWalkItem, (carry.Gaits & CarryGaits.Walk) != 0);
            writer.Bool(CarrySprintItem, (carry.Gaits & CarryGaits.Sprint) != 0);
            writer.Bool(CarryCrouchItem, (carry.Gaits & CarryGaits.Crouch) != 0);
            writer.Bool(CarryProneItem, (carry.Gaits & CarryGaits.Prone) != 0);
            writer.Float(StandingHeightItem, stance.Standing.Height);
            writer.Float(StandingCenterYItem, stance.Standing.CenterY);
            writer.Float(StandingEyeYItem, stance.Standing.EyeY);
            writer.Float(CrouchHeightItem, stance.Crouched.Height);
            writer.Float(RadiusItem, stance.Radius);
            writer.Float(SlopeLimitDegreesItem, mover.SlopeLimitDegrees);
            writer.Float(StepOffsetItem, mover.StepOffset);
            writer.Float(SkinWidthItem, mover.SkinWidth);
            writer.Float(ProneHeightItem, stance.Prone.Height);
            writer.Float(ProneSpeedItem, file.Motor.Prone.Speed);
            writer.Float(DiveDistanceItem, file.Motor.Prone.DiveDistance);
            writer.Float(DiveSecondsItem, file.Motor.Prone.DiveSeconds);
            writer.Float(LeanSideOffsetItem, motor.LeanSideOffset);
            writer.Float(LeanAngleDegreesItem, motor.LeanAngleDegrees);
            writer.Float(LeanSpeedItem, motor.LeanSpeed);
            writer.Int(AirJumpsItem, motor.AirJumps);
            writer.Float(AirJumpSpeedItem, motor.AirJumpSpeed);
            writer.Float(JumpGraceSecondsItem, motor.JumpGraceSeconds);
            var swim = file.Motor.Swim;
            writer.Float(SwimSpeedItem, swim.Speed);
            writer.Float(SwimBuoyancyItem, swim.Buoyancy);
            writer.Float(SwimBreathSecondsItem, swim.BreathSeconds);
            var wallRun = file.Motor.WallRun;
            writer.Bool(WallRunEnabledItem, wallRun.Enabled);
            writer.Float(WallRunMaxSecondsItem, wallRun.MaxSeconds);
            writer.Float(WallRunMinSpeedItem, wallRun.MinSpeed);
            writer.Float(WallRunGravityScaleItem, wallRun.GravityScale);
            writer.Float(WallRunJumpOffSpeedItem, wallRun.JumpOffSpeed);
            var parachute = file.Motor.Parachute;
            writer.Float(ParachuteMinOpenHeightItem, parachute.MinOpenHeight);
            writer.Float(ParachuteDescentSpeedItem, parachute.DescentSpeed);
            writer.Float(ParachuteHorizontalSpeedItem, parachute.HorizontalSpeed);
            writer.Float(ParachuteSteerAccelerationItem, parachute.SteerAcceleration);
            writer.EndObject();

            writer.BeginObject(LoadoutItem);
            writer.String(NameItem, file.LoadoutName);
            writer.Float(SwitchSecondsItem, file.SwitchSeconds);
            writer.BeginArray(SlotsItem);
            foreach (var slot in file.LoadoutRule.Slots)
            {
                var names = new List<string>();
                foreach (var category in slot)
                {
                    names.Add(JsonWriter.Quote(category.ToString()));
                }

                writer.Element("[" + string.Join(", ", names) + "]");
            }

            writer.EndArray();
            writer.StringArray(WeaponsItem, Ids(file.LoadoutWeapons));
            if (file.LoadoutAbilities != null)
            {
                writer.StringArray(AbilitiesItem, file.LoadoutAbilities);
            }

            writer.EndObject();

            var all = new List<WeaponSpec>(file.LoadoutWeapons);
            if (file.Catalog != null)
            {
                writer.BeginObject(CatalogItem);
                writer.String(NameItem, file.CatalogName);
                writer.StringArray(WeaponsItem, Ids(file.Catalog.All));
                writer.EndObject();
                foreach (var weapon in file.Catalog.All)
                {
                    if (!ContainsId(all, weapon.Id))
                    {
                        all.Add(weapon);
                    }
                }
            }

            writer.BeginArray(WeaponsItem);
            foreach (var weapon in all)
            {
                writer.BeginElement();
                WeaponSpecJson.Write(writer, weapon);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }

        private static NetMatchSettings ReadSettings(ServerDataJson.Fields match)
        {
            if (match == null)
            {
                return NetMatchSettings.Default;
            }

            var d = NetMatchSettings.Default;
            var captureTheFlag = ReadCaptureTheFlag(match.Object(CaptureTheFlagItem, _captureTheFlagItems), d.CaptureTheFlag);
            var hardpoint = ReadHardpoint(match.Object(HardpointItem, _hardpointItems), d.Hardpoint);
            var searchAndDestroy = ReadSearchAndDestroy(match.Object(SearchAndDestroyItem, _searchAndDestroyItems), d.SearchAndDestroy);
            var freeForAll = ReadFreeForAll(match.Object(FreeForAllItem, _freeForAllItems), d.FreeForAll);
            var killConfirmed = ReadKillConfirmed(match.Object(KillConfirmedItem, _killConfirmedItems), d.KillConfirmed);
            var gunGame = ReadGunGame(match.Object(GunGameItem, _gunGameItems), d.GunGame);
            var voteKick = ReadVoteKick(match.Object(VoteKickItem, _voteKickItems), d.VoteKick);
            var payload = ReadPayload(match.Object(PayloadItem, _payloadItems), d.Payload);
            var economy = ReadEconomy(match.Object(EconomyItem, _economyItems), d.Economy);
            try
            {
                return new NetMatchSettings(
                    match.ReadInt(ScoreLimitItem, d.ScoreLimit),
                    match.ReadFloat(TimeLimitSecondsItem, d.TimeLimitSeconds),
                    match.ReadFloat(RespawnSecondsItem, d.RespawnSeconds),
                    match.ReadFloat(RestartDelaySecondsItem, d.RestartDelaySeconds),
                    match.ReadInt(MaxHealthItem, d.MaxHealth),
                    match.ReadFloat(MinEnemySpawnDistanceItem, d.MinEnemySpawnDistance),
                    match.ReadFloat(ReconnectGraceSecondsItem, d.ReconnectGraceSeconds),
                    match.ReadFloat(MaxShotOriginOffsetItem, d.MaxShotOriginOffset),
                    match.ReadInt(DominationScoreLimitItem, d.DominationScoreLimit),
                    match.ReadFloat(CaptureSecondsItem, d.CaptureSeconds),
                    match.ReadFloat(PointsPerSecondItem, d.PointsPerSecond),
                    match.ReadInt(MaxCapturersItem, d.MaxCapturers),
                    match.ReadInt(MaxPlayersItem, d.MaxPlayers),
                    ReadMapPool(match),
                    match.ReadFloat(MapVoteSecondsItem, d.MapVoteSeconds),
                    match.ReadInt(MaxTeamSizeDifferenceItem, d.MaxTeamSizeDifference),
                    match.ReadBool(PrivateMatchItem, d.PrivateMatch),
                    match.ReadString(JoinCodeItem, d.JoinCode),
                    match.ReadFloat(ChatIntervalSecondsItem, d.ChatIntervalSeconds),
                    match.ReadInt(MaxChatLengthItem, d.MaxChatLength),
                    match.ReadInt(RoundWinsToWinItem, d.RoundWinsToWin),
                    match.ReadFloat(RoundPreparationSecondsItem, d.RoundPreparationSeconds),
                    match.ReadFloat(RoundTimeLimitSecondsItem, d.RoundTimeLimitSeconds),
                    match.ReadString(WeatherIdItem, d.WeatherId),
                    match.ReadString(TimeOfDayIdItem, d.TimeOfDayId),
                    captureTheFlag,
                    hardpoint,
                    searchAndDestroy,
                    freeForAll,
                    killConfirmed,
                    gunGame,
                    voteKick,
                    payload,
                    economy,
                    match.ReadInt(MaxDisplayNameLengthItem, d.MaxDisplayNameLength));
            }
            catch (ArgumentException e)
            {
                // NetMatchSettings の引数名は項目名と同じ
                throw match.Rejected(e.ParamName == null ? match.Path : match.ItemPath(e.ParamName), e.Message, e);
            }
        }

        // 各モードの Spec の引数名は項目名と同じ
        private static ServerDataJsonException RejectedSpec(ServerDataJson.Fields fields, ArgumentException e)
        {
            return fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
        }

        private static CaptureTheFlagSpec ReadCaptureTheFlag(ServerDataJson.Fields fields, CaptureTheFlagSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new CaptureTheFlagSpec(
                    fields.ReadInt(ScoreLimitItem, fallback.ScoreLimit),
                    fields.ReadFloat(TimeLimitSecondsItem, fallback.TimeLimitSeconds),
                    fields.ReadFloat(RespawnSecondsItem, fallback.RespawnSeconds),
                    fields.ReadFloat(PickupRadiusItem, fallback.PickupRadius),
                    fields.ReadFloat(CaptureRadiusItem, fallback.CaptureRadius),
                    fields.ReadFloat(ReturnSecondsItem, fallback.ReturnSeconds),
                    fields.ReadInt(PointsPerCaptureItem, fallback.PointsPerCapture));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static HardpointSpec ReadHardpoint(ServerDataJson.Fields fields, HardpointSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new HardpointSpec(
                    fallback.PointCount,
                    fields.ReadFloat(RotateSecondsItem, fallback.RotateSeconds),
                    fields.ReadFloat(PointsPerSecondItem, fallback.PointsPerSecond),
                    fields.ReadInt(ScoreLimitItem, fallback.ScoreLimit),
                    fields.ReadFloat(TimeLimitSecondsItem, fallback.TimeLimitSeconds),
                    fields.ReadFloat(RespawnSecondsItem, fallback.RespawnSeconds));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static SearchAndDestroySpec ReadSearchAndDestroy(ServerDataJson.Fields fields, SearchAndDestroySpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new SearchAndDestroySpec(
                    fallback.SiteCount,
                    fields.ReadFloat(RoundSecondsItem, fallback.RoundSeconds),
                    fields.ReadFloat(PlantSecondsItem, fallback.PlantSeconds),
                    fields.ReadFloat(DefuseSecondsItem, fallback.DefuseSeconds),
                    fields.ReadFloat(FuseSecondsItem, fallback.FuseSeconds),
                    fields.ReadInt(WinsToWinItem, fallback.WinsToWin),
                    fields.ReadFloat(PreparationSecondsItem, fallback.PreparationSeconds),
                    fields.ReadInt(SwapEveryRoundsItem, fallback.SwapEveryRounds),
                    fields.ReadFloat(BombPickupRadiusItem, fallback.BombPickupRadius),
                    fields.ReadFloat(DefuseRadiusItem, fallback.DefuseRadius));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static FreeForAllSpec ReadFreeForAll(ServerDataJson.Fields fields, FreeForAllSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new FreeForAllSpec(
                    fields.ReadInt(ScoreLimitItem, fallback.ScoreLimit),
                    fields.ReadFloat(TimeLimitSecondsItem, fallback.TimeLimitSeconds),
                    fields.ReadFloat(RespawnSecondsItem, fallback.RespawnSeconds),
                    fields.ReadInt(PointsPerKillItem, fallback.PointsPerKill));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static KillConfirmedSpec ReadKillConfirmed(ServerDataJson.Fields fields, KillConfirmedSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new KillConfirmedSpec(
                    fields.ReadInt(ScoreLimitItem, fallback.ScoreLimit),
                    fields.ReadFloat(TimeLimitSecondsItem, fallback.TimeLimitSeconds),
                    fields.ReadFloat(RespawnSecondsItem, fallback.RespawnSeconds),
                    fields.ReadFloat(TagLifetimeSecondsItem, fallback.TagLifetimeSeconds),
                    fields.ReadInt(PointsPerConfirmItem, fallback.PointsPerConfirm),
                    fields.ReadFloat(TagPickupRadiusItem, fallback.TagPickupRadius));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static GunGameSpec ReadGunGame(ServerDataJson.Fields fields, GunGameSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            var tiers = fields.Has(TierWeaponIdsItem) ? ReadIds(fields, TierWeaponIdsItem) : fallback.TierWeaponIds;
            var melee = fields.Has(MeleeWeaponIdsItem) ? ReadIds(fields, MeleeWeaponIdsItem) : fallback.MeleeWeaponIds;
            try
            {
                return new GunGameSpec(
                    fields.ReadFloat(TimeLimitSecondsItem, fallback.TimeLimitSeconds),
                    fields.ReadFloat(RespawnSecondsItem, fallback.RespawnSeconds),
                    tiers,
                    melee,
                    fields.ReadBool(DemotesOnMeleeItem, fallback.DemotesOnMelee));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static VoteKickSpec ReadVoteKick(ServerDataJson.Fields fields, VoteKickSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new VoteKickSpec(
                    fields.ReadBool(EnabledItem, fallback.Enabled),
                    fields.ReadFloat(RequiredYesFractionItem, fallback.RequiredYesFraction),
                    fields.ReadFloat(VoteSecondsItem, fallback.VoteSeconds),
                    fields.ReadFloat(CooldownSecondsItem, fallback.CooldownSeconds));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static PayloadSpec ReadPayload(ServerDataJson.Fields fields, PayloadSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            var multipliers = fallback.PusherSpeedMultipliers;
            if (fields.Has(PusherSpeedMultipliersItem))
            {
                var nodes = fields.Array(PusherSpeedMultipliersItem);
                var read = new List<float>(nodes.Count);
                for (var i = 0; i < nodes.Count; i++)
                {
                    read.Add(fields.ToFloat(fields.ElementPath(PusherSpeedMultipliersItem, i), nodes[i]));
                }

                multipliers = read;
            }

            try
            {
                return new PayloadSpec(
                    fields.ReadFloat(CartSpeedItem, fallback.CartSpeed),
                    fields.ReadInt(MinPushersItem, fallback.MinPushers),
                    multipliers,
                    fields.ReadFloat(RollbackDelaySecondsItem, fallback.RollbackDelaySeconds),
                    fields.ReadFloat(RollbackSpeedItem, fallback.RollbackSpeed),
                    fields.ReadFloat(CheckpointBonusSecondsItem, fallback.CheckpointBonusSeconds),
                    fields.ReadFloat(TimeLimitSecondsItem, fallback.TimeLimitSeconds));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static EconomySpec ReadEconomy(ServerDataJson.Fields fields, EconomySpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            try
            {
                return new EconomySpec(
                    fields.ReadInt(StartMoneyItem, fallback.StartMoney),
                    fields.ReadInt(MaxMoneyItem, fallback.MaxMoney),
                    fields.ReadInt(KillRewardItem, fallback.KillReward),
                    fields.ReadInt(RoundWinRewardItem, fallback.RoundWinReward),
                    fields.ReadInt(RoundLossRewardItem, fallback.RoundLossReward),
                    fields.ReadInt(LossStreakBonusItem, fallback.LossStreakBonus),
                    fields.ReadInt(MaxLossRewardItem, fallback.MaxLossReward));
            }
            catch (ArgumentException e)
            {
                throw RejectedSpec(fields, e);
            }
        }

        private static List<string> ReadIds(ServerDataJson.Fields fields, string item)
        {
            var nodes = fields.Array(item);
            var ids = new List<string>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                ids.Add(fields.ToText(fields.ElementPath(item, i), nodes[i]));
            }

            return ids;
        }

        // サーバーはまだ新しい目的を動かさないが、動かすときに要る値が欠けたファイルを先に断っておく
        private static string ObjectiveProblem(GameRules rules, NetMatchSettings settings)
        {
            if (rules.Objective == MatchObjective.GunGame && !settings.GunGame.HasTiers)
            {
                return "a " + MatchObjective.GunGame + " match needs at least one weapon id in "
                    + MatchItem + "." + GunGameItem + "." + TierWeaponIdsItem;
            }

            return null;
        }

        private static List<string> ReadMapPool(ServerDataJson.Fields match)
        {
            var nodes = match.Array(MapPoolItem);
            var mapIds = new List<string>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                mapIds.Add(match.ToText(match.ElementPath(MapPoolItem, i), nodes[i]));
            }

            return mapIds;
        }

        private static NetMotorSettings ReadMotor(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                return NetMotorSettings.Default;
            }

            var d = NetMotorSettings.Default;
            var dm = d.Motor;
            var ds = d.Stance;
            var dc = d.Mover;
            var dp = d.Prone;
            try
            {
                var crouchHeight = fields.ReadFloat(CrouchHeightItem, ds.Crouched.Height);
                var radius = fields.ReadFloat(RadiusItem, ds.Radius);
                var prone = ReadProne(fields, dp);
                var swim = ReadSwim(fields, d.Swim);
                var wallRun = ReadWallRun(fields, d.WallRun);
                var parachute = ReadParachute(fields, d.Parachute);
                var walkDirection = ReadDirection(fields, WalkForwardScaleItem, WalkBackwardScaleItem, WalkSideScaleItem, dm.WalkDirection);
                var sprintDirection = ReadDirection(
                    fields, SprintForwardScaleItem, SprintBackwardScaleItem, SprintSideScaleItem, dm.SprintDirection);
                var carry = ReadCarry(fields, dm.CarryLoad);
                return new NetMotorSettings(
                    new MotorSpec(
                        fields.ReadFloat(WalkSpeedItem, dm.WalkSpeed),
                        fields.ReadFloat(SprintSpeedItem, dm.SprintSpeed),
                        fields.ReadFloat(JumpHeightItem, dm.JumpHeight),
                        fields.ReadFloat(GravityItem, dm.Gravity),
                        fields.ReadFloat(AirControlItem, dm.AirControl),
                        fields.ReadFloat(CrouchSpeedItem, dm.CrouchSpeed),
                        fields.ReadFloat(SafeFallSpeedItem, dm.SafeFallSpeed),
                        fields.ReadFloat(LethalFallSpeedItem, dm.LethalFallSpeed),
                        fields.ReadFloat(DodgeDistanceItem, dm.DodgeDistance),
                        fields.ReadFloat(DodgeSecondsItem, dm.DodgeSeconds),
                        fields.ReadFloat(VaultMaxHeightItem, dm.VaultMaxHeight),
                        fields.ReadFloat(MantleMaxHeightItem, dm.MantleMaxHeight),
                        fields.ReadFloat(MantleSpeedItem, dm.MantleSpeed),
                        walkDirection,
                        sprintDirection,
                        carry,
                        fields.ReadFloat(DodgeCooldownSecondsItem, dm.DodgeCooldownSeconds),
                        fields.ReadFloat(VaultSpeedItem, dm.VaultSpeed),
                        fields.ReadFloat(VaultReachItem, dm.VaultReach),
                        fields.ReadFloat(VaultMaxAngleDegreesItem, dm.VaultMaxAngleDegrees),
                        fields.ReadFloat(VaultMinHeightItem, dm.VaultMinHeight),
                        fields.ReadFloat(VaultMaxDepthItem, dm.VaultMaxDepth),
                        fields.ReadFloat(CoverMaxEnterDistanceItem, dm.CoverMaxEnterDistance),
                        fields.ReadFloat(CoverMaxEnterAngleDegreesItem, dm.CoverMaxEnterAngleDegrees),
                        fields.ReadFloat(CoverLowWallHeightItem, dm.CoverLowWallHeight),
                        fields.ReadFloat(CoverPeekSideDistanceItem, dm.CoverPeekSideDistance),
                        fields.ReadFloat(CoverEnterSecondsItem, dm.CoverEnterSeconds),
                        fields.ReadFloat(CoverBackHoldSecondsItem, dm.CoverBackHoldSeconds),
                        fields.ReadFloat(CoverPeekSlideSecondsItem, dm.CoverPeekSlideSeconds),
                        fields.ReadFloat(LadderTopEntryDepthItem, dm.LadderTopEntryDepth),
                        fields.ReadFloat(LadderMinApproachDotItem, dm.LadderMinApproachDot),
                        fields.ReadFloat(LadderMinApproachSpeedItem, dm.LadderMinApproachSpeed),
                        fields.ReadFloat(LeanSideOffsetItem, dm.LeanSideOffset),
                        fields.ReadFloat(LeanAngleDegreesItem, dm.LeanAngleDegrees),
                        fields.ReadFloat(LeanSpeedItem, dm.LeanSpeed),
                        fields.ReadInt(AirJumpsItem, dm.AirJumps),
                        fields.ReadFloat(AirJumpSpeedItem, dm.AirJumpSpeed),
                        fields.ReadFloat(JumpGraceSecondsItem, dm.JumpGraceSeconds),
                        fields.ReadFloat(CoverEnterSpeedItem, dm.CoverEnterSpeed),
                        fields.ReadFloat(CoverMinWallHeightItem, dm.CoverMinWallHeight)),
                    new StanceGeometry(
                        fields.ReadFloat(StandingHeightItem, ds.Standing.Height),
                        fields.ReadFloat(StandingCenterYItem, ds.Standing.CenterY),
                        fields.ReadFloat(StandingEyeYItem, ds.Standing.EyeY),
                        crouchHeight,
                        NetMotorSettings.FitProneHeight(prone.Height, crouchHeight, radius),
                        radius),
                    new CapsuleMoverSpec(
                        fields.ReadFloat(SlopeLimitDegreesItem, dc.SlopeLimitDegrees),
                        fields.ReadFloat(StepOffsetItem, dc.StepOffset),
                        fields.ReadFloat(SkinWidthItem, dc.SkinWidth),
                        dc.LayerMask),
                    prone,
                    swim,
                    wallRun,
                    parachute);
            }
            catch (ArgumentException e)
            {
                // MotorSpec・StanceGeometry・CapsuleMoverSpec の引数名は項目名と同じ
                throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
            }
        }

        private static CarryLoadSpec ReadCarry(ServerDataJson.Fields fields, CarryLoadSpec fallback)
        {
            var gaits = Gait(fields, CarryWalkItem, CarryGaits.Walk, fallback.Gaits)
                | Gait(fields, CarrySprintItem, CarryGaits.Sprint, fallback.Gaits)
                | Gait(fields, CarryCrouchItem, CarryGaits.Crouch, fallback.Gaits)
                | Gait(fields, CarryProneItem, CarryGaits.Prone, fallback.Gaits);
            try
            {
                return new CarryLoadSpec(
                    fields.ReadEnum(CarryWeightSourceItem, fallback.WeightSource),
                    fields.ReadEnum(CarryMultiplierSourceItem, fallback.MultiplierSource),
                    fields.ReadFloat(CarryFreeWeightItem, fallback.FreeWeight),
                    fields.ReadFloat(CarrySpeedLossPerKgItem, fallback.SpeedLossPerKg),
                    fields.ReadFloat(CarryMinWeightMultiplierItem, fallback.MinWeightMultiplier),
                    gaits);
            }
            catch (ArgumentException e)
            {
                // CarryLoadSpec の引数名には項目名の頭の carry が付いていないので、ここで項目名に直す
                var item = e.ParamName == "weightSource" ? CarryWeightSourceItem
                    : e.ParamName == "multiplierSource" ? CarryMultiplierSourceItem
                    : e.ParamName == "freeWeight" ? CarryFreeWeightItem
                    : e.ParamName == "speedLossPerKg" ? CarrySpeedLossPerKgItem
                    : e.ParamName == "minWeightMultiplier" ? CarryMinWeightMultiplierItem
                    : null;
                throw fields.Rejected(item == null ? fields.Path : fields.ItemPath(item), e.Message, e);
            }
        }

        private static CarryGaits Gait(ServerDataJson.Fields fields, string item, CarryGaits gait, CarryGaits fallback)
        {
            return fields.ReadBool(item, (fallback & gait) != 0) ? gait : CarryGaits.None;
        }

        private static MoveDirectionScale ReadDirection(
            ServerDataJson.Fields fields, string forwardItem, string backwardItem, string sideItem, MoveDirectionScale fallback)
        {
            try
            {
                return new MoveDirectionScale(
                    fields.ReadFloat(forwardItem, fallback.Forward),
                    fields.ReadFloat(backwardItem, fallback.Backward),
                    fields.ReadFloat(sideItem, fallback.Side));
            }
            catch (ArgumentException e)
            {
                // MoveDirectionScale の引数名は歩きとダッシュで共通なので、ここで項目名に直す
                var item = e.ParamName == "forward" ? forwardItem
                    : e.ParamName == "backward" ? backwardItem
                    : e.ParamName == "side" ? sideItem
                    : null;
                throw fields.Rejected(item == null ? fields.Path : fields.ItemPath(item), e.Message, e);
            }
        }

        private static ProneSpec ReadProne(ServerDataJson.Fields fields, ProneSpec fallback)
        {
            try
            {
                return new ProneSpec(
                    fields.ReadFloat(ProneHeightItem, fallback.Height),
                    fields.ReadFloat(ProneSpeedItem, fallback.Speed),
                    fallback.MaxPitchDegrees,
                    fallback.SpreadScale,
                    fields.ReadFloat(DiveDistanceItem, fallback.DiveDistance),
                    fields.ReadFloat(DiveSecondsItem, fallback.DiveSeconds));
            }
            catch (ArgumentException e)
            {
                // ProneSpec の引数名は項目名と違うので、ここで項目名に直す
                var item = e.ParamName == "height" ? ProneHeightItem
                    : e.ParamName == "speed" ? ProneSpeedItem
                    : e.ParamName;
                throw fields.Rejected(item == null ? fields.Path : fields.ItemPath(item), e.Message, e);
            }
        }

        private static SwimTuning ReadSwim(ServerDataJson.Fields fields, SwimTuning fallback)
        {
            try
            {
                return new SwimTuning(
                    fields.ReadFloat(SwimSpeedItem, fallback.Speed),
                    fields.ReadFloat(SwimBuoyancyItem, fallback.Buoyancy),
                    fields.ReadFloat(SwimBreathSecondsItem, fallback.BreathSeconds),
                    fallback.DrownDamage,
                    fallback.DrownIntervalSeconds);
            }
            catch (ArgumentException e)
            {
                // SwimTuning の引数名には項目名の頭の swim が付いていないので、ここで項目名に直す
                var item = e.ParamName == "speed" ? SwimSpeedItem
                    : e.ParamName == "buoyancy" ? SwimBuoyancyItem
                    : e.ParamName == "breathSeconds" ? SwimBreathSecondsItem
                    : null;
                throw fields.Rejected(item == null ? fields.Path : fields.ItemPath(item), e.Message, e);
            }
        }

        private static WallRunTuning ReadWallRun(ServerDataJson.Fields fields, WallRunTuning fallback)
        {
            try
            {
                return new WallRunTuning(
                    fields.ReadBool(WallRunEnabledItem, fallback.Enabled),
                    fields.ReadFloat(WallRunMaxSecondsItem, fallback.MaxSeconds),
                    fields.ReadFloat(WallRunMinSpeedItem, fallback.MinSpeed),
                    fields.ReadFloat(WallRunGravityScaleItem, fallback.GravityScale),
                    fields.ReadFloat(WallRunJumpOffSpeedItem, fallback.JumpOffSpeed));
            }
            catch (ArgumentException e)
            {
                // WallRunTuning の引数名には項目名の頭の wallRun が付いていないので、ここで項目名に直す
                var item = e.ParamName == "maxSeconds" ? WallRunMaxSecondsItem
                    : e.ParamName == "minSpeed" ? WallRunMinSpeedItem
                    : e.ParamName == "gravityScale" ? WallRunGravityScaleItem
                    : e.ParamName == "jumpOffSpeed" ? WallRunJumpOffSpeedItem
                    : null;
                throw fields.Rejected(item == null ? fields.Path : fields.ItemPath(item), e.Message, e);
            }
        }

        private static ParachuteTuning ReadParachute(ServerDataJson.Fields fields, ParachuteTuning fallback)
        {
            try
            {
                return new ParachuteTuning(
                    fields.ReadFloat(ParachuteMinOpenHeightItem, fallback.MinOpenHeight),
                    fields.ReadFloat(ParachuteDescentSpeedItem, fallback.DescentSpeed),
                    fields.ReadFloat(ParachuteHorizontalSpeedItem, fallback.HorizontalSpeed),
                    fields.ReadFloat(ParachuteSteerAccelerationItem, fallback.SteerAcceleration));
            }
            catch (ArgumentException e)
            {
                // ParachuteTuning の引数名には項目名の頭の parachute が付いていないので、ここで項目名に直す
                var item = e.ParamName == "minOpenHeight" ? ParachuteMinOpenHeightItem
                    : e.ParamName == "descentSpeed" ? ParachuteDescentSpeedItem
                    : e.ParamName == "horizontalSpeed" ? ParachuteHorizontalSpeedItem
                    : e.ParamName == "steerAcceleration" ? ParachuteSteerAccelerationItem
                    : null;
                throw fields.Rejected(item == null ? fields.Path : fields.ItemPath(item), e.Message, e);
            }
        }

        private static Dictionary<string, WeaponSpec> ReadWeapons(ServerDataJson.Fields top)
        {
            var weapons = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
            var nodes = top.Array(WeaponsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], WeaponsItem, i, WeaponSpecJson.ItemNames);
                var spec = WeaponSpecJson.Read(fields);
                if (weapons.ContainsKey(spec.Id))
                {
                    throw fields.Rejected(fields.ItemPath(WeaponSpecJson.IdItem), "weapon id " + spec.Id + " is listed more than once");
                }

                weapons.Add(spec.Id, spec);
            }

            return weapons;
        }

        private static LoadoutRule ReadRule(ServerDataJson.Fields loadout)
        {
            var slots = new List<List<WeaponCategory>>();
            var nodes = loadout.Array(SlotsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var item = loadout.ElementPath(SlotsItem, i);
                var node = nodes[i];
                if (node.Kind != JsonNodeKind.Array)
                {
                    throw loadout.Invalid(item, node, "must be an array of weapon categories, but is " + node.Describe());
                }

                var slot = new List<WeaponCategory>();
                for (var j = 0; j < node.Elements.Count; j++)
                {
                    slot.Add(loadout.ToEnum<WeaponCategory>(item + "[" + j + "]", node.Elements[j]));
                }

                slots.Add(slot);
            }

            try
            {
                return new LoadoutRule(slots);
            }
            catch (ArgumentException e)
            {
                throw loadout.Rejected(loadout.ItemPath(SlotsItem), e.Message, e);
            }
        }

        private static List<string> ReadAbilities(ServerDataJson.Fields loadout)
        {
            if (loadout.Find(AbilitiesItem) == null)
            {
                return null;
            }

            var ids = new List<string>();
            var nodes = loadout.Array(AbilitiesItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var item = loadout.ElementPath(AbilitiesItem, i);
                var id = loadout.ToText(item, nodes[i]);
                if (id.Length > 0 && ids.Contains(id))
                {
                    throw loadout.Invalid(item, nodes[i], "names deployable " + id + " again; each ability slot needs a different one");
                }

                ids.Add(id);
            }

            return ids;
        }

        // 枠を「なし」にする空の Id を断らないのは、保存した選択 (LoadoutPreference) と同じ書き方で枠を飛ばせるようにするため
        private static System.Collections.ObjectModel.ReadOnlyCollection<string> CopyAbilities(IReadOnlyList<string> abilities)
        {
            if (abilities == null)
            {
                return null;
            }

            var copy = new List<string>(abilities.Count);
            for (var i = 0; i < abilities.Count; i++)
            {
                var id = abilities[i] ?? throw new ArgumentException("ability slot " + i + " is null; use an empty id for none", nameof(abilities));
                if (id.Length > 0 && copy.Contains(id))
                {
                    throw new ArgumentException("ability slot " + i + " names deployable " + id + " again", nameof(abilities));
                }

                copy.Add(id);
            }

            return copy.AsReadOnly();
        }

        private static List<WeaponSpec> Pick(ServerDataJson.Fields owner, Dictionary<string, WeaponSpec> weapons)
        {
            var picked = new List<WeaponSpec>();
            var nodes = owner.Array(WeaponsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var item = owner.ElementPath(WeaponsItem, i);
                var id = owner.ToText(item, nodes[i]);
                if (!weapons.TryGetValue(id, out var spec))
                {
                    throw owner.Invalid(item, nodes[i], "names weapon " + id + ", which is not in the top-level '" + WeaponsItem + "'");
                }

                picked.Add(spec);
            }

            return picked;
        }

        private static IEnumerable<string> Ids(IEnumerable<WeaponSpec> weapons)
        {
            foreach (var weapon in weapons)
            {
                yield return weapon.Id;
            }
        }

        private static bool ContainsId(List<WeaponSpec> weapons, string id)
        {
            foreach (var weapon in weapons)
            {
                if (string.Equals(weapon.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
