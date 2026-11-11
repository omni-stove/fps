using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// ラグ補償用に、エンティティ ID ごとの位置の履歴を持つ。
    /// サーバーは毎 tick 位置を記録し、撃った側のクライアントが見ていた時点まで巻き戻した位置で当たり判定をする。
    /// </summary>
    public sealed class LagCompensator
    {
        /// <summary>既定の巻き戻し幅の上限。60Hz で約 200ms。</summary>
        public const int DefaultMaxRewindTicks = 12;

        private readonly Dictionary<int, HitboxHistory> _histories = new Dictionary<int, HitboxHistory>();

        /// <summary>エンティティ 1 体あたりに保持する記録の数。</summary>
        public int HistoryCapacity { get; }

        /// <summary>現在の tick から巻き戻せる最大の tick 数。</summary>
        public int MaxRewindTicks { get; }

        /// <param name="historyCapacity">エンティティ 1 体あたりに保持する記録の数。1 以上。</param>
        /// <param name="maxRewindTicks">巻き戻し幅の上限 (tick)。0 以上。</param>
        public LagCompensator(int historyCapacity = HitboxHistory.DefaultCapacity, int maxRewindTicks = DefaultMaxRewindTicks)
        {
            if (historyCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(historyCapacity), historyCapacity, "historyCapacity must be positive");
            }

            if (maxRewindTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRewindTicks), maxRewindTicks, "maxRewindTicks must not be negative");
            }

            HistoryCapacity = historyCapacity;
            MaxRewindTicks = maxRewindTicks;
        }

        /// <summary>entityId の tick 時点の位置を記録する。初めての ID なら履歴を作る。</summary>
        public void Record(int tick, int entityId, Vector3 position)
        {
            if (!_histories.TryGetValue(entityId, out var history))
            {
                history = new HitboxHistory(HistoryCapacity);
                _histories.Add(entityId, history);
            }

            history.Record(tick, position);
        }

        /// <summary>entityId の履歴を捨てる。退出や死亡で当たり判定の対象から外すときに呼ぶ。消したら true。</summary>
        public bool Remove(int entityId)
        {
            return _histories.Remove(entityId);
        }

        /// <summary>tick 時点の位置を、その時点で位置が取れたエンティティだけ返す。巻き戻し幅の上限はかけない。</summary>
        public IReadOnlyDictionary<int, Vector3> Rewind(float tick)
        {
            var result = new Dictionary<int, Vector3>();
            foreach (var pair in _histories)
            {
                if (pair.Value.TrySample(tick, out var position))
                {
                    result.Add(pair.Key, position);
                }
            }

            return result;
        }

        /// <summary>
        /// 現在 currentTick のサーバーで tick 時点まで巻き戻した位置を返す。
        /// currentTick - tick が MaxRewindTicks を超えたら currentTick - MaxRewindTicks に、
        /// tick が currentTick より先なら currentTick に丸める。
        /// </summary>
        public IReadOnlyDictionary<int, Vector3> Rewind(float tick, int currentTick)
        {
            return Rewind(ClampTick(tick, currentTick));
        }

        /// <summary>Rewind(tick, currentTick) が実際に使う tick。NaN はそのまま返す。</summary>
        public float ClampTick(float tick, int currentTick)
        {
            if (float.IsNaN(tick))
            {
                return tick;
            }

            var oldest = (double)currentTick - MaxRewindTicks;
            return (float)Math.Min(Math.Max(tick, oldest), currentTick);
        }
    }
}
