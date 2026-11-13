using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="ModeSession"/> を 1 ラウンドとして繰り返すラウンド制の試合。2 つの陣営 (<see cref="ITeamLayout.FixedSides"/>) が
    /// ラウンドの勝ちを取り合い、先に <see cref="WinsToWin"/> 勝した陣営の勝ちで終わる。時間は <see cref="Tick"/> で外から進める。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>各ラウンドは <see cref="PreparationSeconds"/> の待ち (<see cref="RoundPhase.Preparing"/>) から始まり、待ち終わると
    /// ラウンドの <see cref="ModeSession"/> を Start して <see cref="RoundPhase.Live"/> になる。待ちの間の報告は ModeSession が数えない。</item>
    /// <item>ラウンドは ModeSession が終わったときに終わり、その勝者 (引き分けなら null) がラウンドの勝者になる。
    /// 引き分けのラウンドはどちらの勝ちにも数えないが、ラウンド数と攻守交代の数には入る。</item>
    /// <item>次のラウンドの待ちに入るときに ModeSession を Reset するので、陣営の得点・キル数とデス数・リスポーン待ちは持ち越さない。
    /// 参加者と陣営分けは残る。体力と位置は Core の外にあるので、<see cref="PreparationStarted"/> を受けて戻す。</item>
    /// <item>ラウンド中のリスポーンは ModeSession に渡した <see cref="IRespawnPolicy"/> に任せる。</item>
    /// <item><see cref="SwapEveryRounds"/> ラウンドごとに <see cref="Attacker"/> と <see cref="Defender"/> を入れ替える。
    /// 試合が終わったラウンドの後は入れ替えない。</item>
    /// </list>
    /// ModeSession の Start と Reset は RoundSession が呼ぶので、外からは呼ばない。参加と退出は <see cref="Mode"/> に直接行う。
    /// ModeSession の ReportKill などを直接呼んでラウンドが終わったときは、次の <see cref="Tick"/> でラウンドの終わりを扱う。
    /// すぐに扱わせたいときは RoundSession の <see cref="ReportKill"/>・<see cref="ReportDeath"/>・<see cref="AddScore"/> を使う。
    /// </remarks>
    public sealed class RoundSession
    {
        /// <summary>ラウンドの開始前の待ちに入ったときに呼ばれる。引数はラウンドの番号 (1 から)。体力と位置はここで戻す。</summary>
        public event Action<int> PreparationStarted;

        /// <summary>待ちが終わってラウンドが始まったときに呼ばれる。引数はラウンドの番号 (1 から)。</summary>
        public event Action<int> RoundStarted;

        /// <summary>
        /// ラウンドが終わったときに呼ばれる。引数は勝った陣営で、引き分けなら null。
        /// 呼ばれている間は <see cref="RoundNumber"/> が終わったラウンドのままで、ModeSession も終わった時点の状態を保っている。
        /// 試合も終わるときは <see cref="Phase"/> がすでに Ended で、この後に <see cref="Ended"/> が呼ばれる。
        /// </summary>
        public event Action<Side?> RoundEnded;

        /// <summary>攻守が入れ替わったときに呼ばれる。<see cref="RoundEnded"/> の後、次のラウンドの <see cref="PreparationStarted"/> の前。</summary>
        public event Action SidesSwapped;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は先取数に届いた陣営。</summary>
        public event Action<Side> Ended;

        private readonly ModeSession _mode;
        private readonly Side _firstAttacker;
        private readonly Side _firstDefender;
        private readonly Dictionary<Side, int> _roundWins = new Dictionary<Side, int>();

        // イベントのハンドラで Reset されたら、その後の処理を続けないために数える。
        private int _generation;
        private bool _finishingRound;

        /// <summary>1 ラウンドを表す ModeSession。参加者の出入りと、ラウンド中の得点やキル数はここで見る。</summary>
        public ModeSession Mode => _mode;

        public RoundPhase Phase { get; private set; }

        /// <summary>試合に勝つのに要るラウンドの勝ち数。1 以上。</summary>
        public int WinsToWin { get; }

        /// <summary>各ラウンドの開始前の待ち (秒)。0 以上の有限値。0 なら待たずに始める。</summary>
        public float PreparationSeconds { get; }

        /// <summary>何ラウンドごとに攻守を入れ替えるか。0 なら入れ替えない。</summary>
        public int SwapEveryRounds { get; }

        /// <summary>今のラウンドの番号 (1 から)。開始前は 0。試合が終わった後は最後のラウンドの番号のまま。</summary>
        public int RoundNumber { get; private set; }

        /// <summary>待ちの残り秒数。<see cref="RoundPhase.Preparing"/> 以外では 0。</summary>
        public float PreparationRemaining { get; private set; }

        /// <summary>今のラウンドで攻める陣営。</summary>
        public Side Attacker { get; private set; }

        /// <summary>今のラウンドで守る陣営。</summary>
        public Side Defender { get; private set; }

        /// <summary>試合に勝った陣営。終わる前は null。</summary>
        public Side? Winner { get; private set; }

        /// <param name="mode">
        /// 1 ラウンドを表す ModeSession。開始前 (Waiting) で、チーム分けの <see cref="ITeamLayout.FixedSides"/> がちょうど 2 つあるもの。
        /// 1 つ目の陣営が最初のラウンドで攻める。
        /// </param>
        /// <param name="winsToWin">試合に勝つのに要るラウンドの勝ち数。1 以上。</param>
        /// <param name="preparationSeconds">各ラウンドの開始前の待ち (秒)。0 以上の有限値。</param>
        /// <param name="swapEveryRounds">何ラウンドごとに攻守を入れ替えるか。0 以上。0 なら入れ替えない。</param>
        public RoundSession(ModeSession mode, int winsToWin, float preparationSeconds, int swapEveryRounds)
        {
            _mode = mode ?? throw new ArgumentNullException(nameof(mode));

            if (mode.State != GameModeState.Waiting)
            {
                throw new ArgumentException("mode must not have started", nameof(mode));
            }

            var fixedSides = mode.Layout.FixedSides;
            if (fixedSides.Count != 2)
            {
                throw new ArgumentException("mode must have exactly two fixed sides", nameof(mode));
            }

            if (winsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(winsToWin), winsToWin, "winsToWin must be at least 1");
            }

            if (!(preparationSeconds >= 0f) || float.IsInfinity(preparationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(preparationSeconds), preparationSeconds, "preparationSeconds must not be negative and must be finite");
            }

            if (swapEveryRounds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(swapEveryRounds), swapEveryRounds, "swapEveryRounds must not be negative");
            }

            WinsToWin = winsToWin;
            PreparationSeconds = preparationSeconds;
            SwapEveryRounds = swapEveryRounds;
            _firstAttacker = fixedSides[0];
            _firstDefender = fixedSides[1];
            Attacker = _firstAttacker;
            Defender = _firstDefender;
            _roundWins.Add(_firstAttacker, 0);
            _roundWins.Add(_firstDefender, 0);
            Phase = RoundPhase.Waiting;
        }

        /// <summary>side が勝ったラウンドの数。</summary>
        /// <exception cref="ArgumentOutOfRangeException">side がこの試合の 2 陣営のどちらでもない。</exception>
        public int GetRoundWins(Side side)
        {
            if (!_roundWins.TryGetValue(side, out var wins))
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side is not one of the two sides of this match");
            }

            return wins;
        }

        /// <summary>試合を始め、1 ラウンド目の待ちに入る。Waiting のときだけ呼べる。</summary>
        public void Start()
        {
            if (Phase != RoundPhase.Waiting)
            {
                throw new InvalidOperationException("match has already started");
            }

            if (_mode.State != GameModeState.Waiting)
            {
                throw new InvalidOperationException("the round's ModeSession was started from outside the RoundSession");
            }

            BeginPreparation();
        }

        /// <summary>
        /// ラウンドの勝ち数・ラウンドの番号・攻守・勝者を初期値に戻し、ModeSession も Reset して Waiting に戻す。
        /// 参加者と陣営分けはそのまま残す。どの状態からでも呼べ、イベントのハンドラの中で呼ぶとその後のイベントは出さない。
        /// </summary>
        public void Reset()
        {
            _generation++;
            _finishingRound = false;
            _roundWins[_firstAttacker] = 0;
            _roundWins[_firstDefender] = 0;
            Attacker = _firstAttacker;
            Defender = _firstDefender;
            RoundNumber = 0;
            PreparationRemaining = 0f;
            Winner = null;
            Phase = RoundPhase.Waiting;
            _mode.Reset();
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。待ちの間は待ちを減らし、待ち終わったらラウンドを始めて、余った時間をそのラウンドに進める。
        /// ラウンド中は ModeSession を進め、ラウンドが終わっていればラウンドの終わりを扱って次の待ちに入る。
        /// ラウンドが終わった Tick の余りは次の待ちに回さない。Waiting と Ended では何もしない。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (Phase == RoundPhase.Preparing)
            {
                if (deltaSeconds < PreparationRemaining)
                {
                    PreparationRemaining -= deltaSeconds;
                    return;
                }

                deltaSeconds -= PreparationRemaining;
                var generation = _generation;
                BeginLive();
                if (generation != _generation || Phase != RoundPhase.Live)
                {
                    return;
                }
            }

            if (Phase != RoundPhase.Live)
            {
                return;
            }

            _mode.Tick(deltaSeconds);
            FinishRoundIfEnded();
        }

        /// <summary><see cref="ModeSession.ReportKill"/> を呼び、それでラウンドが終わればその場で扱う。</summary>
        public void ReportKill(int killerId, int victimId)
        {
            _mode.ReportKill(killerId, victimId);
            FinishRoundIfEnded();
        }

        /// <summary><see cref="ModeSession.ReportDeath"/> を呼び、それでラウンドが終わればその場で扱う。</summary>
        public void ReportDeath(int playerId)
        {
            _mode.ReportDeath(playerId);
            FinishRoundIfEnded();
        }

        /// <summary><see cref="ModeSession.AddScore"/> を呼び、それでラウンドが終わればその場で扱う。</summary>
        public void AddScore(Side side, double points)
        {
            _mode.AddScore(side, points);
            FinishRoundIfEnded();
        }

        private void BeginPreparation()
        {
            var generation = _generation;
            RoundNumber++;
            Phase = RoundPhase.Preparing;
            PreparationRemaining = PreparationSeconds;
            _mode.Reset();
            PreparationStarted?.Invoke(RoundNumber);
            if (generation != _generation || Phase != RoundPhase.Preparing)
            {
                return;
            }

            if (PreparationSeconds == 0f)
            {
                BeginLive();
            }
        }

        private void BeginLive()
        {
            if (_mode.State != GameModeState.Waiting)
            {
                throw new InvalidOperationException("the round's ModeSession was started from outside the RoundSession");
            }

            PreparationRemaining = 0f;
            Phase = RoundPhase.Live;
            _mode.Start();
            RoundStarted?.Invoke(RoundNumber);
        }

        private void FinishRoundIfEnded()
        {
            // RoundEnded などのハンドラから報告されても、同じラウンドの終わりを 2 度扱わない。
            if (Phase != RoundPhase.Live || _finishingRound || _mode.State != GameModeState.Ended)
            {
                return;
            }

            var winner = _mode.Winner;
            if (winner.HasValue)
            {
                if (!_roundWins.TryGetValue(winner.Value, out var wins))
                {
                    throw new InvalidOperationException($"the round was won by {winner.Value}, which is not one of the two sides of this match");
                }

                wins++;
                _roundWins[winner.Value] = wins;
                if (wins >= WinsToWin)
                {
                    Winner = winner;
                    Phase = RoundPhase.Ended;
                }
            }

            var generation = _generation;
            _finishingRound = true;
            RoundEnded?.Invoke(winner);
            if (generation != _generation)
            {
                return;
            }

            if (Phase == RoundPhase.Ended)
            {
                Ended?.Invoke(Winner.Value);
                return;
            }

            if (SwapEveryRounds > 0 && RoundNumber % SwapEveryRounds == 0)
            {
                var attacker = Attacker;
                Attacker = Defender;
                Defender = attacker;
                SidesSwapped?.Invoke();
                if (generation != _generation)
                {
                    return;
                }
            }

            _finishingRound = false;
            BeginPreparation();
        }
    }
}
