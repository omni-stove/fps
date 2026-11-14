using System;

namespace Fps.Core
{
    /// <summary>
    /// 乗り物の部位 (エンジン・燃料など) 1 つの設定 (<see cref="VehicleSpec.Parts"/> の 1 つ)。その部位に当たったダメージに掛ける倍率を持つ。
    /// 生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class VehiclePartSpec
    {
        /// <param name="id">部位の Id。空でない。1 台の中で重ならない。</param>
        /// <param name="damageMultiplier">この部位に当たったダメージに掛ける倍率。0 以上の有限値。</param>
        public VehiclePartSpec(string id, float damageMultiplier)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!(damageMultiplier >= 0f) || float.IsInfinity(damageMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damageMultiplier), damageMultiplier, "damageMultiplier must not be negative and must be finite");
            }

            Id = id;
            DamageMultiplier = damageMultiplier;
        }

        public string Id { get; }

        public float DamageMultiplier { get; }
    }
}
