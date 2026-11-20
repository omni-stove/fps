using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 自分がリーダーのときは自分の頭上に目印を出さない。自分には HUD で知らせるため。
    public sealed class GuardianRunner : ModeRunnerBase
    {
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 3f;
        public const int DefaultPointsPerKill = GuardianMode.DefaultPointsPerKill;
        public const string MarkerRootName = "LeaderMarkers";

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の制限時間・リスポーン・キル 1 回の点の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private GuardianDefinition _definition;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("リーダーが生きている間、倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Min(1), Tooltip("別のチームを 1 回倒すごとに入る点")]
        private int _pointsPerKill = DefaultPointsPerKill;

        [SerializeField, Tooltip("リーダーを選ぶ乱数の種。負なら Play ごとに変わる")]
        private int _leaderSeed = -1;

        [SerializeField, Tooltip("リーダーの頭上に目印を出すか")]
        private bool _showLeaderMarkers = true;

        [SerializeField, Tooltip("リーダーの頭上の目印の見た目。原点が目印の下端で、縦の軸で回す。当たり判定は外し、材質はチームの色に塗る。空なら仮の菱形を作る")]
        private GameObject _leaderMarkerPrefab;

        private readonly Dictionary<int, GuardianLeaderMarker> _markers = new Dictionary<int, GuardianLeaderMarker>();
        private GuardianRespawn _leaders;
        private Transform _markerRoot;

        public GuardianDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public GuardianDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.Guardian);

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public int PointsPerKill
        {
            get => _pointsPerKill;
            set => _pointsPerKill = value;
        }

        public int LeaderSeed
        {
            get => _leaderSeed;
            set => _leaderSeed = value;
        }

        public bool ShowsLeaderMarkers
        {
            get => _showLeaderMarkers;
            set => _showLeaderMarkers = value;
        }

        public GameObject LeaderMarkerPrefab
        {
            get => _leaderMarkerPrefab;
            set => _leaderMarkerPrefab = value;
        }

        public GuardianRespawn Leaders => _leaders;

        public GuardianLeaderMarker MarkerOf(int playerId)
        {
            return _markers.TryGetValue(playerId, out var marker) ? marker : null;
        }

        public void ClearMarkers()
        {
            foreach (var marker in _markers.Values)
            {
                if (marker != null)
                {
                    marker.Remove();
                }
            }

            _markers.Clear();
        }

        public void BeginGuardian(
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            Begin(CreateSession(), members, spawnPoints, localPlayer);
        }

        protected override ModeSession CreateSession()
        {
            if (_leaders != null)
            {
                _leaders.LeaderChosen -= OnLeaderChosen;
                _leaders.LeaderFell -= OnLeaderFell;
                ClearMarkers();
            }

            var random = _leaderSeed >= 0 ? new System.Random(_leaderSeed) : new System.Random();
            var definition = EffectiveDefinition;
            GuardianRespawn leaders;
            var session = definition != null
                ? definition.CreateSession(random, out leaders)
                : GuardianMode.CreateSession(_timeLimitSeconds, _respawnSeconds, random, out leaders, _pointsPerKill);
            _leaders = leaders;
            _leaders.LeaderChosen += OnLeaderChosen;
            _leaders.LeaderFell += OnLeaderFell;
            return session;
        }

        protected override object HudDetail
        {
            get
            {
                if (_leaders == null || Session == null)
                {
                    return null;
                }

                var localId = LocalPlayer != null && Session.HasPlayer(LocalPlayerId) ? LocalPlayerId : (int?)null;
                var eliminated = localId.HasValue && Session.State == GameModeState.Playing && Session.IsEliminated(LocalPlayerId);
                return new GuardianStatus(_leaders, localId, eliminated);
            }
        }

        private void OnLeaderChosen(Side side, int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(GuardianLog.LeaderChosenLog(side, playerId), member != null ? member : (UnityEngine.Object)this);

            if (!_showLeaderMarkers || member == null || member == LocalPlayer)
            {
                return;
            }

            if (_markerRoot == null)
            {
                _markerRoot = new GameObject(MarkerRootName).transform;
                _markerRoot.SetParent(transform, false);
            }

            var marker = GuardianLeaderMarker.Create(member, _leaderMarkerPrefab);
            marker.ViewerTeam = PlayerSide.TryGetTeam(out var playerTeam) ? playerTeam : (Team?)null;
            marker.transform.SetParent(_markerRoot, true);
            _markers[playerId] = marker;
        }

        private void OnLeaderFell(Side side, int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(GuardianLog.LeaderFellLog(side, playerId), member != null ? member : (UnityEngine.Object)this);

            var marker = MarkerOf(playerId);
            if (marker != null)
            {
                marker.SetShown(false);
            }

            RefreshHud();
        }
    }
}
