using System;
using System.Numerics;

namespace Shooter.Core
{
    // 見通せるかは世界の当たりを持つ側 (Unity の Flash・サーバーの ServerFlash) が調べ、ここは見えている前提で長さだけを決める。
    public static class BlindEffect
    {
        private const float _minLength = 1e-4f;

        public static float Seconds(BlindSpec spec, Vector3 flash, Vector3 eye, Vector3 facing)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var offset = flash - eye;
            var distance = offset.Length();
            if (!(distance < spec.Radius))
            {
                return 0f;
            }

            var reach = 1f - (distance / spec.Radius);
            return spec.DurationSeconds * reach * FacingFactor(spec, offset, distance, facing);
        }

        public static StatusEffectSpec StatusEffect(float seconds)
        {
            SpecGuard.PositiveFinite(seconds, nameof(seconds));

            // ダメージを与えないので、間隔は時間切れより先に来ないよう長さそのものにする。
            return new StatusEffectSpec(seconds, Math.Max(seconds, StatusEffectSpec.MinTickSeconds), 0, kindId: StatusEffectKinds.Blind);
        }

        private static float FacingFactor(BlindSpec spec, Vector3 offset, float distance, Vector3 facing)
        {
            var facingLength = facing.Length();

            // 目の前で弾けた・向きが分からないときに軽くしてしまわないよう、正面から見たことにする。
            if (distance < _minLength || !(facingLength > _minLength) || float.IsInfinity(facingLength))
            {
                return 1f;
            }

            var cos = Math.Min(1f, Math.Max(-1f, Vector3.Dot(offset / distance, facing / facingLength)));
            var toward = (cos + 1f) * 0.5f;
            return spec.BackFraction + ((1f - spec.BackFraction) * toward);
        }
    }
}
