using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // スタミナ・よろけ・制圧の調整値。よろけと制圧は Unity では Resources の JSON (Tuning/stagger・Tuning/suppression) から読むので、
    // サーバーは同じ形の JSON を server-data/tuning から読み、無ければ同じく既定値で動かす。
    // スタミナは Unity に JSON が無く StaminaSpec の既定値で動くので、ファイルを置いたときだけ読む
    internal sealed class ServerPlayerStatusTuning
    {
        public const string StaminaFileName = "stamina.json";
        public const string StaggerFileName = "stagger.json";
        public const string SuppressionFileName = "suppression.json";

        public ServerPlayerStatusTuning(
            StaminaSpec stamina = null,
            string staminaPath = null,
            StaggerTuning stagger = null,
            string staggerPath = null,
            SuppressionSpec suppression = null,
            string suppressionPath = null)
        {
            Stamina = stamina ?? StaminaSpec.Default;
            StaminaPath = staminaPath;
            Stagger = stagger ?? StaggerTuning.Default;
            StaggerPath = staggerPath;
            Suppression = suppression ?? SuppressionSpec.Default;
            SuppressionPath = suppressionPath;
        }

        public static ServerPlayerStatusTuning Default { get; } = new ServerPlayerStatusTuning();

        public StaminaSpec Stamina { get; }

        public string StaminaPath { get; }

        // 敵の値も読むのは、Unity と同じファイルをそのまま置けるようにするため。サーバーが使うのはプレイヤーの値だけ
        public StaggerTuning Stagger { get; }

        public string StaggerPath { get; }

        public SuppressionSpec Suppression { get; }

        public string SuppressionPath { get; }

        public string LogLine => ServerPlayerStatusLog.Tuning(StaminaPath, StaggerPath, SuppressionPath);

        public static bool TryLoad(IReadOnlyList<string> searchRoots, out ServerPlayerStatusTuning tuning, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);
            tuning = null;
            if (!ServerCombatTuning.TryRead(searchRoots, StaminaFileName, "stamina", StaminaJson.Parse, out var stamina, out var staminaPath, out error)
                || !ServerCombatTuning.TryRead(searchRoots, StaggerFileName, "stagger", StaggerJson.Parse, out var stagger, out var staggerPath, out error)
                || !ServerCombatTuning.TryRead(
                    searchRoots, SuppressionFileName, "suppression", SuppressionJson.Parse, out var suppression, out var suppressionPath, out error))
            {
                return false;
            }

            tuning = new ServerPlayerStatusTuning(stamina, staminaPath, stagger, staggerPath, suppression, suppressionPath);
            return true;
        }
    }
}
