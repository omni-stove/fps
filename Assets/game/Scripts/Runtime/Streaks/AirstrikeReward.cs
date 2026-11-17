using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AirstrikeReward : MonoBehaviour
    {
        // 広がりが 0 に近くても、狙った面の少し上から地面を探せるようにする。
        private const float MinGroundProbeHeight = 1f;

        // 爆発する時刻の順。同じ時刻なら呼んだ順。
        private readonly List<Strike> _pending = new List<Strike>();

        // 既定を Time.time にするのは、ポーズで時間が止まると爆発も待つようにするため。
        public Func<float> Clock { get; set; }

        public int PendingCount => _pending.Count;

        private float Now => Clock != null ? Clock() : Time.time;

        public Vector3 CallAtAim(
            float range,
            int strikes,
            float intervalSeconds,
            float spreadRadius,
            ExplosionSpec explosion,
            string weaponId,
            System.Random random)
        {
            if (float.IsNaN(range) || float.IsInfinity(range) || range <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            ResolveAim(range, out var origin, out var direction);
            var center = Hitscan.TryFirstHit(origin, direction, range, transform, out var hit)
                ? hit.point
                : origin + (direction.normalized * range);
            Call(center, strikes, intervalSeconds, spreadRadius, explosion, weaponId, random);
            return center;
        }

        public void Call(
            Vector3 center,
            int strikes,
            float intervalSeconds,
            float spreadRadius,
            ExplosionSpec explosion,
            string weaponId,
            System.Random random)
        {
            if (!IsFinite(center))
            {
                throw new ArgumentOutOfRangeException(nameof(center), center, "center must be finite");
            }

            if (strikes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(strikes), strikes, "strikes must be at least 1");
            }

            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds) || intervalSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "intervalSeconds must be non-negative and finite");
            }

            if (float.IsNaN(spreadRadius) || float.IsInfinity(spreadRadius) || spreadRadius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spreadRadius), spreadRadius, "spreadRadius must be non-negative and finite");
            }

            if (explosion == null)
            {
                throw new ArgumentNullException(nameof(explosion));
            }

            random = random ?? new System.Random();
            var now = Now;
            for (var i = 0; i < strikes; i++)
            {
                var point = PlaceOnGround(ScatterPoint(center, spreadRadius, random), spreadRadius);
                Enqueue(new Strike(now + ((i + 1) * intervalSeconds), point, i + 1, strikes, explosion, weaponId));
            }

            Debug.Log(StreakRewardLog.AirstrikeCalled(name, center, strikes, intervalSeconds, spreadRadius), this);
            Tick(now);
        }

        public int Tick(float now)
        {
            // 爆発で倒れた者の知らせから Call が呼ばれても、回している途中の並びを書き換えないよう先に取り出す。
            var count = 0;
            while (count < _pending.Count && _pending[count].At <= now)
            {
                count++;
            }

            if (count == 0)
            {
                return 0;
            }

            var due = _pending.GetRange(0, count);
            _pending.RemoveRange(0, count);
            foreach (var strike in due)
            {
                var result = Explosion.Detonate(strike.Point, strike.Explosion, gameObject, strike.WeaponId);
                Debug.Log(StreakRewardLog.AirstrikeStrike(name, strike.Index, strike.Count, result), this);
            }

            return count;
        }

        public int Cancel()
        {
            var count = _pending.Count;
            _pending.Clear();
            return count;
        }

        public static Vector3 ScatterPoint(Vector3 center, float radius, System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be non-negative and finite");
            }

            // 半径を一様に取ると中心に寄るので、面積で一様になるよう平方根を取る。
            var distance = radius * Math.Sqrt(random.NextDouble());
            var angle = random.NextDouble() * 2.0 * Math.PI;
            return center + new Vector3((float)(distance * Math.Cos(angle)), 0f, (float)(distance * Math.Sin(angle)));
        }

        private void Update()
        {
            Tick(Now);
        }

        private void Enqueue(Strike strike)
        {
            var index = _pending.Count;
            while (index > 0 && _pending[index - 1].At > strike.At)
            {
                index--;
            }

            _pending.Insert(index, strike);
        }

        // 坂や段差で円の上の点が地面に埋まったり宙に浮いたりしないよう、真上から地面を探して面の上に置く。
        private Vector3 PlaceOnGround(Vector3 point, float spreadRadius)
        {
            var height = Mathf.Max(spreadRadius, MinGroundProbeHeight);
            if (Hitscan.TryFirstHit(point + (Vector3.up * height), Vector3.down, height * 2f, transform, out var ground))
            {
                return ImpactResolver.DetonationPoint(ground.point.ToNumerics(), ground.normal.ToNumerics()).ToUnity();
            }

            return point;
        }

        // 三人称ではカメラの中央が狙いなので視点から狙う。視点が無い (テストや視点を持たない体) ときだけ体の正面を使う。
        private void ResolveAim(float range, out Vector3 origin, out Vector3 direction)
        {
            var rig = GetComponent<ViewRig>();
            if (rig != null && rig.IsInitialized)
            {
                rig.ResolveAim(range, out origin, out direction);
                return;
            }

            origin = transform.position;
            direction = transform.forward;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private readonly struct Strike
        {
            public Strike(float at, Vector3 point, int index, int count, ExplosionSpec explosion, string weaponId)
            {
                At = at;
                Point = point;
                Index = index;
                Count = count;
                Explosion = explosion;
                WeaponId = weaponId;
            }

            public float At { get; }

            public Vector3 Point { get; }

            public int Index { get; }

            public int Count { get; }

            public ExplosionSpec Explosion { get; }

            public string WeaponId { get; }
        }
    }
}
