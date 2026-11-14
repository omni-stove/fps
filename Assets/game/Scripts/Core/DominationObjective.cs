using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 別の進行役 (オンラインでは <see cref="TeamDeathmatchSession"/>) が回している試合に、拠点制圧 (<see cref="Domination"/>) を重ねて動かす。
    /// 顔ぶれ・チーム・生死・試合が動いているかは外から伝えてもらい、拠点の占領とポイント、ポイントによる勝敗だけをここで決める。
    /// 誰がどの拠点にいるかは、位置を <see cref="Locate"/> に渡すと <see cref="ControlPointMap"/> で引いて伝える。
    /// </summary>
    /// <remarks>
    /// 重ねている試合が Playing になった最初の <see cref="Tick"/> で拠点制圧も始める。ポイントが ScoreLimit に届いたら
    /// 拠点制圧が先に終わるので、呼び出し側は <see cref="Tick"/> が true を返したら重ねている試合を <see cref="DecideWinner"/> の勝者で終える。
    /// 重ねている試合が先に終わった (時間切れなど) ときも、勝者は <see cref="DecideWinner"/> でポイントから決める。
    /// 次の試合は <see cref="Reset"/> で、顔ぶれとチームを残したまま始め直す。
    /// </remarks>
    public sealed class DominationObjective
    {
        private readonly Dictionary<int, Team> _teams = new Dictionary<int, Team>();

        /// <summary>拠点制圧の本体。拠点の持ち主・進み・押し合いとポイントはここから読む。</summary>
        public Domination Mode { get; }

        /// <summary>拠点の範囲。拠点の数は <see cref="Mode"/> の PointCount と同じ。</summary>
        public ControlPointMap Map { get; }

        /// <summary>加えているプレイヤーの数。</summary>
        public int PlayerCount => _teams.Count;

        /// <param name="map">拠点の範囲。拠点の数はここから決める。</param>
        /// <param name="captureSeconds">1 人で中立の拠点を占領し終えるまでの秒数。0 より大きい有限値。</param>
        /// <param name="pointsPerSecond">拠点 1 つあたり 1 秒に入るポイント。0 より大きい有限値。</param>
        /// <param name="scoreLimit">勝利に必要なポイント。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい有限値。重ねている試合と同じ値にする。</param>
        /// <param name="maxCapturers">占領の速さに数える人数の上限。1 以上。</param>
        /// <exception cref="ArgumentOutOfRangeException">値のどれかが範囲の外 (<see cref="DominationSpec"/> と同じ検査)。</exception>
        public DominationObjective(
            ControlPointMap map,
            float captureSeconds,
            float pointsPerSecond,
            int scoreLimit,
            float timeLimitSeconds,
            int maxCapturers = DominationSpec.DefaultMaxCapturers)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Mode = new Domination(new DominationSpec(map.Count, captureSeconds, pointsPerSecond, scoreLimit, timeLimitSeconds, maxCapturers));
        }

        public bool HasPlayer(int playerId) => _teams.ContainsKey(playerId);

        /// <summary>
        /// playerId を team で加える。既に同じチームで加えていれば何もしない。別のチームで加えていれば、抜いてから team で加え直す
        /// (つなぎ直しでチームが替わったときなど)。加えた直後はどの拠点にもいない。
        /// </summary>
        public void SetPlayer(int playerId, Team team)
        {
            Side.FromTeam(team);
            if (_teams.TryGetValue(playerId, out var current))
            {
                if (current == team)
                {
                    return;
                }

                Mode.RemovePlayer(playerId);
                _teams.Remove(playerId);
            }

            Mode.AddPlayer(playerId, team);
            _teams[playerId] = team;
        }

        /// <summary>playerId を抜く。加えていなければ何もせず false。チームのポイントはそのまま残す。</summary>
        public bool RemovePlayer(int playerId)
        {
            if (!_teams.Remove(playerId))
            {
                return false;
            }

            Mode.RemovePlayer(playerId);
            return true;
        }

        /// <summary>
        /// playerId の今の位置から、いる拠点を引いて伝え、その番号を返す。position が null (倒れている・ダウン中など、数えない人) なら
        /// どの拠点にもいないことにして null を返す。次の <see cref="Tick"/> から占領に数える。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId を加えていない。</exception>
        public int? Locate(int playerId, Vector3? position)
        {
            if (!_teams.ContainsKey(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            var point = position.HasValue ? Map.Locate(position.Value) : null;
            Mode.SetPlayerPoint(playerId, point);
            return point;
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。matchPlaying (重ねている試合が Playing か) が false なら何もしない。
        /// true で拠点制圧がまだ始まっていなければ始めてから進める。この呼び出しで拠点制圧が終わったら true を返す。
        /// </summary>
        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            if (Mode.State == GameModeState.Waiting)
            {
                Mode.Start();
            }

            if (Mode.State != GameModeState.Playing)
            {
                return false;
            }

            Mode.Tick(deltaSeconds);
            return Mode.State == GameModeState.Ended;
        }

        /// <summary>
        /// 試合の勝者。拠点制圧が終わっていればその勝者 (ポイントが先に ScoreLimit に届いたチーム)、
        /// まだなら今のポイントの多いほう。同じなら null (引き分け)。
        /// </summary>
        public Team? DecideWinner()
        {
            if (Mode.State == GameModeState.Ended)
            {
                return Mode.Winner;
            }

            return Leader(Mode.GetScore(Team.Alpha), Mode.GetScore(Team.Bravo));
        }

        /// <summary>次の試合のために、ポイントと全拠点の占領を戻して Waiting にする。顔ぶれとチームはそのまま残す。</summary>
        public void Reset()
        {
            Mode.Reset();
        }

        /// <summary>ポイントの多いチーム。同じなら null。</summary>
        public static Team? Leader(float alphaScore, float bravoScore)
        {
            if (alphaScore > bravoScore)
            {
                return Team.Alpha;
            }

            return bravoScore > alphaScore ? Team.Bravo : (Team?)null;
        }
    }
}
