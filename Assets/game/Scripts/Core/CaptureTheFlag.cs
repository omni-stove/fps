using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// 旗取りのルール。Alpha と Bravo がそれぞれ自陣の台に旗を 1 本持ち、敵の旗を自陣の台へ運ぶと
    /// <see cref="CaptureTheFlagSpec.PointsPerCapture"/> 点入る。どちらかが ScoreLimit に届くか制限時間が切れたら終わる。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>生きている敵が旗に触れる (<see cref="Touch"/>) と、台にあっても落ちていても持つ (<see cref="FlagState.Carried"/>)。</item>
    /// <item>運ぶ人が倒れるか抜けたら、旗はその場に落ちる (<see cref="FlagState.Dropped"/>)。このクラスの <see cref="ReportKill"/>・<see cref="ReportDeath"/>・
    /// <see cref="RemovePlayer"/> を通せばその場で落とし、<see cref="Session"/> に直接報告したときは次の <see cref="ModeSession.Tick"/> で落とす
    /// (リスポーンが 0 秒だとその場で戻って倒れたことが残らないので、そのときはこのクラスを通すか、報告の直後に <see cref="DropCarriedFlag"/> を呼ぶ)。</item>
    /// <item>落ちた旗は生きている味方が触れると台に戻り、誰も触れなくても <see cref="CaptureTheFlagSpec.ReturnSeconds"/> 秒で戻る。</item>
    /// <item>敵の旗を運ぶ人が自陣の台に着いたとき (<see cref="ReachBase"/>)、自陣の旗が台にあれば点が入り、運んでいた旗は敵の台に戻る。
    /// 自陣の旗が台に無ければ何も起きず、運び続ける。</item>
    /// </list>
    /// 中身は <see cref="ModeSession"/> に <see cref="TwoTeams"/>・旗の時計 (キルでも時間でも点を入れない <see cref="IScoreRule"/>)・
    /// <see cref="IndividualRespawn"/>・<see cref="ScoreLimitWin"/>・<see cref="TimeLimitWin"/> を組んだもの。
    /// 旗と台の位置は持たず、触れたか・着いたかはシーンの Runner が <see cref="CaptureTheFlagSpec.PickupRadius"/>・
    /// <see cref="CaptureTheFlagSpec.CaptureRadius"/> で決めて伝える。試合が終わったら旗はその時の状態のまま止まる。
    /// </remarks>
    public sealed class CaptureTheFlag
    {
        // 小刻みな Tick を足し合わせると戻る時刻にわずかに届かないことがあるので、この差までは届いたものとして扱う。
        private const double _snap = 1e-6;

        private static readonly Side[] _flagSides = { Side.Alpha, Side.Bravo };
        private static readonly IReadOnlyList<Side> _flagSidesView = Array.AsReadOnly(_flagSides);

        /// <summary>敵の旗を持った瞬間に呼ばれる。引数は旗の陣営と持った人。</summary>
        public event Action<Side, int> FlagTaken;

        /// <summary>運ぶ人が倒れるか抜けて旗が落ちた瞬間に呼ばれる。引数は旗の陣営と運んでいた人。</summary>
        public event Action<Side, int> FlagDropped;

        /// <summary>
        /// 落ちた旗が台に戻った瞬間に呼ばれる。引数は旗の陣営と戻した味方で、秒数がたって戻ったときは null。
        /// 1 回の Tick で両方戻ったら Alpha、Bravo の順に呼ぶ。
        /// </summary>
        public event Action<Side, int?> FlagReturned;

        /// <summary>
        /// 敵の旗を自陣の台へ運んで点が入った後に呼ばれる。引数は運ばれた旗の陣営と運んだ人。
        /// 呼ばれた時点で点は足してあり、旗は台に戻っている。その点で試合が終わったときは <see cref="ModeSession.Ended"/> が先に出る。
        /// </summary>
        public event Action<Side, int> FlagCaptured;

        private readonly Dictionary<Side, Flag> _flags = new Dictionary<Side, Flag>();
        private readonly Dictionary<Side, int> _captures = new Dictionary<Side, int>();

        // イベントのハンドラの中で Reset されたら、その後の処理と通知を止めるために数える。
        private int _generation;

        public CaptureTheFlagSpec Spec { get; }

        /// <summary>進めているモード。参加者の追加・Start・Tick はここに対して行う。</summary>
        public ModeSession Session { get; }

        /// <param name="spec">試合の設定。</param>
        public CaptureTheFlag(CaptureTheFlagSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new FlagClock(this) },
                new IndividualRespawn(spec.RespawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(spec.ScoreLimit), new TimeLimitWin() });
            foreach (var side in _flagSides)
            {
                _flags[side] = new Flag();
            }
        }

        /// <summary>旗を持つ陣営 (Alpha と Bravo)。</summary>
        public static IReadOnlyList<Side> FlagSides => _flagSidesView;

        /// <summary>side の敵の陣営。</summary>
        /// <exception cref="ArgumentOutOfRangeException">side が Alpha でも Bravo でもない。</exception>
        public static Side Opponent(Side side)
        {
            RequireFlagSide(side, nameof(side));
            return side == Side.Alpha ? Side.Bravo : Side.Alpha;
        }

        /// <summary>flag の陣営の旗の状態。</summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもない。</exception>
        public FlagState GetFlagState(Side flag) => Find(flag).State;

        /// <summary>flag の陣営の旗を運んでいる人。運ばれていなければ null。</summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもない。</exception>
        public int? GetCarrierId(Side flag) => Find(flag).CarrierId;

        /// <summary>flag の陣営の旗が落ちていれば、台に戻るまでの秒数。落ちていなければ null。</summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもない。</exception>
        public float? GetReturnRemaining(Side flag)
        {
            var record = Find(flag);
            return record.State == FlagState.Dropped ? (float)Math.Max(0d, record.ReturnRemaining) : (float?)null;
        }

        /// <summary>side が敵の旗を自陣の台へ運んだ回数。</summary>
        public int GetCaptures(Side side) => _captures.TryGetValue(side, out var count) ? count : 0;

        /// <summary>playerId が運んでいる旗の陣営。運んでいなければ null。</summary>
        public Side? GetCarriedFlag(int playerId)
        {
            foreach (var side in _flagSides)
            {
                var record = _flags[side];
                if (record.State == FlagState.Carried && record.CarrierId == playerId)
                {
                    return side;
                }
            }

            return null;
        }

        /// <summary>playerId が旗を運んでいれば true。</summary>
        public bool IsCarrying(int playerId) => GetCarriedFlag(playerId).HasValue;

        /// <summary>
        /// playerId が flag の陣営の旗に触れた。敵の旗が台にあるか落ちていれば持ち (<see cref="FlagTaken"/>)、
        /// 味方の旗が落ちていれば台に戻して (<see cref="FlagReturned"/>)、true を返す。
        /// Playing でない・playerId が倒れている・何も変わらない (運ばれている旗・台にある味方の旗) ときは false。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">flag が Alpha でも Bravo でもないか、playerId が参加していない。</exception>
        public bool Touch(Side flag, int playerId)
        {
            var record = Find(flag);
            RequirePlayer(playerId, nameof(playerId));
            if (!CanAct(playerId))
            {
                return false;
            }

            if (Session.GetSide(playerId) == flag)
            {
                if (record.State != FlagState.Dropped)
                {
                    return false;
                }

                SendHome(record);
                FlagReturned?.Invoke(flag, playerId);
                return true;
            }

            if (record.State == FlagState.Carried)
            {
                return false;
            }

            record.State = FlagState.Carried;
            record.CarrierId = playerId;
            record.ReturnRemaining = 0d;
            FlagTaken?.Invoke(flag, playerId);
            return true;
        }

        /// <summary>
        /// playerId が自陣の台に着いた。敵の旗を運んでいて自陣の旗が台にあれば、運んだ旗を敵の台に戻し、
        /// playerId の陣営に <see cref="CaptureTheFlagSpec.PointsPerCapture"/> 点を足して (得点上限に届けばその陣営の勝ちで終わる)
        /// <see cref="FlagCaptured"/> を出し、true を返す。それ以外 (Playing でない・倒れている・運んでいない・自陣の旗が台に無い) は false。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId が参加していない。</exception>
        public bool ReachBase(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            if (!CanAct(playerId))
            {
                return false;
            }

            var carried = GetCarriedFlag(playerId);
            var side = Session.GetSide(playerId);
            if (!carried.HasValue || _flags[side].State != FlagState.AtBase)
            {
                return false;
            }

            SendHome(_flags[carried.Value]);
            _captures[side] = GetCaptures(side) + 1;
            var generation = _generation;
            Session.AddScore(side, Spec.PointsPerCapture);
            if (generation == _generation)
            {
                FlagCaptured?.Invoke(carried.Value, playerId);
            }

            return true;
        }

        /// <summary>
        /// killerId が victimId を倒したことを <see cref="Session"/> に報告する。倒れたことが数えられ、倒された人が旗を運んでいたら、その場で落とす。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            RequirePlayer(victimId, nameof(victimId));
            var deaths = Session.GetDeaths(victimId);
            Session.ReportKill(killerId, victimId);
            if (Session.GetDeaths(victimId) > deaths)
            {
                DropFrom(victimId);
            }
        }

        /// <summary>playerId が倒した相手なしで倒れたことを <see cref="Session"/> に報告する。倒れたことが数えられ、旗を運んでいたら、その場で落とす。</summary>
        public void ReportDeath(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            var deaths = Session.GetDeaths(playerId);
            Session.ReportDeath(playerId);
            if (Session.GetDeaths(playerId) > deaths)
            {
                DropFrom(playerId);
            }
        }

        /// <summary>
        /// playerId が運んでいる旗を、その場に落とす (<see cref="FlagDropped"/>)。倒れたことを <see cref="Session"/> に直接報告した直後に呼ぶ
        /// (シーンの Runner は報告を土台に任せ、その後で落とす)。試合中で運んでいれば落として true、それ以外は何もせず false。
        /// </summary>
        public bool DropCarriedFlag(int playerId)
        {
            if (Session.State != GameModeState.Playing || !IsCarrying(playerId))
            {
                return false;
            }

            DropFrom(playerId);
            return true;
        }

        /// <summary>
        /// プレイヤーを <see cref="Session"/> から抜く。旗を運んでいたら、試合中ならその場に落とし (<see cref="FlagDropped"/>)、
        /// それ以外なら知らせずに台へ戻す。
        /// </summary>
        public void RemovePlayer(int playerId)
        {
            var carried = GetCarriedFlag(playerId);
            Session.RemovePlayer(playerId);
            if (!carried.HasValue)
            {
                return;
            }

            if (Session.State == GameModeState.Playing)
            {
                DropFrom(playerId);
            }
            else
            {
                SendHome(_flags[carried.Value]);
            }
        }

        /// <summary>
        /// 次の試合のために、両方の旗を台に戻し、運んだ回数を消してから <see cref="ModeSession.Reset"/> する。イベントは出さない。
        /// イベントのハンドラの中で呼ぶと、その後のイベントは出さない。
        /// </summary>
        public void Reset()
        {
            _generation++;
            foreach (var side in _flagSides)
            {
                SendHome(_flags[side]);
            }

            _captures.Clear();
            Session.Reset();
        }

        private bool CanAct(int playerId)
        {
            if (Session.State != GameModeState.Playing)
            {
                return false;
            }

            var generation = _generation;
            DropDownedCarriers();
            return generation == _generation
                && Session.State == GameModeState.Playing
                && Session.HasPlayer(playerId)
                && !Session.IsDown(playerId);
        }

        private void Advance(float seconds)
        {
            var generation = _generation;
            DropDownedCarriers();
            if (generation != _generation || !(seconds > 0f))
            {
                return;
            }

            foreach (var side in _flagSides)
            {
                var record = _flags[side];
                if (record.State != FlagState.Dropped)
                {
                    continue;
                }

                record.ReturnRemaining -= seconds;
                if (record.ReturnRemaining > _snap)
                {
                    continue;
                }

                SendHome(record);
                FlagReturned?.Invoke(side, null);
                if (generation != _generation)
                {
                    return;
                }
            }
        }

        private void DropDownedCarriers()
        {
            var generation = _generation;
            foreach (var side in _flagSides)
            {
                var record = _flags[side];
                if (record.State != FlagState.Carried || !record.CarrierId.HasValue)
                {
                    continue;
                }

                var carrier = record.CarrierId.Value;
                if (!Session.HasPlayer(carrier) || Session.IsDown(carrier))
                {
                    DropFrom(carrier);
                    if (generation != _generation)
                    {
                        return;
                    }
                }
            }
        }

        private void DropFrom(int carrierId)
        {
            if (Session.State != GameModeState.Playing)
            {
                return;
            }

            var flag = GetCarriedFlag(carrierId);
            if (!flag.HasValue)
            {
                return;
            }

            var record = _flags[flag.Value];
            record.State = FlagState.Dropped;
            record.CarrierId = null;
            record.ReturnRemaining = Spec.ReturnSeconds;
            FlagDropped?.Invoke(flag.Value, carrierId);
        }

        private static void SendHome(Flag record)
        {
            record.State = FlagState.AtBase;
            record.CarrierId = null;
            record.ReturnRemaining = 0d;
        }

        private Flag Find(Side flag)
        {
            RequireFlagSide(flag, nameof(flag));
            return _flags[flag];
        }

        private static void RequireFlagSide(Side side, string parameterName)
        {
            if (side != Side.Alpha && side != Side.Bravo)
            {
                throw new ArgumentOutOfRangeException(parameterName, side, "side must be Alpha or Bravo");
            }
        }

        private void RequirePlayer(int playerId, string parameterName)
        {
            if (!Session.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(parameterName, playerId, "no player with this id");
            }
        }

        private sealed class Flag
        {
            public FlagState State { get; set; } = FlagState.AtBase;

            public int? CarrierId { get; set; }

            public double ReturnRemaining { get; set; }
        }

        /// <summary>キルでも時間でも点を入れず、Session の時間で倒れた運ぶ人の旗を落とし、落ちた旗を戻す。</summary>
        private sealed class FlagClock : IScoreRule
        {
            private readonly CaptureTheFlag _owner;

            public FlagClock(CaptureTheFlag owner)
            {
                _owner = owner;
            }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => 0d;

            public void Advance(ModeSession session, float seconds) => _owner.Advance(seconds);
        }
    }
}
