using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AirstrikeReward : MonoBehaviour
    {
        // 爆発する時刻の順。同じ時刻なら呼んだ順。
        private readonly List<Strike> _pending = new List<Strike>();

        // 既定を Time.time にするのは、ポーズで時間が止まると爆発も待つようにするため。
        public Func<float> Clock { get; set; }

        public int PendingCount => _pending.Count;

        private float Now => Clock != null ? Clock() : Time.time;

        // 送られた高さは選んだときの地面なので、中心も弾も真上から落として決め直す (専用サーバーの ServerStreaks と同じ)。
        // targeting を渡すと、探し始めを使った人の高さにし、探す幅を max(viewHeight, dropHeight) にする。空なら選んだ高さから dropHeight で探す。
        public Vector3 Call(Vector3 target, float headingDegrees, StreakAirstrikeSpec spec, string weaponId, System.Random random, StreakTargetingSpec targeting = null)
        {
            if (!IsFinite(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target), target, "target must be finite");
            }

            if (float.IsNaN(headingDegrees) || float.IsInfinity(headingDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(headingDegrees), headingDegrees, "headingDegrees must be finite");
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            random = random ?? new System.Random();
            var now = Now;
            var searchHeight = targeting != null ? Mathf.Max(targeting.ViewHeight, spec.DropHeight) : spec.DropHeight;
            var baseY = targeting != null ? transform.position.y : target.y;
            var center = Drop(new Vector3(target.x, baseY, target.z), searchHeight);
            for (var i = 0; i < spec.ShellCount; i++)
            {
                var shell = AirstrikeLine.ShellPoint(center.ToNumerics(), headingDegrees, spec, i, random).ToUnity();
                var point = Drop(new Vector3(shell.x, baseY, shell.z), searchHeight);
                Enqueue(new Strike(now + ((i + 1) * spec.IntervalSeconds), point, i + 1, spec.ShellCount, spec.Explosion, weaponId));
            }

            Debug.Log(StreakRewardLog.AirstrikeCalled(name, center, headingDegrees, spec.ShellCount, spec.IntervalSeconds, spec.LineLength), this);
            Tick(now);
            return center;
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

        // 坂や段差で線の上の点が地面に埋まったり宙に浮いたりせず、屋根や橋があればその上で止まるよう、上から真下へ探す。
        private Vector3 Drop(Vector3 point, float height)
        {
            if (Hitscan.TryFirstHit(point + (Vector3.up * height), Vector3.down, height * 2f, transform, out var ground))
            {
                return ImpactResolver.DetonationPoint(ground.point.ToNumerics(), ground.normal.ToNumerics()).ToUnity();
            }

            return point;
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
