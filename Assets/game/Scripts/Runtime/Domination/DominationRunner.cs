using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class DominationRunner : ModeRunnerBase
    {
        public const float DefaultCaptureSeconds = 8f;
        public const float DefaultPointsPerSecond = 1f;
        public const int DefaultScoreLimit = 100;
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 5f;

        [SerializeField, Tooltip("調整値のアセット。設定すれば下の値の代わりにこれを使う。空なら入口 (GameContent) のものを使う")]
        private DominationDefinition _definition;

        [SerializeField, Min(0.01f), Tooltip("1 人で中立の拠点を占領し終えるまでの秒数")]
        private float _captureSeconds = DefaultCaptureSeconds;

        [SerializeField, Min(0.01f), Tooltip("拠点 1 つを持っているチームに 1 秒あたり入るポイント")]
        private float _pointsPerSecond = DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこのポイントに届いたら試合が終わる")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(0.01f), Tooltip("試合の制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("占領の速さに数える人数の上限")]
        private int _maxCapturers = DominationSpec.DefaultMaxCapturers;

        [SerializeField, Min(0f), Tooltip("倒れてから自陣営の出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Tooltip("拠点。空ならシーンの ControlPointZone をすべて使う")]
        private ControlPointZone[] _zones = Array.Empty<ControlPointZone>();

        private ControlPointZone[] _activeZones = Array.Empty<ControlPointZone>();
        private ControlPointMap _map;
        private Domination _domination;
        private ScoreboardEntry[] _publishedScoreboard;
        private bool _publishedPaletteViewer;
        private Team _paletteViewer = TeamColors.OfflineViewer;

        public Domination Domination => _domination;

        public IReadOnlyList<ControlPointZone> Zones => _activeZones;

        public IReadOnlyList<ScoreboardEntry> PublishedScoreboard => _publishedScoreboard;

        public DominationDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public DominationDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.Domination);

        public float EffectiveRespawnSeconds => EffectiveDefinition is { } definition ? definition.RespawnSeconds : _respawnSeconds;

        public float CaptureSeconds
        {
            get => _captureSeconds;
            set => _captureSeconds = value;
        }

        public float PointsPerSecond
        {
            get => _pointsPerSecond;
            set => _pointsPerSecond = value;
        }

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public int MaxCapturers
        {
            get => _maxCapturers;
            set => _maxCapturers = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public IReadOnlyList<ControlPointZone> ConfiguredZones => _zones ?? Array.Empty<ControlPointZone>();

        public void SetZones(IReadOnlyList<ControlPointZone> zones)
        {
            var copy = new ControlPointZone[zones != null ? zones.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = zones[i];
            }

            _zones = copy;
        }

        public DominationSpec CreateSpec(int pointCount)
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec(pointCount);
            }

            return new DominationSpec(pointCount, _captureSeconds, _pointsPerSecond, _scoreLimit, _timeLimitSeconds, _maxCapturers);
        }

        public Domination CreateDomination(int pointCount)
        {
            var respawnSeconds = EffectiveRespawnSeconds;
            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(RespawnSeconds), respawnSeconds, "RespawnSeconds must be a finite, non-negative value");
            }

            return new Domination(CreateSpec(pointCount), new IndividualRespawn(respawnSeconds));
        }

        public void CopyFrom(DominationDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _captureSeconds = definition.CaptureSeconds;
            _pointsPerSecond = definition.PointsPerSecond;
            _scoreLimit = definition.ScoreLimit;
            _timeLimitSeconds = definition.TimeLimitSeconds;
            _maxCapturers = definition.MaxCapturers;
            _respawnSeconds = definition.RespawnSeconds;
        }

        public void Begin(
            Domination domination,
            IReadOnlyList<ControlPointZone> zones,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (domination == null)
            {
                throw new ArgumentNullException(nameof(domination));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(DominationRunner)} '{name}' has already started a mode");
            }

            var sorted = SortZones(zones, out var map);
            if (sorted.Length != domination.PointCount)
            {
                throw new ArgumentException(
                    $"there are {sorted.Length} control point(s) but the domination has {domination.PointCount}", nameof(zones));
            }

            Attach(domination, sorted, map);
            try
            {
                Begin(domination.Session, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        protected override ModeSession CreateSession()
        {
            var configured = ConfiguredZones;
            IReadOnlyList<ControlPointZone> zones = configured.Count > 0 ? configured : FindObjectsByType<ControlPointZone>();
            if (zones.Count == 0)
            {
                throw new InvalidOperationException("シーンに ControlPointZone がありません。拠点を 1 つ以上置いてください。");
            }

            var sorted = SortZones(zones, out var map);
            var domination = CreateDomination(sorted.Length);
            Attach(domination, sorted, map);
            return domination.Session;
        }

        protected override void OnBegan()
        {
            // 円盤と同じ陣営から見た色にそろえる
            _paletteViewer = PlayerSide.TryGetTeam(out var viewerTeam) ? viewerTeam : TeamColors.OfflineViewer;
            GameSignals.PaletteViewer.Publish(_paletteViewer);
            _publishedPaletteViewer = true;
            ShowScoreboard();
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            if (_domination != null && IsStarted && _domination.State == GameModeState.Playing)
            {
                UpdatePresence();
            }
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            ShowScoreboard();
        }

        protected override object HudDetail => _domination != null ? DominationStatus.Of(_domination) : null;

        protected override void OnStop()
        {
            if (_publishedScoreboard != null)
            {
                GameSignals.Scoreboard.ClearIfCurrent(_publishedScoreboard);
            }

            _publishedScoreboard = null;
            if (_publishedPaletteViewer)
            {
                GameSignals.PaletteViewer.ClearIfCurrent(_paletteViewer);
                _publishedPaletteViewer = false;
            }

            Detach();
        }

        private void Attach(Domination domination, ControlPointZone[] zones, ControlPointMap map)
        {
            Detach();
            _domination = domination;
            _activeZones = zones;
            _map = map;
            _domination.PointCaptured += OnPointCaptured;
            _domination.PointNeutralized += OnPointNeutralized;

            // 円盤の既定はオフラインの Alpha から見た色なので、陣営を変えたプレイヤーには自分の陣営から見た色にする
            var viewer = PlayerSide.TryGetTeam(out var playerTeam) ? playerTeam : (Team?)null;
            foreach (var zone in _activeZones)
            {
                zone.ViewerTeam = viewer;
                zone.ShowOwner(null);
            }
        }

        private void Detach()
        {
            if (_domination != null)
            {
                _domination.PointCaptured -= OnPointCaptured;
                _domination.PointNeutralized -= OnPointNeutralized;
            }

            _domination = null;
            _activeZones = Array.Empty<ControlPointZone>();
            _map = null;
        }

        private void UpdatePresence()
        {
            var members = Members;
            for (var id = 0; id < members.Count; id++)
            {
                var damageable = members[id] != null ? members[id].Damageable : null;
                int? point = null;
                if (damageable != null && !damageable.IsDead)
                {
                    point = _map.Locate(damageable.transform.position.ToNumerics());
                }

                _domination.SetPlayerPoint(id, point);
            }
        }

        private void OnPointCaptured(int pointIndex, Team team)
        {
            Debug.Log(DominationLog.Captured(pointIndex, team), this);
            _activeZones[pointIndex].ShowOwner(team);
        }

        private void OnPointNeutralized(int pointIndex, Team previousOwner)
        {
            Debug.Log(DominationLog.Neutralized(pointIndex, previousOwner), this);
            _activeZones[pointIndex].ShowOwner(null);
        }

        private void ShowScoreboard()
        {
            var session = Session;
            if (session == null)
            {
                return;
            }

            // 止めるときに自分の出した一覧かを参照で見分けるので、出すたびに新しい配列にする
            var members = Members;
            var entries = new ScoreboardEntry[members.Count];
            for (var id = 0; id < members.Count; id++)
            {
                var member = members[id];
                var isLocal = member != null && member == LocalPlayer;
                entries[id] = new ScoreboardEntry(
                    id,
                    isLocal ? ScoreboardName.LocalPlayer : ScoreboardName.Literal(NameOf(member)),
                    session.GetSide(id).ToTeam(),
                    session.GetKills(id),
                    session.GetDeaths(id),
                    session.GetAssists(id),
                    ScoreboardConnection.None,
                    isLocal);
            }

            _publishedScoreboard = entries;
            GameSignals.Scoreboard.Publish(entries);
        }

        private static string NameOf(SideMember member)
        {
            if (member == null)
            {
                return string.Empty;
            }

            var enemy = member.GetComponent<EnemyAgent>();
            return enemy != null ? enemy.Id : member.name;
        }

        private static ControlPointZone[] SortZones(IReadOnlyList<ControlPointZone> zones, out ControlPointMap map)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            if (zones.Count == 0)
            {
                throw new ArgumentException("at least one ControlPointZone is required", nameof(zones));
            }

            var areas = new ControlPointArea[zones.Count];
            for (var i = 0; i < areas.Length; i++)
            {
                var zone = zones[i];
                if (zone == null)
                {
                    throw new ArgumentException($"zone #{i} is null", nameof(zones));
                }

                try
                {
                    areas[i] = zone.Area;
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new ArgumentException($"ControlPointZone '{zone.name}' has an invalid area: {e.Message}", nameof(zones), e);
                }
            }

            // 番号の抜けと重なりは ControlPointMap が確かめる。
            map = new ControlPointMap(areas);

            var sorted = new ControlPointZone[zones.Count];
            foreach (var zone in zones)
            {
                sorted[zone.Index] = zone;
            }

            return sorted;
        }
    }
}
