using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class GunGame : IWinCondition
    {
        private readonly Dictionary<int, int> _tiers = new Dictionary<int, int>();

        public GunGame(GunGameLadder ladder)
        {
            Ladder = ladder ?? throw new ArgumentNullException(nameof(ladder));
        }

        public GunGameLadder Ladder { get; }

        public int? FinisherId { get; private set; }

        public Side? FinisherSide { get; private set; }

        public double? ScoreLimit => null;

        public int GetTier(int playerId)
        {
            return _tiers.TryGetValue(playerId, out var tier) ? tier : 0;
        }

        public int GetWeaponTier(int playerId)
        {
            return Math.Min(GetTier(playerId), Ladder.TierCount - 1);
        }

        public GunGameTier GetWeapon(int playerId)
        {
            return Ladder.Tiers[GetWeaponTier(playerId)];
        }

        public GunGameTier? GetNextWeapon(int playerId)
        {
            var next = GetTier(playerId) + 1;
            return next < Ladder.TierCount ? Ladder.Tiers[next] : (GunGameTier?)null;
        }

        // ModeSession.ReportKill とは別にキルを受けるのは、どの武器で倒したかが ModeSession.ReportKill に渡らないため。
        public GunGameKill ReportKill(ModeSession session, int killerId, int victimId, string weaponId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var killerSide = session.GetSide(killerId);
            var victimSide = session.GetSide(victimId);
            var killerBefore = GetTier(killerId);
            var victimBefore = GetTier(victimId);

            if (session.State != GameModeState.Playing || FinisherId.HasValue || killerId == victimId || killerSide == victimSide)
            {
                return new GunGameKill(killerId, killerBefore, killerBefore, victimId, victimBefore, victimBefore, false);
            }

            var weapon = weaponId == null ? string.Empty : weaponId.Trim();

            var victimAfter = victimBefore;
            if (Ladder.DemotesOnMelee && Ladder.IsMelee(weapon) && victimBefore > 0)
            {
                victimAfter = victimBefore - 1;
                _tiers[victimId] = victimAfter;
            }

            var killerAfter = killerBefore;
            var finished = false;
            if (weapon.Length > 0 && string.Equals(weapon, GetWeapon(killerId).WeaponId, StringComparison.Ordinal))
            {
                killerAfter = killerBefore + 1;
                _tiers[killerId] = killerAfter;
                if (killerAfter >= Ladder.TierCount)
                {
                    finished = true;
                    FinisherId = killerId;
                    FinisherSide = killerSide;
                }
            }

            return new GunGameKill(killerId, killerBefore, killerAfter, victimId, victimBefore, victimAfter, finished);
        }

        // ReportKill で進めずに段を直に書くのは、オンラインのクライアントにはサーバーが決めた段だけが届き、どの武器で倒したかが届かないため。
        public void SetTier(int playerId, int tier, Side side)
        {
            if (tier < 0 || tier > Ladder.TierCount)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "tier must be 0 to the tier count of the ladder");
            }

            if (tier == 0)
            {
                _tiers.Remove(playerId);
            }
            else
            {
                _tiers[playerId] = tier;
            }

            if (tier == Ladder.TierCount && !FinisherId.HasValue)
            {
                FinisherId = playerId;
                FinisherSide = side;
            }
        }

        public void Reset()
        {
            _tiers.Clear();
            FinisherId = null;
            FinisherSide = null;
        }

        public ModeOutcome CheckStanding(ModeSession session)
        {
            return FinisherSide.HasValue ? ModeOutcome.Win(FinisherSide.Value) : ModeOutcome.Undecided;
        }

        public ModeOutcome CheckTimeUp(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Side? leader = null;
            var best = -1;
            var tied = false;
            foreach (var playerId in session.GetPlayerIds())
            {
                var tier = GetTier(playerId);
                if (tier > best)
                {
                    best = tier;
                    leader = session.GetSide(playerId);
                    tied = false;
                }
                else if (tier == best)
                {
                    tied = true;
                }
            }

            return ModeOutcome.Decided(tied ? null : leader);
        }
    }
}
