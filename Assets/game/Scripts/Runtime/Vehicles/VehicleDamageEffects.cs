using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 乗り物の段階 (Vehicle.DamageStageChanged) を見るだけの部品に分けたのは、ゲームが演出を丸ごと別の部品に替えられるようにするため。
    [DisallowMultipleComponent]
    public sealed class VehicleDamageEffects : MonoBehaviour
    {
        public static readonly Color DefaultWreckTint = new Color(0.12f, 0.11f, 0.1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("演出を出す乗り物。空なら親階層から探す")]
        private Vehicle _vehicle;

        [SerializeField, Tooltip("煙の段階から出す演出のプレハブ。空なら出さない")]
        private GameObject _smokePrefab;

        [SerializeField, Tooltip("燃えている段階で出す演出のプレハブ。空なら出さない")]
        private GameObject _firePrefab;

        [SerializeField, Tooltip("爆発した後に置く残骸のプレハブ。置くと乗り物の見た目を隠す。空なら乗り物の見た目を焦げた色にする")]
        private GameObject _wreckPrefab;

        [SerializeField, Tooltip("煙と火を出す位置。空なら乗り物の原点")]
        private Transform _effectAnchor;

        [SerializeField, Tooltip("残骸のプレハブが無いときに、乗り物の見た目に塗る色")]
        private Color _wreckTint = DefaultWreckTint;

        private Vehicle _attached;
        private GameObject _smoke;
        private GameObject _fire;
        private GameObject _wreck;
        private bool _wreckShown;

        public Vehicle Vehicle => _attached;

        public GameObject SmokeInstance => _smoke;

        public GameObject FireInstance => _fire;

        public GameObject WreckInstance => _wreck;

        public bool IsWreckShown => _wreckShown;

        private Transform Root => _attached != null ? _attached.transform : transform;

        public GameObject SmokePrefab
        {
            get => _smokePrefab;
            set => _smokePrefab = value;
        }

        public GameObject FirePrefab
        {
            get => _firePrefab;
            set => _firePrefab = value;
        }

        public GameObject WreckPrefab
        {
            get => _wreckPrefab;
            set => _wreckPrefab = value;
        }

        public Transform EffectAnchor
        {
            get => _effectAnchor;
            set => _effectAnchor = value;
        }

        private void OnEnable()
        {
            var vehicle = _vehicle != null ? _vehicle : GetComponentInParent<Vehicle>();
            if (vehicle == null)
            {
                Debug.LogWarning($"{Vehicle.LogTag} '{name}' の VehicleDamageEffects に乗り物がありません。", this);
                return;
            }

            Attach(vehicle);
        }

        private void OnDisable()
        {
            Detach();
        }

        // Edit Mode では OnEnable が走らないので、組み立てる側やテストが直接つなげるようにする。
        public void Attach(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            Detach();
            _vehicle = vehicle;
            _attached = vehicle;
            _attached.DamageStageChanged += OnStageChanged;
            Show(vehicle.DamageStage);
        }

        public void Detach()
        {
            if (_attached != null)
            {
                _attached.DamageStageChanged -= OnStageChanged;
            }

            _attached = null;
        }

        public void Show(VehicleDamageStage stage)
        {
            if (!Enum.IsDefined(typeof(VehicleDamageStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage is not a defined VehicleDamageStage");
            }

            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            _smoke = Toggle(_smoke, _smokePrefab, stage >= VehicleDamageStage.Smoking);
            _fire = Toggle(_fire, _firePrefab, stage == VehicleDamageStage.Burning);
            if (stage == VehicleDamageStage.Destroyed && !_wreckShown)
            {
                ShowWreck();
            }
        }

        private void OnStageChanged(Vehicle vehicle, VehicleDamageStage stage)
        {
            Show(stage);
        }

        private GameObject Toggle(GameObject instance, GameObject prefab, bool wanted)
        {
            if (!wanted || prefab == null)
            {
                DestroySafely(instance);
                return null;
            }

            if (instance != null)
            {
                return instance;
            }

            var anchor = _effectAnchor != null ? _effectAnchor : Root;
            return Instantiate(prefab, anchor.position, anchor.rotation, anchor);
        }

        private void ShowWreck()
        {
            _wreckShown = true;
            var root = Root;
            var renderers = OwnRenderers(root);
            if (_wreckPrefab != null)
            {
                _wreck = Instantiate(_wreckPrefab, root.position, root.rotation, root);
                foreach (var renderer in renderers)
                {
                    renderer.enabled = false;
                }

                return;
            }

            // 共有マテリアルを塗り替えると同じマテリアルのほかの物まで焦げるので、この乗り物の Renderer だけ上書きで塗る。
            var block = new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, _wreckTint);
                block.SetColor(ColorId, _wreckTint);
                renderer.SetPropertyBlock(block);
            }
        }

        private List<Renderer> OwnRenderers(Transform root)
        {
            var result = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.enabled && !IsUnder(renderer.transform, _smoke) && !IsUnder(renderer.transform, _fire) && !IsUnder(renderer.transform, _wreck))
                {
                    result.Add(renderer);
                }
            }

            return result;
        }

        private static bool IsUnder(Transform child, GameObject instance)
        {
            return instance != null && child.IsChildOf(instance.transform);
        }

        private static void DestroySafely(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
