using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ストリークの報酬の空爆。使った者 (この部品の付いた GameObject) が呼ぶと、中心の周りの広がりの中に数発の爆発を間を置いて起こす
    /// (<see cref="Explosion.Detonate"/>)。爆発のダメージと倒した数は使った者に帰属させる。
    /// <list type="bullet">
    /// <item>k 発目 (1 から) は、呼んだ時刻から k × 間の秒数の後に爆発する。間が 0 なら呼んだときにすべて爆発する。</item>
    /// <item>各発の位置は呼んだときに決める。中心を通る水平な円の中から乱数で選び (<see cref="ScatterPoint"/>)、その点の上下
    /// (広がりの半径か 1 m の大きい方) に地面があれば、その面の上に置く。</item>
    /// <item>続けて呼ぶと、前の空爆の残りと並んで進む。</item>
    /// </list>
    /// 発数・間・広がり・爆発の値は呼び出し側が渡す。時刻は <see cref="Clock"/> で、Play 中は Update が <see cref="Tick"/> を呼ぶ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AirstrikeReward : MonoBehaviour
    {
        // 広がりが 0 に近くても、狙った面の少し上から地面を探せるようにする。
        private const float MinGroundProbeHeight = 1f;

        // 爆発する時刻の順。同じ時刻なら呼んだ順。
        private readonly List<Strike> _pending = new List<Strike>();

        /// <summary>時刻 (秒) を返すもの。null なら <see cref="Time.time"/> (ポーズで時間が止まると爆発も待つ)。テストが差し込む。</summary>
        public Func<float> Clock { get; set; }

        /// <summary>まだ爆発していない発の数。</summary>
        public int PendingCount => _pending.Count;

        private float Now => Clock != null ? Clock() : Time.time;

        /// <summary>
        /// 照準の先を中心に空爆を呼び、中心を返す。照準は <see cref="ViewRig.ResolveAim(float, out Vector3, out Vector3)"/> で決め、
        /// その線が range までに当たった点 (使った者の体を除く) を中心にし、何にも当たらなければ range 先の点を中心にする。
        /// 視点が無いか初期化前なら、体の位置から体の正面へ狙う。他の値と例外は <see cref="Call"/> と同じ。
        /// </summary>
        /// <param name="range">照準の届く距離 (m)。0 より大きい有限の値。</param>
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

        /// <summary>
        /// center を中心に空爆を呼ぶ。各発の位置をここで決めて <c>[Streak] airstrike called</c> をログに出し、
        /// 爆発するたびに <c>[Streak] airstrike strike</c> を出す。
        /// </summary>
        /// <param name="center">中心。有限の値。</param>
        /// <param name="strikes">発数。1 以上。</param>
        /// <param name="intervalSeconds">発と発の間の秒数。1 発目も呼んでからこの秒数で爆発する。0 以上の有限の値。</param>
        /// <param name="spreadRadius">爆発の位置を選ぶ円の半径 (m)。0 以上の有限の値。</param>
        /// <param name="explosion">1 発の爆発の値。</param>
        /// <param name="weaponId">爆発と倒したことに付ける武器の Id (キルフィードなどが使う)。</param>
        /// <param name="random">位置を選ぶ乱数。null なら新しく作る。</param>
        /// <exception cref="ArgumentNullException">explosion が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">center・strikes・intervalSeconds・spreadRadius が範囲の外。</exception>
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

        /// <summary>now までに爆発する時刻の来た発を、呼んだ順・時刻の順に爆発させ、爆発させた数を返す。</summary>
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

        /// <summary>まだ爆発していない発をすべて取りやめ、取りやめた数を返す。</summary>
        public int Cancel()
        {
            var count = _pending.Count;
            _pending.Clear();
            return count;
        }

        /// <summary>
        /// center を通る水平な円 (半径 radius) の中から、random で一様に 1 点を選ぶ。高さは center のまま。
        /// 同じ種の random なら同じ点を返す。
        /// </summary>
        /// <exception cref="ArgumentNullException">random が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">radius が負か有限でない。</exception>
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
