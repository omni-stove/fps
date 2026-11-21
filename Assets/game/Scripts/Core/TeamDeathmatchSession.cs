using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // イベントはいつも「倒した → 点数 → リスポーン/終了」の順に出す (RespawnSeconds が 0 でも倒したことより先にリスポーンを出さない)。
    public sealed class TeamDeathmatchSession
    {
        public event Action Started;

        public event Action Restarted;

        public event Action<int, int> ScoreChanged;

        public event Action<int> PlayerChanged;

        public event Action<int, int, IReadOnlyList<int>> Killed;

        public event Action<int, SpawnPoint> Spawned;

        // 倒されて戻る時が来て出直すときだけ、その人の Spawned の直前に出す。入った時・試合の頭・ラウンドの頭の並べ直し・外が決める出直し
        // (RespawnPlayer) と見分けて、出す所をビーコンのような復活の場所 (GameRules.RespawnAnchors) に替えられるようにするため
        public event Action<int> Respawning;

        public event Action<Team?> Ended;

        public event Action<int, int> Downed;

        public event Action<int, int> Revived;

        public event Action<int> DownedChanged;

        public event Action<HeldPlayer> HeldExpired;

        public const float DefaultReconnectGraceSeconds = 60f;

        private static readonly int[] _noAssisters = Array.Empty<int>();

        private readonly TeamDeathmatch _match;
        private readonly DamageLedger _ledger;

        // DamageLedger に渡す時刻。Tick で進めた秒数を試合の状態によらず足す。
        private double _clock;
        private readonly SpawnSelector _selector;
        private SpawnPoint[] _spawnPoints;
        private readonly Func<int, Vector3> _positionOf;
        private readonly Func<int, int, bool> _areAllies;

        // playerId の昇順に回して、同じ入力なら同じ結果になるようにする。
        private readonly SortedDictionary<int, Health> _health = new SortedDictionary<int, Health>();

        // _health と同じ顔ぶれを持つ。Health を作り直すたびに作り直す。
        private readonly Dictionary<int, HealthRegenerator> _regenerators = new Dictionary<int, HealthRegenerator>();

        // SetMaxHealth で MaxHealth から替えた人だけを持つ
        private readonly Dictionary<int, int> _maxHealthOf = new Dictionary<int, int>();
        private readonly DownedTracker _downed;

        private readonly Dictionary<int, float> _spawnProtection = new Dictionary<int, float>();
        private readonly List<int> _protectedIds = new List<int>();

        private readonly Dictionary<int, string> _keyByPlayer = new Dictionary<int, string>();
        private readonly Dictionary<string, int> _playerByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Dictionary<string, HeldPlayer> _held = new Dictionary<string, HeldPlayer>(StringComparer.Ordinal);
        private readonly List<string> _heldKeys = new List<string>();

        private readonly Queue<Action> _deferred = new Queue<Action>();
        private int _busy;

        private readonly float? _restartDelaySeconds;
        private float _restartRemaining;
        private bool _restartPending;

        public TeamDeathmatchSpec Spec => _match.Spec;

        public float? RestartDelaySeconds => _restartDelaySeconds;

        public bool IsRestartPending => _restartPending;

        public float RestartRemainingSeconds => _restartPending ? Math.Max(0f, _restartRemaining) : 0f;

        public int MaxHealth { get; }

        public AssistSpec Assist => _ledger.Spec;

        public GameModeState State => _match.State;
        public float RemainingSeconds => _match.RemainingSeconds;
        public Team? Winner => _match.Winner;
        public int PlayerCount => _health.Count;

        public float ReconnectGraceSeconds { get; }

        public int HeldCount => _held.Count;

        public Team NextTeam => TeamAssigner.Choose(CountPlayers(Team.Alpha), CountPlayers(Team.Bravo), GetScore(Team.Alpha), GetScore(Team.Bravo));

        public TeamDeathmatchSession(
            TeamDeathmatchSpec spec,
            int maxHealth,
            IReadOnlyList<SpawnPoint> spawnPoints,
            SpawnSelector selector,
            Func<int, Vector3> positionOf,
            float? restartDelaySeconds = null,
            float reconnectGraceSeconds = DefaultReconnectGraceSeconds,
            AssistSpec assist = null,
            Func<int, int, bool> areAllies = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(reconnectGraceSeconds >= 0f) || float.IsInfinity(reconnectGraceSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reconnectGraceSeconds), reconnectGraceSeconds, "reconnectGraceSeconds must not be negative and must be finite");
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

            _spawnPoints = CopySpawnPoints(spawnPoints, nameof(spawnPoints));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _positionOf = positionOf ?? throw new ArgumentNullException(nameof(positionOf));
            _areAllies = areAllies;
            MaxHealth = maxHealth;
            _ledger = new DamageLedger(assist ?? AssistSpec.Default);
            _restartDelaySeconds = restartDelaySeconds;
            ReconnectGraceSeconds = reconnectGraceSeconds;

            var actions = spec.Rules.Actions;
            if (actions.Downed == Shooter.Core.Downed.Revive)
            {
                _downed = new DownedTracker(
                    DownedTracker.HealthFor(maxHealth, actions.DownedHealthFraction),
                    actions.DownedBleedoutSeconds,
                    actions.ReviveSeconds);
            }

            _match = new TeamDeathmatch(spec);
            _match.RespawnReady += playerId => Defer(() => Respawn(playerId, true));
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

        // 2 チームより細かい組 (Battle Royale の分隊) で戦う試合が、チームの数を増やさずにその組で味方撃ち・ダウン・蘇生・出現の選び方を
        // 決められるよう、areAllies を外から渡せるようにする。渡さなければチームが同じかで決める
        public bool AreAllies(int a, int b)
        {
            FindHealth(a, nameof(a));
            FindHealth(b, nameof(b));
            return a == b || (_areAllies != null ? _areAllies(a, b) : GetTeam(a) == GetTeam(b));
        }

        public int GetKills(int playerId) => _match.GetKills(playerId);

        public int GetDeaths(int playerId) => _match.GetDeaths(playerId);

        public int GetAssists(int playerId) => _match.GetAssists(playerId);

        public int GetScore(Team team) => _match.GetScore(team);

        public int GetHealth(int playerId)
        {
            var health = FindHealth(playerId);
            return IsDownedUnchecked(playerId) ? _downed.GetHealth(playerId) : health.Current;
        }

        public int GetMaxHealth(int playerId)
        {
            FindHealth(playerId);
            return _maxHealthOf.TryGetValue(playerId, out var max) ? max : MaxHealth;
        }

        // 陣営で体の違う試合 (感染者など) が、1 人だけ体力の上限を替えるための口。今の体力は値のまま残し、新しい上限を超える分だけ削る。
        // 満たすかは呼ぶ側が Heal で決める (倒れている人・ダウン中の人は Heal できないので、次の出現か起き上がりで新しい上限になる)。
        // 替えた上限は出直しても残り、抜けたときと次の試合 (Restart) で MaxHealth に戻る
        public bool SetMaxHealth(int playerId, int maxHealth)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            var health = FindHealth(playerId);
            if (health.Max == maxHealth)
            {
                return false;
            }

            if (maxHealth == MaxHealth)
            {
                _maxHealthOf.Remove(playerId);
            }
            else
            {
                _maxHealthOf[playerId] = maxHealth;
            }

            var current = Math.Min(health.Current, maxHealth);
            var resized = new Health(maxHealth);
            resized.TakeDamage(maxHealth - current);
            var regenerator = new HealthRegenerator(resized, Spec.Rules);
            regenerator.ContinueFrom(_regenerators[playerId]);
            _health[playerId] = resized;
            _regenerators[playerId] = regenerator;
            if (current != health.Current)
            {
                PlayerChanged?.Invoke(playerId);
            }

            return true;
        }

        public int Heal(int playerId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var health = FindHealth(playerId);
            if (health.IsDead || IsDownedUnchecked(playerId))
            {
                return 0;
            }

            var before = health.Current;
            health.Heal(amount);
            var healed = health.Current - before;
            if (healed > 0)
            {
                PlayerChanged?.Invoke(playerId);
            }

            return healed;
        }

        public bool IsAlive(int playerId) => IsAliveWith(playerId, FindHealth(playerId));

        public bool IsDowned(int playerId)
        {
            FindHealth(playerId);
            return IsDownedUnchecked(playerId);
        }

        public float? GetDownedRemaining(int playerId)
        {
            FindHealth(playerId);
            return _downed?.GetBleedoutRemaining(playerId);
        }

        public float GetReviveProgress(int playerId)
        {
            FindHealth(playerId);
            return _downed?.GetReviveProgress(playerId) ?? 0f;
        }

        public float GetSpawnProtectionRemaining(int playerId)
        {
            FindHealth(playerId);
            return _spawnProtection.TryGetValue(playerId, out var remaining) ? remaining : 0f;
        }

        public bool IsSpawnProtected(int playerId)
        {
            FindHealth(playerId);
            return _spawnProtection.ContainsKey(playerId);
        }

        public bool EndSpawnProtection(int playerId)
        {
            FindHealth(playerId);
            return _spawnProtection.Remove(playerId);
        }

        public bool IsEliminated(int playerId)
        {
            FindHealth(playerId);
            return _match.IsEliminated(playerId);
        }

        public float? GetRespawnRemaining(int playerId)
        {
            FindHealth(playerId);
            return _match.GetRespawnRemaining(playerId);
        }

        public float? SecondsUntilNextWave => _match.SecondsUntilNextWave;

        public IReadOnlyList<int> GetPlayerIds()
        {
            return new List<int>(_health.Keys);
        }

        public Team Join(int playerId, string key = null)
        {
            return Join(playerId, key, null);
        }

        // 専用サーバーが試合の枠でパーティを見て決めたチームと、NextTeam で決めるチームが食い違わないよう、チームを渡せる形にしている。
        public Team Join(int playerId, string key, Team? team)
        {
            if (team.HasValue && !Enum.IsDefined(typeof(Team), team.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "team must be a Team or null");
            }

            if (_health.ContainsKey(playerId))
            {
                throw new ArgumentException($"player {playerId} has already joined", nameof(playerId));
            }

            if (key != null && _playerByKey.TryGetValue(key, out var owner))
            {
                throw new ArgumentException($"key is already used by player {owner}", nameof(key));
            }

            Team joined;
            if (key != null && _held.TryGetValue(key, out var held))
            {
                joined = held.Team;
                _match.AddPlayer(playerId, joined, held.Kills, held.Deaths, held.Assists);
                _held.Remove(key);
            }
            else
            {
                joined = team ?? NextTeam;
                _match.AddPlayer(playerId, joined);
            }

            if (key != null)
            {
                _keyByPlayer[playerId] = key;
                _playerByKey[key] = playerId;
            }

            ResetHealth(playerId);

            PlayerChanged?.Invoke(playerId);
            Spawned?.Invoke(playerId, SelectSpawn(playerId));
            TryStart();
            return joined;
        }

        public bool Leave(int playerId)
        {
            FindHealth(playerId);
            var held = false;
            if (_keyByPlayer.TryGetValue(playerId, out var key))
            {
                _keyByPlayer.Remove(playerId);
                _playerByKey.Remove(key);
                if (ReconnectGraceSeconds > 0f)
                {
                    _held[key] = new HeldPlayer(
                        playerId, GetTeam(playerId), GetKills(playerId), GetDeaths(playerId), GetAssists(playerId), ReconnectGraceSeconds);
                    held = true;
                }
            }

            _match.RemovePlayer(playerId);
            _health.Remove(playerId);
            _regenerators.Remove(playerId);
            _maxHealthOf.Remove(playerId);
            _spawnProtection.Remove(playerId);
            _ledger.Clear(playerId);
            if (_downed != null)
            {
                _downed.RemovePlayer(playerId);
                KillWipedOutTeams();
            }

            return held;
        }

        public bool ChangeTeam(int playerId, Team team)
        {
            FindHealth(playerId);
            Side.FromTeam(team);

            // 試合中に移すと、その人のキルが入った点数と移った先のチームが食い違うので、終わった後だけにする
            if (State != GameModeState.Ended)
            {
                throw new InvalidOperationException("a player can only change teams after the match has ended, but it is " + State);
            }

            if (GetTeam(playerId) == team)
            {
                return false;
            }

            var kills = GetKills(playerId);
            var deaths = GetDeaths(playerId);
            _match.RemovePlayer(playerId);
            _match.AddPlayer(playerId, team, kills, deaths);
            return true;
        }

        // 前の接続が切れたと分かる前に同じ人がつなぎ直してきたことを見分けるために置く。
        public bool IsKeyInUse(string key) => key != null && _playerByKey.ContainsKey(key);

        public HeldPlayer? FindHeld(string key)
        {
            return key != null && _held.TryGetValue(key, out var held) ? held : (HeldPlayer?)null;
        }

        // 跳ね返しでは返す値を attackerId に起きたことにする。体力が変わったのは撃った本人なので、
        // 結果を見て体力の同期やヒットの知らせを出す呼び出し側が、実際に体力の変わっていない味方に Damaged / Killed を結び付けないようにするため。
        // 撃った人の無敵はここでは解かない (外れた射撃でも解くので、呼び出し側が EndSpawnProtection を呼ぶ)。
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

            if (attackerId == victimId || !AreAllies(attackerId, victimId))
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
            if (_spawnProtection.ContainsKey(victimId))
            {
                return DamageOutcome.Ignored;
            }

            _ledger.Record(victimId, attackerId, amount, byAlly, _clock);
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
                if (_downed != null && attackerId != victimId && (Spec.DownsLastStanding || HasOtherStandingAlly(victimId)))
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

            var assisters = scores && killerId != victimId ? PresentPlayers(_ledger.TakeAssisters(victimId, killerId, _clock)) : _noAssisters;
            if (scores)
            {
                _match.ReportKill(killerId, victimId, assisters);
            }
            else
            {
                _match.ReportDeath(victimId);
            }

            Killed?.Invoke(killerId, victimId, assisters);
            PlayerChanged?.Invoke(victimId);
            if (killerId != victimId && scores)
            {
                PlayerChanged?.Invoke(killerId);
            }

            foreach (var assisterId in assisters)
            {
                // Killed や PlayerChanged の受け手が抜けさせた人は飛ばす
                if (_health.ContainsKey(assisterId))
                {
                    PlayerChanged?.Invoke(assisterId);
                }
            }

            var alpha = _match.GetScore(Team.Alpha);
            var bravo = _match.GetScore(Team.Bravo);
            if (alpha != alphaBefore || bravo != bravoBefore)
            {
                ScoreChanged?.Invoke(alpha, bravo);
            }
        }

        private int[] PresentPlayers(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0)
            {
                return _noAssisters;
            }

            var present = new List<int>(ids.Count);
            foreach (var id in ids)
            {
                if (_health.ContainsKey(id))
                {
                    present.Add(id);
                }
            }

            return present.Count == 0 ? _noAssisters : present.ToArray();
        }

        private void KillWipedOutTeams()
        {
            if (_downed == null || _downed.Count == 0 || State != GameModeState.Playing || Spec.DownsLastStanding)
            {
                return;
            }

            _busy++;
            try
            {
                // 味方の組はチームとは限らない (AreAllies) ので、チームごとでなくダウン中の人ごとに、起こせる味方が立っているかを見る
                foreach (var playerId in _downed.GetDownedIds())
                {
                    // キルで点数が上限に届いて終わったら、残りは倒さない
                    if (State != GameModeState.Playing)
                    {
                        return;
                    }

                    // 受け手が抜けさせた人は飛ばす
                    if (_downed.IsDowned(playerId) && _health.ContainsKey(playerId) && !HasOtherStandingAlly(playerId))
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

        private void KillByBleedout(int playerId)
        {
            KillDowned(playerId, _downed.GetBleedoutCreditId(playerId), _downed.GetDownerId(playerId) ?? playerId);
        }

        // 位置を持たないので、距離は呼ぶ側 (ReviveReach) が確かめる。
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
                || !AreAllies(reviverId, targetId))
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

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            var waitingForRestart = _restartPending && State == GameModeState.Ended;

            // 無限の秒数は _match.Tick が断るので、ここで時計を壊して DamageLedger に渡さないよう足さずにおく。
            if (!float.IsInfinity(deltaSeconds))
            {
                _clock += deltaSeconds;
            }

            TickHeld(deltaSeconds);

            // この Tick の中で出現した人 (リスポーン・Restart) の無敵を減らさないよう、_match.Tick より先に回す
            TickSpawnProtection(deltaSeconds);

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

        public bool Finish(Team? winner) => _match.Finish(winner);

        public void Restart()
        {
            if (State != GameModeState.Ended)
            {
                throw new InvalidOperationException("the match can only be restarted after it has ended, but is " + State);
            }

            _restartPending = false;
            _restartRemaining = 0f;
            _match.Reset();
            _ledger.Reset();

            // 前の試合の成績を次の試合へ持ち込ませない
            _heldKeys.Clear();
            _heldKeys.AddRange(_held.Keys);
            foreach (var key in _heldKeys)
            {
                _held[key] = _held[key].WithoutStats();
            }

            // 前の試合で替えた上限 (感染者など) は、その試合の陣営の体なので次の試合へ持ち込まない
            _maxHealthOf.Clear();

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

        // Restart と違い、点数・キル数・残り時間を戻さずに全員の体力と位置だけを戻す (ラウンド制のラウンドの頭)。
        public void RespawnAll()
        {
            var playerIds = new List<int>(_health.Keys);
            foreach (var playerId in playerIds)
            {
                // 受け手の中で抜けた人は飛ばす
                if (_health.ContainsKey(playerId))
                {
                    Respawn(playerId, false);
                }
            }
        }

        // RespawnAll と同じく点数・キル数・残り時間を戻さずに、倒れている 1 人だけを出直させる。出直す時を外が決める試合
        // (RespawnsByRound で Session が自分では出直させない Battle Royale の復帰) のための口。立っている人・ダウン中の人と試合の外では何もせず false
        public bool RespawnPlayer(int playerId)
        {
            var health = FindHealth(playerId);
            if (State != GameModeState.Playing || IsAliveWith(playerId, health))
            {
                return false;
            }

            Defer(() => Respawn(playerId, false));
            return true;
        }

        // Session を作り直さないのは、成績・切断した人の保持・チーム分けを捨てずに出現地点だけを替えるため。
        public void ReplaceSpawnPoints(IReadOnlyList<SpawnPoint> spawnPoints)
        {
            _spawnPoints = CopySpawnPoints(spawnPoints, nameof(spawnPoints));
        }

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
                if (AreAllies(pair.Key, playerId))
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

        public int? FindNearestEnemy(int playerId)
        {
            var self = FindHealth(playerId);
            if (State != GameModeState.Playing || self.IsDead)
            {
                return null;
            }

            var from = _positionOf(playerId);
            int? nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var pair in _health)
            {
                if (pair.Value.IsDead || AreAllies(pair.Key, playerId))
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

        private void Respawn(int playerId, bool afterDeath)
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

            if (afterDeath)
            {
                Respawning?.Invoke(playerId);
            }

            Spawned?.Invoke(playerId, SelectSpawn(playerId));
        }

        private bool ResetHealth(int playerId)
        {
            var health = new Health(_maxHealthOf.TryGetValue(playerId, out var max) ? max : MaxHealth);
            _health[playerId] = health;
            _ledger.Clear(playerId);
            _regenerators[playerId] = new HealthRegenerator(health, Spec.Rules);

            var protection = Spec.Rules.SpawnProtectionSeconds;
            if (protection > 0f)
            {
                _spawnProtection[playerId] = protection;
            }
            else
            {
                _spawnProtection.Remove(playerId);
            }

            return _downed != null && _downed.Remove(playerId);
        }

        // 試合の状態によらず減らす。終わった後の次の試合までの待ちも、戻ってくる人にとっては同じ時間なので数える
        private void TickHeld(float deltaSeconds)
        {
            if (_held.Count == 0)
            {
                return;
            }

            // 受け手が参加させても回している最中の辞書を崩さないよう、捨てる分を先に決めてから知らせる
            _heldKeys.Clear();
            _heldKeys.AddRange(_held.Keys);
            List<HeldPlayer> expired = null;
            foreach (var key in _heldKeys)
            {
                var held = _held[key];
                var remaining = held.RemainingSeconds - deltaSeconds;
                if (remaining > 0f)
                {
                    _held[key] = held.WithRemaining(remaining);
                    continue;
                }

                _held.Remove(key);
                (expired ??= new List<HeldPlayer>()).Add(held.WithRemaining(0f));
            }

            if (expired == null)
            {
                return;
            }

            foreach (var held in expired)
            {
                HeldExpired?.Invoke(held);
            }
        }

        private int CountPlayers(Team team)
        {
            var count = 0;
            foreach (var playerId in _health.Keys)
            {
                if (GetTeam(playerId) == team)
                {
                    count++;
                }
            }

            return count;
        }

        // 試合の状態によらず減らす。待っている間と終わった後はそもそもダメージが通らないので、出現からの実時間で切れればよい
        private void TickSpawnProtection(float deltaSeconds)
        {
            if (_spawnProtection.Count == 0)
            {
                return;
            }

            // 回している最中の辞書は書き換えられないので、キーを写してから回す
            _protectedIds.Clear();
            _protectedIds.AddRange(_spawnProtection.Keys);
            foreach (var playerId in _protectedIds)
            {
                var remaining = _spawnProtection[playerId] - deltaSeconds;
                if (remaining > 0f)
                {
                    _spawnProtection[playerId] = remaining;
                }
                else
                {
                    _spawnProtection.Remove(playerId);
                }
            }
        }

        // 起き上がった直後から回復させると、起こしてもらうだけで体力が満タンへ向かってしまうので、被弾した直後と同じ待ちから始める
        private void SetRevivedHealth(int playerId, int current)
        {
            var max = _maxHealthOf.TryGetValue(playerId, out var changed) ? changed : MaxHealth;
            var health = new Health(max);
            health.TakeDamage(Math.Max(0, max - current));
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
        private bool HasOtherStandingAlly(int playerId)
        {
            foreach (var pair in _health)
            {
                if (pair.Key != playerId && !pair.Value.IsDead && AreAllies(pair.Key, playerId))
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

        private static SpawnPoint[] CopySpawnPoints(IReadOnlyList<SpawnPoint> spawnPoints, string paramName)
        {
            if (spawnPoints == null)
            {
                throw new ArgumentNullException(paramName);
            }

            var copy = new SpawnPoint[spawnPoints.Count];
            for (var i = 0; i < spawnPoints.Count; i++)
            {
                copy[i] = spawnPoints[i] ?? throw new ArgumentException("spawnPoints must not contain null", paramName);
            }

            RequireSpawnPointFor(copy, Team.Alpha, paramName);
            RequireSpawnPointFor(copy, Team.Bravo, paramName);
            return copy;
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
