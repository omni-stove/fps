using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;

namespace Shooter.Server
{
    // 近接・ストリーク・設置物の調整値。Unity では Resources の JSON (Tuning/melee・Tuning/streaks) と設置物の一覧から読むので、
    // サーバーは同じ形の JSON を server-data/tuning から読み、無ければ同じく既定値 (設置物は何も置かせない) で動かす。
    // アンチチート (anticheat.json) はサーバーだけが使うが、探し方と壊れたファイルで止めるところを他とそろえるため、ここで一緒に読む
    internal sealed class ServerCombatTuning
    {
        public const string MeleeFileName = "melee.json";
        public const string StreaksFileName = "streaks.json";
        public const string AntiCheatFileName = "anticheat.json";

        public ServerCombatTuning(
            MeleeSpec melee = null,
            string meleePath = null,
            StreakSpec streaks = null,
            string streaksPath = null,
            DeployCatalog deployables = null,
            string deployablesPath = null,
            AntiCheatTuning antiCheat = null,
            string antiCheatPath = null)
        {
            Melee = melee ?? MeleeSpec.Default;
            MeleePath = meleePath;
            Streaks = streaks ?? StreakSpec.Default;
            StreaksPath = streaksPath;
            Deployables = deployables ?? DeployCatalog.Empty;
            DeployablesPath = deployablesPath;
            AntiCheat = antiCheat ?? AntiCheatTuning.Default;
            AntiCheatPath = antiCheatPath;
        }

        public static ServerCombatTuning Default { get; } = new ServerCombatTuning();

        public MeleeSpec Melee { get; }

        public string MeleePath { get; }

        public StreakSpec Streaks { get; }

        public string StreaksPath { get; }

        public DeployCatalog Deployables { get; }

        public string DeployablesPath { get; }

        public AntiCheatTuning AntiCheat { get; }

        public string AntiCheatPath { get; }

        public string LogLine => ServerProgram.LogTag + " combat tuning"
            + " melee=" + (MeleePath ?? "default")
            + " streaks=" + (StreaksPath ?? "default")
            + " deployables=" + (DeployablesPath ?? "none")
            + " anticheat=" + (AntiCheatPath ?? "default");

        // ルールのファイルと同じく、探す場所を前から順に見て最初に見つかったファイルを使う。読めないファイルは既定値にせず止める
        public static bool TryLoad(IReadOnlyList<string> searchRoots, out ServerCombatTuning tuning, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);
            tuning = null;
            if (!TryRead(searchRoots, MeleeFileName, "melee", MeleeJson.Parse, out var melee, out var meleePath, out error)
                || !TryRead(searchRoots, StreaksFileName, "streak", StreakJson.Parse, out var streaks, out var streaksPath, out error)
                || !ServerDeployables.TryLoadCatalog(searchRoots, out var deployables, out var deployablesPath, out error)
                || !TryRead(searchRoots, AntiCheatFileName, "anticheat", AntiCheatTuningJson.Parse, out var antiCheat, out var antiCheatPath, out error))
            {
                return false;
            }

            tuning = new ServerCombatTuning(
                melee, meleePath, streaks, streaksPath, deployables, deployablesPath, antiCheat, antiCheatPath);
            return true;
        }

        private static bool TryRead<T>(
            IReadOnlyList<string> searchRoots, string fileName, string what, Func<string, string, T> parse, out T value, out string path, out string error)
            where T : class
        {
            value = null;
            path = null;
            foreach (var root in searchRoots)
            {
                var candidate = Path.GetFullPath(
                    Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, fileName));
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    value = parse(File.ReadAllText(candidate), candidate);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read " + what + " tuning: " + e.Message;
                    return false;
                }

                path = candidate;
                error = null;
                return true;
            }

            error = null;
            return true;
        }
    }
}
