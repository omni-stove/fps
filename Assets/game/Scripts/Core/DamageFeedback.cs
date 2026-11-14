using System;

namespace Fps.Core
{
    /// <summary>
    /// 体力の変化で <see cref="DamageFeedback.SetHealth"/> が返す、低体力の状態の変わり方。
    /// </summary>
    public enum LowHealthChange
    {
        /// <summary>変わらない。</summary>
        None,

        /// <summary>しきい値以下に下がった。端の赤と心音を始める。</summary>
        Started,

        /// <summary>回復してしきい値を超えた。端の赤と心音を止め、息切れの音を鳴らす。</summary>
        Recovered,

        /// <summary>倒れた (0) か体力が無くなった (null)。端の赤と心音を止めるが、息切れの音は鳴らさない。</summary>
        Cleared,
    }

    /// <summary>
    /// 被弾の画面効果と低体力の演出の設定。値は <see cref="DamageFeedback"/> がそのまま使う。
    /// </summary>
    public sealed class DamageFeedbackSettings
    {
        /// <summary>カタログが無いときに使う値。</summary>
        public static readonly DamageFeedbackSettings Default = new DamageFeedbackSettings(
            hitPeakIntensity: 0.8f,
            fullHitDamage: 50f,
            hitFadeSeconds: 0.8f,
            lowHealthThreshold: HudFormat.LowHealthRatio,
            lowHealthMinIntensity: 0.35f,
            lowHealthMaxIntensity: 0.7f);

        /// <param name="hitPeakIntensity">被弾した瞬間の強さの上限 ((0, 1])。</param>
        /// <param name="fullHitDamage">この量以上のダメージで強さが上限になる (&gt; 0)。少ないダメージは量に比例して弱くする。</param>
        /// <param name="hitFadeSeconds">被弾の強さが 0 に戻るまでの秒数 (&gt; 0)。</param>
        /// <param name="lowHealthThreshold">低体力と見なす、最大体力に対する割合 ((0, 1))。この割合以下 (0 は除く) で低体力。</param>
        /// <param name="lowHealthMinIntensity">しきい値ちょうどのときの低体力の強さ ([0, 1])。</param>
        /// <param name="lowHealthMaxIntensity">体力が 0 に近いときの低体力の強さ ([min, 1])。</param>
        /// <exception cref="ArgumentOutOfRangeException">どれかが範囲外か有限でない。</exception>
        public DamageFeedbackSettings(
            float hitPeakIntensity,
            float fullHitDamage,
            float hitFadeSeconds,
            float lowHealthThreshold,
            float lowHealthMinIntensity,
            float lowHealthMaxIntensity)
        {
            if (!IsFinite(hitPeakIntensity) || hitPeakIntensity <= 0f || hitPeakIntensity > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(hitPeakIntensity), hitPeakIntensity, "must be in (0, 1]");
            }

            if (!IsFinite(fullHitDamage) || fullHitDamage <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fullHitDamage), fullHitDamage, "must be a finite number > 0");
            }

            if (!IsFinite(hitFadeSeconds) || hitFadeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(hitFadeSeconds), hitFadeSeconds, "must be a finite number > 0");
            }

            if (!IsFinite(lowHealthThreshold) || lowHealthThreshold <= 0f || lowHealthThreshold >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lowHealthThreshold), lowHealthThreshold, "must be in (0, 1)");
            }

            if (!IsFinite(lowHealthMinIntensity) || lowHealthMinIntensity < 0f || lowHealthMinIntensity > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lowHealthMinIntensity), lowHealthMinIntensity, "must be in [0, 1]");
            }

            if (!IsFinite(lowHealthMaxIntensity) || lowHealthMaxIntensity < lowHealthMinIntensity || lowHealthMaxIntensity > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lowHealthMaxIntensity), lowHealthMaxIntensity, "must be in [lowHealthMinIntensity, 1]");
            }

            HitPeakIntensity = hitPeakIntensity;
            FullHitDamage = fullHitDamage;
            HitFadeSeconds = hitFadeSeconds;
            LowHealthThreshold = lowHealthThreshold;
            LowHealthMinIntensity = lowHealthMinIntensity;
            LowHealthMaxIntensity = lowHealthMaxIntensity;
        }

        public float HitPeakIntensity { get; }

        public float FullHitDamage { get; }

        public float HitFadeSeconds { get; }

        public float LowHealthThreshold { get; }

        public float LowHealthMinIntensity { get; }

        public float LowHealthMaxIntensity { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// 被弾の画面効果 (画面端の赤) の強さと低体力の状態を、時間と体力から求める。
    /// 被弾 (<see cref="Hit"/>) するとダメージに応じた強さになり、<see cref="DamageFeedbackSettings.HitFadeSeconds"/> 秒かけて直線で 0 に戻る。
    /// 体力の割合 (<see cref="SetHealth"/>) がしきい値以下の間は、低いほど強い一定の強さを足す。
    /// 画面に出す強さ (<see cref="Intensity"/>) は両方の大きい方。
    /// </summary>
    public sealed class DamageFeedback
    {
        private float _hitStrength;
        private float _hitElapsed;
        private float? _healthFraction;

        /// <exception cref="ArgumentNullException">settings が null。</exception>
        public DamageFeedback(DamageFeedbackSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public DamageFeedbackSettings Settings { get; }

        /// <summary>しきい値以下の体力でいるか。</summary>
        public bool IsLowHealth { get; private set; }

        /// <summary>最後に渡された体力の割合。渡されていなければ null。</summary>
        public float? HealthFraction => _healthFraction;

        /// <summary>被弾による今の強さ ([0, 1])。</summary>
        public float HitIntensity
        {
            get
            {
                if (_hitStrength <= 0f)
                {
                    return 0f;
                }

                var remaining = 1f - (_hitElapsed / Settings.HitFadeSeconds);
                return remaining > 0f ? _hitStrength * remaining : 0f;
            }
        }

        /// <summary>低体力による今の強さ ([0, 1])。低体力でなければ 0。</summary>
        public float LowHealthIntensity
        {
            get
            {
                if (!IsLowHealth || !_healthFraction.HasValue)
                {
                    return 0f;
                }

                var depth = 1f - (_healthFraction.Value / Settings.LowHealthThreshold);
                depth = Math.Max(0f, Math.Min(1f, depth));
                return Settings.LowHealthMinIntensity + ((Settings.LowHealthMaxIntensity - Settings.LowHealthMinIntensity) * depth);
            }
        }

        /// <summary>画面に出す強さ ([0, 1])。<see cref="HitIntensity"/> と <see cref="LowHealthIntensity"/> の大きい方。</summary>
        public float Intensity => Math.Max(HitIntensity, LowHealthIntensity);

        /// <summary>
        /// amount のダメージを受けた。強さを amount / <see cref="DamageFeedbackSettings.FullHitDamage"/> (上限 1) に
        /// <see cref="DamageFeedbackSettings.HitPeakIntensity"/> を掛けたものにし、薄くなる時間を最初からにする。
        /// 薄くなりかけの前の被弾の方が強ければ、その強さで最初から薄くする (続けて撃たれて弱まらない)。
        /// 0 以下のダメージでは何もせず false。
        /// </summary>
        public bool Hit(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var strength = Settings.HitPeakIntensity * Math.Min(1f, amount / Settings.FullHitDamage);
            _hitStrength = Math.Max(strength, HitIntensity);
            _hitElapsed = 0f;
            return true;
        }

        /// <summary>
        /// 最大体力に対する今の体力の割合を渡し、低体力の状態の変わり方を返す。null は体力が無い (HUD の仮の表示) とする。
        /// 0 より大きくしきい値以下なら低体力。範囲外の値は [0, 1] に収め、NaN は null と同じに扱う。
        /// </summary>
        public LowHealthChange SetHealth(float? fraction)
        {
            if (fraction.HasValue && float.IsNaN(fraction.Value))
            {
                fraction = null;
            }

            _healthFraction = fraction.HasValue ? Math.Max(0f, Math.Min(1f, fraction.Value)) : (float?)null;
            var low = _healthFraction.HasValue && _healthFraction.Value > 0f && _healthFraction.Value <= Settings.LowHealthThreshold;
            if (low == IsLowHealth)
            {
                return LowHealthChange.None;
            }

            IsLowHealth = low;
            if (low)
            {
                return LowHealthChange.Started;
            }

            return _healthFraction.HasValue && _healthFraction.Value > 0f ? LowHealthChange.Recovered : LowHealthChange.Cleared;
        }

        /// <summary>deltaSeconds 秒進める。0 以下か有限でない値では進めない。</summary>
        public void Tick(float deltaSeconds)
        {
            if (_hitStrength <= 0f || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _hitElapsed += deltaSeconds;
            if (_hitElapsed >= Settings.HitFadeSeconds)
            {
                _hitStrength = 0f;
                _hitElapsed = 0f;
            }
        }

        /// <summary>被弾の強さを消し、低体力でない状態に戻す (体力の割合も忘れる)。</summary>
        public void Reset()
        {
            _hitStrength = 0f;
            _hitElapsed = 0f;
            _healthFraction = null;
            IsLowHealth = false;
        }
    }
}
