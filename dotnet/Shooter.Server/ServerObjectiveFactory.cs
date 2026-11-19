using System;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerObjectiveFactory
    {
        public static IMatchObjectiveRules Create(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host)
        {
            return Create(objective, settings, host, null);
        }

        // parts は Battle Royale・Extraction の試合の場と持ち物庫。null なら (部品をつながずに目的だけを作るなら) その 2 つを理由を付けて断る
        public static IMatchObjectiveRules Create(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host, ServerObjectiveParts parts)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(host);
            switch (objective)
            {
                case MatchObjective.None:
                    return new TeamKillsRules();
                case MatchObjective.Domination:
                    return new DominationRules(settings, host);
                case MatchObjective.Elimination:
                    return new EliminationRules(settings, host);
                case MatchObjective.CaptureTheFlag:
                    return new CaptureTheFlagRules(settings, host);
                case MatchObjective.Hardpoint:
                    return new HardpointRules(settings, host);
                case MatchObjective.SearchAndDestroy:
                    return new SearchAndDestroyRules(settings, host);
                case MatchObjective.FreeForAll:
                    return new FreeForAllRules(settings, host);
                case MatchObjective.KillConfirmed:
                    return new KillConfirmedRules(settings, host);
                case MatchObjective.GunGame:
                    return new GunGameRules(settings, host);
                case MatchObjective.Payload:
                    return new PayloadRules(settings, host);
                case MatchObjective.BattleRoyale:
                    return CreateBattleRoyale(host, RequireParts(objective, parts));
                case MatchObjective.Extraction:
                    return CreateExtraction(host, RequireParts(objective, parts));
                default:
                    throw new InvalidOperationException("the dedicated server has no rules for the match objective " + objective);
            }
        }

        // 調整値 (battle-royale.json) は NetMatchSettings に無く、ルールを替えても同じファイルのまま (ServerRulesData.WithRules) なので、今のルールの読んだ値を使う
        private static BattleRoyaleRules CreateBattleRoyale(IMatchObjectiveHost host, ServerObjectiveParts parts)
        {
            var spec = parts.Rules.Data?.BattleRoyale ?? BattleRoyaleSpec.Default;
            return new BattleRoyaleRules(host, spec, parts.World, parts.Seed);
        }

        // ExtractionRules も持ち物庫が無ければ断るが、起動のときと同じ理由 (--stash-dir を渡す) を試合の途中の替え先にも出すため、先に確かめる
        private static ExtractionRules CreateExtraction(IMatchObjectiveHost host, ServerObjectiveParts parts)
        {
            if (parts.Stashes == null)
            {
                throw new InvalidOperationException(ServerLaunch.NoStashDirectoryProblem);
            }

            return new ExtractionRules(host, parts.Rules, parts.Loot, parts.Stashes, parts.Seed);
        }

        private static ServerObjectiveParts RequireParts(MatchObjective objective, ServerObjectiveParts parts)
        {
            return parts ?? throw new InvalidOperationException(
                "the " + objective + " rules need the loot module and the match world; create the server modules with ServerModules.Create");
        }
    }
}
