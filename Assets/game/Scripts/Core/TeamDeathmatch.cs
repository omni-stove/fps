using System;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチのルール。Alpha と Bravo の 2 チームでキル数を競い、どちらかが ScoreLimit に届くか
    /// 制限時間が切れたら終わる。時間は <see cref="Tick"/> で外から進める。
    /// 終了後は何を報告しても状態が変わらない。次の試合は <see cref="Reset"/> で同じ顔ぶれのまま始め直す。
    /// </summary>
    /// <remarks>
    /// 中身は <see cref="ModeSession"/> に <see cref="TwoTeams"/>・<see cref="KillScore"/>・<see cref="RespawnPolicy.FromRules"/>・
    /// <see cref="ScoreLimitWin"/>・<see cref="TimeLimitWin"/> (None ではその前に <see cref="LastSideStandingWin"/>) を組んだもの。
    /// 倒された人がいつ戻るかは <see cref="GameRules.RespawnMode"/> で決まる。
    /// <list type="bullet">
    /// <item><see cref="RespawnMode.Individual"/>: 倒されてから RespawnSeconds 後に <see cref="RespawnReady"/>。</item>
    /// <item><see cref="RespawnMode.Wave"/>: 試合開始から WaveIntervalSeconds ごとに波が来る。波の時刻までに
    /// 倒されてから RespawnSeconds 以上たった人を、その波でまとめて <see cref="RespawnReady"/> にする。
    /// 波の時刻ちょうどに倒された人 (その波を出した <see cref="Tick"/> の後に報告された人) は次の波を待つ。</item>
    /// <item><see cref="RespawnMode.None"/>: 試合中は戻らない。<see cref="Tick"/> のたびに、1 人以上いて全員が倒れている
    /// チームがあれば相手チームの勝ちで終える。両チームともそうなら引き分け。</item>
    /// </list>
    /// </remarks>
    public sealed class TeamDeathmatch : IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。Ended では <see cref="Ended"/> より先に呼ばれ、その時点で Winner は確定済み。</summary>
        public event Action<GameModeState> StateChanged;

        /// <summary>倒されたプレイヤーがリスポーンできるようになったときに呼ばれる。引数は playerId。None では呼ばれない。</summary>
        public event Action<int> RespawnReady;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は勝ったチームで、同点なら null。</summary>
        public event Action<Team?> Ended;

        private readonly ModeSession _session;

        public TeamDeathmatchSpec Spec { get; }
        public GameModeState State => _session.State;

        /// <summary>残り時間 (秒)。開始前は TimeLimitSeconds のまま、終了後は終了した時点の値のまま。</summary>
        public float RemainingSeconds => _session.RemainingSeconds;

        /// <summary>勝ったチーム。終了前と、同点で終わったときは null。</summary>
        public Team? Winner => Side.ToTeam(_session.Winner);

        public int PlayerCount => _session.PlayerCount;

        /// <param name="spec">試合の設定。</param>
        public TeamDeathmatch(TeamDeathmatchSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            var scoreLimit = new ScoreLimitWin(spec.ScoreLimit);
            var timeLimit = new TimeLimitWin();
            var winConditions = spec.Rules.RespawnMode == RespawnMode.None
                ? new IWinCondition[] { new LastSideStandingWin(), scoreLimit, timeLimit }
                : new IWinCondition[] { scoreLimit, timeLimit };

            _session = new ModeSession(
                new TwoTeams(),
                spec.TimeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                RespawnPolicy.FromRules(spec.Rules, spec.RespawnSeconds),
                winConditions);
            _session.StateChanged += state => StateChanged?.Invoke(state);
            _session.RespawnReady += playerId => RespawnReady?.Invoke(playerId);
            _session.Ended += winner => Ended?.Invoke(Side.ToTeam(winner));
        }

        /// <summary>試合を始める。Waiting のときだけ呼べる。</summary>
        public void Start() => _session.Start();

        /// <summary>
        /// 次の試合のために、チームの点数・各プレイヤーのキル数とデス数・残り時間・勝者・波の時刻を初期値に戻し、
        /// リスポーン待ちを取り消して全員を生き返らせ、Waiting に戻す。参加者とチーム分けはそのまま残す。
        /// どの状態からでも呼べる。状態が変わったときだけ <see cref="StateChanged"/> を出す。
        /// </summary>
        public void Reset() => _session.Reset();

        /// <summary>プレイヤーを team に加える。キル数とデス数は 0 から数える。</summary>
        public void AddPlayer(int playerId, Team team)
        {
            _session.AddPlayer(playerId, Side.FromTeam(team));
        }

        /// <summary>
        /// プレイヤーを抜く。待っていたリスポーンは取り消す。チームの点数はそのまま残す。
        /// None で抜けたことによりチームが全滅した場合は、次の <see cref="Tick"/> で終える。
        /// </summary>
        public void RemovePlayer(int playerId) => _session.RemovePlayer(playerId);

        public bool HasPlayer(int playerId) => _session.HasPlayer(playerId);

        public Team GetTeam(int playerId) => _session.GetSide(playerId).ToTeam();

        public int GetKills(int playerId) => _session.GetKills(playerId);

        public int GetDeaths(int playerId) => _session.GetDeaths(playerId);

        /// <summary>倒されてリスポーンを待っている間は true。None では戻らないので常に false。</summary>
        public bool IsAwaitingRespawn(int playerId) => _session.IsAwaitingRespawn(playerId);

        /// <summary>None で倒され、この試合ではもう戻らない間は true。None 以外では常に false。</summary>
        public bool IsEliminated(int playerId) => _session.IsEliminated(playerId);

        /// <summary>
        /// <see cref="RespawnReady"/> が出るまでの秒数。Individual では倒されてからの待ちの残り、
        /// Wave では戻れる最初の波の時刻までの秒数。リスポーンを待っていないとき (None を含む) は null。
        /// 制限時間より後になる場合もそのまま返す。
        /// </summary>
        public float? GetRespawnRemaining(int playerId) => _session.GetRespawnRemaining(playerId);

        /// <summary>Wave で次の波が来るまでの秒数。Wave 以外のときと、Playing 以外のときは null。</summary>
        public float? SecondsUntilNextWave => _session.SecondsUntilNextWave;

        // 点はキル 1 回につき 1 点しか入らないので、double で持っていても整数のまま。
        public int GetScore(Team team) => (int)_session.GetScore(Side.FromTeam(team));

        /// <summary>
        /// killerId が victimId を倒したことを報告する。別チームを倒したら倒した側のチームに 1 点入り、
        /// 倒した人の Kills が増える。同じチームや自滅では加点せず、倒された人の Deaths だけ増える。
        /// Waiting・Ended の間と、倒された人がすでに倒れているとき (二重の報告) は何もしない。
        /// </summary>
        public void ReportKill(int killerId, int victimId) => _session.ReportKill(killerId, victimId);

        /// <summary>
        /// playerId が倒した相手なしで死んだこと (落下・環境ダメージなど) を報告する。自滅と同じく Deaths だけ増やし、
        /// 点数は動かさずにリスポーン待ちにする。Waiting・Ended の間と、すでに倒れているときは何もしない。
        /// </summary>
        public void ReportDeath(int playerId) => _session.ReportDeath(playerId);

        /// <summary>
        /// 時間を deltaSeconds 進める。None では先に全滅したチームがないか調べ、あれば時間を進めずに終える。
        /// 続けてリスポーン待ちが終わったプレイヤーについて RespawnReady を出し、
        /// 残り時間が 0 になったら点数の多いほうを勝ちとして終える。Playing 以外では何もしない。
        /// </summary>
        public void Tick(float deltaSeconds) => _session.Tick(deltaSeconds);
    }
}
