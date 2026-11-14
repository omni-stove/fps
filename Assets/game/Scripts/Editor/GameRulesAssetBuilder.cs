using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// モード別のルールのアセット (<see cref="Folder"/> の <see cref="GameRulesDefinition"/>。<see cref="BuildAt"/> の 7 つと、
    /// 対戦・PvE モードの <see cref="ModeNames"/> の 9 つ) を作るか上書きする。
    /// 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    /// リスポーン直後の無敵は、チームデスマッチ (と、その値を引き継ぐドミネーション) と、そこから作る個別リスポーンのモードだけ
    /// <see cref="TeamDeathmatchSpawnProtectionSeconds"/> 秒で、他のモードは無し。どのモードも落下ダメージなし・HUD はすべて出す・
    /// Bot で埋めない (<see cref="GameRules"/> の既定)。放置で切るのは、オンラインの試合が使うチームデスマッチと、
    /// その値から作るモード (ドミネーション・対戦・PvE のモード) だけで <see cref="OnlineIdleKickSeconds"/> 秒。キャンペーンと射撃場は切らない。
    /// オンラインの試合の勝敗を拠点で決める (<see cref="MatchObjective.Domination"/>) のはドミネーションだけで、他のモードは拠点なし。
    /// アクションは、チームデスマッチとドミネーションがダウン (蘇生あり) とアクティブリロード、キャンペーンと射撃場がカバー・ブラインドファイア・アクティブリロード、
    /// TPS 射撃場がそれに回避・乗り越え・グレネードを足したもの。
    /// 出血・ダウン中の体力・蘇生の秒数はどのモードも <see cref="ActionRules.Default"/> の値。
    /// 何度実行しても同じ結果になる。値が不正なら保存せずに例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
    /// <see cref="BuildAt"/> の 7 つの値を変えたら、同じ値のサンプル JSON (<see cref="GameRulesJsonImporter.SamplesFolder"/>) も直す。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.GameRulesAssetBuilder.Build</code>
    /// </summary>
    public static class GameRulesAssetBuilder
    {
        /// <summary>ルールのアセットを置く、出力先ルート (<see cref="FpsKitRoot.Output"/>) の下のフォルダ。</summary>
        public static string Folder => FpsKitRoot.OutputPath("Rules");

        public static string TeamDeathmatchPath => Folder + "/TeamDeathmatch.asset";
        public static string CampaignEasyPath => Folder + "/CampaignEasy.asset";
        public static string CampaignNormalPath => Folder + "/CampaignNormal.asset";
        public static string CampaignHardPath => Folder + "/CampaignHard.asset";
        public static string RangePath => Folder + "/Range.asset";
        public static string TpsRangePath => Folder + "/TpsRange.asset";
        public static string DominationPath => Folder + "/Domination.asset";

        public const string FreeForAllName = "FreeForAll";
        public const string KillConfirmedName = "KillConfirmed";
        public const string GunGameName = "GunGame";
        public const string HardpointName = "Hardpoint";
        public const string GuardianName = "Guardian";
        public const string ExecutionName = "Execution";
        public const string SearchAndDestroyName = "SearchAndDestroy";
        public const string HordeName = "Horde";
        public const string BattleRoyaleName = "BattleRoyale";

        public static string FreeForAllPath => Folder + "/" + FreeForAllName + ".asset";
        public static string KillConfirmedPath => Folder + "/" + KillConfirmedName + ".asset";
        public static string GunGamePath => Folder + "/" + GunGameName + ".asset";
        public static string HardpointPath => Folder + "/" + HardpointName + ".asset";
        public static string GuardianPath => Folder + "/" + GuardianName + ".asset";
        public static string ExecutionPath => Folder + "/" + ExecutionName + ".asset";
        public static string SearchAndDestroyPath => Folder + "/" + SearchAndDestroyName + ".asset";
        public static string HordePath => Folder + "/" + HordeName + ".asset";
        public static string BattleRoyalePath => Folder + "/" + BattleRoyaleName + ".asset";

        /// <summary><see cref="HordeRules"/> で、倒れた人がまとめて戻る波の間隔 (秒)。</summary>
        public const float HordeWaveIntervalSeconds = 30f;

        /// <summary><see cref="HordeRules"/> の予備弾の倍率。敵の数が多く、倒れても戻るまで補給できないので多めに持たせる。</summary>
        public const float HordeReserveAmmoMultiplier = 2f;

        /// <summary><see cref="BattleRoyaleRules"/> の予備弾の倍率。長く生き残るほど撃つので多めに持たせる。</summary>
        public const float BattleRoyaleReserveAmmoMultiplier = 2f;

        /// <summary>
        /// 対戦・PvE モードの名前。<see cref="BuildModesAt"/> はこの順に <c>&lt;フォルダ&gt;/&lt;名前&gt;.asset</c> を作って返す。
        /// シーンは <see cref="BuildSceneList"/> の <c>&lt;出力先ルート&gt;/Scenes/&lt;名前&gt;.unity</c>。
        /// </summary>
        public static IReadOnlyList<string> ModeNames { get; } = new[]
        {
            FreeForAllName,
            KillConfirmedName,
            GunGameName,
            HardpointName,
            GuardianName,
            ExecutionName,
            SearchAndDestroyName,
            HordeName,
            BattleRoyaleName,
        };

        /// <summary><see cref="TpsRangeActions"/> で出撃ごとに持つグレネードの数。</summary>
        public const int TpsRangeGrenadeCount = 3;

        /// <summary><see cref="TeamDeathmatchActions"/> (ドミネーションも同じ) で出撃ごとに持つグレネードの数。</summary>
        public const int TeamDeathmatchGrenadeCount = 2;

        public const string MenuPath = "Fps/Build Game Rules Assets";
        public const string LogTag = "[GameRulesAssetBuilder]";

        /// <summary><see cref="TeamDeathmatchRules"/> の出現直後の無敵の秒数。</summary>
        public const float TeamDeathmatchSpawnProtectionSeconds = 2f;

        /// <summary>
        /// <see cref="TeamDeathmatchRules"/> (オンラインの試合が使うルール) で、入力が無いプレイヤーをサーバーから切るまでの秒数。
        /// </summary>
        public const float OnlineIdleKickSeconds = 120f;

        /// <summary>
        /// チームデスマッチ。味方撃ちなし、個別リスポーン、4 秒後から毎秒 20 回復、体は倒れる、リスポーンまで生きている味方の視点で観戦する、予備弾は有限で 1 倍、
        /// 建造物は弾で壊れる、出現直後は <see cref="TeamDeathmatchSpawnProtectionSeconds"/> 秒無敵、
        /// 入力が <see cref="OnlineIdleKickSeconds"/> 秒無ければサーバーから切る。アクションは <see cref="TeamDeathmatchActions"/>。
        /// </summary>
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
                .WithIdleKickSeconds(OnlineIdleKickSeconds);
        }

        /// <summary>
        /// キャンペーン (易)。味方撃ちなし、個別リスポーン、3 秒後から毎秒 25 回復、体は倒れる、死亡カメラなし、予備弾は有限で 1.5 倍、
        /// 建造物は壊れない。アクションは <see cref="CampaignActions"/>。
        /// </summary>
        public static GameRules CampaignEasyRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                3f,
                25f,
                DeathBody.Collapse,
                DeathCamera.None,
                false,
                1.5f,
                Destruction.Off,
                CampaignActions());
        }

        /// <summary>
        /// キャンペーン (普通)。<see cref="GameRules.Default"/> のうち、5 秒後から毎秒 15 回復、体は倒れる、予備弾は有限で 1 倍にしたもの。建造物は壊れない。
        /// アクションは <see cref="CampaignActions"/>。
        /// </summary>
        public static GameRules CampaignNormalRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 5f, 15f, 1f, CampaignActions());
        }

        /// <summary>
        /// キャンペーン (難)。<see cref="GameRules.Default"/> のうち、7 秒後から毎秒 10 回復、体は倒れる、予備弾は有限で 0.6 倍にしたもの。建造物は壊れない。
        /// アクションは <see cref="CampaignActions"/>。
        /// </summary>
        public static GameRules CampaignHardRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 7f, 10f, 0.6f, CampaignActions());
        }

        /// <summary>
        /// 射撃場。味方撃ちなし、個別リスポーン、4 秒後から毎秒 20 回復、体は消える、死亡カメラなし、予備弾は無限、建造物は弾で壊れる。
        /// アクションは <see cref="RangeActions"/>。
        /// </summary>
        public static GameRules RangeRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                4f,
                20f,
                DeathBody.Vanish,
                DeathCamera.None,
                true,
                1f,
                Destruction.On,
                RangeActions());
        }

        /// <summary>
        /// TPS 射撃場。<see cref="RangeRules"/> (予備弾は無限) のアクションを <see cref="TpsRangeActions"/> にしたもの。
        /// </summary>
        public static GameRules TpsRangeRules()
        {
            return RangeRules().WithActions(TpsRangeActions());
        }

        /// <summary>
        /// ドミネーション。<see cref="TeamDeathmatchRules"/> (アクションも <see cref="TeamDeathmatchActions"/>) のうち、
        /// オンラインの試合の勝敗を拠点のポイントで決める (<see cref="MatchObjective.Domination"/>) ようにしたもの。
        /// チームデスマッチの値を後で変えてもドミネーションが付いて変わるよう、値を写さずに呼び出して作る。
        /// オフラインの DominationRunner は Objective を読まず、いつも拠点で勝敗を決める。
        /// </summary>
        public static GameRules DominationRules()
        {
            return TeamDeathmatchRules().WithObjective(MatchObjective.Domination);
        }

        /// <summary>
        /// フリーフォーオール。<see cref="TeamDeathmatchRules"/> のうち、味方がいないので死亡カメラを倒した相手 (<see cref="DeathCamera.Killer"/>) にし、
        /// アクションを <see cref="FreeForAllActions"/> (起こす味方がいないのでダウンしない) にしたもの。
        /// </summary>
        public static GameRules FreeForAllRules()
        {
            return TeamDeathmatchRules()
                .WithDeathCamera(DeathCamera.Killer)
                .WithActions(FreeForAllActions());
        }

        /// <summary>キルコンファームド。今は <see cref="TeamDeathmatchRules"/> と同じ値 (呼び出して作るので付いて変わる)。</summary>
        public static GameRules KillConfirmedRules()
        {
            return TeamDeathmatchRules();
        }

        /// <summary>
        /// ガンゲーム。<see cref="FreeForAllRules"/> のうち、キルのたびに武器が替わって補給の機会が無いので予備弾を無限にしたもの。
        /// </summary>
        public static GameRules GunGameRules()
        {
            return FreeForAllRules().WithInfiniteReserve(true);
        }

        /// <summary>ハードポイント。今は <see cref="TeamDeathmatchRules"/> と同じ値 (ドミネーションと同じく拠点の取り合い)。</summary>
        public static GameRules HardpointRules()
        {
            return TeamDeathmatchRules();
        }

        /// <summary>ガーディアン (VIP の護衛)。今は <see cref="TeamDeathmatchRules"/> と同じ値。</summary>
        public static GameRules GuardianRules()
        {
            return TeamDeathmatchRules();
        }

        /// <summary>
        /// エクスキューション。<see cref="TeamDeathmatchRules"/> のうち、ラウンド中は戻らない (<see cref="RespawnMode.None"/>) ので出現直後の無敵を無くしたもの。
        /// ダウンは <see cref="TeamDeathmatchActions"/> のまま (<see cref="Downed.Revive"/>) で、味方が起こせる。
        /// </summary>
        public static GameRules ExecutionRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f);
        }

        /// <summary>
        /// 爆破 (サーチアンドデストロイ)。<see cref="ExecutionRules"/> のうち、倒れたらその場で終わるようダウンを切ったもの
        /// (<see cref="SearchAndDestroyActions"/>)。
        /// </summary>
        public static GameRules SearchAndDestroyRules()
        {
            return ExecutionRules().WithActions(SearchAndDestroyActions());
        }

        /// <summary>
        /// ホード (PvE)。<see cref="TeamDeathmatchRules"/> のうち、倒れた人は <see cref="HordeWaveIntervalSeconds"/> 秒ごとの波でまとめて戻り
        /// (<see cref="RespawnMode.Wave"/>、戻るので出現直後の無敵も残す)、予備弾を <see cref="HordeReserveAmmoMultiplier"/> 倍にしたもの。
        /// ダウンは <see cref="Downed.Revive"/> で、味方が起こせる。
        /// </summary>
        public static GameRules HordeRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.Wave)
                .WithWaveIntervalSeconds(HordeWaveIntervalSeconds)
                .WithReserveAmmoMultiplier(HordeReserveAmmoMultiplier);
        }

        /// <summary>
        /// バトルロイヤル。<see cref="TeamDeathmatchRules"/> のうち、倒れたら戻らず (<see cref="RespawnMode.None"/>、出現直後の無敵なし)、
        /// 予備弾を <see cref="BattleRoyaleReserveAmmoMultiplier"/> 倍にしたもの。ダウンは <see cref="Downed.Revive"/> で、分隊の味方が起こせる。
        /// </summary>
        public static GameRules BattleRoyaleRules()
        {
            return TeamDeathmatchRules()
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f)
                .WithReserveAmmoMultiplier(BattleRoyaleReserveAmmoMultiplier);
        }

        /// <summary>
        /// <see cref="ModeNames"/> の name のルールを作る。知らない名前なら <see cref="ArgumentException"/>。
        /// </summary>
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
                case HordeName:
                    return HordeRules();
                case BattleRoyaleName:
                    return BattleRoyaleRules();
                default:
                    throw new ArgumentException($"'{name}' is not one of the mode names ({string.Join(", ", ModeNames)})", nameof(name));
            }
        }

        /// <summary><see cref="ModeNames"/> の name のルールを置くパス (<see cref="Folder"/> の <c>&lt;名前&gt;.asset</c>)。知らない名前なら <see cref="ArgumentException"/>。</summary>
        public static string ModeRulesPath(string name)
        {
            return ModeRulesPath(Folder, name);
        }

        /// <summary>フリーフォーオールのアクション。<see cref="TeamDeathmatchActions"/> のうち、起こす味方がいないのでダウンしないようにしたもの。</summary>
        public static ActionRules FreeForAllActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        /// <summary>爆破のアクション。<see cref="TeamDeathmatchActions"/> のうち、倒れたらその場でラウンドから外れるようダウンしないようにしたもの。</summary>
        public static ActionRules SearchAndDestroyActions()
        {
            return TeamDeathmatchActions().WithDowned(Downed.Off);
        }

        /// <summary>
        /// TPS 射撃場のアクション。<see cref="RangeActions"/> に回避・乗り越え・崖のぼり・グレネード (<see cref="TpsRangeGrenadeCount"/> 個)・スライディング・怯みを足したもの。
        /// </summary>
        public static ActionRules TpsRangeActions()
        {
            return RangeActions()
                .WithDodge(Dodge.On)
                .WithVault(Vault.On)
                .WithMantle(Mantle.On)
                .WithGrenade(Grenade.On)
                .WithGrenadeCount(TpsRangeGrenadeCount)
                .WithSlide(Slide.On)
                .WithStagger(Stagger.On);
        }

        /// <summary>
        /// チームデスマッチのアクション。倒れたらダウンして味方が起こせ、アクティブリロードを使い、グレネードを
        /// <see cref="TeamDeathmatchGrenadeCount"/> 個持つ (オンラインではサーバーが数えて投げる)。
        /// カバーとブラインドファイアはオンラインのプレイヤーが対応していないので使わない。
        /// </summary>
        public static ActionRules TeamDeathmatchActions()
        {
            return WithDefaultDownedTimings(Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On)
                .WithGrenade(Grenade.On)
                .WithGrenadeCount(TeamDeathmatchGrenadeCount);
        }

        /// <summary>キャンペーン (どの難易度も) のアクション。カバー・ブラインドファイア・アクティブリロードを使い、ダウンはしない。</summary>
        public static ActionRules CampaignActions()
        {
            return WithDefaultDownedTimings(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On);
        }

        /// <summary>射撃場のアクション。カバー・ブラインドファイア・アクティブリロードを使い、ダウンはしない。</summary>
        public static ActionRules RangeActions()
        {
            return WithDefaultDownedTimings(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On);
        }

        /// <summary>path の <see cref="GameRulesDefinition"/> を読み、<see cref="GameRulesDefinition.ToRules"/> に通ることを確かめて返す。</summary>
        public static GameRulesDefinition LoadRules(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"game rules '{path}' were not found; run {MenuPath} (Fps.Editor.GameRulesAssetBuilder.Build) first");
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

        /// <summary>
        /// folder に <see cref="ModeNames"/> の各モードのルール (<c>&lt;名前&gt;.asset</c>) を作るか上書きして保存し、<see cref="ModeNames"/> の順に返す。
        /// folder は Assets/ 以下のフォルダ (無ければ作る)。ルールの検査に通らなければ、そのアセットから先は保存せずに例外を投げる。
        /// </summary>
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

        /// <summary>folder の中の、name のルールを置くパス。folder か name が不正なら <see cref="ArgumentException"/>。</summary>
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

        /// <summary>
        /// 各パスにモード別のルールを作るか上書きして保存し、
        /// teamDeathmatch・campaignEasy・campaignNormal・campaignHard・range・tpsRange・domination の順に返す。
        /// 7 つのパスは互いに異なる Assets/ 以下の .asset であること。
        /// </summary>
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

        /// <summary>definition の中身を rules の値にし、<see cref="GameRulesDefinition.ToRules"/> に通ることを確かめる。</summary>
        public static void Configure(GameRulesDefinition definition, GameRules rules)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            definition.CopyFrom(rules);

            // rules は検査済みだが、アセットの形で読み直せることも確かめる。不正なら保存しない。
            definition.ToRules();
        }

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
                source.IdleKickSeconds);
        }
    }
}
