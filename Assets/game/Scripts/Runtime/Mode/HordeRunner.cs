using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class HordeRunner : ModeRunnerBase
    {
        public const int DefaultEnemySideId = HordeSpec.DefaultEnemySideId;

        [SerializeField, RequiredReference, Tooltip("波の敵を出す WaveSpawner。シーン生成スクリプトが配線する。BeginOnStart は切っておく")]
        private WaveSpawner _spawner;

        [SerializeField, Tooltip("通貨で直す・置く防衛物。空ならシーンの HordeFortification をすべて使う")]
        private HordeFortification[] _fortifications = Array.Empty<HordeFortification>();

        [SerializeField, Min(0), Tooltip("波の敵の陣営の ID。HordeDefinition が無いときだけ使う。WaveSpawner の敵の陣営と合わせる")]
        private int _enemySideId = DefaultEnemySideId;

        [SerializeField, Tooltip("通貨と波の調整値。空なら入口 (GameContent) のもの、それも無ければ HordeSpec の既定値と、WaveSpawner に設定された波を使う")]
        private HordeDefinition _definition;

        private readonly HashSet<Damageable> _enemies = new HashSet<Damageable>();
        private readonly List<HordeFortification> _activeFortifications = new List<HordeFortification>();
        private Horde _horde;
        private WaveSpawner _subscribedSpawner;

        public Horde Horde => _horde;

        public WaveSpawner Spawner
        {
            get => _spawner;
            set => _spawner = value;
        }

        public Side EnemySide
        {
            get => EffectiveDefinition is { } definition ? definition.EnemySide : new Side(Math.Max(0, _enemySideId));
            set => _enemySideId = value.Id;
        }

        public HordeDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public HordeDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.Horde);

        public string InteractKeyLabel { get; set; }

        public IReadOnlyList<HordeFortification> ConfiguredFortifications => _fortifications ?? Array.Empty<HordeFortification>();

        public IReadOnlyList<HordeFortification> Fortifications => _activeFortifications;

        public void SetFortifications(IReadOnlyList<HordeFortification> fortifications)
        {
            var copy = new HordeFortification[fortifications != null ? fortifications.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = fortifications[i];
            }

            _fortifications = copy;
        }

        public HordeSpec CreateSpec()
        {
            return EffectiveDefinition is { } definition ? definition.ToSpec(PlayerSide, EnemySide) : new HordeSpec(PlayerSide, EnemySide);
        }

        public WaveSchedule CreateSchedule()
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.CreateSchedule();
            }

            if (_spawner == null)
            {
                throw new InvalidOperationException($"HordeRunner '{name}' に HordeDefinition も WaveSpawner も無いため、波を決められません");
            }

            return _spawner.CreateSchedule();
        }

        public void BeginHorde(
            WaveSchedule schedule,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            if (schedule.Phase != WavePhase.Waiting)
            {
                throw new ArgumentException($"schedule must be Waiting, but is {schedule.Phase}", nameof(schedule));
            }

            var definition = EffectiveDefinition;
            var spec = CreateSpec();
            if (definition != null && _spawner != null && !_spawner.IsStarted)
            {
                _spawner.ApplySpec(spec);
            }

            Begin(PrepareHorde(schedule, spec, definition != null), members, spawnPoints, localPlayer);
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

            var definition = EffectiveDefinition;
            var spec = CreateSpec();
            if (definition != null)
            {
                _spawner.ApplySpec(spec);
            }
            else if (_spawner.EnemySide != EnemySide)
            {
                throw new InvalidOperationException(
                    $"WaveSpawner '{_spawner.name}' の敵の陣営 {_spawner.EnemySide.Id} が HordeRunner の EnemySide {EnemySide.Id} と違います");
            }

            _spawner.BeginOnStart = false;
            return PrepareHorde(CreateSchedule(), spec, definition != null);
        }

        protected override void OnEnded(Side? winner)
        {
            if (_horde == null)
            {
                return;
            }

            var schedule = _horde.Schedule;
            Debug.Log(HordeLog.ResultLog(_horde.IsVictory, schedule.WaveNumber, schedule.WaveCount), this);
            StopWaves();
        }

        protected override object HudDetail
        {
            get
            {
                if (_horde == null)
                {
                    return null;
                }

                var local = LocalPlayer;
                var session = _horde.Session;
                if (local == null || !session.HasPlayer(LocalPlayerId))
                {
                    return new HordeStatus(_horde, null, null);
                }

                return new HordeStatus(_horde, LocalPlayerId, Offer(local));
            }
        }

        protected override bool OffersInteract
        {
            get
            {
                var local = LocalPlayer;
                return _horde != null && local != null && _horde.Session.HasPlayer(LocalPlayerId) && Offer(local)?.Verdict == HordePurchaseVerdict.Ok;
            }
        }

        private HordeFortificationOffer? Offer(SideMember local)
        {
            var session = _horde.Session;
            if (session.State != GameModeState.Playing || !local.IsAlive
                || (session.IsAwaitingRespawn(LocalPlayerId) && !_horde.IsBetweenWaves))
            {
                return null;
            }

            var target = NearestFortification(local.GroundPosition);
            if (target == null)
            {
                return null;
            }

            var action = target.PendingAction;
            var cost = target.CostOf(action, target.RestorablePieces().Count);
            return new HordeFortificationOffer(action, target.name, cost, _horde.CheckPurchase(cost), InteractKeyLabel);
        }

        public HordePurchaseVerdict TryInteract()
        {
            return TryInteract(LocalPlayer);
        }

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
                Debug.Log(HordeLog.DeniedLog(action, target.name, HordePurchaseVerdict.NothingToDo, cost, _horde.Currency), target);
                return HordePurchaseVerdict.NothingToDo;
            }

            var verdict = _horde.TryPurchase(cost);
            if (verdict != HordePurchaseVerdict.Ok)
            {
                Debug.Log(HordeLog.DeniedLog(action, target.name, verdict, cost, _horde.Currency), target);
                return verdict;
            }

            var restored = target.Restore(action, pieces);
            Debug.Log(HordeLog.PurchasedLog(action, target.name, restored, cost, _horde.Currency), target);
            RefreshHud();
            return HordePurchaseVerdict.Ok;
        }

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
                Debug.Log(HordeLog.EarnedLog(waveNumber, reward, _horde.Currency), this);
            }

            RefreshHud();
            return reward;
        }

        private ModeSession PrepareHorde(WaveSchedule schedule, HordeSpec spec, bool fromDefinition)
        {
            DetachHorde();

            _horde = new Horde(schedule, spec);
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
                    if (fromDefinition)
                    {
                        fortification.ApplySpec(spec);
                    }

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
            Debug.Log(HordeLog.WaveStartedLog(number, _horde.Schedule.WaveCount, wave.EnemyCount), this);
        }

        private void OnWaveCleared(int number)
        {
            Debug.Log(HordeLog.WaveClearedLog(number, _horde.Currency, _horde.Respawn.WaitingCount), this);
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
