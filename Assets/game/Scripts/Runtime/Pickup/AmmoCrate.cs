using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 置きっぱなしの弾薬箱。範囲にいるこの画面のプレイヤー (<see cref="Damageable.IsLocalPlayer"/>) が Interact (キーボードは E の長押し) をすると、
    /// 持ち武器すべての予備弾を上限まで足す (<see cref="Refill"/>)。拾っても消えず、<see cref="CooldownSeconds"/> 経てば何度でも使える。
    /// 足したかどうかと数は <c>[AmmoCrate]</c> のログに出す。
    /// 範囲にプレイヤーがいる間は、HUD の <see cref="IInteractPrompt"/> に使い方 (<see cref="PromptKey"/>) か満タン (<see cref="FullKey"/>) を出す。
    /// </summary>
    /// <remarks>
    /// 見た目は <see cref="VisualPrefab"/>、空なら仮の箱を作る。どちらも固い物として当たり判定を残す (身を隠せる物として置くため)。
    /// 使える範囲は見た目とは別のトリガーの球 (<see cref="Radius"/>) で決める。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AmmoCrate : MonoBehaviour
    {
        public const string LogTag = "[AmmoCrate]";

        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        public const float DefaultRadius = 1.6f;
        public const float DefaultCooldownSeconds = 1f;
        public const string VisualName = "Visual";

        /// <summary>使える間の案内の文のキー。{0} は Interact のキー。</summary>
        public const string PromptKey = "hud.ammoCrate.prompt";

        /// <summary>どの武器も満タンの間の案内の文のキー。</summary>
        public const string FullKey = "hud.ammoCrate.full";

        /// <summary>仮の箱の大きさ (m)。</summary>
        public static readonly Vector3 DefaultVisualSize = new Vector3(0.9f, 0.5f, 0.6f);

        /// <summary>仮の箱の色。拾う物 (回る小さな箱) と見分けが付くよう、くすんだ緑にする。</summary>
        public static readonly Color DefaultVisualColor = new Color(0.3f, 0.38f, 0.22f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Min(0.1f), Tooltip("使える範囲の半径 (m)。足元から球が立つ")]
        private float _radius = DefaultRadius;

        [SerializeField, Min(0f), Tooltip("使ってから次に使えるまでの秒数")]
        private float _cooldownSeconds = DefaultCooldownSeconds;

        [SerializeField, Tooltip("箱の見た目。原点が底面の中心。空なら仮の箱を作る")]
        private GameObject _visualPrefab;

        private readonly HashSet<Collider> _inside = new HashSet<Collider>();
        private readonly InteractKeyLabel _keyLabel = new InteractKeyLabel();
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private float _readyAt;
        private IHud _hud;

        public float Radius
        {
            get => _radius;
            set
            {
                if (!(value >= 0.1f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "radius must be at least 0.1 and finite");
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
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "cooldown must be non-negative and finite");
                }

                _cooldownSeconds = value;
            }
        }

        /// <summary>見た目。次に組み立てるとき (Awake) から使う。</summary>
        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        /// <summary>範囲にいる人のコライダーの数。</summary>
        public int InsideCount => _inside.Count;

        private void Awake()
        {
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
            if (other != null && other.GetComponentInParent<Damageable>() != null)
            {
                _inside.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null)
            {
                _inside.Remove(other);
            }
        }

        /// <summary>
        /// 範囲にいるこの画面のプレイヤーに補給する。足したら true。使えるまでの時間が残っている・範囲にプレイヤーがいない・
        /// 倒れている・どの武器も満タンなら false。Interact の入力から呼ばれ、コードからも呼べる。
        /// </summary>
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

        /// <summary>範囲にいるこの画面のプレイヤー。いなければ null。</summary>
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

        /// <summary>
        /// player に出す案内。使えれば Interact のキー (keyLabel) を入れた <see cref="PromptKey"/>、どの武器も満タンなら <see cref="FullKey"/>。
        /// 倒れている・武器を持たないなら案内は出さない (false)。
        /// </summary>
        public static bool TryDescribe(Damageable player, string keyLabel, out LocalizedText text)
        {
            text = default;
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

            text = room ? new LocalizedText(PromptKey, keyLabel ?? string.Empty) : new LocalizedText(FullKey);
            return true;
        }

        /// <summary>user の持ち武器に補給する。now は使えるまでの時間を測る時刻。足したら true。</summary>
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

            _readyAt = now + _cooldownSeconds;
            return true;
        }

        /// <summary>weapons の持ち武器すべての予備弾を上限まで足し、足した数の合計を返す。予備弾が尽きない武器には足さない。</summary>
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

        /// <summary>
        /// トリガーの球・動かない剛体・見た目を、無ければ作る。何度呼んでもよい。Awake でも呼ぶが、Edit Mode で置いたときに
        /// シーンへ見た目を残すには置いた側が呼ぶ。
        /// </summary>
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

            trigger.radius = _radius;
            trigger.center = new Vector3(0f, _radius, 0f);
        }

        private Transform CreateVisual()
        {
            if (_visualPrefab != null)
            {
                return Fps.Runtime.VisualPrefab.Instantiate(_visualPrefab, transform, VisualName, stripColliders: false).transform;
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

        private void UpdatePrompt()
        {
            var player = _inside.Count > 0 ? LocalPlayerInside() : null;
            if (player == null)
            {
                HidePrompt();
                return;
            }

            // HUD はプレイヤーが範囲に入ったときにだけ探す (シーン全体を見るので毎フレームは探さない)。
            if (!GameUi.IsAlive(_hud))
            {
                _hud = GameUi.FindHud();
            }

            var prompt = GameUi.IsAlive(_hud) ? _hud.InteractPrompt : null;
            if (prompt == null)
            {
                return;
            }

            if (!TryDescribe(player, _keyLabel.For(_interactAction), out var text))
            {
                prompt.Hide(this);
                return;
            }

            prompt.Show(this, text);
        }

        private void HidePrompt()
        {
            if (GameUi.IsAlive(_hud))
            {
                _hud.InteractPrompt?.Hide(this);
            }
        }
    }
}
