using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroy"/>) の HUD に出す文字列と、<c>[SearchAndDestroy]</c> 付きのログの文言。
    /// 陣営名は <see cref="ModeHudText.SideName"/>、設置地点の名前は <see cref="DominationHudFormat.PointName"/> (0 = A, 1 = B) と同じ。
    /// </summary>
    public static class SearchAndDestroyText
    {
        public const string Tag = "[SearchAndDestroy]";

        public const string AttackRole = "攻撃";
        public const string DefendRole = "防衛";

        /// <example><c>SiteName(1)</c> は <c>B</c></example>
        public static string SiteName(int site) => DominationHudFormat.PointName(site);

        /// <summary>
        /// HUD の時計。待ちの間は開始までの秒数、設置前はラウンドの残り時間、設置後は起爆までの秒数 (<see cref="HudFormat.Time"/> の書式)。
        /// </summary>
        public static string Clock(SearchAndDestroy game)
        {
            Require(game);
            if (game.Phase == RoundPhase.Preparing)
            {
                return HudFormat.Time(game.Rounds.PreparationRemaining);
            }

            return HudFormat.Time(IsPlantedOrAfter(game.Bomb) ? game.FuseRemaining : game.RoundRemaining);
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
                var text = ModeHudText.SideName(side) + " " + Number(wins) + " (" + (isAttacker ? AttackRole : DefendRole) + ")";
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
        public static IReadOnlyList<string> Lines(SearchAndDestroy game, int? localPlayerId)
        {
            Require(game);
            var lines = new List<string>();
            if (game.Phase == RoundPhase.Waiting)
            {
                return lines;
            }

            lines.Add("ラウンド " + Number(game.RoundNumber) + " (" + Number(game.Spec.WinsToWin) + " 勝先取)");

            var local = localPlayerId.HasValue && game.Mode.HasPlayer(localPlayerId.Value) ? localPlayerId : null;
            if (local.HasValue && game.Phase != RoundPhase.Ended)
            {
                lines.Add(game.IsAttacker(local.Value)
                    ? "あなたは攻撃側: 爆弾を設置地点に設置する"
                    : "あなたは防衛側: 設置を防ぐか解除する");
            }

            if (game.Phase == RoundPhase.Preparing)
            {
                if (game.LastRoundEnd.HasValue)
                {
                    lines.Add("前のラウンド: " + RoundResult(game.LastRoundWinner, game.LastRoundEnd.Value));
                }

                lines.Add("開始まで " + Number(HudFormat.DisplayedSeconds(game.Rounds.PreparationRemaining)) + " 秒");
                return lines;
            }

            if (game.Phase == RoundPhase.Ended)
            {
                if (game.LastRoundEnd.HasValue)
                {
                    lines.Add("最後のラウンド: " + RoundResult(game.LastRoundWinner, game.LastRoundEnd.Value));
                }

                return lines;
            }

            switch (game.Bomb)
            {
                case BombState.Carried:
                    lines.Add(local.HasValue && game.CarrierId == local
                        ? "爆弾を持っている: 設置地点で長押しして設置"
                        : "爆弾: 攻撃側が所持");
                    if (game.PlantingSite.HasValue)
                    {
                        lines.Add("設置中 " + SiteName(game.PlantingSite.Value) + " " + Percent(game.PlantFraction));
                    }

                    break;
                case BombState.Dropped:
                    lines.Add("爆弾: 落ちている");
                    break;
                case BombState.Planted:
                    lines.Add(Fuse(game.PlantedSite.Value, game.FuseRemaining));
                    if (game.DefuserId.HasValue)
                    {
                        lines.Add("解除中 " + Percent(game.DefuseFraction));
                    }

                    break;
                case BombState.Defused:
                    lines.Add("爆弾を解除した");
                    break;
                case BombState.Detonated:
                    lines.Add("爆弾が起爆した");
                    break;
            }

            return lines;
        }

        /// <summary>設置後の起爆までの秒数 (端数は切り上げ)。</summary>
        /// <example><c>Fuse(0, 12.3f)</c> は <c>爆弾 A に設置: 起爆まで 13 秒</c></example>
        public static string Fuse(int site, float fuseRemaining)
        {
            return "爆弾 " + SiteName(site) + " に設置: 起爆まで " + Number(HudFormat.DisplayedSeconds(fuseRemaining)) + " 秒";
        }

        /// <summary>ラウンドの結果。</summary>
        /// <example><c>RoundResult(Side.Bravo, RoundEndReason.Detonated)</c> は <c>BRAVO の勝ち (起爆)</c>、引き分けなら <c>引き分け (同時に全滅)</c></example>
        public static string RoundResult(Side? winner, RoundEndReason reason)
        {
            var head = winner.HasValue ? ModeHudText.SideName(winner.Value) + " の勝ち" : HudFormat.Draw;
            return head + " (" + ReasonText(reason) + ")";
        }

        public static string ReasonText(RoundEndReason reason)
        {
            switch (reason)
            {
                case RoundEndReason.Detonated:
                    return "起爆";
                case RoundEndReason.Defused:
                    return "解除";
                case RoundEndReason.DefendersEliminated:
                    return "防衛側が全滅";
                case RoundEndReason.AttackersEliminated:
                    return "攻撃側が全滅";
                case RoundEndReason.TimeUp:
                    return "時間切れ";
                case RoundEndReason.BothEliminated:
                    return "同時に全滅";
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a valid RoundEndReason");
            }
        }

        /// <summary>試合が終わったときの結果。終わっていなければ <see cref="HudFormat.NoResult"/>。</summary>
        public static string Result(SearchAndDestroy game)
        {
            Require(game);
            return game.Phase == RoundPhase.Ended && game.Winner.HasValue
                ? ModeHudText.SideName(game.Winner.Value) + " の勝ち"
                : HudFormat.NoResult;
        }

        /// <example><c>[SearchAndDestroy] round=3 attacker=1 defender=0</c></example>
        public static string RoundLog(int round, Side attacker, Side defender)
        {
            return Tag + " round=" + Number(round) + " attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        /// <example><c>[SearchAndDestroy] live round=3</c></example>
        public static string LiveLog(int round)
        {
            return Tag + " live round=" + Number(round);
        }

        /// <example><c>[SearchAndDestroy] round_end round=3 winner=1 reason=Detonated wins=0:1,1:2</c>、引き分けなら <c>winner=draw</c></example>
        public static string RoundEndLog(int round, Side? winner, RoundEndReason reason, int alphaWins, int bravoWins)
        {
            return Tag + " round_end round=" + Number(round)
                + " winner=" + (winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw)
                + " reason=" + reason
                + " wins=" + Number(Side.Alpha.Id) + ":" + Number(alphaWins) + "," + Number(Side.Bravo.Id) + ":" + Number(bravoWins);
        }

        /// <example><c>[SearchAndDestroy] swap attacker=1 defender=0</c></example>
        public static string SwapLog(Side attacker, Side defender)
        {
            return Tag + " swap attacker=" + Number(attacker.Id) + " defender=" + Number(defender.Id);
        }

        /// <example><c>[SearchAndDestroy] pickup player=2</c></example>
        public static string PickUpLog(int playerId) => Tag + " pickup player=" + Number(playerId);

        /// <example><c>[SearchAndDestroy] drop player=2</c></example>
        public static string DropLog(int playerId) => Tag + " drop player=" + Number(playerId);

        /// <example><c>[SearchAndDestroy] planted player=0 site=A</c></example>
        public static string PlantedLog(int playerId, int site) => Tag + " planted player=" + Number(playerId) + " site=" + SiteName(site);

        /// <example><c>[SearchAndDestroy] defused player=4</c></example>
        public static string DefusedLog(int playerId) => Tag + " defused player=" + Number(playerId);

        /// <example><c>[SearchAndDestroy] detonated site=B</c></example>
        public static string DetonatedLog(int site) => Tag + " detonated site=" + SiteName(site);

        /// <example><c>[SearchAndDestroy] ended winner=0</c></example>
        public static string EndedLog(Side winner) => Tag + " ended winner=" + Number(winner.Id);

        private static bool IsPlantedOrAfter(BombState bomb)
        {
            return bomb == BombState.Planted || bomb == BombState.Defused || bomb == BombState.Detonated;
        }

        private static string Percent(float fraction)
        {
            var clamped = fraction > 0f ? Math.Min(1f, fraction) : 0f;
            return Number((int)Math.Floor(clamped * 100f)) + "%";
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static void Require(SearchAndDestroy game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }
        }
    }
}
