using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 弾と移動を遮らないよう、当たり判定は持たせない。
    [DisallowMultipleComponent]
    public sealed class BombObject : MonoBehaviour
    {
        public const float CarryHeight = 1.1f;

        public const float CarryBack = 0.35f;

        // 運ばれている爆弾は落ちている爆弾より暗くし、地面に落ちた爆弾を見つけやすくする。
        public const float CarriedBrightness = 0.85f;

        private Transform _carrier;
        private Renderer _renderer;
        private Material _material;
        private BombState _shownState = BombState.Carried;
        private bool _followsSettings;

        public Transform Carrier => _carrier;

        public bool IsVisible => Renderer != null && Renderer.enabled;

        public BombState ShownState => _shownState;

        public static Color ColorOf(BombState state, TeamPalette palette)
        {
            if (palette == null)
            {
                throw new System.ArgumentNullException(nameof(palette));
            }

            switch (state)
            {
                case BombState.Carried:
                    var objective = TeamColors.ToColor(palette.Objective);
                    return new Color(objective.r * CarriedBrightness, objective.g * CarriedBrightness, objective.b * CarriedBrightness, objective.a);
                case BombState.Dropped:
                    return TeamColors.ToColor(palette.Objective);
                case BombState.Planted:
                case BombState.Detonated:
                    return TeamColors.ToColor(palette.Danger);
                case BombState.Defused:
                    return TeamColors.ToColor(palette.Safe);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(state), state, "unknown bomb state");
            }
        }

        private Renderer Renderer
        {
            get
            {
                if (_renderer == null)
                {
                    _renderer = GetComponent<Renderer>();
                }

                return _renderer;
            }
        }

        public void ShowCarried(Transform carrier)
        {
            _carrier = carrier;
            if (carrier == null)
            {
                Hide();
                return;
            }

            Follow();
            Paint(BombState.Carried, true);
        }

        public void ShowOnGround(Vector3 ground, BombState state)
        {
            _carrier = null;
            transform.position = ground + (Vector3.up * HalfHeight());
            switch (state)
            {
                case BombState.Planted:
                case BombState.Defused:
                    Paint(state, true);
                    break;
                case BombState.Detonated:
                    Paint(state, false);
                    break;
                default:
                    Paint(BombState.Dropped, true);
                    break;
            }
        }

        public void Hide()
        {
            _carrier = null;
            Paint(BombState.Carried, false);
        }

        private void LateUpdate()
        {
            if (_carrier != null)
            {
                Follow();
            }
        }

        private void OnDestroy()
        {
            if (_followsSettings)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsSettings = false;
            }

            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }

                _material = null;
            }
        }

        private void Follow()
        {
            var forward = _carrier.forward;
            forward.y = 0f;
            var back = forward.sqrMagnitude > 1e-6f ? -forward.normalized : Vector3.zero;
            transform.SetPositionAndRotation(
                _carrier.position + (Vector3.up * (CarryHeight - HalfHeight())) + (back * CarryBack),
                Quaternion.LookRotation(back.sqrMagnitude > 0f ? -back : Vector3.forward, Vector3.up));
        }

        // 見た目は 1m 四方の立方体を拡縮したものなので、高さの半分は拡縮の y の半分。
        private float HalfHeight()
        {
            return transform.lossyScale.y * 0.5f;
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            ApplyColor();
        }

        private void Paint(BombState state, bool visible)
        {
            _shownState = state;
            var renderer = Renderer;
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = visible;
            if (!_followsSettings)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsSettings = true;
            }

            ApplyColor();
        }

        private void ApplyColor()
        {
            var renderer = Renderer;
            if (renderer == null)
            {
                return;
            }

            // 共有のマテリアルを塗り替えると他のプリミティブまで色が変わるので、この爆弾用に複製する。
            if (_material == null)
            {
                if (renderer.sharedMaterial == null)
                {
                    return;
                }

                _material = new Material(renderer.sharedMaterial) { name = name };
                renderer.sharedMaterial = _material;
            }

            _material.color = ColorOf(_shownState, TeamColors.Current);
        }
    }
}
