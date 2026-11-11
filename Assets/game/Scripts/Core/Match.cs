using System;

namespace Fps.Core
{
    /// <summary>試合の状態。</summary>
    public enum MatchState
    {
        /// <summary>開始前。</summary>
        Waiting,

        /// <summary>試合中。</summary>
        Playing,

        /// <summary>規定キル数に到達して勝利。</summary>
        Won,

        /// <summary>時間切れかプレイヤー死亡で敗北。</summary>
        Lost,
    }

    /// <summary>
    /// ソロで標的を倒すタイムアタック形式の試合進行。
    /// 制限時間内に規定キル数へ到達すれば勝利、時間切れかプレイヤー死亡で敗北する。
    /// 時間は <see cref="Tick"/> で外から渡す。勝敗確定後は何を呼んでも状態もカウントも変わらない。
    /// </summary>
    public sealed class Match
    {
        /// <summary>状態が変わるたびに新しい状態を渡して呼ばれる。</summary>
        public event Action<MatchState> StateChanged;

        /// <summary>制限時間（秒）。</summary>
        public float TimeLimitSeconds { get; }

        /// <summary>勝利に必要なキル数。</summary>
        public int KillsToWin { get; }

        public MatchState State { get; private set; } = MatchState.Waiting;

        /// <summary>残り時間（秒）。0 未満にはならない。</summary>
        public float RemainingSeconds { get; private set; }

        public int Kills { get; private set; }

        /// <summary>勝敗が確定しているか。</summary>
        public bool IsOver => State == MatchState.Won || State == MatchState.Lost;

        /// <param name="timeLimitSeconds">制限時間（秒）。正の有限値。</param>
        /// <param name="killsToWin">勝利に必要なキル数。1 以上。</param>
        public Match(float timeLimitSeconds, int killsToWin)
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
            RemainingSeconds = timeLimitSeconds;
        }

        /// <summary>試合を開始する。Waiting 以外では何もしない。</summary>
        public void Start()
        {
            if (State != MatchState.Waiting)
            {
                return;
            }

            ChangeState(MatchState.Playing);
        }

        /// <summary>時間を deltaSeconds 進める。Playing 中のみ有効で、残り時間が 0 になったら敗北する。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            if (State != MatchState.Playing)
            {
                return;
            }

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds <= 0f)
            {
                ChangeState(MatchState.Lost);
            }
        }

        /// <summary>キルを 1 つ記録する。Playing 中のみ有効で、KillsToWin に到達したら勝利する。</summary>
        public void RegisterKill()
        {
            if (State != MatchState.Playing)
            {
                return;
            }

            Kills++;
            if (Kills >= KillsToWin)
            {
                ChangeState(MatchState.Won);
            }
        }

        /// <summary>プレイヤーの死亡を通知する。Playing 中なら敗北する。</summary>
        public void PlayerDied()
        {
            if (State != MatchState.Playing)
            {
                return;
            }

            ChangeState(MatchState.Lost);
        }

        private void ChangeState(MatchState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
