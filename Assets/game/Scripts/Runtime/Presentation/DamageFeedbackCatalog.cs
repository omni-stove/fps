using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DamageFeedbackCatalog", menuName = "Shooter/Damage Feedback Catalog")]
    public sealed class DamageFeedbackCatalog : ScriptableObject
    {
        public const string CatalogName = "DamageFeedback";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("画面全体に引き伸ばして重ねる画像。端が赤く中央が透明なもの")]
        private Texture2D _vignette;

        [SerializeField, Tooltip("画像に掛ける色。画像の色をそのまま出すなら白")]
        private Color _tint = Color.white;

        [SerializeField, Range(0.01f, 1f), Tooltip("被弾した瞬間の強さの上限")]
        private float _hitPeakIntensity = 0.8f;

        [SerializeField, Min(1f), Tooltip("このダメージ以上で強さが上限になる。少ないダメージは比例して弱い")]
        private float _fullHitDamage = 50f;

        [SerializeField, Min(0.05f), Tooltip("被弾の赤が消えるまでの秒数")]
        private float _hitFadeSeconds = 0.8f;

        [SerializeField, Range(0.01f, 0.99f), Tooltip("低体力と見なす、最大体力に対する割合 (この割合以下)")]
        private float _lowHealthThreshold = HudFormat.LowHealthRatio;

        [SerializeField, Range(0f, 1f), Tooltip("しきい値ちょうどのときの低体力の赤の強さ")]
        private float _lowHealthMinIntensity = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip("体力が 0 に近いときの低体力の赤の強さ (最小より小さくしない)")]
        private float _lowHealthMaxIntensity = 0.7f;

        [SerializeField, Tooltip("低体力の間くり返し鳴らす心音")]
        private AudioClip _heartbeat;

        [SerializeField, Range(0f, 1f), Tooltip("心音の音量")]
        private float _heartbeatVolume = 0.6f;

        [SerializeField, Tooltip("低体力から回復したときに 1 度鳴らす息切れの音")]
        private AudioClip _breath;

        [SerializeField, Range(0f, 1f), Tooltip("息切れの音の音量")]
        private float _breathVolume = 0.5f;

        public Texture2D Vignette
        {
            get => _vignette;
            set => _vignette = value;
        }

        public Color Tint
        {
            get => _tint;
            set => _tint = value;
        }

        public AudioClip Heartbeat
        {
            get => _heartbeat;
            set => _heartbeat = value;
        }

        public float HeartbeatVolume
        {
            get => _heartbeatVolume;
            set => _heartbeatVolume = Mathf.Clamp01(value);
        }

        public AudioClip Breath
        {
            get => _breath;
            set => _breath = value;
        }

        public float BreathVolume
        {
            get => _breathVolume;
            set => _breathVolume = Mathf.Clamp01(value);
        }

        public static DamageFeedbackCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.DamageFeedback);
            return fromContent != null ? fromContent : Resources.Load<DamageFeedbackCatalog>(ResourcePath);
        }

        public void SetSettings(DamageFeedbackSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _hitPeakIntensity = settings.HitPeakIntensity;
            _fullHitDamage = settings.FullHitDamage;
            _hitFadeSeconds = settings.HitFadeSeconds;
            _lowHealthThreshold = settings.LowHealthThreshold;
            _lowHealthMinIntensity = settings.LowHealthMinIntensity;
            _lowHealthMaxIntensity = settings.LowHealthMaxIntensity;
        }

        public DamageFeedbackSettings ToSettings()
        {
            return new DamageFeedbackSettings(
                _hitPeakIntensity, _fullHitDamage, _hitFadeSeconds, _lowHealthThreshold, _lowHealthMinIntensity, _lowHealthMaxIntensity);
        }

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed damage feedback catalog)" : name;
            var problems = new List<string>();

            // 破棄されたアセットや参照切れも拾うため、Unity の == null で比べる。
            if (_vignette == null)
            {
                problems.Add("vignette is missing");
            }

            if (_heartbeat == null)
            {
                problems.Add("heartbeat is missing");
            }

            if (_breath == null)
            {
                problems.Add("breath is missing");
            }

            try
            {
                ToSettings();
            }
            catch (ArgumentOutOfRangeException e)
            {
                problems.Add(e.Message);
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"damage feedback catalog '{source}': {string.Join("; ", problems)}");
            }
        }
    }
}
