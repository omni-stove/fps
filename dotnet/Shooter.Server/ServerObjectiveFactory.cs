using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // parts は Battle Royale・Extraction の試合の場と持ち物庫。部品をつながずに目的だけを作るときは null が来る
    internal delegate IMatchObjectiveRules ServerObjectiveCreator(NetMatchSettings settings, IMatchObjectiveHost host, ServerObjectiveParts parts);

    internal static class ServerObjectiveFactory
    {
        // 後から足す目的は Create を書き換えず、ここに 1 行ずつ並べる。ここに無い目的 (列挙にだけ先に足したもの) は Create が断る
        private static readonly Dictionary<MatchObjective, ServerObjectiveCreator> _creators = Register(
            (MatchObjective.None, (settings, host, parts) => new TeamKillsRules()),
            (MatchObjective.Domination, (settings, host, parts) => new DominationRules(settings, host)),
            (MatchObjective.Elimination, (settings, host, parts) => new EliminationRules(settings, host)),
            (MatchObjective.CaptureTheFlag, (settings, host, parts) => new CaptureTheFlagRules(settings, host)),
            (MatchObjective.Hardpoint, (settings, host, parts) => new HardpointRules(settings, host)),
            (MatchObjective.SearchAndDestroy, (settings, host, parts) => new SearchAndDestroyRules(settings, host)),
            (MatchObjective.FreeForAll, (settings, host, parts) => new FreeForAllRules(settings, host)),
            (MatchObjective.KillConfirmed, (settings, host, parts) => new KillConfirmedRules(settings, host)),
            (MatchObjective.GunGame, (settings, host, parts) => new GunGameRules(settings, host)),
            (MatchObjective.Payload, (settings, host, parts) => new PayloadRules(settings, host)),
            (MatchObjective.BattleRoyale, (settings, host, parts) => CreateBattleRoyale(host, RequireParts(MatchObjective.BattleRoyale, parts))),
            (MatchObjective.Extraction, (settings, host, parts) => CreateExtraction(host, RequireParts(MatchObjective.Extraction, parts))));

        public static IReadOnlyCollection<MatchObjective> Supported => _creators.Keys;

        public static bool Supports(MatchObjective objective)
        {
            return _creators.ContainsKey(objective);
        }

        public static IMatchObjectiveRules Create(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host)
        {
            return Create(objective, settings, host, null);
        }

        // parts が null なら (部品をつながずに目的だけを作るなら) Battle Royale・Extraction を理由を付けて断る
        public static IMatchObjectiveRules Create(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host, ServerObjectiveParts parts)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(host);
            if (!_creators.TryGetValue(objective, out var create))
            {
                throw new InvalidOperationException("the dedicated server has no rules for the match objective " + objective);
            }

            return create(settings, host, parts);
        }

        internal static Dictionary<MatchObjective, ServerObjectiveCreator> Register(params (MatchObjective Objective, ServerObjectiveCreator Create)[] entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            var creators = new Dictionary<MatchObjective, ServerObjectiveCreator>();
            foreach (var (objective, create) in entries)
            {
                if (create == null)
                {
                    throw new ArgumentException("the match objective " + objective + " is registered without rules", nameof(entries));
                }

                if (!creators.TryAdd(objective, create))
                {
                    throw new ArgumentException("the match objective " + objective + " is registered twice", nameof(entries));
                }
            }

            return creators;
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
