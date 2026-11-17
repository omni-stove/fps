using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroy"/>) の HUD に出す文。表のキー <c>mode.snd.*</c> と引数の <see cref="LocalizedText"/> で返し、受け取った側が今の言語で文にする。
    /// 陣営名は <see cref="ModeHudText.SideName"/>、設置地点の名前は <see cref="SearchAndDestroyText.SiteName"/> と同じ。
    /// <see cref="SearchAndDestroyHudFormatter"/> が <see cref="SearchAndDestroyStatus"/> を読んでこれを呼ぶ。
    /// </summary>
    public static class SearchAndDestroyHudText
    {
        public static LocalizedText AttackRole => new LocalizedText("mode.snd.attackRole");
        public static LocalizedText DefendRole => new LocalizedText("mode.snd.defendRole");

        /// <summary>
        /// HUD の時計。待ちの間は開始までの秒数、設置前はラウンドの残り時間、設置後は起爆までの秒数 (<see cref="HudFormat.Time"/> の書式)。
        /// 言語に依らないので表を引かずにそのまま運ぶ。
        /// </summary>
        public static LocalizedText Clock(SearchAndDestroy game)
        {
            Require(game);
            if (game.Phase == RoundPhase.Preparing)
            {
                return LocalizedText.Literal(HudFormat.Time(game.Rounds.PreparationRemaining));
            }

            return LocalizedText.Literal(HudFormat.Time(IsPlantedOrAfter(game.Bomb) ? game.FuseRemaining : game.RoundRemaining));
        }

        /// <summary>
        /// 陣営ごとのラウンドの勝ち数と今の役割の行。ownSide があればその行を先頭に置く。勝ち数が単独で多い陣営を首位にする。
        /// </summary>
        /// <example><c>ALPHA 2 (攻撃)</c></example>
        public static IReadOnlyList<ModeHudScoreLine> Scores(SearchAndDestroy game, Side? ownSide)
        {
            Require(game);
            var attacker = game.Attacker;
            var defender = game.Defender;
            var attackerWins = game.GetRoundWins(attacker);
            var defenderWins = game.GetRoundWins(defender);
            var sides = attacker < defender ? new[] { attacker, defender } : new[] { defender, attacker };

            var lines = new List<ModeHudScoreLine>(2);
            foreach (var side in sides)
            {
                var isAttacker = side == attacker;
                var wins = isAttacker ? attackerWins : defenderWins;
                var other = isAttacker ? defenderWins : attackerWins;
                var text = new LocalizedText("mode.snd.score", ModeHudText.SideName(side), wins, isAttacker ? AttackRole : DefendRole);
                var line = new ModeHudScoreLine(side, text, side == ownSide, wins > other);
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
        /// HUD の得点の下に足す行。ラウンドの番号と先取数、自分の役割、待ちの間は前のラウンドの結果、ラウンド中は爆弾の様子
        /// (設置後は <c>起爆まで n 秒</c>)、設置と解除の進みを出す。
        /// </summary>
        /// <param name="game">表示する試合。</param>
        /// <param name="localPlayerId">この画面で操作しているプレイヤー。参加していなければ null。</param>
        public static IReadOnlyList<LocalizedText> Lines(SearchAndDestroy game, int? localPlayerId)
        {
            Require(game);
            var lines = new List<LocalizedText>();
            if (game.Phase == RoundPhase.Waiting)
            {
                return lines;
            }

            lines.Add(new LocalizedText("mode.snd.round", game.RoundNumber, game.Spec.WinsToWin));

            var local = localPlayerId.HasValue && game.Mode.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            if (local.HasValue && game.Phase != RoundPhase.Ended)
            {
                lines.Add(new LocalizedText(game.IsAttacker(local.Value) ? "mode.snd.youAttack" : "mode.snd.youDefend"));
            }

            if (game.Phase == RoundPhase.Preparing)
            {
                if (game.LastRoundEnd.HasValue)
                {
                    lines.Add(new LocalizedText("mode.snd.previousRound", RoundResult(game.LastRoundWinner, game.LastRoundEnd.Value)));
                }

                lines.Add(new LocalizedText("mode.snd.startsIn", HudFormat.DisplayedSeconds(game.Rounds.PreparationRemaining)));
                return lines;
            }

            if (game.Phase == RoundPhase.Ended)
            {
                if (game.LastRoundEnd.HasValue)
                {
                    lines.Add(new LocalizedText("mode.snd.lastRound", RoundResult(game.LastRoundWinner, game.LastRoundEnd.Value)));
                }

                return lines;
            }

            switch (game.Bomb)
            {
                case BombState.Carried:
                    lines.Add(new LocalizedText(local.HasValue && game.CarrierId == local
                        ? "mode.snd.youCarry"
                        : "mode.snd.carried"));
                    if (game.PlantingSite.HasValue)
                    {
                        lines.Add(new LocalizedText("mode.snd.planting", SearchAndDestroyText.SiteName(game.PlantingSite.Value), Percent(game.PlantFraction)));
                    }

                    break;
                case BombState.Dropped:
                    lines.Add(new LocalizedText("mode.snd.dropped"));
                    break;
                case BombState.Planted:
                    lines.Add(Fuse(game.PlantedSite.Value, game.FuseRemaining));
                    if (game.DefuserId.HasValue)
                    {
                        lines.Add(new LocalizedText("mode.snd.defusing", Percent(game.DefuseFraction)));
                    }

                    break;
                case BombState.Defused:
                    lines.Add(new LocalizedText("mode.snd.defused"));
                    break;
                case BombState.Detonated:
                    lines.Add(new LocalizedText("mode.snd.detonated"));
                    break;
            }

            return lines;
        }

        /// <summary>設置後の起爆までの秒数 (端数は切り上げ)。</summary>
        /// <example><c>Fuse(0, 12.3f)</c> は <c>爆弾 A に設置: 起爆まで 13 秒</c></example>
        public static LocalizedText Fuse(int site, float fuseRemaining)
        {
            return new LocalizedText("mode.snd.fuse", SearchAndDestroyText.SiteName(site), HudFormat.DisplayedSeconds(fuseRemaining));
        }

        /// <summary>ラウンドの結果。</summary>
        /// <example><c>RoundResult(Side.Bravo, RoundEndReason.Detonated)</c> は <c>BRAVO の勝ち (起爆)</c>、引き分けなら <c>引き分け (同時に全滅)</c></example>
        public static LocalizedText RoundResult(Side? winner, RoundEndReason reason)
        {
            var head = winner.HasValue ? HudFormat.Wins(ModeHudText.SideName(winner.Value)) : HudFormat.Draw;
            return new LocalizedText("mode.snd.roundResult", head, ReasonText(reason));
        }

        public static LocalizedText ReasonText(RoundEndReason reason)
        {
            switch (reason)
            {
                case RoundEndReason.Detonated:
                    return new LocalizedText("mode.snd.reason.detonated");
                case RoundEndReason.Defused:
                    return new LocalizedText("mode.snd.reason.defused");
                case RoundEndReason.DefendersEliminated:
                    return new LocalizedText("mode.snd.reason.defendersEliminated");
                case RoundEndReason.AttackersEliminated:
                    return new LocalizedText("mode.snd.reason.attackersEliminated");
                case RoundEndReason.TimeUp:
                    return new LocalizedText("mode.snd.reason.timeUp");
                case RoundEndReason.BothEliminated:
                    return new LocalizedText("mode.snd.reason.bothEliminated");
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a valid RoundEndReason");
            }
        }

        /// <summary>試合が終わったときの結果。終わっていなければ <c>default</c> (空の文)。</summary>
        public static LocalizedText Result(SearchAndDestroy game)
        {
            Require(game);
            return game.Phase == RoundPhase.Ended && game.Winner.HasValue
                ? HudFormat.Wins(ModeHudText.SideName(game.Winner.Value))
                : default;
        }

        private static bool IsPlantedOrAfter(BombState bomb)
        {
            return bomb == BombState.Planted || bomb == BombState.Defused || bomb == BombState.Detonated;
        }

        private static int Percent(float fraction)
        {
            var clamped = fraction > 0f ? Math.Min(1f, fraction) : 0f;
            return (int)Math.Floor(clamped * 100f);
        }

        private static void Require(SearchAndDestroy game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }
        }
    }
}
