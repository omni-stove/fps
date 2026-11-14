using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ガンゲームの段の進み方と勝ち方。<see cref="ModeSession"/> の勝利条件として渡し、キルは <see cref="ReportKill"/> で別に伝える
    /// (どの武器で倒したかは <see cref="ModeSession.ReportKill"/> に渡らないため)。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>全員 0 段目から始める。別の陣営の人を今の段の武器で倒すと 1 段進む。今の段と違う武器 (前の段の弾・グレネードなど) のキルでは進まない。</item>
    /// <item>並びの近接の武器 (<see cref="GunGameLadder.IsMelee"/>) で倒されたら、<see cref="GunGameLadder.DemotesOnMelee"/> のときは倒された人が 1 段戻る (0 段目より下へは行かない)。
    /// 近接の武器が倒した人の今の段の武器なら、倒した人は進みもする。</item>
    /// <item>最後の段の武器で倒した最初の人の勝ち。決まった後のキルでは誰の段も動かさない。<see cref="ModeSession"/> は次の
    /// <see cref="ModeSession.Tick"/> の頭 (<see cref="CheckStanding"/>) でその人の陣営の勝ちで終える。</item>
    /// <item>時間切れでは段のいちばん進んだ人の陣営の勝ち。いちばん進んだ人が複数いれば引き分け。</item>
    /// <item>得点の上限は持たない。陣営の得点 (キル数など) は別の <see cref="IScoreRule"/> が足し、勝敗には使わない。</item>
    /// </list>
    /// 自滅・味方のキル・Playing 以外のときのキルでは段を動かさない。
    /// </remarks>
    public sealed class GunGame : IWinCondition
    {
        private readonly Dictionary<int, int> _tiers = new Dictionary<int, int>();

        public GunGame(GunGameLadder ladder)
        {
            Ladder = ladder ?? throw new ArgumentNullException(nameof(ladder));
        }

        public GunGameLadder Ladder { get; }

        /// <summary>最後の段の武器で倒して勝ちを決めた人。まだいなければ null。</summary>
        public int? FinisherId { get; private set; }

        /// <summary><see cref="FinisherId"/> の人の陣営。まだいなければ null。</summary>
        public Side? FinisherSide { get; private set; }

        public double? ScoreLimit => null;

        /// <summary>playerId の段 (0 から)。最後の段を終えた人は <see cref="GunGameLadder.TierCount"/>。まだ動いていない人は 0。</summary>
        public int GetTier(int playerId)
        {
            return _tiers.TryGetValue(playerId, out var tier) ? tier : 0;
        }

        /// <summary>playerId が今持つ武器の段 (0 から TierCount - 1)。最後の段を終えた人は最後の段の武器のまま。</summary>
        public int GetWeaponTier(int playerId)
        {
            return Math.Min(GetTier(playerId), Ladder.TierCount - 1);
        }

        /// <summary>playerId が今持つ武器の段。</summary>
        public GunGameTier GetWeapon(int playerId)
        {
            return Ladder.Tiers[GetWeaponTier(playerId)];
        }

        /// <summary>playerId の次の段。今が最後の段 (か終えた) なら null。</summary>
        public GunGameTier? GetNextWeapon(int playerId)
        {
            var next = GetTier(playerId) + 1;
            return next < Ladder.TierCount ? Ladder.Tiers[next] : (GunGameTier?)null;
        }

        /// <summary>
        /// killerId が victimId を weaponId の武器で倒したことを伝え、段を動かして結果を返す。<see cref="ModeSession.ReportKill"/> の後に呼ぶ。
        /// 動かさなかったときも、前後の段が同じ結果を返す。
        /// </summary>
        /// <param name="session">このガンゲームを勝利条件に持つモード。陣営と状態を読む。</param>
        /// <param name="killerId">倒した人。</param>
        /// <param name="victimId">倒された人。</param>
        /// <param name="weaponId">倒した武器の Id。分からなければ null か空 (近接でも今の段の武器でもないものとして扱う)。</param>
        /// <exception cref="ArgumentNullException">session が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">killerId か victimId が session に参加していない。</exception>
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

        /// <summary>全員の段を 0 に戻し、勝ちを決めた人を消す。次の試合を <see cref="ModeSession.Reset"/> で始め直すときに一緒に呼ぶ。</summary>
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
