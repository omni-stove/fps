using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 見た目の当たり判定を外さないのは、身を隠せる固い物として置くため。使える範囲は見た目とは別のトリガーの球で決める。
    [DisallowMultipleComponent]
    public sealed class AmmoCrate : MonoBehaviour
    {
        public const string LogTag = "[AmmoCrate]";

        public const string InteractActionPath = InputActionPaths.Interact;

        public const float DefaultRadius = 1.6f;
        public const float MinRadius = 0.1f;
        public const float DefaultCooldownSeconds = 1f;
        public const string VisualName = "Visual";

        public static readonly Vector3 DefaultVisualSize = new Vector3(0.9f, 0.5f, 0.6f);

        // 拾う物 (回る小さな箱) と見分けが付くよう、くすんだ緑にする。
        public static readonly Color DefaultVisualColor = new Color(0.3f, 0.38f, 0.22f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("調整値。入れると下の範囲と使ってから次に使えるまでの秒数の代わりにこれの弾薬箱の欄を使う")]
        private PickupDefinition _definition;

        [SerializeField, Min(MinRadius), Tooltip("使える範囲の半径 (m)。足元から球が立つ")]
        private float _radius = DefaultRadius;

        [SerializeField, Min(0f), Tooltip("使ってから次に使えるまでの秒数")]
        private float _cooldownSeconds = DefaultCooldownSeconds;

        [SerializeField, Tooltip("箱の見た目。原点が底面の中心。空なら仮の箱を作る")]
        private GameObject _visualPrefab;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private float _readyAt;
        private InteractHintSlot _hint;

        public PickupDefinition Definition
        {
            get => _definition;
            set
            {
                if (value != null)
                {
                    value.ValidateAmmoCrate();
                }

                _definition = value;
                ApplyRadius();
            }
        }

        public float Radius
        {
            get => _radius;
            set
            {
                if (!IsValidRadius(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"radius must be at least {MinRadius} and finite");
                }

                _radius = value;
                ApplyRadius();
            }
        }

        public float CooldownSeconds
        {
            get => _cooldownSeconds;
            set
            {
                if (!IsValidCooldown(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "cooldown must be non-negative and finite");
                }

                _cooldownSeconds = value;
            }
        }

        public float EffectiveRadius =>
            _definition != null && IsValidRadius(_definition.AmmoCrateRadius) ? _definition.AmmoCrateRadius : _radius;

        public float EffectiveCooldownSeconds =>
            _definition != null && IsValidCooldown(_definition.AmmoCrateCooldownSeconds) ? _definition.AmmoCrateCooldownSeconds : _cooldownSeconds;

        public static bool IsValidRadius(float radius) => radius >= MinRadius && !float.IsInfinity(radius);

        public static bool IsValidCooldown(float seconds) => seconds >= 0f && !float.IsInfinity(seconds);

        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        public int InsideCount => _inside.Count;

        private void Awake()
        {
            if (_definition != null)
            {
                try
                {
                    _definition.ValidateAmmoCrate();
                }
                catch (ArgumentException e)
                {
                    // 置き場所の遮蔽物として残すため見た目と当たり判定は作り、使えないことだけを知らせる。
                    Debug.LogError($"{LogTag} '{name}' の設定が不正です: {e.Message}", this);
                    enabled = false;
                }
            }

            Build();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                AcquireInteractAction();
            }
        }

        private void OnDisable()
        {
            ReleaseInteractAction();
            _inside.Clear();
            HidePrompt();
        }

        private void Update()
        {
            UpdatePrompt();
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

        public void HandleEnter(Collider other)
        {
            if (other != null && other.GetComponentInParent<Damageable>() != null)
            {
                _inside.Add(other);
            }
        }

        public void HandleExit(Collider other)
        {
            if (other != null)
            {
                _inside.Remove(other);
            }
        }

        public bool Interact()
        {
            foreach (var collider in new List<Collider>(_inside))
            {
                var player = collider != null ? collider.GetComponentInParent<Damageable>() : null;
                if (player != null && player.IsLocalPlayer)
                {
                    return TryRefill(player, Time.time);
                }
            }

            return false;
        }

        public Damageable LocalPlayerInside()
        {
            foreach (var collider in _inside)
            {
                var player = collider != null ? collider.GetComponentInParent<Damageable>() : null;
                if (player != null && player.IsLocalPlayer)
                {
                    return player;
                }
            }

            return null;
        }

        public static bool TryDescribe(Damageable player, out AmmoCrateHint hint)
        {
            hint = default;
            if (player == null || player.IsDead || player.IsDowned)
            {
                return false;
            }

            var weapons = Pickup.WeaponsOf(player);
            if (weapons == null)
            {
                return false;
            }

            var room = false;
            foreach (var state in weapons.DescribeWeapons(out _))
            {
                room |= state.Room > 0;
            }

            hint = new AmmoCrateHint(room, InteractActionPath);
            return true;
        }

        public bool TryRefill(Damageable user, float now)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (now < _readyAt || user.IsDead || user.IsDowned)
            {
                return false;
            }

            var weapons = Pickup.WeaponsOf(user);
            if (weapons == null)
            {
                Debug.Log($"{LogTag} '{name}' user='{user.name}' has no weapons", this);
                return false;
            }

            var added = Refill(weapons);
            Debug.Log($"{LogTag} '{name}' user='{user.name}' added={added}", this);
            if (added <= 0)
            {
                return false;
            }

            _readyAt = now + EffectiveCooldownSeconds;
            return true;
        }

        public static int Refill(IWeaponHolder weapons)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            var added = 0;
            foreach (var state in weapons.DescribeWeapons(out _))
            {
                if (state.Room > 0)
                {
                    added += weapons.AddReserveAmmo(state.WeaponId, state.Room);
                }
            }

            return added;
        }

        // Awake でも呼ぶが、Edit Mode で置いたときにシーンへ見た目を残すには置いた側が呼ぶので公開する。
        public void Build()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            ApplyRadius();

            // 動かない箱でもトリガーの出入りを受け取れるよう、動かない剛体を付ける。
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;

            var visual = transform.Find(VisualName);
            if (visual == null)
            {
                visual = CreateVisual();
            }

            // 色の上書きはシーンに保存されないので、Edit Mode で置いた仮の箱にも組み立てるたびに付け直す。
            if (_visualPrefab == null)
            {
                PaintPlaceholder(visual);
            }
        }

        private void ApplyRadius()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                return;
            }

            var radius = EffectiveRadius;
            trigger.radius = radius;
            trigger.center = new Vector3(0f, radius, 0f);
        }

        private Transform CreateVisual()
        {
            if (_visualPrefab != null)
            {
                return Shooter.Runtime.VisualPrefab.Instantiate(_visualPrefab, transform, VisualName, stripColliders: false).transform;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = VisualName;
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, DefaultVisualSize.y / 2f, 0f);
            visual.transform.localScale = DefaultVisualSize;
            return visual.transform;
        }

        private static void PaintPlaceholder(Transform visual)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この箱だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, DefaultVisualColor);
            block.SetColor(ColorId, DefaultVisualColor);
            renderer.SetPropertyBlock(block);
        }

        private void AcquireInteractAction()
        {
            if (_interactAction != null)
            {
                return;
            }

            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(InteractActionPath) : null;
            if (action == null)
            {
                Debug.LogError($"{LogTag} '{InteractActionPath}' が Input Actions に無いため、'{name}' は使えません (Project Settings > Input System Package)。", this);
                return;
            }

            _interactAction = action;
            _interactAction.performed += OnInteractPerformed;

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
        {
            if (_interactAction == null)
            {
                return;
            }

            _interactAction.performed -= OnInteractPerformed;
            if (_enabledInteractAction)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
            _interactAction = null;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            Interact();
        }

        internal void UpdatePrompt()
        {
            var player = _inside.Count > 0 ? LocalPlayerInside() : null;
            if (player == null || !TryDescribe(player, out var hint))
            {
                HidePrompt();
                return;
            }

            (_hint ?? (_hint = new InteractHintSlot(this))).Show(hint, hint.HasRoom && Time.time >= _readyAt);
        }

        private void HidePrompt()
        {
            _hint?.Clear();
        }
    }
}
