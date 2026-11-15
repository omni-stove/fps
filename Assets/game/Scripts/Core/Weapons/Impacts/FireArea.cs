using System;
using System.Runtime.CompilerServices;

namespace Fps.Core
{
    /// <summary>
    /// 燃える範囲 (焼夷グレネード) の決まり。中にいる相手に間隔ごとに状態異常をかけ直すだけにし、範囲が直接ダメージを与えないのは、
    /// 外に出た相手を状態異常が切れるまで燃やし続け、中でも外でも同じ間隔・同じ帰属でダメージを数えるため。
    /// オフライン (AreaEffectZone) とサーバー (ServerAreaEffects) で結果をそろえるため、ここに置く。
    /// </summary>
    public static class FireArea
    {
        public const float DefaultBurnSeconds = 3f;

        // 時間を小さく刻んで足し重ねた誤差で、ちょうど区切りの回を取りこぼさないための幅。
        private const float _epsilon = 1e-4f;

        // 範囲は毎 tick かけ直すので、同じ spec から毎回作り直さないよう覚えておく。spec が要らなくなれば一緒に消えるよう弱い参照にする。
        private static readonly ConditionalWeakTable<AreaEffectSpec, StatusEffectSpec> _derived =
            new ConditionalWeakTable<AreaEffectSpec, StatusEffectSpec>();

        /// <summary>
        /// statusEffect を書いていない範囲でも damagePerTick だけで燃えるよう、範囲の間隔とダメージから燃焼を作る。
        /// 出た後に燃える長さは書けば statusEffect.durationSeconds、書かなければ <see cref="DefaultBurnSeconds"/>。
        /// </summary>
        public static StatusEffectSpec Affliction(AreaEffectSpec spec)
        {
            if (spec == null)
            {
                return null;
            }

            if (spec.StatusEffect != null)
            {
                return spec.StatusEffect;
            }

            return spec.DamagePerTick > 0 ? _derived.GetValue(spec, Derive) : null;
        }

        public static bool IsFire(AreaEffectSpec spec)
        {
            var affliction = Affliction(spec);
            return affliction != null && string.Equals(affliction.KindId, StatusEffectKinds.Burn, StringComparison.Ordinal);
        }

        /// <summary>
        /// 毎フレームかけ直すと、かけるたびの知らせ (ネット対戦では全員へのメッセージ) が増えすぎるので、出た直後と
        /// <see cref="AreaEffectSpec.TickSeconds"/> ごとの回だけ true にする。経った時間から数え、範囲ごとに状態を持たずに済ませる。
        /// </summary>
        public static bool Pulses(AreaEffectSpec spec, float elapsedSeconds, float deltaSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!(elapsedSeconds > _epsilon))
            {
                return true;
            }

            // 消える瞬間にかけ直すと、もう無い炎の分だけ燃え続けるので、寿命ちょうどの回は数えない。
            var end = Math.Min(elapsedSeconds + deltaSeconds, spec.DurationSeconds - (2f * _epsilon));
            var before = Math.Floor((elapsedSeconds + _epsilon) / spec.TickSeconds);
            var after = Math.Floor((end + _epsilon) / spec.TickSeconds);
            return after > before;
        }

        private static StatusEffectSpec Derive(AreaEffectSpec spec)
        {
            // StatusEffectSpec は極端に短い間隔を受け付けないので、範囲の間隔がそれより短くても作れるようにする。
            var tickSeconds = Math.Max(spec.TickSeconds, StatusEffectSpec.MinTickSeconds);
            return new StatusEffectSpec(DefaultBurnSeconds, tickSeconds, spec.DamagePerTick, kindId: StatusEffectKinds.Burn);
        }
    }
}
