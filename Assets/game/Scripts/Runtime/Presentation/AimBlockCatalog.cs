using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "AimBlockCatalog", menuName = "Shooter/Aim Block Catalog")]
    public sealed class AimBlockCatalog : ScriptableObject
    {
        public const string CatalogName = "AimBlock";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("目からの射線が手前の物に遮られているとき、照準の見た目を変えて遮られた点に印を出すか")]
        private bool _enabled = AimBlockSettings.DefaultEnabled;

        [SerializeField, Min(0f), Tooltip("狙う点からこれより手前 (m) で当たったときだけ遮られたとみなす")]
        private float _margin = AimBlockSettings.DefaultMargin;

        [SerializeField, Tooltip("遮られた点に出す印の画像。空なら × の形をコードで作る")]
        private Texture2D _marker;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public Texture2D Marker
        {
            get => _marker;
            set => _marker = value;
        }

        public static AimBlockCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.AimBlock);
            return fromContent != null ? fromContent : Resources.Load<AimBlockCatalog>(ResourcePath);
        }

        public void SetSettings(AimBlockSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _enabled = settings.Enabled;
            _margin = settings.Margin;
        }

        public AimBlockSettings ToSettings()
        {
            return new AimBlockSettings(_enabled, _margin);
        }

        public void ResetToDefaults()
        {
            SetSettings(AimBlockSettings.Default);
            _marker = null;
        }

        public void Validate()
        {
            try
            {
                ToSettings();
            }
            catch (ArgumentOutOfRangeException e)
            {
                var source = string.IsNullOrEmpty(name) ? "(unnamed aim block catalog)" : name;
                throw new PresentationCatalogException($"aim block catalog '{source}': {e.Message}");
            }
        }
    }
}
