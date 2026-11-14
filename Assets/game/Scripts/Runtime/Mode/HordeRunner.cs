using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ホード (<see cref="Fps.Core.Horde"/>) をシーンで進める <see cref="ModeRunnerBase"/>。プレイヤーと味方の bot (<see cref="PlayerSide"/>) が、
    /// <see cref="WaveSpawner"/> の出す波の敵 (<see cref="EnemySide"/>) を耐える。波の敵はモードの参加者にしない。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>試合が始まったら (Playing になったら) <see cref="Spawner"/> に波の予定を渡して 1 波目を始める。Spawner の BeginOnStart は切っておく
    /// (自分で波を始めていたら理由をログに出して止まる)。Spawner の敵の陣営は <see cref="EnemySide"/> と同じであること。</item>
    /// <item>Spawner が出した敵が倒されたら通貨を入れる (<see cref="RewardEnemyKill"/>)。倒されずに消えた敵では入らない。</item>
    /// <item>最後の波を全滅させたら勝ち、全員が倒れたら負け。倒れた味方は波の間に戻る。終わったら Spawner を止め、残った敵の動きも止める。</item>
    /// <item>操作しているプレイヤーが防衛物 (<see cref="HordeFortification"/>) の近くで <see cref="TryInteract()"/> すると (入力は
    /// <see cref="HordeInteractInput"/>)、通貨を払って壊れた部品を直すか、まだ置かれていない防衛物を置く。</item>
    /// <item>HUD には経過時間・倒した敵の数 (得点) の下に、波の番号・残りの敵か次の波までの秒数・通貨・近くの防衛物の案内を出す。</item>
    /// <item>波の始まりと全滅・通貨が入ったとき・直した・置いた・断られたとき・勝ち負けに <c>[Mode]</c> のログ (<see cref="HordeText"/>) を出す。</item>
    /// </list>
    /// </remarks>
    public sealed class HordeRunner : ModeRunnerBase
    {
        public const int DefaultEnemySideId = WaveSpawner.DefaultEnemySideId;

        [SerializeField, Tooltip("波の敵を出す WaveSpawner。シーン生成スクリプトが配線する。BeginOnStart は切っておく")]
        private WaveSpawner _spawner;

        [SerializeField, Tooltip("通貨で直す・置く防衛物。空ならシーンの HordeFortification をすべて使う")]
        private HordeFortification[] _fortifications = Array.Empty<HordeFortification>();

        [SerializeField, Min(0), Tooltip("波の敵の陣営の ID。WaveSpawner の敵の陣営と合わせる")]
        private int _enemySideId = DefaultEnemySideId;

        [SerializeField, Min(0), Tooltip("始めに持っている通貨")]
        private int _startingCurrency = HordeSpec.DefaultStartingCurrency;

        [SerializeField, Min(0), Tooltip("1 波目で敵を 1 体倒したときに入る通貨")]
        private int _killReward = HordeSpec.DefaultKillReward;

        [SerializeField, Min(0), Tooltip("波が 1 つ進むごとに、1 体あたりの通貨に足す量")]
        private int _killRewardPerWave = HordeSpec.DefaultKillRewardPerWave;

        [SerializeField, Tooltip("防衛物を直す・置くのを波の間だけにするか")]
        private bool _purchasesOnlyBetweenWaves = true;

        private readonly HashSet<Damageable> _enemies = new HashSet<Damageable>();
        private readonly List<HordeFortification> _activeFortifications = new List<HordeFortification>();
        private Horde _horde;
        private WaveSpawner _subscribedSpawner;

        /// <summary>進めているホード。開始前は null。</summary>
        public Horde Horde => _horde;

        /// <summary>波の敵を出す WaveSpawner。始める前に設定する。</summary>
        public WaveSpawner Spawner
        {
            get => _spawner;
            set => _spawner = value;
        }

        /// <summary>波の敵の陣営。</summary>
        public Side EnemySide
        {
            get => new Side(Math.Max(0, _enemySideId));
            set => _enemySideId = value.Id;
        }

        public int StartingCurrency
        {
            get => _startingCurrency;
            set => _startingCurrency = value;
        }

        public int KillReward
        {
            get => _killReward;
            set => _killReward = value;
        }

        public int KillRewardPerWave
        {
            get => _killRewardPerWave;
            set => _killRewardPerWave = value;
        }

        public bool PurchasesOnlyBetweenWaves
        {
            get => _purchasesOnlyBetweenWaves;
            set => _purchasesOnlyBetweenWaves = value;
        }

        /// <summary>HUD の案内に出す、直す・置く操作のキー。空なら出さない。<see cref="HordeInteractInput"/> が入れる。</summary>
        public string InteractKeyLabel { get; set; }

        /// <summary>始めるときに使う防衛物。空なら始めるときにシーンの HordeFortification をすべて使う。</summary>
        public IReadOnlyList<HordeFortification> ConfiguredFortifications => _fortifications ?? Array.Empty<HordeFortification>();

        /// <summary>使っている防衛物。開始前は空。</summary>
        public IReadOnlyList<HordeFortification> Fortifications => _activeFortifications;

        /// <summary>始めるときに使う防衛物を設定する。null か空なら、シーンの HordeFortification をすべて使う。始めた後に変えても効かない。</summary>
        public void SetFortifications(IReadOnlyList<HordeFortification> fortifications)
        {
            var copy = new HordeFortification[fortifications != null ? fortifications.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = fortifications[i];
            }

            _fortifications = copy;
        }

        /// <summary>Inspector の設定から <see cref="HordeSpec"/> を作る。値が不正なら <see cref="ArgumentException"/>。</summary>
        public HordeSpec CreateSpec()
        {
            return new HordeSpec(PlayerSide, EnemySide, _startingCurrency, _killReward, _killRewardPerWave, _purchasesOnlyBetweenWaves);
        }

        /// <summary>
        /// schedule の波でホードを組み、シーンを探さずに members で始める (<see cref="ModeRunnerBase.Begin"/>)。Edit Mode のテストとスクリプトから使う。
        /// <see cref="Spawner"/> があれば試合の始まりに schedule で波を始め、無ければ schedule を始めるだけにする (敵を出して倒すのは呼び出し側)。
        /// </summary>
        public void BeginHorde(
            WaveSchedule schedule,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            ModeHudView hudView = null)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            if (schedule.Phase != WavePhase.Waiting)
            {
                throw new ArgumentException($"schedule must be Waiting, but is {schedule.Phase}", nameof(schedule));
            }

            Begin(PrepareHorde(schedule), members, spawnPoints, localPlayer, hudView);
        }

        protected override ModeSession CreateSession()
        {
            if (_spawner == null)
            {
                throw new InvalidOperationException("HordeRunner の WaveSpawner (Spawner) が設定されていません。シーン生成スクリプトで配線してください");
            }

            if (_spawner.IsStarted)
            {
                throw new InvalidOperationException($"WaveSpawner '{_spawner.name}' が自分で波を始めています。BeginOnStart を切ってください");
            }

            if (_spawner.EnemySide != EnemySide)
            {
                throw new InvalidOperationException(
                    $"WaveSpawner '{_spawner.name}' の敵の陣営 {_spawner.EnemySide.Id} が HordeRunner の EnemySide {EnemySide.Id} と違います");
            }

            _spawner.BeginOnStart = false;
            return PrepareHorde(_spawner.CreateSchedule());
        }

        protected override void OnEnded(Side? winner)
        {
            if (_horde == null)
            {
                return;
            }

            var schedule = _horde.Schedule;
            Debug.Log(HordeText.ResultLog(_horde.IsVictory, schedule.WaveNumber, schedule.WaveCount), this);
            StopWaves();
        }

        protected override string HudTime(ModeSession session)
        {
            return HudFormat.Time((float)session.ElapsedSeconds);
        }

        protected override void AppendHudLines(List<string> lines)
        {
            if (_horde == null)
            {
                return;
            }

            var schedule = _horde.Schedule;
            lines.Add(HordeText.Wave(schedule.WaveNumber, schedule.WaveCount));
            if (schedule.Phase == WavePhase.Fighting)
            {
                lines.Add(HordeText.RemainingEnemies(schedule.RemainingEnemies));
            }
            else if (schedule.Phase == WavePhase.Resting)
            {
                lines.Add(HordeText.NextWave(schedule.RestRemaining));
            }

            lines.Add(HordeText.Currency(_horde.Currency));

            var session = _horde.Session;
            if (session.State == GameModeState.Ended)
            {
                lines.Add(_horde.IsVictory ? HordeText.Victory : HordeText.Defeat);
                return;
            }

            var local = LocalPlayer;
            if (local == null || !session.HasPlayer(LocalPlayerId))
            {
                return;
            }

            if (session.IsAwaitingRespawn(LocalPlayerId) && !_horde.IsBetweenWaves)
            {
                lines.Add(HordeText.ReturnsAfterWave);
                return;
            }

            if (session.State != GameModeState.Playing || !local.IsAlive)
            {
                return;
            }

            var target = NearestFortification(local.GroundPosition);
            if (target != null)
            {
                var action = target.PendingAction;
                var cost = target.CostOf(action, target.RestorablePieces().Count);
                lines.Add(HordeText.Prompt(InteractKeyLabel, action, target.name, cost, _horde.CheckPurchase(cost)));
            }
        }

        /// <summary>操作しているプレイヤーで <see cref="TryInteract(SideMember)"/> する。プレイヤーがいなければ NothingToDo。</summary>
        public HordePurchaseVerdict TryInteract()
        {
            return TryInteract(LocalPlayer);
        }

        /// <summary>
        /// member の近くでいちばん近い、直す・置くものがある防衛物について、通貨を払って直す・置く。払えなければ通貨も防衛物も変えずに理由を返す。
        /// 始まっていないか、member が倒れているか、近くに防衛物が無いか、戻せる部品が無ければ NothingToDo。
        /// </summary>
        public HordePurchaseVerdict TryInteract(SideMember member)
        {
            if (_horde == null || member == null || !member.IsAlive || IdOf(member) < 0)
            {
                return HordePurchaseVerdict.NothingToDo;
            }

            var target = NearestFortification(member.GroundPosition);
            if (target == null)
            {
                return HordePurchaseVerdict.NothingToDo;
            }

            var action = target.PendingAction;
            var pieces = target.RestorablePieces();
            var cost = target.CostOf(action, pieces.Count);
            if (pieces.Count == 0)
            {
                Debug.Log(HordeText.DeniedLog(action, target.name, HordePurchaseVerdict.NothingToDo, cost, _horde.Currency), target);
                return HordePurchaseVerdict.NothingToDo;
            }

            var verdict = _horde.TryPurchase(cost);
            if (verdict != HordePurchaseVerdict.Ok)
            {
                Debug.Log(HordeText.DeniedLog(action, target.name, verdict, cost, _horde.Currency), target);
                return verdict;
            }

            var restored = target.Restore(action, pieces);
            Debug.Log(HordeText.PurchasedLog(action, target.name, restored, cost, _horde.Currency), target);
            RefreshHud();
            return HordePurchaseVerdict.Ok;
        }

        /// <summary>position から届く防衛物のうち、直す・置くものがあっていちばん近いもの。無ければ null。</summary>
        public HordeFortification NearestFortification(Vector3 position)
        {
            HordeFortification nearest = null;
            var best = float.PositiveInfinity;
            foreach (var fortification in _activeFortifications)
            {
                if (fortification == null || !fortification.IsInReach(position) || fortification.PendingAction == HordeFortificationAction.None)
                {
                    continue;
                }

                var distance = fortification.HorizontalDistanceTo(position);
                if (distance < best)
                {
                    best = distance;
                    nearest = fortification;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 波の敵を 1 体倒したことをホードに伝え、入った通貨を返す (<see cref="Fps.Core.Horde.ReportEnemyKilled"/>)。
        /// Spawner の敵が倒されたときに呼ばれる。Spawner を使わないテストでは、予定に倒したことを伝えた後に呼ぶ。
        /// </summary>
        public int RewardEnemyKill()
        {
            if (_horde == null)
            {
                throw new InvalidOperationException($"{nameof(HordeRunner)} '{name}' has not started a horde");
            }

            var waveNumber = _horde.Schedule.WaveNumber;
            var reward = _horde.ReportEnemyKilled();
            if (reward > 0)
            {
                Debug.Log(HordeText.EarnedLog(waveNumber, reward, _horde.Currency), this);
            }

            RefreshHud();
            return reward;
        }

        private ModeSession PrepareHorde(WaveSchedule schedule)
        {
            DetachHorde();

            _horde = new Horde(schedule, CreateSpec());
            _horde.Session.StateChanged += OnHordeStateChanged;
            schedule.WaveStarted += OnWaveStarted;
            schedule.WaveCleared += OnWaveCleared;

            _activeFortifications.Clear();
            var source = _fortifications != null && _fortifications.Length > 0
                ? (IReadOnlyList<HordeFortification>)_fortifications
                : FindObjectsByType<HordeFortification>(FindObjectsInactive.Exclude);
            foreach (var fortification in source)
            {
                if (fortification != null && !_activeFortifications.Contains(fortification))
                {
                    fortification.EnsurePrepared();
                    _activeFortifications.Add(fortification);
                }
            }

            // FindObjectsByType の順は決まっていないので、近さが同じときに選ぶものが Play ごとに変わらないよう名前で並べる。
            _activeFortifications.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return _horde.Session;
        }

        private void DetachHorde()
        {
            if (_horde != null)
            {
                _horde.Session.StateChanged -= OnHordeStateChanged;
                _horde.Schedule.WaveStarted -= OnWaveStarted;
                _horde.Schedule.WaveCleared -= OnWaveCleared;
            }

            if (_subscribedSpawner != null)
            {
                _subscribedSpawner.EnemySpawned -= OnEnemySpawned;
                _subscribedSpawner = null;
            }

            foreach (var enemy in _enemies)
            {
                if (enemy != null)
                {
                    enemy.Died -= OnEnemyDied;
                }
            }

            _enemies.Clear();
            _horde = null;
        }

        private void OnHordeStateChanged(GameModeState state)
        {
            if (state != GameModeState.Playing || _horde == null)
            {
                return;
            }

            var schedule = _horde.Schedule;
            if (schedule.Phase != WavePhase.Waiting)
            {
                return;
            }

            if (_spawner == null)
            {
                schedule.Start();
                return;
            }

            _subscribedSpawner = _spawner;
            _spawner.EnemySpawned += OnEnemySpawned;
            try
            {
                _spawner.Begin(schedule);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Debug.LogError($"{LogTag} 波を始められません: {e.Message}", _spawner);
            }
        }

        private void OnEnemySpawned(EnemyAgent enemy)
        {
            var damageable = enemy != null ? enemy.GetComponent<Damageable>() : null;
            if (damageable != null && _enemies.Add(damageable))
            {
                damageable.Died += OnEnemyDied;
            }
        }

        private void OnEnemyDied(Damageable damageable)
        {
            damageable.Died -= OnEnemyDied;
            if (_enemies.Remove(damageable) && _horde != null)
            {
                RewardEnemyKill();
            }
        }

        private void OnWaveStarted(int number, Wave wave)
        {
            Debug.Log(HordeText.WaveStartedLog(number, _horde.Schedule.WaveCount, wave.EnemyCount), this);
        }

        private void OnWaveCleared(int number)
        {
            Debug.Log(HordeText.WaveClearedLog(number, _horde.Currency, _horde.Respawn.WaitingCount), this);
            RefreshHud();
        }

        private void StopWaves()
        {
            if (_spawner == null || !_spawner.IsStarted)
            {
                return;
            }

            // Stop で出ている敵の一覧が空になるので、先に動きを止める。
            foreach (var enemy in _spawner.AliveEnemies)
            {
                var member = enemy != null ? enemy.GetComponent<SideMember>() : null;
                if (member != null)
                {
                    member.SetActing(false);
                }
            }

            _spawner.Stop();
            _spawner.enabled = false;
        }
    }
}
