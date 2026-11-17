using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DamageNumberCatalog", menuName = "Shooter/Damage Number Catalog")]
    public sealed class DamageNumberCatalog : ScriptableObject
    {
        public const string CatalogName = "DamageNumber";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        public const bool DefaultEnabled = false;

        [SerializeField, Tooltip("当てたときにダメージの数値を出すか")]
        private bool _enabled = DefaultEnabled;

        [SerializeField, Min(0.01f), Tooltip("1 つの数値を出しておく秒数")]
        private float _lifetimeSeconds = DamageNumberSettings.DefaultLifetimeSeconds;

        [SerializeField, Min(0f), Tooltip("出している間に上へ動く量 (HUD の基準解像度の px)")]
        private float _riseDistance = DamageNumberSettings.DefaultRiseDistance;

        [SerializeField, Range(0f, 1f), Tooltip("消える前の、薄くしていく時間の割合")]
        private float _fadeFraction = DamageNumberSettings.DefaultFadeFraction;

        [SerializeField, Min(1), Tooltip("同時に出す数の上限。超えたら古いものから消す")]
        private int _maxCount = DamageNumberSettings.DefaultMaxCount;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static DamageNumberCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.DamageNumbers);
            return fromContent != null ? fromContent : Resources.Load<DamageNumberCatalog>(ResourcePath);
        }

        public void SetSettings(DamageNumberSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _lifetimeSeconds = settings.LifetimeSeconds;
            _riseDistance = settings.RiseDistance;
            _fadeFraction = settings.FadeFraction;
            _maxCount = settings.MaxCount;
        }

        public DamageNumberSettings ToSettings()
        {
            return new DamageNumberSettings(_lifetimeSeconds, _riseDistance, _fadeFraction, _maxCount);
        }

        public void ResetToDefaults()
        {
            _enabled = DefaultEnabled;
            SetSettings(DamageNumberSettings.Default);
        }

        public void Validate()
        {
            try
            {
                ToSettings();
            }
            catch (ArgumentOutOfRangeException e)
            {
                var source = string.IsNullOrEmpty(name) ? "(unnamed damage number catalog)" : name;
                throw new PresentationCatalogException($"damage number catalog '{source}': {e.Message}");
            }
        }
    }
}
