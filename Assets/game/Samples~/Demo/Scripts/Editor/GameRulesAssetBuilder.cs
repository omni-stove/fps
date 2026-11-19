using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    // BuildAt の 7 つの値を変えたら、同じ値のサンプル JSON (GameRulesJsonImporter.SamplesFolder) も直す。
    public static class GameRulesAssetBuilder
    {
        public static string Folder => GameRulesAssets.Folder;

        public static string TeamDeathmatchPath => Folder + "/TeamDeathmatch.asset";
        public static string CampaignEasyPath => Folder + "/CampaignEasy.asset";
        public static string CampaignNormalPath => Folder + "/CampaignNormal.asset";
        public static string CampaignHardPath => Folder + "/CampaignHard.asset";
        public static string RangePath => Folder + "/Range.asset";
        public static string TpsRangePath => Folder + "/TpsRange.asset";

        public static string FeatureFolder => Folder + "/Features";
        public static string DominationPath => Folder + "/Domination.asset";

        public const string DominationName = "Domination";
        public const string FreeForAllName = "FreeForAll";
        public const string KillConfirmedName = "KillConfirmed";
        public const string GunGameName = "GunGame";
        public const string HardpointName = "Hardpoint";
        public const string GuardianName = "Guardian";
        public const string ExecutionName = "Execution";
        public const string SearchAndDestroyName = "SearchAndDestroy";
        public const string CaptureTheFlagName = "CaptureTheFlag";
        public const string HordeName = "Horde";
        public const string BattleRoyaleName = "BattleRoyale";
        public const string InfectedName = "Infected";
        public const string ExtractionName = "Extraction";

        public static string FreeForAllPath => Folder + "/" + FreeForAllName + ".asset";
        public static string KillConfirmedPath => Folder + "/" + KillConfirmedName + ".asset";
        public static string GunGamePath => Folder + "/" + GunGameName + ".asset";
        public static string HardpointPath => Folder + "/" + HardpointName + ".asset";
        public static string GuardianPath => Folder + "/" + GuardianName + ".asset";
        public static string ExecutionPath => Folder + "/" + ExecutionName + ".asset";
        public static string SearchAndDestroyPath => Folder + "/" + SearchAndDestroyName + ".asset";
        public static string CaptureTheFlagPath => Folder + "/" + CaptureTheFlagName + ".asset";
        public static string HordePath => Folder + "/" + HordeName + ".asset";
        public static string BattleRoyalePath => Folder + "/" + BattleRoyaleName + ".asset";
        public static string InfectedPath => Folder + "/" + InfectedName + ".asset";
        public static string ExtractionPath => Folder + "/" + ExtractionName + ".asset";

        public const float HordeWaveIntervalSeconds = 30f;

        // ホードは敵の数が多く、倒れても戻るまで補給できないので多めに持たせる。
        public const float HordeReserveAmmoMultiplier = 2f;

        // バトルロイヤルは長く生き残るほど撃つので多めに持たせる。
        public const float BattleRoyaleReserveAmmoMultiplier = 2f;

        public static IReadOnlyList<string> ModeNames { get; } = new[]
        {
            FreeForAllName,
            KillConfirmedName,
            GunGameName,
            HardpointName,
            GuardianName,
            ExecutionName,
            SearchAndDestroyName,
            CaptureTheFlagName,
            HordeName,
            BattleRoyaleName,
            InfectedName,
            ExtractionName,
        };

        public const int TpsRangeGrenadeCount = 3;

        // 既定のロードアウト (2 本) でもう届く数にして、拾った武器は今の武器と持ち替えさせる。
        public const int DemoMaxWeapons = 2;

        public const int TeamDeathmatchGrenadeCount = 2;

        public const string MenuPath = "Shooter/Build Game Rules Assets";
        public const string LogTag = "[GameRulesAssetBuilder]";

        public const float TeamDeathmatchSpawnProtectionSeconds = 2f;

        public const float OnlineIdleKickSeconds = 120f;

        public static GameRules TeamDeathmatchRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                4f,
                20f,
                DeathBody.Collapse,
                DeathCamera.Ally,
                false,
                1f,
                Destruction.On,
                TeamDeathmatchActions())
                .WithSpawnProtectionSeconds(TeamDeathmatchSpawnProtectionSeconds)
                .WithIdleKickSeconds(OnlineIdleKickSeconds)
                .WithMaxWeapons(DemoMaxWeapons);
        }

        public static GameRules CampaignEasyRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                3f,
                25f,
                DeathBody.Collapse,
                DeathCamera.Fallen,
                false,
                1.5f,
                Destruction.Off,
                CampaignActions())
                .WithMaxWeapons(DemoMaxWeapons)
                .WithStreaks(Streaks.Off);
        }

        public static GameRules CampaignNormalRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 5f, 15f, 1f, CampaignActions())
                .WithDeathCamera(DeathCamera.Fallen)
                .WithMaxWeapons(DemoMaxWeapons)
                .WithStreaks(Streaks.Off);
        }

        public static GameRules CampaignHardRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 7f, 10f, 0.6f, CampaignActions())
                .WithDeathCamera(DeathCamera.Fallen)
                .WithMaxWeapons(DemoMaxWeapons)
                .WithStreaks(Streaks.Off);
        }

        public static GameRules RangeRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                4f,
                20f,
                DeathBody.Vanish,
                DeathCamera.Fallen,
                true,
                1f,
                Destruction.On,
                RangeActions())
                .WithMaxWeapons(DemoMaxWeapons);
        }

        public static GameRules TpsRangeRules()
        {
            return RangeRules().WithActions(TpsRangeActions()).WithViewMode(ViewMode.ThirdPerson);
        }

        public static GameRules FeatureBaseRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                4f,
                20f,
                DeathBody.Vanish,
                DeathCamera.Fallen,
                false,
                1f,
                Destruction.Off,
                ActionRules.Default)
                .WithMaxWeapons(DemoMaxWeapons);
        }

        // GameRulesDebugDraft と同じく、ブラインドファイアはカバーも、グレネードは数も一緒に入れる。
        // ダウンは倒れる手段が要るので落下ダメージも入れる。
        // カバーとブラインドファイアは、一人称では張り付くと視界が壁で埋まるだけなので三人称で見せる。
        public static GameRules FeatureRules(GameRulesDebugField field)
        {
            if (!GameRulesDebugDraft.IsToggle(field))
            {
                throw new ArgumentException($"{field} is not an on/off rule", nameof(field));
            }

            var draft = new GameRulesDebugDraft(FeatureBaseRules());
            draft.Set(field, 1);
            if (field == GameRulesDebugField.Downed)
            {
                draft.Set(GameRulesDebugField.FallDamage, (int)FallDamage.On);
            }

            var rules = draft.Build();
            return field == GameRulesDebugField.Cover || field == GameRulesDebugField.BlindFire
                ? rules.WithViewMode(ViewMode.ThirdPerson)
                : rules;
        }

        public static string FeatureRulesPath(GameRulesDebugField field)
        {
            return FeatureFolder + "/" + field + ".asset";
        }

        // チームデスマッチの値を後で変えてもドミネーションが付いて変わるよう、値を写さずに呼び出して作る。
        // オフラインの DominationRunner は Objective を読まず、いつも拠点で勝敗を決める。
        public static GameRules DominationRules()
        {
            return TeamDeathmatchRules().WithObjective(MatchObjective.Domination);
        }

        // 味方がいないので、死亡カメラは倒した相手 (DeathCamera.Killer) を映す。
        public static GameRules FreeForAllRules()
        {
            return TeamDeathmatchRules()
                .WithDeathCamera(DeathCamera.Killer)
                .WithActions(FreeForAllActions());
        }

        public static GameRules KillConfirmedRules()
        {
            return TeamDeathmatchRules();
        }

        // キルのたびに武器が替わって補給の機会が無いので予備弾を無限にし、近接で倒した相手を 1 段戻せるよう近接攻撃を使えるようにする。
        public static GameRules GunGameRules()
        {
            var free = FreeForAllRules();
            return free.WithInfiniteReserve(true).WithActions(free.Actions.WithMelee(Melee.On));
        }

        public static GameRules HardpointRules()
        {
            return TeamDeathmatchRules();
        }

        public static GameRules GuardianRules()
        {
            return TeamDeathmatchRules();
        }

        // ラウンド中は戻らない (RespawnMode.None) ので、出現直後の無敵は無くす。
        public static GameRules ExecutionRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f);
        }

        public static GameRules SearchAndDestroyRules()
        {
            return ExecutionRules().WithActions(SearchAndDestroyActions());
        }

        public static GameRules CaptureTheFlagRules()
        {
            return TeamDeathmatchRules().WithActions(CaptureTheFlagActions());
        }

        // 倒れた人は波でまとめて戻る (RespawnMode.Wave) ので、出現直後の無敵も残す。
        public static GameRules HordeRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.Wave)
                .WithWaveIntervalSeconds(HordeWaveIntervalSeconds)
                .WithReserveAmmoMultiplier(HordeReserveAmmoMultiplier);
        }

        // 倒れたら戻らない (RespawnMode.None) ので、出現直後の無敵は無くす。
        public static GameRules BattleRoyaleRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f)
                .WithReserveAmmoMultiplier(BattleRoyaleReserveAmmoMultiplier);
        }

        // 倒された人はすぐ感染者として戻る (RespawnMode.Individual) ので、起こしてもらうダウンは使わない。
        public static GameRules InfectedRules()
        {
            return TeamDeathmatchRules().WithActions(InfectedActions());
        }

        // 倒れたら戻らず (RespawnMode.None) 持ち込んだ物を失うので、出現直後の無敵は無くし、起こす味方もいないのでダウンさせない。
        // 弾は箱から拾わせるので、バトルロイヤルと違って予備弾は増やさない。
        public static GameRules ExtractionRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f)
                .WithActions(ExtractionActions());
        }

        public static GameRules ModeRules(string name)
        {
            switch (name)
            {
                case FreeForAllName:
                    return FreeForAllRules();
                case KillConfirmedName:
                    return KillConfirmedRules();
                case GunGameName:
                    return GunGameRules();
                case HardpointName:
                    return HardpointRules();
                case GuardianName:
                    return GuardianRules();
                case ExecutionName:
                    return ExecutionRules();
                case SearchAndDestroyName:
                    return SearchAndDestroyRules();
                case CaptureTheFlagName:
                    return CaptureTheFlagRules();
                case HordeName:
                    return HordeRules();
                case BattleRoyaleName:
                    return BattleRoyaleRules();
                case InfectedName:
                    return InfectedRules();
                case ExtractionName:
                    return ExtractionRules();
                default:
                    throw new ArgumentException($"'{name}' is not one of the mode names ({string.Join(", ", ModeNames)})", nameof(name));
            }
        }

        public static string ModeRulesPath(string name)
        {
            return ModeRulesPath(Folder, name);
        }

        // 起こす味方がいないので、ダウンしないようにする。
        public static ActionRules FreeForAllActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        // 倒れたらその場でラウンドから外れるよう、ダウンしないようにする。
        public static ActionRules SearchAndDestroyActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        // ダウンさせると、倒れるまで旗を持ったまま残り、運ぶ人を撃っても旗が落ちないので、ダウンしないようにする。
        public static ActionRules CaptureTheFlagActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        public static ActionRules InfectedActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        public static ActionRules ExtractionActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        // ジャンプを切っても、段差は乗り越えと崖のぼりで上がれる。
        public static ActionRules TpsRangeActions()
        {
            return RangeActions()
                .WithCover(Cover.On)
                .WithBlindFire(BlindFire.On)
                .WithDodge(Dodge.On)
                .WithVault(Vault.On)
                .WithMantle(Mantle.On)
                .WithGrenade(Grenade.On)
                .WithGrenadeCount(TpsRangeGrenadeCount)
                .WithSlide(Slide.On)
                .WithProne(Prone.On)
                .WithStagger(Stagger.On)
                .WithStealth(Stealth.On)
                .WithSuppression(Suppression.On)
                .WithJump(Jump.Off);
        }

        // カバーとブラインドファイアはオンラインのプレイヤーが対応していないので使わない。
        public static ActionRules TeamDeathmatchActions()
        {
            return WithDefaultDownedTimings(Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On)
                .WithGrenade(Grenade.On)
                .WithGrenadeCount(TeamDeathmatchGrenadeCount);
        }

        // 一人称では壁に張り付いても視界が壁で埋まるだけなので、カバーと (カバー中にだけ撃てる) ブラインドファイアを使わない。
        public static ActionRules CampaignActions()
        {
            return WithDefaultDownedTimings(Downed.Off, Cover.Off, BlindFire.Off, ActiveReload.On);
        }

        // 一人称なのでカバーを使わない (CampaignActions と同じ理由)。
        public static ActionRules RangeActions()
        {
            return WithDefaultDownedTimings(Downed.Off, Cover.Off, BlindFire.Off, ActiveReload.On)
                .WithEmote(Emote.On)
                .WithStamina(Stamina.On);
        }

        public static GameRulesDefinition LoadRules(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"game rules '{path}' were not found; run {MenuPath} (Shooter.Editor.GameRulesAssetBuilder.Build) first");
            }

            definition.ToRules();
            return definition;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(
                TeamDeathmatchPath, CampaignEasyPath, CampaignNormalPath, CampaignHardPath, RangePath, TpsRangePath, DominationPath);
            BuildModesAt(Folder);
        }

        // ゲーム側で調整した値を残すよう、既にあれば値は変えない。
        public static GameRulesDefinition LoadOrBuildFeatureRules(GameRulesDebugField field, string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            if (AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path) != null)
            {
                return LoadRules(path);
            }

            var rules = FeatureRules(field);
            var built = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(path, d => Configure(d, rules));
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        // シーンを 1 つ組むメニューからでも、先に Build を回さずに遊べるよう、無ければそのモードの分だけ作る。
        // ゲーム側で調整した値を残すよう、既にあれば値は変えない。
        public static GameRulesDefinition LoadOrBuildModeRules(string name)
        {
            var path = ModeRulesPath(name);
            if (AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path) != null)
            {
                return LoadRules(path);
            }

            var rules = ModeRules(name);
            var built = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(path, d => Configure(d, rules));
            Debug.Log($"{LogTag} saved '{path}'");
            return built;
        }

        public static GameRulesDefinition[] BuildModesAt(string folder)
        {
            var paths = ModeRulesPaths(folder);

            // 書き始める前にすべてのルールを作っておき、値が不正なモードがあれば 1 つも保存しない。
            var rules = new GameRules[ModeNames.Count];
            for (var i = 0; i < rules.Length; i++)
            {
                rules[i] = ModeRules(ModeNames[i]);
            }

            var built = new GameRulesDefinition[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                var modeRules = rules[i];
                built[i] = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(paths[i], d => Configure(d, modeRules));
            }

            Debug.Log($"{LogTag} saved {string.Join(", ", Array.ConvertAll(paths, p => "'" + p + "'"))}");
            return built;
        }

        public static string ModeRulesPath(string folder, string name)
        {
            var known = false;
            foreach (var mode in ModeNames)
            {
                known |= string.Equals(mode, name, StringComparison.Ordinal);
            }

            if (!known)
            {
                throw new ArgumentException($"'{name}' is not one of the mode names ({string.Join(", ", ModeNames)})", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("rules folder must not be empty", nameof(folder));
            }

            return SceneBuildHelpers.ValidateAssetPath(folder.Trim().Replace('\\', '/').TrimEnd('/') + "/" + name + ".asset");
        }

        private static string[] ModeRulesPaths(string folder)
        {
            var paths = new string[ModeNames.Count];
            for (var i = 0; i < paths.Length; i++)
            {
                paths[i] = ModeRulesPath(folder, ModeNames[i]);
            }

            return paths;
        }

        public static GameRulesDefinition[] BuildAt(
            string teamDeathmatchPath,
            string campaignEasyPath,
            string campaignNormalPath,
            string campaignHardPath,
            string rangePath,
            string tpsRangePath,
            string dominationPath)
        {
            var entries = new[]
            {
                (Path: SceneBuildHelpers.ValidateAssetPath(teamDeathmatchPath), Rules: TeamDeathmatchRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignEasyPath), Rules: CampaignEasyRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignNormalPath), Rules: CampaignNormalRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignHardPath), Rules: CampaignHardRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(rangePath), Rules: RangeRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(tpsRangePath), Rules: TpsRangeRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(dominationPath), Rules: DominationRules()),
            };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (!seen.Add(entry.Path))
                {
                    throw new ArgumentException($"game rules asset path '{entry.Path}' is used more than once");
                }
            }

            var built = new GameRulesDefinition[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var rules = entries[i].Rules;
                built[i] = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(entries[i].Path, d => Configure(d, rules));
            }

            Debug.Log($"{LogTag} saved {string.Join(", ", Array.ConvertAll(entries, e => "'" + e.Path + "'"))}");
            return built;
        }

        public static void Configure(GameRulesDefinition definition, GameRules rules) => GameRulesAssets.Configure(definition, rules);

        private static ActionRules WithDefaultDownedTimings(Downed downed, Cover cover, BlindFire blindFire, ActiveReload activeReload)
        {
            var defaults = ActionRules.Default;
            return new ActionRules(
                downed,
                defaults.DownedBleedoutSeconds,
                defaults.DownedHealthFraction,
                defaults.ReviveSeconds,
                cover,
                blindFire,
                activeReload);
        }

        private static GameRules WithOverrides(
            GameRules source,
            DeathBody deathBody,
            float regenDelaySeconds,
            float regenPerSecond,
            float reserveAmmoMultiplier,
            ActionRules actions)
        {
            return new GameRules(
                source.FriendlyFire,
                source.RespawnMode,
                source.WaveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                deathBody,
                source.DeathCamera,
                source.InfiniteReserve,
                reserveAmmoMultiplier,
                Destruction.Off,
                actions,
                source.SpawnProtectionSeconds,
                source.FallDamage,
                source.HudDensity,
                source.Objective,
                source.MinPlayers,
                source.IdleKickSeconds,
                source.MaxWeapons,
                source.ReloadMode,
                source.ViewMode);
        }
    }
}
