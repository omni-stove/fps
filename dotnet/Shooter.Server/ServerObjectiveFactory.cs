using System;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerObjectiveFactory
    {
        public static IMatchObjectiveRules Create(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host)
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
                default:
                    throw new InvalidOperationException("the dedicated server has no rules for the match objective " + objective);
            }
        }
    }
}
