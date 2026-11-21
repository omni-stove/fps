using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 持っている盾 (WeaponSpec.Shield) の板。オフラインの HeldRiotShield の仮の板と同じく、持ち主の原点から向きの前へ RiotShieldSpec.HeldForward
    // 出した所に、幅と高さが RiotShieldSpec の板を立てる。撃った位置が盾の扇 (BlockArcDegrees) の中なら弾を止める。
    internal sealed partial class ServerHitboxes
    {
        private readonly Dictionary<int, ShieldHistory> _shields = new Dictionary<int, ShieldHistory>();

        // 位置と同じ tick の向きで板を置くよう、構えた盾と向きも位置と一緒に記録する。構えていない tick も記録し、巻き戻した先で盾を出さない
        private void RecordShield(int tick, ServerBody body)
        {
            var raised = body.RaisedShield;
            if (!_shields.TryGetValue(body.PlayerId, out var history))
            {
                if (raised == null)
                {
                    return;
                }

                history = new ShieldHistory(HitboxHistory.DefaultCapacity);
                _shields.Add(body.PlayerId, history);
            }

            history.Record(tick, body.Yaw, raised);
        }

        public bool TryGetHeldShield(int playerId, float tick, out Vector3 center, out Vector3 forward, out RiotShieldSpec spec)
        {
            center = default;
            forward = default;
            spec = null;
            if (!HasHistory)
            {
                return false;
            }

            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            return _lag.Rewind(clamped).TryGetValue(playerId, out var position) && TryPlate(playerId, position, clamped, out center, out forward, out spec);
        }

        private bool TryPlate(int playerId, Vector3 position, float tick, out Vector3 center, out Vector3 forward, out RiotShieldSpec spec)
        {
            center = default;
            forward = default;
            spec = null;
            if (!_shields.TryGetValue(playerId, out var history) || !history.TrySample(tick, out var yaw, out spec) || spec == null)
            {
                return false;
            }

            forward = CharacterMotorSimulation.YawForward(yaw);
            center = position + (forward * RiotShieldSpec.HeldForward);
            return true;
        }

        private bool NearerHeldShield(
            Vector3 origin, Vector3 unit, float radius, float limit, float tick, List<(int Id, RewoundBody Body)> bodies, ref ShotHit hit)
        {
            if (_shields.Count == 0 || !(limit > 0f))
            {
                return false;
            }

            var found = false;
            foreach (var (id, body) in bodies)
            {
                if (TryHitPlate(origin, unit, radius, limit, tick, id, body.Position, out var shield))
                {
                    hit = shield;
                    limit = shield.Distance;
                    found = true;
                }
            }

            return found;
        }

        // 盾は弾を止めるので、貫通する弾の当たりの並びに足すだけで、その先の物は並びの順で弾が止まって拾われない
        private void AddHeldShieldHits(
            Vector3 origin, Vector3 unit, float range, float tick, List<(int Id, RewoundBody Body)> bodies, List<ShotHit> hits)
        {
            if (_shields.Count == 0)
            {
                return;
            }

            foreach (var (id, body) in bodies)
            {
                if (TryHitPlate(origin, unit, 0f, range, tick, id, body.Position, out var shield))
                {
                    hits.Add(shield);
                }
            }
        }

        // 扇の外から撃たれた弾は板を素通りさせ、後ろの持ち主の体に当てる。オフラインでは板に当たると持ち主の胴に当たったことになるが、
        // 扇の外から板に届く線は体の側面か背中を先に通るので、体の当たりに任せても止まり方は変わらない
        private bool TryHitPlate(Vector3 origin, Vector3 unit, float radius, float limit, float tick, int holderId, Vector3 position, out ShotHit hit)
        {
            hit = default;
            if (!TryPlate(holderId, position, tick, out var center, out var forward, out var spec)
                || !Covers(center, forward, spec.BlockArcDegrees, origin))
            {
                return false;
            }

            var half = new Vector3(spec.Width / 2f, spec.Height / 2f, RiotShieldSpec.HeldThickness / 2f) + new Vector3(radius);
            if (!RayPlate(origin, unit, center, forward, half, limit, out var distance, out var normal))
            {
                return false;
            }

            hit = ShotHit.Shield(origin + (unit * distance), normal, distance, holderId);
            return true;
        }

        // 撃った位置はオフラインの BulletShield.Covers と同じく板の中心から見る。弾の始点で見るので、飛ぶ弾と跳ねた弾は今いる所の向きで決まる
        internal static bool Covers(Vector3 center, Vector3 forward, float arcDegrees, Vector3 source)
        {
            return arcDegrees >= RiotShieldSpec.MaxBlockArcDegrees || MeleeJudge.IsWithinArc(center, forward, arcDegrees, source);
        }

        // 縦に立てて水平に回した箱と線の当たり。始点が箱の中なら当てない (Unity の Physics.Raycast が中から撃った面を拾わないのと同じ)
        internal static bool RayPlate(
            Vector3 origin, Vector3 unit, Vector3 center, Vector3 forward, Vector3 halfExtents, float maxDistance, out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = -unit;
            var right = new Vector3(forward.Z, 0f, -forward.X);
            var offset = origin - center;
            var enter = float.NegativeInfinity;
            var exit = float.PositiveInfinity;
            for (var axis = 0; axis < 3; axis++)
            {
                var direction = axis == 0 ? right : axis == 1 ? Vector3.UnitY : forward;
                var half = axis == 0 ? halfExtents.X : axis == 1 ? halfExtents.Y : halfExtents.Z;
                var start = Vector3.Dot(direction, offset);
                var speed = Vector3.Dot(direction, unit);
                if (MathF.Abs(speed) < 1e-8f)
                {
                    if (MathF.Abs(start) > half)
                    {
                        return false;
                    }

                    continue;
                }

                var t1 = (-half - start) / speed;
                var t2 = (half - start) / speed;
                var near = MathF.Min(t1, t2);
                var far = MathF.Max(t1, t2);
                if (near > enter)
                {
                    enter = near;
                    normal = speed > 0f ? -direction : direction;
                }

                exit = MathF.Min(exit, far);
                if (enter > exit)
                {
                    return false;
                }
            }

            if (!(enter >= 0f) || enter > maxDistance)
            {
                return false;
            }

            distance = enter;
            return true;
        }

        // しゃがみ (StanceHistory) と同じく補間しない。盾はある tick で構える・下げるもので、途中の板は持ち主の画面にも無いため
        private sealed class ShieldHistory
        {
            private readonly int[] _ticks;
            private readonly float[] _yaws;
            private readonly RiotShieldSpec[] _specs;
            private int _start;
            private int _count;

            public ShieldHistory(int capacity)
            {
                _ticks = new int[capacity];
                _yaws = new float[capacity];
                _specs = new RiotShieldSpec[capacity];
            }

            public void Record(int tick, float yaw, RiotShieldSpec spec)
            {
                if (_count > 0)
                {
                    var newest = IndexOf(_count - 1);
                    if (tick == _ticks[newest])
                    {
                        _yaws[newest] = yaw;
                        _specs[newest] = spec;
                        return;
                    }
                }

                int index;
                if (_count < _ticks.Length)
                {
                    index = IndexOf(_count);
                    _count++;
                }
                else
                {
                    index = _start;
                    _start = (_start + 1) % _ticks.Length;
                }

                _ticks[index] = tick;
                _yaws[index] = yaw;
                _specs[index] = spec;
            }

            public bool TrySample(float tick, out float yaw, out RiotShieldSpec spec)
            {
                yaw = 0f;
                spec = null;
                if (_count == 0 || float.IsNaN(tick) || tick < _ticks[IndexOf(0)])
                {
                    return false;
                }

                var lo = 0;
                var hi = _count - 1;
                while (lo < hi)
                {
                    var mid = (lo + hi + 1) / 2;
                    if (_ticks[IndexOf(mid)] <= tick)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }

                var index = IndexOf(lo);
                yaw = _yaws[index];
                spec = _specs[index];
                return true;
            }

            private int IndexOf(int logical)
            {
                return (_start + logical) % _ticks.Length;
            }
        }
    }
}
