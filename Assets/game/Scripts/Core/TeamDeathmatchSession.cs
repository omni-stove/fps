using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// サーバーで動かすチームデスマッチの進行役。<see cref="TeamDeathmatch"/> (点数・時間・リスポーン待ち) に、
    /// 参加順のチーム割り当て (<see cref="TeamAssigner"/>)、プレイヤーごとの <see cref="Health"/>、
    /// <see cref="SpawnSelector"/> による出現地点の選択をまとめる。ネットワークの層はここを呼び、
    /// イベントを受けて同期するだけにする。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>両チームに 1 人以上そろった時点で試合を始める。</item>
    /// <item>味方へのダメージは <see cref="GameRules.FriendlyFire"/> に従い、無視する・与える・撃った本人に跳ね返すを切り替える (自滅はいつも数える)。</item>
    /// <item>プレイヤーごとに <see cref="HealthRegenerator"/> を持ち、<see cref="TeamDeathmatchSpec.Rules"/> に従って
    /// 試合中だけ体力を自動回復させる。</item>
    /// <item>体力が 0 になったら <see cref="TeamDeathmatch.ReportKill"/> (味方に倒されたときと跳ね返しで倒れたときは
    /// <see cref="TeamDeathmatch.ReportDeath"/>) を呼び、リスポーン待ちが終わったら
    /// 体力を満タンに戻して地点を選び <see cref="Spawned"/> を出す。いつ待ちが終わるかは
    /// <see cref="GameRules.RespawnMode"/> に従う (None では試合中は戻らず、全滅したチームの負けになる)。</item>
    /// <item>イベントはいつも「倒した → 点数 → リスポーン/終了」の順に出す
    /// (RespawnSeconds が 0 でも倒したことより先にリスポーンを出さない)。</item>
    /// <item>再開までの秒数 (<see cref="RestartDelaySeconds"/>) を渡したときは、終わってからその秒数だけ
    /// <see cref="Tick"/> が進んだら <see cref="Restart"/> して同じ顔ぶれ・同じチーム分けで次の試合を始める。</item>
    /// <item><see cref="ActionRules.Downed"/> が <see cref="Fps.Core.Downed.Revive"/> なら、体力が 0 になっても倒さずにダウンさせる
    /// (<see cref="ApplyDamage"/>)。ダウン中は <see cref="IsAlive"/> のまま、自動回復せず、動けるプレイヤー (<see cref="SelectSpawn"/> と
    /// <see cref="FindNearestEnemy"/> が数える人) には入らない。ダウン中の体力が尽きるか出血の秒数が過ぎると倒れ、
    /// 味方が <see cref="TryRevive"/> を押し続けると起き上がる。チームの全員がダウンか死亡になったら、ダウン中の人もその場で倒す。</item>
    /// </list>
    /// </remarks>
    public sealed class TeamDeathmatchSession
    {
        /// <summary>試合が始まるたびに呼ばれる (<see cref="Restart"/> で始め直したときも)。</summary>
        public event Action Started;

        /// <summary>
        /// <see cref="Restart"/> で点数・キル数・デス数・残り時間・体力を初期値に戻した直後に呼ばれる。
        /// 続けて <see cref="ScoreChanged"/> (0, 0)、各プレイヤーの <see cref="PlayerChanged"/> と <see cref="Spawned"/>、
        /// 両チームがそろっていれば <see cref="Started"/> の順に出る。
        /// </summary>
        public event Action Restarted;

        /// <summary>チームの点数が変わったときに呼ばれる。引数は (Alpha の点数, Bravo の点数)。</summary>
        public event Action<int, int> ScoreChanged;

        /// <summary>プレイヤーのチーム・キル数・デス数・体力のどれかが変わったときに呼ばれる。引数は playerId。</summary>
        public event Action<int> PlayerChanged;

        /// <summary>プレイヤーが倒されたときに呼ばれる。引数は (killerId, victimId)。</summary>
        public event Action<int, int> Killed;

        /// <summary>参加したときとリスポーンするときに、出現する地点を知らせる。引数は (playerId, 地点)。</summary>
        public event Action<int, SpawnPoint> Spawned;

        /// <summary>試合が終わったときに 1 度だけ呼ばれる。引数は勝ったチームで、同点なら null。</summary>
        public event Action<Team?> Ended;

        /// <summary>
        /// 倒さずにダウンさせたときに呼ばれる。引数は (attackerId, victimId)。続けて victimId の <see cref="PlayerChanged"/> と
        /// <see cref="DownedChanged"/> が出る。
        /// </summary>
        public event Action<int, int> Downed;

        /// <summary>
        /// ダウン中の人が起き上がったときに呼ばれる。引数は (reviverId, targetId)。続けて targetId の <see cref="PlayerChanged"/> と
        /// <see cref="DownedChanged"/> が出る。
        /// </summary>
        public event Action<int, int> Revived;

        /// <summary>
        /// <see cref="IsDowned"/> が変わったときに呼ばれる (ダウンした・起き上がった・ダウン中に倒れた・<see cref="Restart"/> で戻った)。
        /// 引数は playerId。抜けたときは呼ばない。
        /// </summary>
        public event Action<int> DownedChanged;

        private static readonly Team[] _teams = { Team.Alpha, Team.Bravo };

        private readonly TeamDeathmatch _match;
        private readonly TeamAssigner _assigner = new TeamAssigner();
        private readonly SpawnSelector _selector;
        private readonly SpawnPoint[] _spawnPoints;
        private readonly Func<int, Vector3> _positionOf;

        // playerId の昇順に回して、同じ入力なら同じ結果になるようにする。
        private readonly SortedDictionary<int, Health> _health = new SortedDictionary<int, Health>();

        // _health と同じ顔ぶれを持つ。Health を作り直すたびに作り直す。
        private readonly Dictionary<int, HealthRegenerator> _regenerators = new Dictionary<int, HealthRegenerator>();
        // Downed が Off なら null
        private readonly DownedTracker _downed;
        private readonly Queue<Action> _deferred = new Queue<Action>();
        private int _busy;

        // 終わってから次の試合までの待ち。待っていないときは _restartPending が false
        private readonly float? _restartDelaySeconds;
        private float _restartRemaining;
        private bool _restartPending;

        public TeamDeathmatchSpec Spec => _match.Spec;

        /// <summary>終わってから自動で次の試合を始めるまでの秒数。null なら自動では始めない。</summary>
        public float? RestartDelaySeconds => _restartDelaySeconds;

        /// <summary>終わった後、次の試合を待っている間は true。</summary>
        public bool IsRestartPending => _restartPending;

        /// <summary>次の試合が始まるまでの残り秒数。待っていないときは 0。</summary>
        public float RestartRemainingSeconds => _restartPending ? Math.Max(0f, _restartRemaining) : 0f;

        /// <summary>参加したときとリスポーンしたときの体力。</summary>
        public int MaxHealth { get; }

        public GameModeState State => _match.State;
        public float RemainingSeconds => _match.RemainingSeconds;
        public Team? Winner => _match.Winner;
        public int PlayerCount => _health.Count;

        /// <summary>次に参加する人のチーム。</summary>
        public Team NextTeam => _assigner.Peek;

        /// <param name="spec">試合の設定。</param>
        /// <param name="maxHealth">プレイヤーの最大体力。1 以上。</param>
        /// <param name="spawnPoints">出現地点。両チームとも 1 か所以上使えること。順番は同点時の決め手になる。</param>
        /// <param name="selector">出現地点の選び方。</param>
        /// <param name="positionOf">playerId から現在位置を引く。参加中のプレイヤーについてだけ呼ぶ。</param>
        /// <param name="restartDelaySeconds">
        /// 終わってから自動で次の試合を始めるまでの秒数。0 以上の有限値。null なら自動では始めない。
        /// </param>
        public TeamDeathmatchSession(
            TeamDeathmatchSpec spec,
            int maxHealth,
            IReadOnlyList<SpawnPoint> spawnPoints,
            SpawnSelector selector,
            Func<int, Vector3> positionOf,
            float? restartDelaySeconds = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (restartDelaySeconds.HasValue
                && (!(restartDelaySeconds.Value >= 0f) || float.IsInfinity(restartDelaySeconds.Value)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(restartDelaySeconds), restartDelaySeconds, "restartDelaySeconds must not be negative and must be finite");
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (spawnPoints == null)
            {
                throw new ArgumentNullException(nameof(spawnPoints));
            }

            _spawnPoints = new SpawnPoint[spawnPoints.Count];
            for (var i = 0; i < spawnPoints.Count; i++)
            {
                _spawnPoints[i] = spawnPoints[i] ?? throw new ArgumentException("spawnPoints must not contain null", nameof(spawnPoints));
            }

            RequireSpawnPointFor(_spawnPoints, Team.Alpha, nameof(spawnPoints));
            RequireSpawnPointFor(_spawnPoints, Team.Bravo, nameof(spawnPoints));

            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _positionOf = positionOf ?? throw new ArgumentNullException(nameof(positionOf));
            MaxHealth = maxHealth;
            _restartDelaySeconds = restartDelaySeconds;

            var actions = spec.Rules.Actions;
            if (actions.Downed == Fps.Core.Downed.Revive)
            {
                _downed = new DownedTracker(
                    DownedTracker.HealthFor(maxHealth, actions.DownedHealthFraction),
                    actions.DownedBleedoutSeconds,
                    actions.ReviveSeconds);
            }

            _match = new TeamDeathmatch(spec);
            _match.RespawnReady += playerId => Defer(() => Respawn(playerId));
            _match.Ended += winner =>
            {
                // 終わった瞬間から数える (Ended の受け手が残り秒数を読めるよう、知らせるより先に)
                if (_restartDelaySeconds.HasValue)
                {
                    _restartPending = true;
                    _restartRemaining = _restartDelaySeconds.Value;
                }

                Defer(() => Ended?.Invoke(winner));
            };
        }

        public bool HasPlayer(int playerId) => _health.ContainsKey(playerId);

        public Team GetTeam(int playerId) => _match.GetTeam(playerId);

        public int GetKills(int playerId) => _match.GetKills(playerId);

        public int GetDeaths(int playerId) => _match.GetDeaths(playerId);

        public int GetScore(Team team) => _match.GetScore(team);

        /// <summary>体力。ダウン中はダウン中の体力。</summary>
        public int GetHealth(int playerId)
        {
            var health = FindHealth(playerId);
            return IsDownedUnchecked(playerId) ? _downed.GetHealth(playerId) : health.Current;
        }

        /// <summary>倒れていなければ true。ダウン中も true。</summary>
        public bool IsAlive(int playerId) => IsAliveWith(playerId, FindHealth(playerId));

        /// <summary>ダウン中なら true。<see cref="ActionRules.Downed"/> が Off ならいつも false。</summary>
        public bool IsDowned(int playerId)
        {
            FindHealth(playerId);
            return IsDownedUnchecked(playerId);
        }

        /// <summary>ダウン中の人が出血で倒れるまでの残り秒数。ダウンしていなければ null。</summary>
        public float? GetDownedRemaining(int playerId)
        {
            FindHealth(playerId);
            return _downed?.GetBleedoutRemaining(playerId);
        }

        /// <summary>ダウン中の人の蘇生の進み具合 (0 以上 1 以下)。ダウンしていない・誰も起こしていなければ 0。</summary>
        public float GetReviveProgress(int playerId)
        {
            FindHealth(playerId);
            return _downed?.GetReviveProgress(playerId) ?? 0f;
        }

        /// <inheritdoc cref="TeamDeathmatch.IsEliminated"/>
        public bool IsEliminated(int playerId)
        {
            FindHealth(playerId);
            return _match.IsEliminated(playerId);
        }

        /// <inheritdoc cref="TeamDeathmatch.GetRespawnRemaining"/>
        public float? GetRespawnRemaining(int playerId)
        {
            FindHealth(playerId);
            return _match.GetRespawnRemaining(playerId);
        }

        /// <inheritdoc cref="TeamDeathmatch.SecondsUntilNextWave"/>
        public float? SecondsUntilNextWave => _match.SecondsUntilNextWave;

        /// <summary>参加中のプレイヤーの id を昇順で返す。</summary>
        public IReadOnlyList<int> GetPlayerIds()
        {
            return new List<int>(_health.Keys);
        }

        /// <summary>
        /// プレイヤーを参加させ、参加順に交互にチームを割り当てる。<see cref="PlayerChanged"/> と
        /// <see cref="Spawned"/> を出し、両チームがそろったら試合を始める。
        /// </summary>
        /// <returns>割り当てたチーム。</returns>
        /// <exception cref="ArgumentException">すでに参加している。</exception>
        public Team Join(int playerId)
        {
            if (_health.ContainsKey(playerId))
            {
                throw new ArgumentException($"player {playerId} has already joined", nameof(playerId));
            }

            var team = _assigner.Assign();
            _match.AddPlayer(playerId, team);
            ResetHealth(playerId);

            PlayerChanged?.Invoke(playerId);
            Spawned?.Invoke(playerId, SelectSpawn(playerId));
            TryStart();
            return team;
        }

        /// <summary>
        /// プレイヤーを抜く。チームの点数はそのまま残し、リスポーン待ちとダウンは取り消す。抜けた人にはそれ以後キルを付けない。
        /// 抜けたことでチームの全員がダウンか死亡になったら、ダウン中の人をその場で倒す。
        /// </summary>
        public void Leave(int playerId)
        {
            FindHealth(playerId);
            _match.RemovePlayer(playerId);
            _health.Remove(playerId);
            _regenerators.Remove(playerId);
            if (_downed != null)
            {
                _downed.RemovePlayer(playerId);
                KillWipedOutTeams();
            }
        }

        /// <summary>
        /// attackerId が victimId に amount のダメージを与える。体力が 0 になったら倒したことにして
        /// <see cref="TeamDeathmatch.ReportKill"/> を呼ぶ。ネットワーク側のダメージ処理はすべてここを通す。
        /// 味方へのダメージは <see cref="TeamDeathmatchSpec.Rules"/> の <see cref="GameRules.FriendlyFire"/> に従う。
        /// <list type="bullet">
        /// <item><see cref="FriendlyFire.Off"/>: 無視して <see cref="DamageOutcome.Ignored"/> を返す。</item>
        /// <item><see cref="FriendlyFire.On"/>: 味方の体力を減らす。倒したら <see cref="Killed"/> を (attackerId, victimId) で出し、
        /// <see cref="TeamDeathmatch.ReportDeath"/> で倒された側の Deaths だけを増やす (倒した側のキルとチームの点数は動かさない)。</item>
        /// <item><see cref="FriendlyFire.Reflect"/>: victimId ではなく attackerId の体力を減らす。それで倒れたら
        /// <see cref="Killed"/> を (attackerId, attackerId) で出し、<see cref="TeamDeathmatch.ReportDeath"/> で attackerId の Deaths を増やす。
        /// 返す値は attackerId に起きたこと (体力が減った・倒れた・何も起きなかった) を表す。体力が変わったのは撃った本人なので、
        /// 結果を見て体力の同期やヒットの知らせを出す呼び出し側が、実際に体力の変わっていない味方に Damaged / Killed を
        /// 結び付けないようにするため。attackerId がすでに倒れていれば何もせず <see cref="DamageOutcome.Ignored"/> を返す。</item>
        /// </list>
        /// 自滅 (attackerId == victimId) はモードによらず数える。
        /// <para>
        /// <see cref="ActionRules.Downed"/> が <see cref="Fps.Core.Downed.Revive"/> のときは、体力が 0 になっても倒さずにダウンさせ、
        /// <see cref="Downed"/> を出して <see cref="DamageOutcome.Downed"/> を返す。ただし自滅 (跳ね返しを含む) と、
        /// ダウンさせるとチームに動ける人がいなくなるときは、ダウンさせずにその場で倒す。
        /// ダウン中の人へのダメージはダウン中の体力を減らし (味方からは上の味方撃ちのルールに従う)、0 になったら倒す。
        /// キルは最後に当てた敵に付ける (味方や本人がとどめを刺しても、ダウン中に当てた敵がいればその敵)。
        /// 敵がいなければ倒された側の Deaths だけを増やす。
        /// </para>
        /// </summary>
        /// <param name="attackerId">撃った人。victimId と同じなら自滅。</param>
        /// <param name="victimId">撃たれた人。</param>
        /// <param name="amount">ダメージ。0 以上。</param>
        public DamageOutcome ApplyDamage(int attackerId, int victimId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var attacker = FindHealth(attackerId, nameof(attackerId));
            var victim = FindHealth(victimId, nameof(victimId));

            if (amount == 0 || State != GameModeState.Playing || !IsAliveWith(victimId, victim))
            {
                return DamageOutcome.Ignored;
            }

            if (attackerId == victimId || GetTeam(attackerId) != GetTeam(victimId))
            {
                return Damage(attackerId, victimId, victim, amount, byAlly: false);
            }

            var friendlyFire = Spec.Rules.FriendlyFire;
            switch (friendlyFire)
            {
                case FriendlyFire.Off:
                    return DamageOutcome.Ignored;
                case FriendlyFire.On:
                    return Damage(attackerId, victimId, victim, amount, byAlly: true);
                case FriendlyFire.Reflect:
                    return !IsAliveWith(attackerId, attacker)
                        ? DamageOutcome.Ignored
                        : Damage(attackerId, attackerId, attacker, amount, byAlly: true);
                default:
                    throw new InvalidOperationException("unknown FriendlyFire " + friendlyFire);
            }
        }

        private DamageOutcome Damage(int attackerId, int victimId, Health victim, int amount, bool byAlly)
        {
            _busy++;
            try
            {
                if (IsDownedUnchecked(victimId))
                {
                    return DamageDowned(attackerId, victimId, amount, byAlly);
                }

                victim.TakeDamage(amount);
                _regenerators[victimId].NotifyDamaged();
                if (!victim.IsDead)
                {
                    PlayerChanged?.Invoke(victimId);
                    return DamageOutcome.Damaged;
                }

                // ダウンさせても全滅ですぐ倒すことになるときは、Downed と Killed を続けて出さずに初めから倒す
                if (_downed != null && attackerId != victimId && HasOtherStandingAlly(victimId))
                {
                    _downed.Down(victimId, attackerId, byEnemy: !byAlly);
                    Downed?.Invoke(attackerId, victimId);
                    PlayerChanged?.Invoke(victimId);
                    DownedChanged?.Invoke(victimId);
                    return DamageOutcome.Downed;
                }

                ReportKilled(attackerId, victimId, scores: !byAlly);
                KillWipedOutTeams();
                return DamageOutcome.Killed;
            }
            finally
            {
                _busy--;
                FlushDeferred();
            }
        }

        private DamageOutcome DamageDowned(int attackerId, int victimId, int amount, bool byAlly)
        {
            var byEnemy = !byAlly && attackerId != victimId;
            if (_downed.TakeDamage(victimId, attackerId, byEnemy, amount) > 0)
            {
                PlayerChanged?.Invoke(victimId);
                return DamageOutcome.Damaged;
            }

            KillDowned(victimId, byEnemy ? attackerId : _downed.GetLastEnemyId(victimId), attackerId);
            KillWipedOutTeams();
            return DamageOutcome.Killed;
        }

        /// <summary>
        /// ダウン中の victimId を倒す。creditId があればその敵のキルにし、なければ fallbackKillerId を倒した人として
        /// <see cref="Killed"/> を出し、Deaths だけを増やす。
        /// </summary>
        private void KillDowned(int victimId, int? creditId, int fallbackKillerId)
        {
            _downed.Remove(victimId);
            if (creditId.HasValue)
            {
                ReportKilled(creditId.Value, victimId, scores: true);
            }
            else
            {
                ReportKilled(fallbackKillerId, victimId, scores: false);
            }

            if (_health.ContainsKey(victimId))
            {
                DownedChanged?.Invoke(victimId);
            }
        }

        // 味方相手の ReportKill も加点はしないが、倒した側の記録に触れないことを TeamDeathmatch の判定に頼らず
        // ここで決めるため、味方に倒されたときは scores を false にして ReportDeath にする。
        private void ReportKilled(int killerId, int victimId, bool scores)
        {
            var alphaBefore = _match.GetScore(Team.Alpha);
            var bravoBefore = _match.GetScore(Team.Bravo);
            if (scores)
            {
                _match.ReportKill(killerId, victimId);
            }
            else
            {
                _match.ReportDeath(victimId);
            }

            Killed?.Invoke(killerId, victimId);
            PlayerChanged?.Invoke(victimId);
            if (killerId != victimId && scores)
            {
                PlayerChanged?.Invoke(killerId);
            }

            var alpha = _match.GetScore(Team.Alpha);
            var bravo = _match.GetScore(Team.Bravo);
            if (alpha != alphaBefore || bravo != bravoBefore)
            {
                ScoreChanged?.Invoke(alpha, bravo);
            }
        }

        /// <summary>
        /// 試合中、1 人以上いて動ける人のいないチームがあれば、そのチームのダウン中の人を倒す。
        /// キルは出血で倒れたときと同じ付け方にする。
        /// </summary>
        private void KillWipedOutTeams()
        {
            if (_downed == null || _downed.Count == 0 || State != GameModeState.Playing)
            {
                return;
            }

            _busy++;
            try
            {
                foreach (var team in _teams)
                {
                    if (HasStanding(team))
                    {
                        continue;
                    }

                    foreach (var playerId in _downed.GetDownedIds())
                    {
                        // キルで点数が上限に届いて終わったら、残りは倒さない
                        if (State != GameModeState.Playing)
                        {
                            return;
                        }

                        // 受け手が抜けさせた人は飛ばす
                        if (_downed.IsDowned(playerId) && _health.ContainsKey(playerId) && GetTeam(playerId) == team)
                        {
                            KillByBleedout(playerId);
                        }
                    }
                }
            }
            finally
            {
                _busy--;
                FlushDeferred();
            }
        }

        private void KillByBleedout(int playerId)
        {
            KillDowned(playerId, _downed.GetBleedoutCreditId(playerId), _downed.GetDownerId(playerId) ?? playerId);
        }

        /// <summary>
        /// reviverId が、ダウン中の味方 targetId を deltaSeconds だけ起こす。起こす人が押し続けている間、
        /// <see cref="Tick"/> のたびに 1 度呼ぶ。前の <see cref="Tick"/> から 1 度も呼ばれなかったら、その Tick で累計を 0 に戻す。
        /// 累計が <see cref="ActionRules.ReviveSeconds"/> に達したら、体力を最大値の <see cref="ActionRules.DownedHealthFraction"/>
        /// (端数は切り上げ) にして起き上がらせ、<see cref="Revived"/> を出す。距離は呼ぶ側が確かめる。
        /// </summary>
        /// <remarks>
        /// 次のときは何もせず false を返す。<see cref="ActionRules.Downed"/> が Off、試合中でない、targetId がダウンしていない、
        /// reviverId と targetId が同じか別のチーム、reviverId が倒れているかダウン中、
        /// 同じ Tick の間に別の人がすでに targetId を起こしている。
        /// </remarks>
        /// <param name="reviverId">起こす人。</param>
        /// <param name="targetId">起こされる人。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        /// <returns>累計を進めたら (起き上がったときも) true。</returns>
        public bool TryRevive(int reviverId, int targetId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            var reviver = FindHealth(reviverId, nameof(reviverId));
            FindHealth(targetId, nameof(targetId));

            // ダウン中の人も Health は 0 なので、reviver.IsDead で倒れている人とダウン中の人を両方はじける
            if (_downed == null
                || State != GameModeState.Playing
                || reviverId == targetId
                || !_downed.IsDowned(targetId)
                || reviver.IsDead
                || GetTeam(reviverId) != GetTeam(targetId))
            {
                return false;
            }

            var step = _downed.AddReviveProgress(targetId, reviverId, deltaSeconds);
            if (step != ReviveStep.Completed)
            {
                return step == ReviveStep.Progressed;
            }

            _downed.Remove(targetId);
            SetRevivedHealth(targetId, _downed.DownedHealth);
            Revived?.Invoke(reviverId, targetId);
            PlayerChanged?.Invoke(targetId);
            DownedChanged?.Invoke(targetId);
            return true;
        }

        /// <summary>
        /// 時間を進める。試合中なら先に各プレイヤーの体力を自動回復させ (<see cref="GameRules.RegenPerSecond"/> が
        /// 0 なら何もしない)、体力が変わった人ごとに <see cref="PlayerChanged"/> を出す。次に、前の Tick から
        /// <see cref="TryRevive"/> されなかったダウン中の人の蘇生の累計を 0 に戻し、出血を進めて、秒数が過ぎた人を倒す。続けて
        /// リスポーン待ちが終わった人を出現させ、時間切れなら試合を終える。
        /// 終わった後は次の試合までの待ちを進め、待ち終わったら <see cref="Restart"/> する
        /// (試合が終わった回の Tick の残りは待ちに数えない)。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            var waitingForRestart = _restartPending && State == GameModeState.Ended;

            // _match.Tick より先に回すのは、この Tick で時間切れになっても終わるまでの分は回復させるため
            if (State == GameModeState.Playing)
            {
                var playingSeconds = Math.Min(deltaSeconds, _match.RemainingSeconds);
                TickRegeneration(playingSeconds);
                TickDowned(playingSeconds);
            }

            // 終わった後は時間を進めないので何も起きない
            _match.Tick(deltaSeconds);

            if (!waitingForRestart)
            {
                return;
            }

            _restartRemaining -= deltaSeconds;
            if (_restartRemaining <= 0f)
            {
                Restart();
            }
        }

        /// <summary>
        /// 終わった試合を同じ顔ぶれ・同じチーム分けで始め直す。チームの点数、各プレイヤーのキル数とデス数、
        /// 残り時間を初期値に戻し、全員を体力満タンにしてダウンを消し、自分のチームの地点へ出現させる (<see cref="Spawned"/>)。
        /// ダウンしていた人には、<see cref="PlayerChanged"/> を出し終えた後に <see cref="DownedChanged"/> を出す。
        /// 両チームに 1 人以上いればそのまま始まり、いなければ Waiting に戻って参加を待つ。
        /// </summary>
        /// <exception cref="InvalidOperationException">試合がまだ終わっていない。</exception>
        public void Restart()
        {
            if (State != GameModeState.Ended)
            {
                throw new InvalidOperationException("the match can only be restarted after it has ended, but is " + State);
            }

            _restartPending = false;
            _restartRemaining = 0f;
            _match.Reset();

            var playerIds = new List<int>(_health.Keys);
            List<int> wasDowned = null;
            foreach (var playerId in playerIds)
            {
                if (ResetHealth(playerId))
                {
                    (wasDowned ??= new List<int>()).Add(playerId);
                }
            }

            Restarted?.Invoke();
            ScoreChanged?.Invoke(0, 0);

            // 受け手の中で抜けた人は飛ばす
            foreach (var playerId in playerIds)
            {
                if (_health.ContainsKey(playerId))
                {
                    PlayerChanged?.Invoke(playerId);
                }
            }

            if (wasDowned != null)
            {
                foreach (var playerId in wasDowned)
                {
                    if (_health.ContainsKey(playerId))
                    {
                        DownedChanged?.Invoke(playerId);
                    }
                }
            }

            foreach (var playerId in playerIds)
            {
                if (_health.ContainsKey(playerId))
                {
                    Spawned?.Invoke(playerId, SelectSpawn(playerId));
                }
            }

            TryStart();
        }

        /// <summary>
        /// playerId が出現する地点を選ぶ。動ける (倒れていなくてダウンもしていない) 他のプレイヤーを敵・味方に分けて
        /// <see cref="SpawnSelector"/> に渡す。
        /// </summary>
        public SpawnPoint SelectSpawn(int playerId)
        {
            var team = GetTeam(playerId);
            var enemies = new List<Vector3>();
            var allies = new List<Vector3>();
            foreach (var pair in _health)
            {
                if (pair.Key == playerId || pair.Value.IsDead)
                {
                    continue;
                }

                var position = _positionOf(pair.Key);
                if (GetTeam(pair.Key) == team)
                {
                    allies.Add(position);
                }
                else
                {
                    enemies.Add(position);
                }
            }

            return _selector.Select(_spawnPoints, enemies, allies, team);
        }

        /// <summary>
        /// playerId から最も近い、動ける (倒れていなくてダウンもしていない) 敵の id。同じ距離なら id の小さいほう。
        /// そういう敵がいないとき、試合中でないとき、playerId 自身が倒れているかダウン中のときは null。
        /// </summary>
        public int? FindNearestEnemy(int playerId)
        {
            var self = FindHealth(playerId);
            if (State != GameModeState.Playing || self.IsDead)
            {
                return null;
            }

            var team = GetTeam(playerId);
            var from = _positionOf(playerId);
            int? nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var pair in _health)
            {
                if (pair.Value.IsDead || GetTeam(pair.Key) == team)
                {
                    continue;
                }

                var distance = Vector3.DistanceSquared(from, _positionOf(pair.Key));
                if (nearest == null || distance < nearestDistance)
                {
                    nearest = pair.Key;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void TryStart()
        {
            if (State != GameModeState.Waiting)
            {
                return;
            }

            var hasAlpha = false;
            var hasBravo = false;
            foreach (var playerId in _health.Keys)
            {
                if (GetTeam(playerId) == Team.Alpha)
                {
                    hasAlpha = true;
                }
                else
                {
                    hasBravo = true;
                }
            }

            if (hasAlpha && hasBravo)
            {
                _match.Start();
                Started?.Invoke();
            }
        }

        private void Respawn(int playerId)
        {
            // 待っている間に抜けていたら何もしない。
            if (!_health.ContainsKey(playerId))
            {
                return;
            }

            var wasDowned = ResetHealth(playerId);
            PlayerChanged?.Invoke(playerId);
            if (wasDowned)
            {
                DownedChanged?.Invoke(playerId);
            }

            Spawned?.Invoke(playerId, SelectSpawn(playerId));
        }

        /// <summary>体力を満タンにし、ダウンを消す。ダウンしていたら true。</summary>
        private bool ResetHealth(int playerId)
        {
            var health = new Health(MaxHealth);
            _health[playerId] = health;
            _regenerators[playerId] = new HealthRegenerator(health, Spec.Rules);
            return _downed != null && _downed.Remove(playerId);
        }

        // 起き上がった直後から回復させると、起こしてもらうだけで体力が満タンへ向かってしまうので、被弾した直後と同じ待ちから始める
        private void SetRevivedHealth(int playerId, int current)
        {
            var health = new Health(MaxHealth);
            health.TakeDamage(MaxHealth - current);
            var regenerator = new HealthRegenerator(health, Spec.Rules);
            regenerator.NotifyDamaged();
            _health[playerId] = health;
            _regenerators[playerId] = regenerator;
        }

        private void TickDowned(float deltaSeconds)
        {
            if (_downed == null || _downed.Count == 0)
            {
                return;
            }

            var bledOut = _downed.Tick(deltaSeconds);
            if (bledOut.Count == 0)
            {
                return;
            }

            _busy++;
            try
            {
                foreach (var playerId in bledOut)
                {
                    if (State != GameModeState.Playing)
                    {
                        return;
                    }

                    if (_downed.IsDowned(playerId) && _health.ContainsKey(playerId))
                    {
                        KillByBleedout(playerId);
                    }
                }
            }
            finally
            {
                _busy--;
                FlushDeferred();
            }
        }

        private bool IsAliveWith(int playerId, Health health) => !health.IsDead || IsDownedUnchecked(playerId);

        private bool IsDownedUnchecked(int playerId) => _downed != null && _downed.IsDowned(playerId);

        // ダウン中の人も Health は 0 のままなので、IsDead でないことが「動ける」ことになる
        private bool HasStanding(Team team)
        {
            foreach (var pair in _health)
            {
                if (!pair.Value.IsDead && GetTeam(pair.Key) == team)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasOtherStandingAlly(int playerId)
        {
            var team = GetTeam(playerId);
            foreach (var pair in _health)
            {
                if (pair.Key != playerId && !pair.Value.IsDead && GetTeam(pair.Key) == team)
                {
                    return true;
                }
            }

            return false;
        }

        private void TickRegeneration(float deltaSeconds)
        {
            if (Spec.Rules.RegenPerSecond == 0f)
            {
                return;
            }

            // 受け手が抜けたりダメージを与えたりしても回している最中の辞書を崩さないよう、知らせるのは回し終えてから
            List<int> changed = null;
            foreach (var playerId in _health.Keys)
            {
                if (_regenerators[playerId].Tick(deltaSeconds) > 0)
                {
                    (changed ??= new List<int>()).Add(playerId);
                }
            }

            if (changed == null)
            {
                return;
            }

            foreach (var playerId in changed)
            {
                if (_health.ContainsKey(playerId))
                {
                    PlayerChanged?.Invoke(playerId);
                }
            }
        }

        /// <summary>ApplyDamage の途中ならそれが終わるまで遅らせ、そうでなければすぐに実行する。</summary>
        private void Defer(Action action)
        {
            if (_busy > 0)
            {
                _deferred.Enqueue(action);
                return;
            }

            action();
        }

        private void FlushDeferred()
        {
            while (_busy == 0 && _deferred.Count > 0)
            {
                _deferred.Dequeue()();
            }
        }

        private static void RequireSpawnPointFor(SpawnPoint[] spawnPoints, Team team, string paramName)
        {
            foreach (var point in spawnPoints)
            {
                if (point.IsUsableBy(team))
                {
                    return;
                }
            }

            throw new ArgumentException($"no spawn point is usable by team {team}", paramName);
        }

        private Health FindHealth(int playerId, string paramName = "playerId")
        {
            if (!_health.TryGetValue(playerId, out var health))
            {
                throw new ArgumentOutOfRangeException(paramName, playerId, "no player with this id");
            }

            return health;
        }
    }
}
