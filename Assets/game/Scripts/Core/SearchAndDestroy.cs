using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 爆破 (サーチアンドデストロイ) のルール。<see cref="RoundSession"/> で Alpha と Bravo がラウンドを取り合い、
    /// 先に <see cref="SearchAndDestroySpec.WinsToWin"/> 勝した陣営の勝ちで終わる。リスポーンはなく (<see cref="NoRespawn"/>)、
    /// <see cref="SearchAndDestroySpec.SwapEveryRounds"/> ラウンドごとに攻守が入れ替わる。時間は <see cref="Tick"/> で外から進める。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>各ラウンドの待ちに入るとき、攻撃側でいちばん ID の小さい参加者に爆弾を持たせる。攻撃側に誰もいなければ持ち主のいない <see cref="BombState.Dropped"/>。</item>
    /// <item>持ち主が倒れるか抜けたら、爆弾はその場に落ちる (<see cref="BombDropped"/>)。倒れたことは次の <see cref="Tick"/> で気づくが、
    /// このクラスの <see cref="ReportKill"/>・<see cref="ReportDeath"/>・<see cref="RemovePlayer"/> を通せばその場で落とす。
    /// 落ちた爆弾は、生きている攻撃側が <see cref="PickUpBomb"/> で拾える。</item>
    /// <item>持ち主が設置地点の中で長押しを続けている間 (<see cref="SetPlanting"/>) 設置が進み、<see cref="SearchAndDestroySpec.PlantSeconds"/> 続けたら設置になる。
    /// 離すか別の地点に移ると 0 からやり直す。設置するとラウンドの制限時間は止まり、<see cref="SearchAndDestroySpec.FuseSeconds"/> 後に起爆する。</item>
    /// <item>設置後、生きている防衛側の誰かが長押しを続けている間 (<see cref="SetDefusing"/>) 解除が進み、<see cref="SearchAndDestroySpec.DefuseSeconds"/> に届いたら解除になる。
    /// 誰も続けていなければ 0 に戻る。解除し終わる時刻と起爆の時刻が同じなら解除を先にする。</item>
    /// <item>攻撃側の勝ち: 起爆するか、防衛側が全滅する。防衛側の勝ち: 解除するか、設置前に攻撃側が全滅するか、設置前に制限時間が切れる。
    /// 設置前に両陣営が同時に全滅したら引き分け (どちらの勝ちにも数えない)。設置後は攻撃側が全滅しても起爆か解除まで続く。</item>
    /// <item>全滅は <see cref="ModeSession"/> と同じく <see cref="Tick"/> の頭でまとめて調べる (同じ間の相打ちを報告の順によらず同時に扱うため)。</item>
    /// </list>
    /// 参加者は <see cref="Mode"/> に直接加えてもよい (<see cref="ModeSession.AddPlayer(int, Side)"/>)。陣営は 2 チーム (<see cref="TwoTeams"/>) で、
    /// ラウンドをまたいで変わらない。攻めるか守るかは <see cref="Attacker"/> と <see cref="Defender"/> が決める。
    /// <see cref="Mode"/> の制限時間は無限で、ラウンドの残り時間は <see cref="RoundRemaining"/> が持つ。
    /// </remarks>
    public sealed class SearchAndDestroy
    {
        /// <summary>ラウンドの開始前の待ちに入ったときに呼ばれる。引数はラウンドの番号 (1 から)。爆弾はもう配り終えている。体力と位置はここで戻す。</summary>
        public event Action<int> PreparationStarted;

        /// <summary>待ちが終わってラウンドが始まったときに呼ばれる。引数はラウンドの番号 (1 から)。</summary>
        public event Action<int> RoundStarted;

        /// <summary>ラウンドが終わったときに呼ばれる。引数は勝った陣営 (引き分けなら null) と終わった理由。</summary>
        public event Action<Side?, RoundEndReason> RoundEnded;

        /// <summary>攻守が入れ替わったときに呼ばれる。<see cref="RoundEnded"/> の後、次の <see cref="PreparationStarted"/> の前。</summary>
        public event Action SidesSwapped;

        /// <summary>試合が終わった瞬間に 1 度だけ呼ばれる。引数は先取数に届いた陣営。</summary>
        public event Action<Side> Ended;

        /// <summary>落ちていた爆弾を拾ったときに呼ばれる。引数は拾った人。</summary>
        public event Action<int> BombPickedUp;

        /// <summary>持ち主が倒れるか抜けて、爆弾が落ちたときに呼ばれる。引数はそれまでの持ち主。</summary>
        public event Action<int> BombDropped;

        /// <summary>設置し終えたときに呼ばれる。引数は設置した人と設置地点の番号。</summary>
        public event Action<int, int> BombPlanted;

        /// <summary>解除し終えたときに呼ばれる。引数は解除した人。この後の <see cref="Tick"/> でラウンドが終わる。</summary>
        public event Action<int> BombDefused;

        /// <summary>起爆したときに呼ばれる。引数は設置地点の番号。この後の <see cref="Tick"/> でラウンドが終わる。</summary>
        public event Action<int> BombDetonated;

        private readonly ModeSession _mode;
        private readonly RoundSession _rounds;

        // 参加者ごとの、長押しを続けている設置地点 (続けていなければ無い)。顔ぶれの変化は Tick で拾う。
        private readonly Dictionary<int, int> _plantIntents = new Dictionary<int, int>();
        private readonly HashSet<int> _defuseIntents = new HashSet<int>();

        // イベントのハンドラで Reset されたら、その後の処理を続けないために数える。
        private int _generation;
        private RoundEndReason? _decidedReason;
        private Side? _decidedWinner;

        public SearchAndDestroySpec Spec { get; }

        /// <summary>1 ラウンドを表す ModeSession。参加者の出入りと、ラウンド中のキル数とデス数はここで見る。</summary>
        public ModeSession Mode => _mode;

        public RoundSession Rounds => _rounds;

        public RoundPhase Phase => _rounds.Phase;

        /// <summary>今のラウンドの番号 (1 から)。開始前は 0。</summary>
        public int RoundNumber => _rounds.RoundNumber;

        /// <summary>今のラウンドで攻める (爆弾を持つ) 陣営。</summary>
        public Side Attacker => _rounds.Attacker;

        /// <summary>今のラウンドで守る陣営。</summary>
        public Side Defender => _rounds.Defender;

        /// <summary>試合に勝った陣営。終わる前は null。</summary>
        public Side? Winner => _rounds.Winner;

        public BombState Bomb { get; private set; }

        /// <summary>爆弾の持ち主。<see cref="BombState.Carried"/> 以外では null。</summary>
        public int? CarrierId { get; private set; }

        /// <summary>設置した地点の番号。<see cref="BombState.Planted"/>・<see cref="BombState.Defused"/>・<see cref="BombState.Detonated"/> 以外では null。</summary>
        public int? PlantedSite { get; private set; }

        /// <summary>設置した人。設置前は null。</summary>
        public int? PlanterId { get; private set; }

        /// <summary>設置が進んでいる地点。進んでいなければ null。</summary>
        public int? PlantingSite { get; private set; }

        /// <summary>設置の進み (秒)。0 から <see cref="SearchAndDestroySpec.PlantSeconds"/>。</summary>
        public float PlantProgress { get; private set; }

        /// <summary>最後の <see cref="Tick"/> で解除を進めた防衛側。進めていなければ null。</summary>
        public int? DefuserId { get; private set; }

        /// <summary>解除の進み (秒)。0 から <see cref="SearchAndDestroySpec.DefuseSeconds"/>。</summary>
        public float DefuseProgress { get; private set; }

        /// <summary>ラウンドの残り時間 (秒)。設置すると止まる。</summary>
        public float RoundRemaining { get; private set; }

        /// <summary>起爆までの残り (秒)。設置前は <see cref="SearchAndDestroySpec.FuseSeconds"/> のまま。</summary>
        public float FuseRemaining { get; private set; }

        /// <summary>最後に終わったラウンドの理由。まだ 1 ラウンドも終わっていなければ null。</summary>
        public RoundEndReason? LastRoundEnd { get; private set; }

        /// <summary>最後に終わったラウンドの勝者。引き分けか、まだ終わっていなければ null。</summary>
        public Side? LastRoundWinner { get; private set; }

        /// <summary>設置の進みの割合 (0 から 1)。</summary>
        public float PlantFraction => PlantProgress / Spec.PlantSeconds;

        /// <summary>解除の進みの割合 (0 から 1)。</summary>
        public float DefuseFraction => DefuseProgress / Spec.DefuseSeconds;

        /// <param name="spec">試合の設定。</param>
        public SearchAndDestroy(SearchAndDestroySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            // ラウンドの時間は設置で止める必要があり、ModeSession の時間切れでは止められないので、このクラスで数える。
            _mode = new ModeSession(
                new TwoTeams(),
                float.PositiveInfinity,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new BombWin(this) });
            _rounds = new RoundSession(_mode, spec.WinsToWin, spec.PreparationSeconds, spec.SwapEveryRounds);
            _rounds.PreparationStarted += OnPreparationStarted;
            _rounds.RoundStarted += round => RoundStarted?.Invoke(round);
            _rounds.RoundEnded += OnRoundEnded;
            _rounds.SidesSwapped += () => SidesSwapped?.Invoke();
            _rounds.Ended += winner => Ended?.Invoke(winner);
            ResetRoundState();
        }

        /// <summary>試合を始め、1 ラウンド目の待ちに入る。Waiting のときだけ呼べる。</summary>
        public void Start() => _rounds.Start();

        /// <summary>
        /// ラウンドの勝ち数・攻守・爆弾・最後のラウンドの結果を初期値に戻し、Waiting に戻す。参加者と陣営分けは残す。
        /// どの状態からでも呼べ、イベントのハンドラの中で呼ぶとその後のイベントは出さない。
        /// </summary>
        public void Reset()
        {
            _generation++;
            _rounds.Reset();
            ResetRoundState();
            LastRoundEnd = null;
            LastRoundWinner = null;
        }

        /// <summary>プレイヤーを side に加える。待ちの間に攻撃側へ入り、爆弾の持ち主がいなければ持たせる。</summary>
        public void AddPlayer(int playerId, Side side)
        {
            _mode.AddPlayer(playerId, side);
            if (Phase != RoundPhase.Live && Phase != RoundPhase.Ended && Bomb == BombState.Dropped && side == Attacker)
            {
                AssignBomb();
            }
        }

        /// <summary>
        /// プレイヤーを抜く。爆弾を持っていたら、ラウンド中ならその場に落とし (<see cref="BombDropped"/>)、待ちの間なら次の攻撃側に持たせる。
        /// 抜けたことで陣営が全滅した場合は、次の <see cref="Tick"/> で扱う。
        /// </summary>
        public void RemovePlayer(int playerId)
        {
            _mode.RemovePlayer(playerId);
            _plantIntents.Remove(playerId);
            _defuseIntents.Remove(playerId);
            if (CarrierId != playerId)
            {
                return;
            }

            if (Phase == RoundPhase.Live)
            {
                DropFrom(playerId);
            }
            else
            {
                AssignBomb();
            }
        }

        /// <summary>side が勝ったラウンドの数。</summary>
        public int GetRoundWins(Side side) => _rounds.GetRoundWins(side);

        /// <summary>playerId が今のラウンドで攻撃側なら true。</summary>
        public bool IsAttacker(int playerId) => _mode.GetSide(playerId) == Attacker;

        /// <summary>
        /// killerId が victimId を倒したことを報告する (<see cref="RoundSession.ReportKill"/>)。倒された人が爆弾を持っていれば、その場で落とす。
        /// </summary>
        public void ReportKill(int killerId, int victimId)
        {
            _rounds.ReportKill(killerId, victimId);
            DropIfCarrierDown();
        }

        /// <summary>playerId が倒した相手なしで倒れたことを報告する (<see cref="RoundSession.ReportDeath"/>)。爆弾を持っていれば、その場で落とす。</summary>
        public void ReportDeath(int playerId)
        {
            _rounds.ReportDeath(playerId);
            DropIfCarrierDown();
        }

        /// <summary>
        /// playerId が設置地点 site の中で長押しを続けているかを伝える。続けていなければ null。次の <see cref="Tick"/> から数える。
        /// 爆弾を持っている生きた攻撃側のものだけが設置を進める。どの状態でも呼べる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId が参加していないか、site が地点の番号の外。</exception>
        public void SetPlanting(int playerId, int? site)
        {
            _mode.GetSide(playerId);
            if (site.HasValue && (site.Value < 0 || site.Value >= Spec.SiteCount))
            {
                throw new ArgumentOutOfRangeException(nameof(site), site, "no bomb site with this index");
            }

            if (site.HasValue)
            {
                _plantIntents[playerId] = site.Value;
            }
            else
            {
                _plantIntents.Remove(playerId);
            }
        }

        /// <summary>
        /// playerId が設置された爆弾のそばで長押しを続けているかを伝える。次の <see cref="Tick"/> から数える。
        /// 生きた防衛側のものだけが解除を進める。どの状態でも呼べる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId が参加していない。</exception>
        public void SetDefusing(int playerId, bool defusing)
        {
            _mode.GetSide(playerId);
            if (defusing)
            {
                _defuseIntents.Add(playerId);
            }
            else
            {
                _defuseIntents.Remove(playerId);
            }
        }

        /// <summary>
        /// 落ちている爆弾を playerId に拾わせる。ラウンド中で、爆弾が落ちていて、playerId が生きている攻撃側のときだけ拾って true を返す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">playerId が参加していない。</exception>
        public bool PickUpBomb(int playerId)
        {
            var side = _mode.GetSide(playerId);
            if (Phase != RoundPhase.Live
                || _mode.State != GameModeState.Playing
                || Bomb != BombState.Dropped
                || side != Attacker
                || _mode.IsDown(playerId))
            {
                return false;
            }

            Bomb = BombState.Carried;
            CarrierId = playerId;
            BombPickedUp?.Invoke(playerId);
            return true;
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。ラウンド中は、倒れた持ち主の爆弾を落とし、設置・解除・起爆とラウンドの残り時間を進めてから
        /// <see cref="RoundSession.Tick"/> する。1 回の Tick が長くても、設置し終えた時刻から起爆までの時間を数え始める。
        /// 待ちの間は待ちだけを進め、待ちが終わった Tick の余りは設置と残り時間に回さない。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (Phase == RoundPhase.Live && _mode.State == GameModeState.Playing && !_decidedReason.HasValue)
            {
                var generation = _generation;
                AdvanceObjective(deltaSeconds);
                if (generation != _generation)
                {
                    return;
                }
            }

            _rounds.Tick(deltaSeconds);
        }

        private void OnPreparationStarted(int round)
        {
            ResetRoundState();
            PreparationStarted?.Invoke(round);
        }

        private void OnRoundEnded(Side? winner)
        {
            LastRoundEnd = _decidedReason ?? (winner.HasValue
                ? (winner.Value == Attacker ? RoundEndReason.DefendersEliminated : RoundEndReason.AttackersEliminated)
                : RoundEndReason.BothEliminated);
            LastRoundWinner = winner;
            RoundEnded?.Invoke(winner, LastRoundEnd.Value);
        }

        private void ResetRoundState()
        {
            _plantIntents.Clear();
            _defuseIntents.Clear();
            _decidedReason = null;
            _decidedWinner = null;
            PlantedSite = null;
            PlanterId = null;
            PlantingSite = null;
            PlantProgress = 0f;
            DefuserId = null;
            DefuseProgress = 0f;
            RoundRemaining = Spec.RoundSeconds;
            FuseRemaining = Spec.FuseSeconds;
            AssignBomb();
        }

        /// <summary>攻撃側でいちばん ID の小さい参加者に爆弾を持たせる。攻撃側に誰もいなければ持ち主なしで落ちていることにする。</summary>
        private void AssignBomb()
        {
            int? carrier = null;
            foreach (var playerId in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(playerId) == Attacker)
                {
                    carrier = playerId;
                    break;
                }
            }

            CarrierId = carrier;
            Bomb = carrier.HasValue ? BombState.Carried : BombState.Dropped;
        }

        private void DropIfCarrierDown()
        {
            if (Phase != RoundPhase.Live || Bomb != BombState.Carried || !CarrierId.HasValue)
            {
                return;
            }

            var carrier = CarrierId.Value;
            if (!_mode.HasPlayer(carrier) || _mode.IsDown(carrier) || _mode.GetSide(carrier) != Attacker)
            {
                DropFrom(carrier);
            }
        }

        private void DropFrom(int carrierId)
        {
            Bomb = BombState.Dropped;
            CarrierId = null;
            PlantingSite = null;
            PlantProgress = 0f;
            BombDropped?.Invoke(carrierId);
        }

        private void AdvanceObjective(float deltaSeconds)
        {
            var generation = _generation;
            DropIfCarrierDown();
            if (generation != _generation)
            {
                return;
            }

            if (Bomb == BombState.Planted)
            {
                AdvanceFuse(deltaSeconds);
                return;
            }

            var site = PlantCandidate();
            if (site.HasValue)
            {
                if (PlantingSite != site)
                {
                    PlantingSite = site;
                    PlantProgress = 0f;
                }

                var needed = Spec.PlantSeconds - PlantProgress;
                if (needed <= deltaSeconds && needed <= RoundRemaining)
                {
                    RoundRemaining -= needed;
                    Plant(CarrierId.Value, site.Value);
                    if (generation != _generation)
                    {
                        return;
                    }

                    AdvanceFuse(deltaSeconds - needed);
                    return;
                }

                PlantProgress += Math.Min(deltaSeconds, RoundRemaining);
            }
            else
            {
                PlantingSite = null;
                PlantProgress = 0f;
            }

            RoundRemaining -= deltaSeconds;
            if (RoundRemaining <= 0f)
            {
                RoundRemaining = 0f;
                PlantingSite = null;
                PlantProgress = 0f;
                Decide(Defender, RoundEndReason.TimeUp);
            }
        }

        private void AdvanceFuse(float deltaSeconds)
        {
            var defuser = FirstDefuser();
            if (defuser.HasValue)
            {
                DefuserId = defuser;
                var needed = Spec.DefuseSeconds - DefuseProgress;
                if (needed <= deltaSeconds && needed <= FuseRemaining)
                {
                    FuseRemaining -= needed;
                    DefuseProgress = Spec.DefuseSeconds;
                    Bomb = BombState.Defused;
                    Decide(Defender, RoundEndReason.Defused);
                    BombDefused?.Invoke(defuser.Value);
                    return;
                }

                DefuseProgress += Math.Min(deltaSeconds, FuseRemaining);
            }
            else
            {
                DefuserId = null;
                DefuseProgress = 0f;
            }

            FuseRemaining -= deltaSeconds;
            if (FuseRemaining <= 0f)
            {
                FuseRemaining = 0f;
                Bomb = BombState.Detonated;
                Decide(Attacker, RoundEndReason.Detonated);
                BombDetonated?.Invoke(PlantedSite.Value);
            }
        }

        private void Plant(int playerId, int site)
        {
            Bomb = BombState.Planted;
            CarrierId = null;
            PlantedSite = site;
            PlanterId = playerId;
            PlantingSite = null;
            PlantProgress = 0f;
            FuseRemaining = Spec.FuseSeconds;
            DefuseProgress = 0f;
            DefuserId = null;
            BombPlanted?.Invoke(playerId, site);
        }

        /// <summary>持ち主が生きていて、設置地点で長押しを続けていればその地点。</summary>
        private int? PlantCandidate()
        {
            if (Bomb != BombState.Carried || !CarrierId.HasValue)
            {
                return null;
            }

            var carrier = CarrierId.Value;
            if (!_plantIntents.TryGetValue(carrier, out var site) || _mode.IsDown(carrier))
            {
                return null;
            }

            return site;
        }

        /// <summary>長押しを続けている、生きた防衛側のうち ID がいちばん小さい人。</summary>
        private int? FirstDefuser()
        {
            int? first = null;
            foreach (var playerId in _defuseIntents)
            {
                if (!_mode.HasPlayer(playerId) || _mode.IsDown(playerId) || _mode.GetSide(playerId) != Defender)
                {
                    continue;
                }

                if (!first.HasValue || playerId < first.Value)
                {
                    first = playerId;
                }
            }

            return first;
        }

        private void Decide(Side? winner, RoundEndReason reason)
        {
            _decidedWinner = winner;
            _decidedReason = reason;
        }

        private ModeOutcome CheckStanding()
        {
            if (_decidedReason.HasValue)
            {
                return ModeOutcome.Decided(_decidedWinner);
            }

            var attackersOut = _mode.IsSideEliminated(Attacker);
            var defendersOut = _mode.IsSideEliminated(Defender);
            var planted = Bomb == BombState.Planted;
            if (defendersOut)
            {
                if (attackersOut && !planted)
                {
                    Decide(null, RoundEndReason.BothEliminated);
                }
                else
                {
                    Decide(Attacker, RoundEndReason.DefendersEliminated);
                }
            }
            else if (attackersOut && !planted)
            {
                Decide(Defender, RoundEndReason.AttackersEliminated);
            }
            else
            {
                return ModeOutcome.Undecided;
            }

            return ModeOutcome.Decided(_decidedWinner);
        }

        /// <summary>爆弾と全滅でラウンドを終える。時間切れは <see cref="SearchAndDestroy"/> が数えるので、ここでは扱わない。</summary>
        private sealed class BombWin : IWinCondition
        {
            private readonly SearchAndDestroy _owner;

            public BombWin(SearchAndDestroy owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => _owner.CheckStanding();

            public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
        }
    }
}
