using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 被弾の画面効果と低体力の演出 (<c>DamageVignetteView</c>) の素材と値。
    /// <see cref="PresentationCatalog"/> の規約どおり Resources の <see cref="ResourcePath"/> に置き、<see cref="Load"/> で読む。
    /// 中身は Fps.Editor.DamageFeedbackAssetStep が生成した仮の素材で、本物の素材に替えるときはこのアセットの参照を差し替える。
    /// 素材の枠はどれも空でよく、空の枠の演出だけを出さない (画像が無ければ端を赤くせず、音が無ければ鳴らさない)。
    /// </summary>
    [CreateAssetMenu(fileName = "DamageFeedbackCatalog", menuName = "Fps/Damage Feedback Catalog")]
    public sealed class DamageFeedbackCatalog : ScriptableObject
    {
        /// <summary><see cref="PresentationCatalog.CatalogResourcePath"/> に渡す演出の名前。</summary>
        public const string CatalogName = "DamageFeedback";

        /// <summary>Resources からのパス (Presentation/DamageFeedbackCatalog)。</summary>
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

        /// <summary>画面に重ねる画像。無ければ null (端を赤くしない)。</summary>
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

        /// <summary>低体力の間くり返し鳴らす心音。無ければ null。</summary>
        public AudioClip Heartbeat
        {
            get => _heartbeat;
            set => _heartbeat = value;
        }

        /// <summary>心音の音量 ([0, 1])。</summary>
        public float HeartbeatVolume
        {
            get => _heartbeatVolume;
            set => _heartbeatVolume = Mathf.Clamp01(value);
        }

        /// <summary>回復したときの息切れの音。無ければ null。</summary>
        public AudioClip Breath
        {
            get => _breath;
            set => _breath = value;
        }

        /// <summary>息切れの音の音量 ([0, 1])。</summary>
        public float BreathVolume
        {
            get => _breathVolume;
            set => _breathVolume = Mathf.Clamp01(value);
        }

        /// <summary>入口 (<see cref="GameContent"/>) のもの、無ければ Resources の <see cref="ResourcePath"/> から読む。どちらも無ければ null。</summary>
        public static DamageFeedbackCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.DamageFeedback);
            return fromContent != null ? fromContent : Resources.Load<DamageFeedbackCatalog>(ResourcePath);
        }

        /// <summary>強さとしきい値をまとめて差し替える。値は <see cref="DamageFeedbackSettings"/> の規則で確かめてから書く。</summary>
        /// <exception cref="ArgumentNullException">settings が null。</exception>
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

        /// <summary>強さとしきい値を <see cref="DamageFeedbackSettings"/> にして返す。</summary>
        /// <exception cref="ArgumentOutOfRangeException">インスペクターや手で書き換えた値が範囲外。</exception>
        public DamageFeedbackSettings ToSettings()
        {
            return new DamageFeedbackSettings(
                _hitPeakIntensity, _fullHitDamage, _hitFadeSeconds, _lowHealthThreshold, _lowHealthMinIntensity, _lowHealthMaxIntensity);
        }

        /// <summary>
        /// 仮素材が必ず埋める枠 (画像・心音・息切れ) が埋まっていて、強さとしきい値が範囲内であることを確かめる。
        /// 足りないものはすべて並べて 1 つの例外にする。
        /// </summary>
        /// <exception cref="PresentationCatalogException">どれかが欠けているか範囲外。</exception>
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
