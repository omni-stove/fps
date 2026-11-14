using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// バトルロイヤル (<see cref="BattleRoyaleSession"/>) の HUD に出す文 (表のキー <c>mode.br.*</c> の <see cref="LocalizedText"/>)。
    /// 得点の欄には分隊ごとの残り人数 (脱落した分隊は順位) を、
    /// その下に残り分隊の数・自分の順位・縮むエリアの案内・自分の分隊の様子・ダウンや蘇生の進み・武器を持っていない案内を出す。
    /// </summary>
    public static class BattleRoyaleHudText
    {
        /// <summary>武器を持っていない人への案内。</summary>
        public static LocalizedText NoWeapon => new LocalizedText("mode.br.noWeapon");

        /// <example><c>SquadsLeft(3, 4)</c> は <c>残り分隊 3/4</c></example>
        /// <exception cref="ArgumentOutOfRangeException">remaining が負か total より多い。</exception>
        public static LocalizedText SquadsLeft(int remaining, int total)
        {
            if (remaining < 0 || remaining > total)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must be between 0 and total");
            }

            return new LocalizedText("mode.br.squadsLeft", remaining, total);
        }

        /// <summary>
        /// 自分の分隊の順位。順位が決まっていれば (脱落したか試合が終わった) その順位、まだ残っていれば残っている分隊の数を「以内」で出す。
        /// </summary>
        /// <example><c>Placement(3, 2)</c> は <c>順位 3 位</c>、<c>Placement(null, 2)</c> は <c>順位 2 位以内</c></example>
        /// <exception cref="ArgumentOutOfRangeException">place か remaining が 1 未満。</exception>
        public static LocalizedText Placement(int? place, int remaining)
        {
            if (place.HasValue)
            {
                if (place.Value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(place), place.Value, "place must be at least 1");
                }

                return new LocalizedText("mode.br.placement", place.Value);
            }

            if (remaining < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must be at least 1 while the squad is still in");
            }

            return new LocalizedText("mode.br.placementAtLeast", remaining);
        }

        /// <summary>分隊の立っている人数。ダウン中がいればその数を添える。</summary>
        /// <example><c>Squad(2, 1)</c> は <c>分隊 生存 2 (ダウン 1)</c>、<c>Squad(2, 0)</c> は <c>分隊 生存 2</c></example>
        /// <exception cref="ArgumentOutOfRangeException">standing か downed が負。</exception>
        public static LocalizedText Squad(int standing, int downed)
        {
            if (standing < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(standing), standing, "standing must not be negative");
            }

            if (downed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(downed), downed, "downed must not be negative");
            }

            var text = new LocalizedText("mode.br.squad", standing);
            return downed == 0 ? text : ModeHudText.WithDowned(text, downed);
        }

        /// <summary>
        /// 得点の欄に出す、開始時に人のいた分隊ごとの行。残っていれば <c>SIDE 2 残り 2</c>、順位が付いていれば <c>SIDE 2 3 位</c>。
        /// ownSide があればその行を先頭にし、残りは分隊の ID の昇順。順位が 1 の分隊を先頭に立っているとする。
        /// </summary>
        public static IReadOnlyList<ModeHudScoreLine> Squads(BattleRoyaleSession session, Side? ownSide)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var mode = session.Mode;
            var lines = new List<ModeHudScoreLine>();
            foreach (var side in mode.Sides)
            {
                var place = session.GetPlacement(side);
                if (!place.HasValue && mode.CountMembers(side) == 0)
                {
                    continue;
                }

                var text = place.HasValue
                    ? new LocalizedText("mode.br.squadPlace", ModeHudText.SideName(side), place.Value)
                    : ModeHudText.Remaining(side, session.CountRemaining(side));
                var line = new ModeHudScoreLine(side, text, side == ownSide, place == 1);
                if (line.IsOwn)
                {
                    lines.Insert(0, line);
                }
                else
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        /// 得点の下に出す行。残り分隊の数、localPlayerId がいれば自分の順位、zoneLine (空の文でなければ)、自分の分隊の様子、
        /// 自分がダウン中ならその案内 (<see cref="HudFormat.Downed(int, int)"/>)、reviveTargetId を起こしているならその進み、
        /// unarmed なら <see cref="NoWeapon"/>。
        /// </summary>
        /// <param name="session">読むモード。</param>
        /// <param name="localPlayerId">この画面で操作している人。いなければ null。</param>
        /// <param name="reviveTargetId">localPlayerId がいま起こしている相手。起こしていなければ null。</param>
        /// <param name="zoneLine">縮むエリアの 1 行 (<see cref="ZoneHudText.Line"/>)。エリアが無ければ <c>default</c> (空の文)。</param>
        /// <param name="unarmed">localPlayerId が武器を持っていなければ true。</param>
        public static IReadOnlyList<LocalizedText> Lines(
            BattleRoyaleSession session, int? localPlayerId, int? reviveTargetId, LocalizedText zoneLine, bool unarmed)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var mode = session.Mode;
            var lines = new List<LocalizedText>();
            if (session.EntrantCount > 0)
            {
                lines.Add(SquadsLeft(session.SquadsRemaining, session.EntrantCount));
            }

            var local = localPlayerId.HasValue && mode.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            if (local.HasValue && session.EntrantCount > 0)
            {
                var squad = mode.GetSide(local.Value);
                var place = session.GetPlacement(squad);
                if (place.HasValue || session.SquadsRemaining > 0)
                {
                    lines.Add(Placement(place, session.SquadsRemaining));
                }
            }

            if (!zoneLine.IsEmpty)
            {
                lines.Add(zoneLine);
            }

            if (!local.HasValue || mode.State != GameModeState.Playing)
            {
                return lines;
            }

            var ownSquad = mode.GetSide(local.Value);
            lines.Add(Squad(session.CountStanding(ownSquad), session.CountDowned(ownSquad)));

            if (session.IsDowned(local.Value))
            {
                var bleedout = session.GetBleedoutRemaining(local.Value) ?? 0f;
                lines.Add(HudFormat.Downed(HudFormat.DisplayedSeconds(bleedout), HudFormat.RevivePercent(session.GetReviveProgress(local.Value))));
                return lines;
            }

            if (reviveTargetId.HasValue && mode.HasPlayer(reviveTargetId.Value) && session.IsDowned(reviveTargetId.Value))
            {
                lines.Add(HudFormat.Reviving(HudFormat.RevivePercent(session.GetReviveProgress(reviveTargetId.Value))));
            }

            if (unarmed && session.IsStanding(local.Value))
            {
                lines.Add(NoWeapon);
            }

            return lines;
        }

        /// <summary>試合が終わったときの結果。終わっていなければ <c>default</c> (空の文)。</summary>
        /// <example><c>SIDE 1 の勝ち</c>、引き分けなら <c>引き分け</c></example>
        public static LocalizedText Result(BattleRoyaleSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return ModeHudText.Result(session.Mode);
        }
    }
}
