using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// PvE の波を順に進める。各波の値 (<see cref="Wave"/>) は表 (<see cref="FromTable"/>) か式 (<see cref="FromFormula"/>) から決める。
    /// 敵を実際に出すのは外 (Runtime の WaveSpawner) で、ここは何体出してよいか (<see cref="SpawnBudget"/>) を答え、
    /// 出した・倒されたの報告 (<see cref="ReportSpawned"/>・<see cref="ReportKilled"/>) を数えて波を進める。時間は <see cref="Tick"/> で外から進める。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="Start"/> で 1 波目を始める (休憩を挟まない)。</item>
    /// <item>波の敵をすべて出し、すべて倒されたら波を全滅させたことになる。最後の波なら <see cref="WavePhase.Completed"/>、
    /// そうでなければ <see cref="Wave.RestSeconds"/> の休憩 (<see cref="WavePhase.Resting"/>) に入る。</item>
    /// <item>休憩は <see cref="Tick"/> で減らし、0 になった Tick で次の波を始める。休憩が 0 秒の波でも、次の波は次の Tick で始まる。
    /// 余った時間は次の波に持ち越さない。</item>
    /// <item>イベントは状態を変え終えてから呼ぶので、ハンドラの中で見える値は変わった後のもの。</item>
    /// </list>
    /// </remarks>
    public sealed class WaveSchedule
    {
        /// <summary>波が始まったときに、波の番号 (1 から) とその値を渡して呼ばれる。</summary>
        public event Action<int, Wave> WaveStarted;

        /// <summary>
        /// 波の敵をすべて倒したときに、その波の番号を渡して呼ばれる。<see cref="Phase"/> は既に Resting か Completed になっている。
        /// </summary>
        public event Action<int> WaveCleared;

        /// <summary>休憩が終わったときに、これから始まる波の番号を渡して呼ばれる。この直後に <see cref="WaveStarted"/> が呼ばれる。</summary>
        public event Action<int> RestEnded;

        /// <summary>最後の波を全滅させたときに 1 度だけ呼ばれる。<see cref="WaveCleared"/> の後。終わりの無い予定では呼ばれない。</summary>
        public event Action Completed;

        private readonly Func<int, Wave> _waveAt;

        private WaveSchedule(Func<int, Wave> waveAt, int? waveCount)
        {
            _waveAt = waveAt;
            WaveCount = waveCount;
            Phase = WavePhase.Waiting;
        }

        /// <summary>波の数。null なら終わりが無い。</summary>
        public int? WaveCount { get; }

        public WavePhase Phase { get; private set; }

        /// <summary>今の波の番号 (1 から)。休憩中と終わった後は最後に全滅させた波の番号。開始前は 0。</summary>
        public int WaveNumber { get; private set; }

        /// <summary>今の波の値。休憩中と終わった後は最後に全滅させた波の値。開始前は <see cref="InvalidOperationException"/>。</summary>
        public Wave CurrentWave
        {
            get
            {
                if (WaveNumber == 0)
                {
                    throw new InvalidOperationException("the wave schedule has not started");
                }

                return _current;
            }
        }

        /// <summary>今の波で出した敵の数。</summary>
        public int Spawned { get; private set; }

        /// <summary>今の波で倒された敵の数。</summary>
        public int Killed { get; private set; }

        /// <summary>出ていてまだ倒されていない敵の数。</summary>
        public int Alive => Spawned - Killed;

        /// <summary>今の波でまだ出していない敵の数。戦っていなければ 0。</summary>
        public int RemainingToSpawn => Phase == WavePhase.Fighting ? _current.EnemyCount - Spawned : 0;

        /// <summary>今の波で倒すべき残りの敵の数 (まだ出していない分を含む)。戦っていなければ 0。</summary>
        public int RemainingEnemies => Phase == WavePhase.Fighting ? _current.EnemyCount - Killed : 0;

        /// <summary>
        /// 今出してよい敵の数。戦っている間だけ、同時に出す上限 (<see cref="Wave.MaxAlive"/>) とまだ出していない数の小さい方から、
        /// 出ている数を引いた値。それ以外は 0。
        /// </summary>
        public int SpawnBudget
        {
            get
            {
                if (Phase != WavePhase.Fighting)
                {
                    return 0;
                }

                return Math.Max(0, Math.Min(_current.MaxAlive - Alive, RemainingToSpawn));
            }
        }

        /// <summary>休憩の残り秒数。休憩中でなければ 0。</summary>
        public float RestRemaining { get; private set; }

        /// <summary>次に始まる波の番号。終わった後と、最後の波の途中では null。</summary>
        public int? NextWaveNumber
        {
            get
            {
                if (Phase == WavePhase.Completed)
                {
                    return null;
                }

                var next = WaveNumber + 1;
                return WaveCount.HasValue && next > WaveCount.Value ? (int?)null : next;
            }
        }

        private Wave _current;

        /// <summary>
        /// waves を 1 波目から順に使う予定。最後の波を全滅させたら終わる。waves は写して持つ。
        /// waves が空か、既定値 (default) の Wave が入っていれば <see cref="ArgumentException"/>。
        /// </summary>
        public static WaveSchedule FromTable(IReadOnlyList<Wave> waves)
        {
            if (waves == null)
            {
                throw new ArgumentNullException(nameof(waves));
            }

            if (waves.Count == 0)
            {
                throw new ArgumentException("waves must not be empty", nameof(waves));
            }

            var copy = new Wave[waves.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                if (waves[i].EnemyCount < 1)
                {
                    throw new ArgumentException($"wave {i + 1} has no enemies; build each wave with the Wave constructor", nameof(waves));
                }

                copy[i] = waves[i];
            }

            return new WaveSchedule(number => copy[number - 1], copy.Length);
        }

        /// <summary>
        /// formula で各波を決める予定。waveCount 個の波を全滅させたら終わる。waveCount が 0 なら終わりが無い。
        /// </summary>
        /// <param name="formula">波の番号から値を決める式。</param>
        /// <param name="waveCount">波の数。0 以上。0 なら終わりが無い。</param>
        public static WaveSchedule FromFormula(WaveFormula formula, int waveCount)
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            if (waveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveCount), waveCount, "waveCount must not be negative");
            }

            return new WaveSchedule(formula.At, waveCount == 0 ? (int?)null : waveCount);
        }

        /// <summary>
        /// number 番目 (1 から) の波の値。number が 1 未満か <see cref="WaveCount"/> を超えれば <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public Wave WaveAt(int number)
        {
            if (number < 1 || (WaveCount.HasValue && number > WaveCount.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be between 1 and the wave count");
            }

            return _waveAt(number);
        }

        /// <summary>1 波目を始める。開始済みなら <see cref="InvalidOperationException"/>。</summary>
        public void Start()
        {
            if (Phase != WavePhase.Waiting)
            {
                throw new InvalidOperationException("the wave schedule has already started");
            }

            BeginWave(1);
        }

        /// <summary>
        /// 敵を 1 体出したことを伝える。<see cref="SpawnBudget"/> が 0 なら (戦っていない・上限まで出ている・出し切った)
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public void ReportSpawned()
        {
            if (SpawnBudget <= 0)
            {
                throw new InvalidOperationException($"no enemy may be spawned now (phase={Phase}, alive={Alive}, spawned={Spawned})");
            }

            Spawned++;
        }

        /// <summary>
        /// 出した敵が 1 体倒されたことを伝える。これで波の敵をすべて倒したら、休憩に入るか終わる。
        /// 戦っていないか、出ている敵がいなければ <see cref="InvalidOperationException"/>。
        /// </summary>
        public void ReportKilled()
        {
            if (Phase != WavePhase.Fighting)
            {
                throw new InvalidOperationException($"enemies can only be killed while fighting, but the phase is {Phase}");
            }

            if (Alive <= 0)
            {
                throw new InvalidOperationException("there is no spawned enemy left to kill");
            }

            Killed++;
            if (Killed < _current.EnemyCount)
            {
                return;
            }

            var cleared = WaveNumber;
            var completed = WaveCount.HasValue && cleared >= WaveCount.Value;
            if (completed)
            {
                Phase = WavePhase.Completed;
                RestRemaining = 0f;
            }
            else
            {
                Phase = WavePhase.Resting;
                RestRemaining = _current.RestSeconds;
            }

            WaveCleared?.Invoke(cleared);
            if (completed)
            {
                Completed?.Invoke();
            }
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。休憩中なら残りを減らし、0 になったら次の波を始める。それ以外の段階では何もしない。
        /// deltaSeconds が負か有限でなければ <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (Phase != WavePhase.Resting)
            {
                return;
            }

            RestRemaining = Math.Max(0f, RestRemaining - deltaSeconds);
            if (RestRemaining > 0f)
            {
                return;
            }

            BeginWave(WaveNumber + 1, raiseRestEnded: true);
        }

        private void BeginWave(int number, bool raiseRestEnded = false)
        {
            var wave = _waveAt(number);
            WaveNumber = number;
            _current = wave;
            Spawned = 0;
            Killed = 0;
            RestRemaining = 0f;
            Phase = WavePhase.Fighting;

            if (raiseRestEnded)
            {
                RestEnded?.Invoke(number);
            }

            // RestEnded のハンドラが波を進めていたら (報告や Tick を呼んだなど)、古い番号で始まりを知らせない。
            if (WaveNumber == number && Phase == WavePhase.Fighting)
            {
                WaveStarted?.Invoke(number, wave);
            }
        }
    }
}
