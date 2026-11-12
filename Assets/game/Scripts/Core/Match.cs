using System;

namespace Fps.Core
{
    /// <summary>
    /// ソロで標的を倒すタイムアタック形式の試合進行。
    /// 制限時間内に規定キル数へ到達すれば勝利、時間切れかプレイヤー死亡で敗北する。
    /// プレイヤーは <see cref="PlayerId"/> で区別し、標的の ID は何でもよい。
    /// 時間は <see cref="Tick"/> で外から渡す。勝敗確定後は何を呼んでも状態もカウントも変わらない。
    /// </summary>
    public sealed class Match : IGameMode
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。Ended で呼ばれた時点で <see cref="Result"/> は確定済み。</summary>
        public event Action<GameModeState> StateChanged;

        /// <summary>制限時間（秒）。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>勝利に必要なキル数。</summary>
        public int KillsToWin { get; }

        /// <summary>プレイヤーの ID。これ以外の ID は標的として扱う。</summary>
        public int PlayerId { get; }

        public GameModeState State { get; private set; } = GameModeState.Waiting;

        /// <summary>勝敗。終了前は null。</summary>
        public MatchResult? Result { get; private set; }

        /// <summary>残り時間（秒）。0 未満にはならない。</summary>
        public float RemainingSeconds { get; private set; }

        public int Kills { get; private set; }

        /// <summary>勝敗が確定しているか。</summary>
        public bool IsOver => State == GameModeState.Ended;

        /// <param name="timeLimitSeconds">制限時間（秒）。正の有限値。</param>
        /// <param name="killsToWin">勝利に必要なキル数。1 以上。</param>
        /// <param name="playerId">プレイヤーの ID。</param>
        public Match(float timeLimitSeconds, int killsToWin, int playerId)
        {
            if (!(timeLimitSeconds > 0f) || float.IsInfinity(timeLimitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "timeLimitSeconds must be positive and finite");
            }

            if (killsToWin <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killsToWin), killsToWin, "killsToWin must be positive");
            }

            TimeLimitSeconds = timeLimitSeconds;
            KillsToWin = killsToWin;
            PlayerId = playerId;
            RemainingSeconds = timeLimitSeconds;
        }

        /// <summary>試合を開始する。Waiting 以外では何もしない。</summary>
        public void Start()
        {
            if (State != GameModeState.Waiting)
            {
                return;
            }

            State = GameModeState.Playing;
            StateChanged?.Invoke(State);
        }

        /// <summary>時間を deltaSeconds 進める。Playing 中のみ有効で、残り時間が 0 になったら敗北する。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            if (State != GameModeState.Playing)
            {
                return;
            }

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds <= 0f)
            {
                End(MatchResult.Lost);
            }
        }

        /// <summary>
        /// killerId が victimId を倒したことを報告する。Playing 中のみ有効。
        /// プレイヤーが倒されたら (自滅も含む) 敗北し、プレイヤーが標的を倒したらキルを 1 つ数えて
        /// KillsToWin に到達したら勝利する。標的どうしのキルは数えない。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            if (State != GameModeState.Playing)
            {
                return;
            }

            if (victimId == PlayerId)
            {
                End(MatchResult.Lost);
                return;
            }

            if (killerId != PlayerId)
            {
                return;
            }

            Kills++;
            if (Kills >= KillsToWin)
            {
                End(MatchResult.Won);
            }
        }

        /// <summary>playerId の死亡を報告する。Playing 中にプレイヤーが死んだら敗北する。標的の死亡はキルとして数えない。</summary>
        public void ReportDeath(int playerId)
        {
            if (State != GameModeState.Playing || playerId != PlayerId)
            {
                return;
            }

            End(MatchResult.Lost);
        }

        private void End(MatchResult result)
        {
            Result = result;
            State = GameModeState.Ended;
            StateChanged?.Invoke(State);
        }
    }
}
