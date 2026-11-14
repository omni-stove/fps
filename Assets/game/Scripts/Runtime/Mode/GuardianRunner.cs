using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガーディアン (VIP の護衛) をシーンで進める。試合開始時に Alpha と Bravo から 1 人ずつリーダーを選び (<see cref="GuardianMode"/>)、
    /// リーダーが生きている間は倒れた味方が <see cref="RespawnSeconds"/> 後に戻る。リーダーが倒れたチームは戻れなくなり、全員倒れたら負け。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>リーダーの頭上にチームの色の目印 (<see cref="GuardianLeaderMarker"/>) を出し、倒れたら隠す。自分がリーダーのときは自分には出さない (HUD で知らせる)。</item>
    /// <item>HUD には両チームのリーダーの生死 (自分のチームが先) と、自分がリーダーであること・リーダーが倒れて戻れないことを出す。</item>
    /// <item>リーダーが決まったとき・倒れたときに <c>[Mode] leader</c>・<c>[Mode] leader-down</c> のログ (<see cref="GuardianText"/>) を出す。</item>
    /// </list>
    /// </remarks>
    public sealed class GuardianRunner : ModeRunnerBase
    {
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 3f;
        public const string MarkerRootName = "LeaderMarkers";

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("リーダーが生きている間、倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Tooltip("リーダーを選ぶ乱数の種。負なら Play ごとに変わる")]
        private int _leaderSeed = -1;

        [SerializeField, Tooltip("リーダーの頭上に目印を出すか")]
        private bool _showLeaderMarkers = true;

        private readonly Dictionary<int, GuardianLeaderMarker> _markers = new Dictionary<int, GuardianLeaderMarker>();
        private GuardianRespawn _leaders;
        private Transform _markerRoot;

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

        /// <summary>リーダーを選ぶ乱数の種。負なら Play ごとに変わる。</summary>
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

        /// <summary>今のモードのリーダー。モードを組む前は null。</summary>
        public GuardianRespawn Leaders => _leaders;

        /// <summary>playerId のリーダーの頭上の目印。出していなければ null。</summary>
        public GuardianLeaderMarker MarkerOf(int playerId)
        {
            return _markers.TryGetValue(playerId, out var marker) ? marker : null;
        }

        /// <summary>出している目印をすべて消す。Runner を消すと目印も一緒に消える (Runner の子に置く)。</summary>
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

        /// <summary>
        /// このモードを組んで、シーンを探さずに members で始める (<see cref="ModeRunnerBase.Begin"/>)。Edit Mode のテストとスクリプトから使う。
        /// </summary>
        public void BeginGuardian(
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            IModeHud hudView = null)
        {
            Begin(CreateSession(), members, spawnPoints, localPlayer, hudView);
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
            var session = GuardianMode.CreateSession(_timeLimitSeconds, _respawnSeconds, random, out var leaders);
            _leaders = leaders;
            _leaders.LeaderChosen += OnLeaderChosen;
            _leaders.LeaderFell += OnLeaderFell;
            return session;
        }

        protected override void AppendHudLines(List<string> lines)
        {
            if (_leaders == null || Session == null)
            {
                return;
            }

            var local = LocalPlayer;
            var ownSide = local != null ? local.Side : (Side?)null;
            if (local != null && Session.HasPlayer(LocalPlayerId))
            {
                if (_leaders.GetLeader(local.Side) == LocalPlayerId && !_leaders.HasLeaderFallen(local.Side))
                {
                    lines.Add(GuardianText.YouAreLeader);
                }
                else if (Session.State == GameModeState.Playing && Session.IsEliminated(LocalPlayerId) && _leaders.HasLeaderFallen(local.Side))
                {
                    lines.Add(GuardianText.NoRespawnLeaderDown);
                }
            }

            lines.AddRange(GuardianText.Leaders(_leaders, ownSide));
        }

        private void OnLeaderChosen(Side side, int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(GuardianText.LeaderChosenLog(side, playerId), member != null ? member : (UnityEngine.Object)this);

            if (!_showLeaderMarkers || member == null || member == LocalPlayer)
            {
                return;
            }

            if (_markerRoot == null)
            {
                _markerRoot = new GameObject(MarkerRootName).transform;
                _markerRoot.SetParent(transform, false);
            }

            var marker = GuardianLeaderMarker.Create(member);
            marker.ViewerTeam = PlayerSide.TryGetTeam(out var playerTeam) ? playerTeam : (Team?)null;
            marker.transform.SetParent(_markerRoot, true);
            _markers[playerId] = marker;
        }

        private void OnLeaderFell(Side side, int playerId)
        {
            var member = MemberOf(playerId);
            Debug.Log(GuardianText.LeaderFellLog(side, playerId), member != null ? member : (UnityEngine.Object)this);

            var marker = MarkerOf(playerId);
            if (marker != null)
            {
                marker.SetShown(false);
            }

            RefreshHud();
        }
    }
}
