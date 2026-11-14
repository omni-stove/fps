using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 人型のリグには手に持つ銃が無いので、プレイヤーの三人称のモック (PlayerMockBody) と同じ寸法・構えの箱を本体の子に置く。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    public sealed class EnemyWeaponView : MonoBehaviour
    {
        public const string RootName = "EnemyGun";
        public const string MuzzleName = "Muzzle";
        public const string ModelName = "Model";

        [SerializeField, Tooltip("銃の見た目。原点が握り、前が +Z。子孫に Muzzle という名前の物があれば銃口に使う。当たり判定は外す。空なら仮の箱を作る")]
        private GameObject _modelPrefab;

        private EnemyAgent _enemy;
        private Damageable _damageable;
        private WeaponPresentation _presentation;
        private Transform _root;
        private Transform _muzzle;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Material _material;
        private float _kick;
        private MockPartPose _pose;
        private bool _hasPose;

        public Transform Root => _root;
        public Transform Muzzle => _muzzle;
        public bool IsBuilt => _root != null;
        public bool IsShown => _root != null && _root.gameObject.activeSelf;
        public float Kick => _kick;

        public GameObject ModelPrefab
        {
            get => _modelPrefab;
            set => _modelPrefab = value;
        }

        private void Awake()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            _enemy = GetComponent<EnemyAgent>();
            _damageable = GetComponent<Damageable>();
        }

        private void OnEnable()
        {
            if (_enemy != null)
            {
                _enemy.Fired += OnFired;
            }
        }

        private void OnDisable()
        {
            if (_enemy != null)
            {
                _enemy.Fired -= OnFired;
            }

            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_presentation != null && _presentation.Muzzle == _muzzle)
            {
                _presentation.Muzzle = null;
            }

            DestroySafely(_root != null ? _root.gameObject : null);
            DestroySafely(_material);
        }

        private void LateUpdate()
        {
            Refresh(Time.deltaTime);
        }

        public void Build()
        {
            if (IsBuilt)
            {
                return;
            }

            if (_enemy == null)
            {
                _enemy = GetComponent<EnemyAgent>();
                _damageable = GetComponent<Damageable>();
            }

            _root = new GameObject(RootName).transform;
            _root.SetParent(transform, false);

            if (_modelPrefab != null)
            {
                var model = EnemyVisualPrefab.Instantiate(_modelPrefab, _root);
                // 見せたときに Renderer を点け直すので、プレハブで消してある物は混ぜない。
                _renderers = Array.FindAll(model.GetComponentsInChildren<Renderer>(true), r => r.enabled);
                _muzzle = FindDescendant(model.transform, MuzzleName);
            }
            else
            {
                BuildPlaceholderModel();
            }

            if (_muzzle == null)
            {
                _muzzle = new GameObject(MuzzleName).transform;
                _muzzle.SetParent(_root, false);
                _muzzle.localPosition = new Vector3(0f, 0f, PlayerMockPose.GunSize.z / 2f);
            }

            _presentation = GetComponent<WeaponPresentation>();
            if (_presentation != null && _presentation.Muzzle == null)
            {
                _presentation.Muzzle = _muzzle;
            }

            _hasPose = false;
        }

        private void BuildPlaceholderModel()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = ModelName;
            // Destroy は次のフレームまで残り、その間に射撃や NavMeshAgent の判定に当たるので、すぐ消す。
            DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(_root, false);
            model.transform.localScale = PlayerMockPose.GunSize;
            var renderer = model.GetComponent<Renderer>();
            _material = new Material(renderer.sharedMaterial) { name = "EnemyGun", color = PlayerMockBody.GunColor };
            renderer.sharedMaterial = _material;
            _renderers = new[] { renderer };
        }

        public void Refresh(float deltaSeconds)
        {
            if (!IsBuilt)
            {
                Build();
            }

            var shown = _enemy.Weapon != null && (_damageable == null || !_damageable.IsDead);
            if (_root.gameObject.activeSelf != shown)
            {
                _root.gameObject.SetActive(shown);
                // DeathBodyPresenter は倒れたときに体の Renderer を消し、復活で戻すのは持ち主に任せている。
                foreach (var gunRenderer in _renderers)
                {
                    if (gunRenderer != null)
                    {
                        gunRenderer.enabled = true;
                    }
                }

                _hasPose = false;
            }

            if (!shown)
            {
                return;
            }

            _kick = PlayerMockPose.RecoverKick(_kick, deltaSeconds);
            var target = TargetPose();
            _pose = _hasPose ? Blend(_pose, target, PlayerMockPose.BlendFactor(deltaSeconds)) : target;
            _hasPose = true;

            var posed = PlayerMockPose.WithKick(_pose, _kick, PlayerMockPose.BodyKickDistance, PlayerMockPose.BodyKickDegrees);
            var feet = transform.InverseTransformPoint(_enemy.GroundPosition);
            _root.localPosition = feet + posed.Position;
            _root.localRotation = posed.Rotation;
        }

        private MockPartPose TargetPose()
        {
            var weapon = _enemy.Weapon;
            var scale = _enemy.IsCrouching && _enemy.EyeHeight > 0f ? Mathf.Min(1f, _enemy.CrouchEyeHeight / _enemy.EyeHeight) : 1f;
            if (weapon.IsReloading)
            {
                return new MockPartPose(ScaleHeight(PlayerMockPose.BodyReloadGunPosition, scale), Quaternion.Euler(PlayerMockPose.BodyReloadGunEuler));
            }

            var position = _enemy.IsAiming ? PlayerMockPose.BodyAimGunPosition : PlayerMockPose.BodyHipGunPosition;
            return new MockPartPose(ScaleHeight(position, scale), AimPitch());
        }

        // 体の向きは EnemyAgent が目標へ回すので、銃は上下だけを目標の狙う点へ合わせる。
        private Quaternion AimPitch()
        {
            var target = _enemy.Target;
            if (!_enemy.IsAiming || target == null || target.IsDead)
            {
                return Quaternion.identity;
            }

            var local = transform.InverseTransformDirection(target.AimPoint - _enemy.EyePosition);
            var horizontal = new Vector2(local.x, local.z).magnitude;
            if (horizontal < 1e-4f)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(-Mathf.Atan2(local.y, horizontal) * Mathf.Rad2Deg, 0f, 0f);
        }

        private void OnFired(Weapon _)
        {
            _kick = 1f;
        }

        private static Transform FindDescendant(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                var found = FindDescendant(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Vector3 ScaleHeight(Vector3 position, float scale)
        {
            return new Vector3(position.x, position.y * scale, position.z);
        }

        private static MockPartPose Blend(MockPartPose from, MockPartPose to, float t)
        {
            return new MockPartPose(Vector3.Lerp(from.Position, to.Position, t), Quaternion.Slerp(from.Rotation, to.Rotation, t));
        }

        private static void DestroySafely(UnityEngine.Object target)
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
