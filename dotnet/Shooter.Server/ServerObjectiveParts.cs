using System;

namespace Shooter.Server
{
    // Battle Royale と Extraction は設定と IMatchObjectiveHost だけでは作れず、試合の場 (箱・移動・武器) と持ち物庫に触れるので、
    // ServerModules が同じ一覧に並べた部品をここにまとめて目的の工場へ渡す
    internal sealed class ServerObjectiveParts
    {
        // stashes は起動で --stash-dir を渡されなければ null。そのときは Extraction を作らずに理由を返す
        public ServerObjectiveParts(MatchRulesModule rules, ServerLoot loot, IBattleRoyaleWorld world, ServerStashStore stashes, int? seed = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Loot = loot ?? throw new ArgumentNullException(nameof(loot));
            World = world ?? throw new ArgumentNullException(nameof(world));
            Stashes = stashes;
            Seed = seed;
        }

        public MatchRulesModule Rules { get; }

        public ServerLoot Loot { get; }

        public IBattleRoyaleWorld World { get; }

        public ServerStashStore Stashes { get; }

        // null なら目的ごとに起動の時刻から決める。テストで同じ動きを繰り返すために渡す
        public int? Seed { get; }
    }
}
