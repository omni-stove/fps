using System;
using System.Globalization;
using Fps.Core;

namespace Fps.Runtime
{
    public static class HudText
    {
        public static LocalizedText Notice(HudNotice notice)
        {
            switch (notice.Kind)
            {
                case HudNoticeKind.None:
                    return default;
                case HudNoticeKind.CheckpointSaved:
                    return CheckpointSaved;
                case HudNoticeKind.CheckpointRestart:
                    return CheckpointRestart(notice.Seconds);
                case HudNoticeKind.NextMatch:
                    return NextMatch(notice.Seconds);
                case HudNoticeKind.IdleWarning:
                    return IdleWarning(notice.Seconds);
                case HudNoticeKind.Downed:
                    return Downed(notice.Seconds, notice.RevivePercent);
                case HudNoticeKind.Respawn:
                    return Respawn(notice.Seconds);
                case HudNoticeKind.NoRespawnUntilNextMatch:
                    return NoRespawnUntilNextMatch;
                case HudNoticeKind.SpawnProtected:
                    return SpawnProtected(notice.Seconds);
                case HudNoticeKind.StatusEffect:
                    return StatusEffect(notice.KindId, notice.Seconds);
                default:
                    throw new ArgumentOutOfRangeException(nameof(notice), notice.Kind, "notice kind is not a defined HudNoticeKind");
            }
        }

        public static LocalizedText Score(Team? team, int? score)
        {
            return team.HasValue && score.HasValue ? TeamScore(team.Value, score.Value) : default;
        }

        /// <summary>チームの結果があればそれを、無ければソロの試合の勝敗を出す。どちらも無ければ空の文。</summary>
        public static LocalizedText Result(bool hasResult, Team? winner, MatchResult? soloResult)
        {
            if (hasResult)
            {
                return Result(winner);
            }

            return soloResult.HasValue ? SoloResult(soloResult.Value) : default;
        }

        /// <summary>ソロの試合が終わっていて、やり直すキーがあるときだけ案内を出す。</summary>
        public static LocalizedText Restart(MatchResult? soloResult, string restartKey)
        {
            return soloResult.HasValue && !string.IsNullOrEmpty(restartKey) ? Restart(restartKey) : default;
        }

        public static LocalizedText WeaponName(WeaponSpec spec)
        {
            return spec != null ? DisplayNames.Of(spec) : default;
        }

        public static LocalizedText AmmoType(AmmoTypeSpec ammoType)
        {
            return ammoType != null ? DisplayNames.Of(ammoType) : default;
        }

        /// <summary>状態異常の残りの文のキーの頭。種類の Id を続けて <c>hud.status.burn</c> のようにする。</summary>
        public const string StatusEffectKeyPrefix = "hud.status.";

        /// <summary>試合が終わった後、次の試合が始まるまでの案内。秒数は整数 (同期されている切り上げの値) で受ける。</summary>
        /// <example><c>NextMatch(10)</c> は <c>次の試合まで 10 秒</c></example>
        public static LocalizedText NextMatch(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.nextMatch", seconds);
        }

        /// <summary>HUD に出すチーム名。</summary>
        /// <example><c>TeamName(Team.Alpha)</c> は <c>ALPHA</c></example>
        public static LocalizedText TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return new LocalizedText("hud.team.alpha");
                case Team.Bravo:
                    return new LocalizedText("hud.team.bravo");
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>陣営名 name と点数 score を並べた得点の文。チームの点数とモードの陣営ごとの得点 (<see cref="ModeHudText.ScoreText"/>) が使う。</summary>
        /// <example><c>Score(TeamName(Team.Bravo), 3)</c> は <c>BRAVO 3</c></example>
        public static LocalizedText Score(LocalizedText name, int score)
        {
            return new LocalizedText("hud.score", name, score);
        }

        /// <summary>チーム名と点数。</summary>
        /// <example><c>TeamScore(Team.Bravo, 3)</c> は <c>BRAVO 3</c></example>
        public static LocalizedText TeamScore(Team team, int score)
        {
            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), score, "score must not be negative");
            }

            return Score(TeamName(team), score);
        }

        /// <summary>引き分けで終わったときの結果。</summary>
        public static LocalizedText Draw => new LocalizedText("hud.draw");

        /// <summary>試合が終わったときの結果。勝ったチームか、勝者がいなければ引き分け。</summary>
        /// <example><c>Result(Team.Alpha)</c> は <c>ALPHA の勝ち</c>、<c>Result(null)</c> は <c>引き分け</c></example>
        public static LocalizedText Result(Team? winner)
        {
            return winner.HasValue ? Wins(TeamName(winner.Value)) : Draw;
        }

        /// <summary>ソロの試合 (<see cref="Match"/>) が終わったときの結果。</summary>
        /// <example><c>SoloResult(MatchResult.Won)</c> は <c>クリア</c>、<c>SoloResult(MatchResult.Lost)</c> は <c>失敗</c></example>
        public static LocalizedText SoloResult(MatchResult result)
        {
            switch (result)
            {
                case MatchResult.Won:
                    return new LocalizedText("hud.solo.won");
                case MatchResult.Lost:
                    return new LocalizedText("hud.solo.lost");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "result is not a defined MatchResult");
            }
        }

        /// <summary>ソロの試合の結果の下に出す、key を押すとやり直せるという案内。</summary>
        /// <example><c>Restart("R")</c> は <c>[R] でやり直す</c></example>
        public static LocalizedText Restart(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            return new LocalizedText("hud.solo.restart", key);
        }

        /// <summary>勝った陣営の名前 sideName の結果。モードごとの結果 (<see cref="ModeHudText.Result"/> など) も使う。</summary>
        /// <example><c>Wins(TeamName(Team.Alpha))</c> は <c>ALPHA の勝ち</c></example>
        public static LocalizedText Wins(LocalizedText sideName)
        {
            return new LocalizedText("hud.wins", sideName);
        }

        /// <summary>倒れてからリスポーンするまでの案内。秒数は切り上げた整数で受ける。</summary>
        /// <example><c>Respawn(3)</c> は <c>リスポーンまで 3 秒</c></example>
        public static LocalizedText Respawn(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.respawn", seconds);
        }

        /// <summary><see cref="RespawnMode.None"/> で倒れたとき、次の試合が始まるまで戻らないことの案内。</summary>
        public static LocalizedText NoRespawnUntilNextMatch => new LocalizedText("hud.noRespawnUntilNextMatch");

        /// <summary>
        /// 倒れている間の案内を、リスポーン方式に合わせて選ぶ。Individual は自分のリスポーンまで、Wave は次のウェーブまでの秒数を
        /// <see cref="Respawn"/> で、None は <see cref="NoRespawnUntilNextMatch"/> を出す (seconds は使わない)。
        /// </summary>
        /// <example><c>RespawnNotice(RespawnMode.Wave, 7)</c> は <c>リスポーンまで 7 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">mode が定義済みの値でないか、None 以外で seconds が負。</exception>
        public static LocalizedText RespawnNotice(RespawnMode mode, int seconds)
        {
            switch (mode)
            {
                case RespawnMode.Individual:
                case RespawnMode.Wave:
                    return Respawn(seconds);
                case RespawnMode.None:
                    return NoRespawnUntilNextMatch;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined RespawnMode");
            }
        }

        /// <summary>ダウン中の人の案内。出血で倒れるまでの秒数は切り上げた整数で受ける。</summary>
        /// <example><c>Downed(12)</c> は <c>ダウン中 あと 12 秒</c></example>
        public static LocalizedText Downed(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.downed", seconds);
        }

        /// <summary>
        /// ダウン中の人の案内に、味方に起こされている進みを添える。revivePercent が 0 なら誰も起こしていないので <see cref="Downed(int)"/> と同じ。
        /// </summary>
        /// <example><c>Downed(12, 40)</c> は <c>ダウン中 あと 12 秒 (蘇生 40%)</c>、<c>Downed(12, 0)</c> は <c>ダウン中 あと 12 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負か、revivePercent が 0〜100 の外。</exception>
        public static LocalizedText Downed(int seconds, int revivePercent)
        {
            CheckPercent(revivePercent);
            var text = Downed(seconds);
            return revivePercent == 0 ? text : new LocalizedText("hud.downedWithRevive", text, revivePercent);
        }

        /// <summary>味方を起こしている人の案内。</summary>
        /// <example><c>Reviving(40)</c> は <c>蘇生中 40%</c></example>
        /// <exception cref="ArgumentOutOfRangeException">percent が 0〜100 の外。</exception>
        public static LocalizedText Reviving(int percent)
        {
            CheckPercent(percent);
            return new LocalizedText("hud.reviving", percent);
        }

        /// <summary>出現直後の無敵の最中の案内。無敵が切れるまでの秒数は切り上げた整数で受ける。</summary>
        /// <example><c>SpawnProtected(2)</c> は <c>保護中 あと 2 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負。</exception>
        public static LocalizedText SpawnProtected(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.spawnProtected", seconds);
        }

        /// <summary>入力が無いままだとサーバーから切られる (<see cref="GameRules.IdleKickSeconds"/>) ことの警告。切られるまでの秒数は切り上げた整数で受ける。</summary>
        /// <example><c>IdleWarning(15)</c> は <c>操作がありません あと 15 秒で切断されます</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負。</exception>
        public static LocalizedText IdleWarning(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("hud.idleWarning", seconds);
        }

        /// <summary>自分にかかっている状態異常の残り。文のキーは <c>hud.status.&lt;種類の Id&gt;</c>。</summary>
        /// <example><c>StatusEffect("burn", 3)</c> は <c>燃焼中 あと 3 秒</c></example>
        /// <exception cref="ArgumentException">kindId が空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負。</exception>
        public static LocalizedText StatusEffect(string kindId, int seconds)
        {
            if (string.IsNullOrEmpty(kindId))
            {
                throw new ArgumentException("kind id must not be empty", nameof(kindId));
            }

            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText(StatusEffectKeyPrefix + kindId, seconds);
        }

        private static void CheckPercent(int percent)
        {
            if (percent < 0 || percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, "percent must be between 0 and 100");
            }
        }

        /// <summary>キャンペーンのチェックポイントでセーブしたときの案内。</summary>
        public static LocalizedText CheckpointSaved => new LocalizedText("campaign.checkpoint.saved");

        /// <summary>キャンペーンでプレイヤーが倒れた後、チェックポイントから再開するまでの案内。秒数は切り上げた整数で受ける。</summary>
        /// <example><c>CheckpointRestart(3)</c> は <c>チェックポイントから再開まで 3 秒</c></example>
        public static LocalizedText CheckpointRestart(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return new LocalizedText("campaign.checkpoint.restart", seconds);
        }

        /// <summary>残りのグレネードの数。</summary>
        /// <example><c>Grenades(2)</c> は <c>グレネード 2</c></example>
        /// <exception cref="ArgumentOutOfRangeException">remaining が負。</exception>
        public static LocalizedText Grenades(int remaining)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            return new LocalizedText("hud.grenades", remaining);
        }

        /// <summary>種類を切り替えて持っているときの、投げる種類の名前と残りの数。</summary>
        /// <example><c>Grenades(new LocalizedText("grenade.smoke"), 2)</c> は <c>スモークグレネード 2</c></example>
        /// <exception cref="ArgumentOutOfRangeException">remaining が負。</exception>
        public static LocalizedText Grenades(LocalizedText typeName, int remaining)
        {
            if (remaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remaining), remaining, "remaining must not be negative");
            }

            return new LocalizedText("hud.grenadesOfType", typeName, remaining);
        }

        /// <summary>
        /// 能力の枠 1 つの表示。使っている最中は「使用中」、次に使えるまで待つ (待ち時間か、回数が無く戻るのを待つ) 間は残りの秒数を
        /// <see cref="AbilityWaitSeconds"/> で添え、それ以外は回数だけを出す。回数が無く戻らない設定なら回数だけを出す。
        /// </summary>
        /// <example><c>Ability(1, slot)</c> は <c>能力1 1/2</c>・<c>能力1 0/2 あと 3.5 秒</c>・<c>能力1 1/2 使用中</c></example>
        /// <param name="slotNumber">枠の番号。1 から (入力の Ability1 / Ability2 に合わせる)。</param>
        /// <param name="slot">枠の状態。</param>
        /// <exception cref="ArgumentOutOfRangeException">slotNumber が 1 より小さい。</exception>
        /// <exception cref="ArgumentNullException">slot が null。</exception>
        public static LocalizedText Ability(int slotNumber, AbilitySlot slot)
        {
            if (slotNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), slotNumber, "slotNumber must be at least 1");
            }

            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            var charges = slot.Charges;
            var max = slot.Spec.MaxCharges;
            if (slot.IsRunning)
            {
                return new LocalizedText("hud.ability.active", slotNumber, charges, max);
            }

            var wait = slot.SecondsUntilReady;
            if (wait > 0f && !float.IsInfinity(wait))
            {
                return new LocalizedText("hud.ability.wait", slotNumber, charges, max, AbilityWaitSeconds(wait));
            }

            return new LocalizedText("hud.ability", slotNumber, charges, max);
        }

        /// <summary>
        /// 能力の待ち時間の秒数の表示。小数 1 桁に切り上げる (<see cref="HudFormat.AbilityWaitTenths"/>)。
        /// </summary>
        /// <example><c>AbilityWaitSeconds(3.42f)</c> は <c>3.5</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負か有限でない。</exception>
        public static string AbilityWaitSeconds(float seconds)
        {
            return (HudFormat.AbilityWaitTenths(seconds) / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
