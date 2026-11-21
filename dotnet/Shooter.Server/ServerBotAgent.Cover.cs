using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // サーバーのマップには物陰の点 (オフラインの CoverPointRegistry) が無いので、撃たれた・弱った時にまわりを探して点を作り、
    // 判断 (EnemyBrain) の物陰の選び方・隠れ方・覗き方にそのまま渡す。
    // 点の一覧を Bot ごとに持つので、2 体の Bot が同じ物陰を選ぶことはある
    internal sealed partial class ServerBotAgent
    {
        private const float _minThreatOffsetSquared = 1e-6f;

        private readonly List<CoverPoint> _coverPoints = new List<CoverPoint>();
        private float _nextCoverSearch;

        public int CoverPointCount => _coverPoints.Count;

        // 判断が物陰にいる間は、選んだ点を放させないよう一覧を替えない
        public bool PrepareCover(in EnemyPerception perception, ServerBotCoverProbe probe)
        {
            ArgumentNullException.ThrowIfNull(probe);
            if (Brain.State == EnemyState.TakeCover)
            {
                return false;
            }

            var tactics = _spec.Tactics;
            var threat = perception.Target ?? (perception.WasHit ? perception.HitSource : null);
            var wants = tactics.TakeCover
                && UsesTactics
                && threat.HasValue
                && (perception.WasHit || perception.HealthFraction < Brain.Spec.CoverHealthFraction);
            if (!wants || Clock < _nextCoverSearch)
            {
                Brain.UseCover(null);
                return false;
            }

            _nextCoverSearch = Clock + tactics.CoverSearchSeconds;
            FindCover(perception.Self, threat.Value, tactics, probe, _coverPoints);
            Brain.UseCover(_coverPoints.Count > 0
                ? new CoverPointSelector(
                    _coverPoints.ToArray(),
                    tactics.CoverSearchRadius + tactics.CoverSearchStep,
                    tactics.CoverMaxAngleDegrees,
                    tactics.CoverMinThreatDistance)
                : null);
            return _coverPoints.Count > 0;
        }

        // 今いる所と、そのまわりの輪の上の点を NavMesh に置き、しゃがんだ目から脅威への線が遮られる所を物陰にする。
        // 立った目からも遮られるなら高い物陰 (覗いても見えない)、通るなら低い物陰
        internal static void FindCover(Vector3 self, Vector3 threat, BotTacticsSpec tactics, ServerBotCoverProbe probe, List<CoverPoint> into)
        {
            ArgumentNullException.ThrowIfNull(tactics);
            ArgumentNullException.ThrowIfNull(probe);
            ArgumentNullException.ThrowIfNull(into);
            into.Clear();
            if (!IsFinite(self) || !IsFinite(threat))
            {
                return;
            }

            var up = Vector3.UnitY;
            var from = self - (up * probe.CenterHeight);
            TryAdd(from, from, threat, tactics, probe, into);
            var rings = (int)MathF.Floor(tactics.CoverSearchRadius / tactics.CoverSearchStep);
            for (var ring = 1; ring <= rings; ring++)
            {
                var radius = ring * tactics.CoverSearchStep;
                for (var i = 0; i < tactics.CoverSearchDirections; i++)
                {
                    var angle = 2f * MathF.PI * i / tactics.CoverSearchDirections;
                    TryAdd(from, from + (new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * radius), threat, tactics, probe, into);
                }
            }
        }

        private static void TryAdd(
            Vector3 from, Vector3 candidate, Vector3 threat, BotTacticsSpec tactics, ServerBotCoverProbe probe, List<CoverPoint> into)
        {
            if (!probe.Place(from, candidate, out var feet))
            {
                return;
            }

            var toThreat = new Vector3(threat.X - feet.X, 0f, threat.Z - feet.Z);
            if (toThreat.LengthSquared() < _minThreatOffsetSquared || toThreat.Length() < tactics.CoverMinThreatDistance)
            {
                return;
            }

            var up = Vector3.UnitY;
            if (!probe.IsBlocked(feet + (up * probe.HideHeight), threat))
            {
                return;
            }

            var height = probe.IsBlocked(feet + (up * probe.PeekHeight), threat) ? CoverHeight.High : CoverHeight.Low;
            into.Add(new CoverPoint(feet + (up * probe.CenterHeight), toThreat, height));
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }

    // 物陰を探すときに Bot のモジュールが渡す、置ける所と線の遮りの問い合わせ。高さはどれも足元からの高さ
    internal sealed class ServerBotCoverProbe
    {
        private readonly Func<Vector3, Vector3, Vector3?> _place;
        private readonly Func<Vector3, Vector3, bool> _isBlocked;

        // place は (今の足元, 探す点の足元) を受け、行ける足元の位置か、行けないなら null を返す
        public ServerBotCoverProbe(
            Func<Vector3, Vector3, Vector3?> place, Func<Vector3, Vector3, bool> isBlocked, float centerHeight, float hideHeight, float peekHeight)
        {
            _place = place ?? throw new ArgumentNullException(nameof(place));
            _isBlocked = isBlocked ?? throw new ArgumentNullException(nameof(isBlocked));
            RequireHeight(centerHeight, nameof(centerHeight));
            RequireHeight(hideHeight, nameof(hideHeight));
            RequireHeight(peekHeight, nameof(peekHeight));
            CenterHeight = centerHeight;
            HideHeight = hideHeight;
            PeekHeight = peekHeight;
        }

        // 判断の位置 (体の中心) の、足元からの高さ。物陰の点もこの高さに置き、着いたかを判断と同じ測り方で見る
        public float CenterHeight { get; }

        public float HideHeight { get; }

        public float PeekHeight { get; }

        public bool Place(Vector3 from, Vector3 candidate, out Vector3 feet)
        {
            var placed = _place(from, candidate);
            feet = placed ?? default;
            return placed.HasValue;
        }

        public bool IsBlocked(Vector3 from, Vector3 to)
        {
            return _isBlocked(from, to);
        }

        private static void RequireHeight(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }
    }
}
