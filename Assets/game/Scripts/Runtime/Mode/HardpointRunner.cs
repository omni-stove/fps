using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ハードポイント (<see cref="Fps.Core.Hardpoint"/>) をシーンで進める <see cref="ModeRunnerBase"/>。陣営 0 が Alpha、陣営 1 が Bravo。
    /// 拠点は <see cref="ControlPointZone"/> を使い、番号の順に有効になる。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>拠点は <see cref="SetZones"/> で渡したもの。渡していなければ始めるときにシーンの ControlPointZone をすべて使う。
    /// 番号は 0 から抜けも重なりもなく、2 つ以上あること (でなければ理由をログに出して止まる)。</item>
    /// <item>毎 Tick の前に、有効な拠点の範囲にいる生きた参加者を調べ (<see cref="ControlPointZone.Scan"/>)、
    /// <see cref="Fps.Core.Hardpoint.SetPlayerPoint"/> に伝える。</item>
    /// <item>有効な拠点の円盤だけを光らせて確保しているチームの色に塗り、他の拠点は暗くする (<see cref="ControlPointZone.SetHighlight"/>)。</item>
    /// <item>bot (<see cref="EnemyAgent"/>) には有効な拠点の中を往復する巡回路を渡し、拠点が移るたびに渡し直す。
    /// 敵を見つけたら EnemyAgent がいつもどおり追って撃ち、見失えば巡回路 (有効な拠点) に戻る。</item>
    /// <item>HUD には得点の下に、有効な拠点と次へ移るまでの秒数・拠点の様子 (<see cref="HardpointText.HudLines"/>) を出す。
    /// 拠点が移ったときと拠点の様子が変わったときに <c>[Mode] hardpoint</c> のログを出す。</item>
    /// </list>
    /// </remarks>
    public sealed class HardpointRunner : ModeRunnerBase
    {
        public const float DefaultRotateSeconds = 30f;
        public const float DefaultPointsPerSecond = 1f;
        public const int DefaultScoreLimit = 100;
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 3f;

        /// <summary>bot の巡回路の端を、拠点の中心から半径のこの割合だけ離す (範囲の縁で出入りを繰り返さないように内側に置く)。</summary>
        public const float RouteRadiusFraction = 0.5f;

        // bot ごとに巡回する向きを黄金角ずつ回し、同じ拠点に集まっても重ならないようにする。
        private const float RouteAngleStepDegrees = 137.5f;

        [SerializeField, Min(0.01f), Tooltip("有効な拠点が次の拠点へ移るまでの秒数")]
        private float _rotateSeconds = DefaultRotateSeconds;

        [SerializeField, Min(0.01f), Tooltip("有効な拠点に自チームだけがいる間、1 秒あたり入るポイント")]
        private float _pointsPerSecond = DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこのポイントに届いたら試合が終わる")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Tooltip("拠点。空ならシーンの ControlPointZone をすべて使う")]
        private ControlPointZone[] _zones = Array.Empty<ControlPointZone>();

        private readonly List<Damageable> _candidates = new List<Damageable>();
        private Hardpoint _hardpoint;
        private ControlPointZone[] _activeZones = Array.Empty<ControlPointZone>();
        private int _routedPoint = -1;
        private HardpointStatus? _loggedStatus;
        private Side? _loggedHolder;

        /// <summary>進めているハードポイント。開始前は null。</summary>
        public Hardpoint Hardpoint => _hardpoint;

        /// <summary>使っている拠点。番号の順。開始前は空。</summary>
        public IReadOnlyList<ControlPointZone> Zones => _activeZones;

        public float RotateSeconds
        {
            get => _rotateSeconds;
            set => _rotateSeconds = value;
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

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        /// <summary>始めるときに使う拠点。空なら始めるときにシーンの ControlPointZone をすべて使う。</summary>
        public IReadOnlyList<ControlPointZone> ConfiguredZones => _zones ?? Array.Empty<ControlPointZone>();

        /// <summary>始めるときに使う拠点を設定する。null か空なら、シーンの ControlPointZone をすべて使う。始めた後に変えても効かない。</summary>
        public void SetZones(IReadOnlyList<ControlPointZone> zones)
        {
            var copy = new ControlPointZone[zones != null ? zones.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = zones[i];
            }

            _zones = copy;
        }

        /// <summary>Inspector の設定から、拠点が pointCount 個の <see cref="HardpointSpec"/> を作る。値が不正なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public HardpointSpec CreateSpec(int pointCount)
        {
            return new HardpointSpec(pointCount, _rotateSeconds, _pointsPerSecond, _scoreLimit, _timeLimitSeconds, _respawnSeconds);
        }

        /// <summary>
        /// hardpoint の拠点を zones にして、members を参加者に hardpoint.Session を始める (<see cref="ModeRunnerBase.Begin"/>)。
        /// zones は番号が 0 から抜けも重なりもなく、数が hardpoint.PointCount と同じであること。
        /// 引数が不正なら <see cref="ArgumentException"/> を投げ、何も購読しない。
        /// </summary>
        public void Begin(
            Hardpoint hardpoint,
            IReadOnlyList<ControlPointZone> zones,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            ModeHudView hudView = null)
        {
            if (hardpoint == null)
            {
                throw new ArgumentNullException(nameof(hardpoint));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(HardpointRunner)} '{name}' has already started a mode");
            }

            var sorted = SortZones(zones);
            if (sorted.Length != hardpoint.PointCount)
            {
                throw new ArgumentException(
                    $"there are {sorted.Length} control point(s) but the hardpoint has {hardpoint.PointCount}", nameof(zones));
            }

            Attach(hardpoint, sorted);
            try
            {
                Begin(hardpoint.Session, members, spawnPoints, localPlayer, hudView);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        /// <summary>
        /// bot が有効な拠点の中を往復する巡回路 (2 点)。中心から半径の <see cref="RouteRadiusFraction"/> だけ離れた、向かい合う 2 点で、
        /// 向きは slot ごとに回す。
        /// </summary>
        public static Vector3[] RouteAround(Vector3 center, float radius, int slot)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            var angle = (slot * RouteAngleStepDegrees % 360f) * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * RouteRadiusFraction);
            return new[] { center + offset, center - offset };
        }

        protected override ModeSession CreateSession()
        {
            var configured = ConfiguredZones;
            IReadOnlyList<ControlPointZone> zones = configured.Count > 0 ? configured : FindObjectsByType<ControlPointZone>();
            if (zones.Count == 0)
            {
                throw new InvalidOperationException("シーンに ControlPointZone がありません。拠点を 2 つ以上置いてください。");
            }

            var sorted = SortZones(zones);
            var hardpoint = new Hardpoint(CreateSpec(sorted.Length));
            Attach(hardpoint, sorted);
            return hardpoint.Session;
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            if (_hardpoint == null || !IsStarted)
            {
                return;
            }

            if (_routedPoint != _hardpoint.ActivePoint)
            {
                RouteBots();
            }

            UpdatePresence();
            ShowStatus();
        }

        protected override void AppendHudLines(List<string> lines)
        {
            if (_hardpoint != null)
            {
                lines.AddRange(HardpointText.HudLines(_hardpoint));
            }
        }

        private void Attach(Hardpoint hardpoint, ControlPointZone[] zones)
        {
            Detach();
            _hardpoint = hardpoint;
            _activeZones = zones;
            _hardpoint.PointMoved += OnPointMoved;

            // 円盤の既定はオフラインの Alpha から見た色なので、陣営を変えたプレイヤーには自分の陣営から見た色にする
            var viewer = PlayerSide.TryGetTeam(out var playerTeam) ? playerTeam : (Team?)null;
            foreach (var zone in _activeZones)
            {
                if (zone != null)
                {
                    zone.ViewerTeam = viewer;
                }
            }

            ShowZones();
        }

        private void Detach()
        {
            if (_hardpoint != null)
            {
                _hardpoint.PointMoved -= OnPointMoved;
            }

            _hardpoint = null;
            _activeZones = Array.Empty<ControlPointZone>();
            _routedPoint = -1;
            _loggedStatus = null;
            _loggedHolder = null;
        }

        /// <summary>有効な拠点の範囲にいる生きた参加者を調べ、Hardpoint に伝える。他の拠点にいる人は数えないので、どこにもいないことにする。</summary>
        private void UpdatePresence()
        {
            var members = Members;
            _candidates.Clear();
            foreach (var member in members)
            {
                _candidates.Add(member != null ? member.Damageable : null);
            }

            var zone = _activeZones[_hardpoint.ActivePoint];
            zone.Scan(_candidates);
            var occupants = zone.Occupants;
            for (var id = 0; id < members.Count; id++)
            {
                var damageable = _candidates[id];
                var inside = damageable != null && Contains(occupants, damageable);
                _hardpoint.SetPlayerPoint(id, inside ? _hardpoint.ActivePoint : (int?)null);
            }
        }

        /// <summary>拠点の様子が変わったらログに出し、有効な拠点の円盤を確保しているチームの色にする。</summary>
        private void ShowStatus()
        {
            var status = _hardpoint.Status;
            var holder = _hardpoint.Holder;
            if (_loggedStatus == status && _loggedHolder == holder)
            {
                return;
            }

            _loggedStatus = status;
            _loggedHolder = holder;
            Debug.Log(HardpointText.StatusLog(_hardpoint.ActivePoint, status, holder), this);
            ShowZones();
        }

        private void ShowZones()
        {
            var holder = Side.ToTeam(_hardpoint.Holder);
            for (var i = 0; i < _activeZones.Length; i++)
            {
                var active = i == _hardpoint.ActivePoint;
                _activeZones[i].SetHighlight(active);
                _activeZones[i].ShowOwner(active ? holder : null);
            }
        }

        private void OnPointMoved(int previous, int next)
        {
            Debug.Log(HardpointText.MovedLog(previous, next), this);
            LogScore();

            // 移った先にいる人はまだ調べていないので、次の Tick で調べるまで誰もいないことにする。
            foreach (var member in Members)
            {
                var id = IdOf(member);
                if (id >= 0)
                {
                    _hardpoint.SetPlayerPoint(id, null);
                }
            }

            _loggedStatus = null;
            ShowZones();
            RouteBots();
        }

        private void RouteBots()
        {
            if (_hardpoint == null)
            {
                return;
            }

            var zone = _activeZones[_hardpoint.ActivePoint];
            var center = zone.transform.position;
            var slot = 0;
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent == null)
                {
                    continue;
                }

                agent.SetPatrolRoute(RouteAround(center, zone.Radius, slot));
                slot++;
            }

            _routedPoint = _hardpoint.ActivePoint;
        }

        /// <summary>zones を番号の順に並べる。null・範囲の不正・番号の抜けや重なり・2 つ未満なら <see cref="ArgumentException"/>。</summary>
        private static ControlPointZone[] SortZones(IReadOnlyList<ControlPointZone> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            var areas = new ControlPointArea[zones.Count];
            for (var i = 0; i < areas.Length; i++)
            {
                var zone = zones[i];
                if (zone == null)
                {
                    throw new ArgumentException($"control point #{i} is null", nameof(zones));
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

            if (areas.Length < 2)
            {
                throw new ArgumentException($"a hardpoint needs at least 2 control points but there are {areas.Length}", nameof(zones));
            }

            // 番号の抜けと重なりは ControlPointMap が確かめる。
            _ = new ControlPointMap(areas);

            var sorted = new ControlPointZone[zones.Count];
            foreach (var zone in zones)
            {
                sorted[zone.Index] = zone;
            }

            return sorted;
        }

        private static bool Contains(IReadOnlyList<Damageable> list, Damageable item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == item)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
