using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    /// <summary>
    /// 自機の今のアクション (<see cref="PlayerActionReader.Current"/>) を、プリミティブで作った仮の体で見せる自機モック。
    /// <list type="bullet">
    /// <item>三人称: 胴 (カプセル)・頭 (球)・銃 (細長い箱) を本体の子に置き、<see cref="PlayerMockPose"/> の姿勢へ寄せる。</item>
    /// <item>一人称: 体は影だけにし、カメラの子に置いた銃 (ビューモデル) だけを右下に見せる。</item>
    /// </list>
    /// <see cref="CharacterAnimator"/> が人型のリグを置いたら体は出さず (<see cref="ShowsBody"/>)、一人称の銃だけを受け持つ。
    /// 部品にはコライダーを付けない (当たり判定は CharacterController のまま)。部品と重ならないよう、本体自身のレンダラー
    /// (プリミティブのカプセル) は組み立てている間だけ消し、外すときに元へ戻す。
    /// 読み元は <see cref="GameBootstrap"/> が <see cref="Bind"/> で渡す。渡されないまま Awake を迎えたら同じ GameObject から探し、
    /// 無ければエラーを出して自分を無効にする。部品は Play 中の最初の LateUpdate で組み立てる (<see cref="Build"/>)。
    /// </summary>
    [DisallowMultipleComponent]
    // PlayerActionReader (100) がそのフレームの状態を組み立て、ViewRig がカメラを置き終えてから姿勢を決める。
    [DefaultExecutionOrder(110)]
    public sealed class PlayerMockBody : MonoBehaviour
    {
        public const string LogTag = "[MockBody]";

        public const string BodyRootName = "MockBody";
        public const string ViewModelRootName = "MockViewModel";

        /// <summary>体の色。敵 (既定の白いカプセル) ともネット対戦のチーム色 (青・赤) とも見分けが付く緑。</summary>
        public static readonly Color BodyColor = new Color(0.2f, 0.75f, 0.45f);

        /// <summary>銃の色。体と見分けが付くよう暗くする。</summary>
        public static readonly Color GunColor = new Color(0.15f, 0.15f, 0.17f);

        [SerializeField, Tooltip("見せるアクションの読み元。GameBootstrap が配線する")]
        private PlayerActionReader _reader;

        private Transform _bodyRoot;
        private Transform _torso;
        private Transform _head;
        private Transform _bodyGun;
        private Transform _viewModelRoot;
        private Transform _viewModelGun;
        private readonly List<Renderer> _bodyRenderers = new List<Renderer>();
        private readonly List<Renderer> _viewModelRenderers = new List<Renderer>();
        private Material _bodyMaterial;
        private Material _gunMaterial;

        private Renderer _hostRenderer;
        private bool _hostRendererWasEnabled;

        private WeaponController _subscribedWeapon;
        private float _kick;
        private bool _hasPose;
        private float _stanceScale = 1f;
        private Vector3 _shift;
        private Quaternion _lean = Quaternion.identity;
        private MockPartPose _gunPose;
        private MockPartPose _viewPose;
        private bool _showsBody = true;

        /// <summary>見せるアクションの読み元。未配線なら null。</summary>
        public PlayerActionReader Reader => _reader;

        /// <summary>部品を組み立て済みか。</summary>
        public bool IsBuilt => _bodyRoot != null && _viewModelRoot != null;

        /// <summary>三人称の体の根 (足元)。未組み立てなら null。</summary>
        public Transform BodyRoot => _bodyRoot;

        public Transform Torso => _torso;
        public Transform Head => _head;
        public Transform BodyGun => _bodyGun;

        /// <summary>一人称の銃の根 (カメラの子)。未組み立てなら null。</summary>
        public Transform ViewModelRoot => _viewModelRoot;

        public Transform ViewModelGun => _viewModelGun;

        /// <summary>今の反動の強さ。0 (なし) 〜 1 (撃った瞬間)。</summary>
        public float Kick => _kick;

        /// <summary>
        /// 三人称の体 (<see cref="BodyRoot"/>) を出すか。既定は true。<see cref="CharacterAnimator"/> が人型のリグを置いたら false にし、
        /// 一人称の銃 (ビューモデル) だけを残す。変えるとすぐ反映する。
        /// </summary>
        public bool ShowsBody
        {
            get => _showsBody;
            set
            {
                _showsBody = value;
                if (_bodyRoot != null && !value && _bodyRoot.gameObject.activeSelf)
                {
                    _bodyRoot.gameObject.SetActive(false);
                }
            }
        }

        private void Awake()
        {
            // 専用サーバーには画面が無く、見せる相手がいない。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (_reader != null)
            {
                return;
            }

            _reader = GetComponent<PlayerActionReader>();
            if (_reader == null)
            {
                Debug.LogError($"{LogTag} '{name}' に PlayerActionReader がありません。見せるアクションはそこから読みます。", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (IsBuilt)
            {
                HideHostRenderer();
            }
        }

        private void OnDisable()
        {
            UnsubscribeWeapon();
            if (IsBuilt)
            {
                _bodyRoot.gameObject.SetActive(false);
                _viewModelRoot.gameObject.SetActive(false);
                RestoreHostRenderer();
            }
        }

        private void OnDestroy()
        {
            Dismantle();
        }

        private void LateUpdate()
        {
            if (_reader == null || !_reader.IsBound)
            {
                Debug.LogError($"{LogTag} '{name}' の読み元 (PlayerActionReader) が無いか読み元を失ったため、自機モックを止めます。", this);
                enabled = false;
                return;
            }

            if (!IsBuilt)
            {
                var camera = _reader.Rig.Camera;
                if (camera == null)
                {
                    Debug.LogError($"{LogTag} '{name}' の ViewRig にカメラがありません。一人称の銃をカメラの子に置けないので、自機モックを止めます。", this);
                    enabled = false;
                    return;
                }

                Build(camera.transform);
            }

            SyncWeaponSubscription();

            var mover = _reader.Mover;
            var controller = mover.Controller;
            var velocity = controller != null ? controller.velocity : Vector3.zero;
            var feetY = controller != null ? controller.center.y - (controller.height / 2f) : -GameBootstrap.PlayerHeight / 2f;
            var eye = _reader.Rig.Eye;
            var pitch = eye != null ? eye.localRotation : Quaternion.identity;

            Apply(_reader.Current, velocity, _reader.Cover.CoverNormal, pitch, feetY, Time.deltaTime);
        }

        /// <summary>見せるアクションの読み元を渡す。</summary>
        public void Bind(PlayerActionReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            UnsubscribeWeapon();
            _reader = reader;
        }

        /// <summary>
        /// 部品を組み立てる。三人称の体は本体の子、一人称の銃は camera の子に置き、本体自身のレンダラーを消す。
        /// 組み立て済みなら何もしない。camera はこの本体の子孫であること (視点と一緒に動き、本体と一緒に消えるように)。
        /// </summary>
        public void Build(Transform camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!camera.IsChildOf(transform) || camera == transform)
            {
                throw new ArgumentException($"camera '{camera.name}' must be a descendant of '{name}'", nameof(camera));
            }

            if (IsBuilt)
            {
                return;
            }

            // 途中で失敗したときに半端な部品を残さない。
            Dismantle();
            try
            {
                _bodyRoot = CreateRoot(BodyRootName, transform);
                _torso = CreatePart(PrimitiveType.Capsule, "Torso", _bodyRoot, isGun: false, _bodyRenderers);
                _head = CreatePart(PrimitiveType.Sphere, "Head", _bodyRoot, isGun: false, _bodyRenderers);
                _head.localScale = Vector3.one * PlayerMockPose.HeadDiameter;
                _bodyGun = CreateRoot("Gun", _bodyRoot);
                CreatePart(PrimitiveType.Cube, "GunBox", _bodyGun, isGun: true, _bodyRenderers).localScale = PlayerMockPose.GunSize;

                _viewModelRoot = CreateRoot(ViewModelRootName, camera);
                _viewModelGun = CreateRoot("Gun", _viewModelRoot);
                var viewBox = CreatePart(PrimitiveType.Cube, "GunBox", _viewModelGun, isGun: true, _viewModelRenderers);
                viewBox.localScale = PlayerMockPose.GunSize;
                foreach (var viewRenderer in _viewModelRenderers)
                {
                    // 銃がカメラの手前にあるので、影を落とすと画面に大きな影が出る。
                    viewRenderer.receiveShadows = false;
                }
            }
            catch
            {
                Dismantle();
                throw;
            }

            _hasPose = false;
            if (isActiveAndEnabled)
            {
                HideHostRenderer();
            }

            Debug.Log($"{LogTag} built for '{name}' body={_bodyRenderers.Count} viewModel={_viewModelRenderers.Count}", this);
        }

        /// <summary>
        /// state の姿勢へ deltaSeconds 分だけ寄せる (組み立てた直後の 1 回目はそのまま置く)。一人称なら体を影だけにして
        /// ビューモデルを出し、三人称なら体を描いてビューモデルを隠す。
        /// </summary>
        /// <param name="state">見せるアクション。</param>
        /// <param name="worldVelocity">本体の速度 (ワールド)。回避で傾ける向きに使う。</param>
        /// <param name="worldCoverNormal">カバーしている壁の面の向き (ワールド、壁から離れる向き)。</param>
        /// <param name="aimPitch">目の上下の向き (目のローカルの回転)。三人称の銃口をこれに合わせる。</param>
        /// <param name="feetLocalY">本体のローカルでの足元の高さ。</param>
        /// <param name="deltaSeconds">前の Apply からの秒数。0 以上の有限値。</param>
        public void Apply(
            PlayerActionState state,
            Vector3 worldVelocity,
            Vector3 worldCoverNormal,
            Quaternion aimPitch,
            float feetLocalY,
            float deltaSeconds)
        {
            if (!IsBuilt)
            {
                throw new InvalidOperationException($"PlayerMockBody '{name}' is not built");
            }

            if (float.IsNaN(feetLocalY) || float.IsInfinity(feetLocalY))
            {
                throw new ArgumentOutOfRangeException(nameof(feetLocalY), feetLocalY, "feetLocalY must be finite");
            }

            var blend = PlayerMockPose.BlendFactor(deltaSeconds);
            _kick = PlayerMockPose.RecoverKick(_kick, deltaSeconds);

            var localMove = transform.InverseTransformDirection(worldVelocity);
            var localNormal = transform.InverseTransformDirection(worldCoverNormal);
            var targetScale = PlayerMockPose.StanceScale(state);
            var targetShift = PlayerMockPose.BodyShift(state, localNormal);
            var targetLean = PlayerMockPose.BodyLean(state, localMove, localNormal);
            var targetGun = PlayerMockPose.BodyGun(state, aimPitch);
            var targetView = PlayerMockPose.ViewModel(state);

            if (!_hasPose)
            {
                blend = 1f;
                _hasPose = true;
            }

            _stanceScale = Mathf.Lerp(_stanceScale, targetScale, blend);
            _shift = Vector3.Lerp(_shift, targetShift, blend);
            _lean = Quaternion.Slerp(_lean, targetLean, blend);
            _gunPose = Blend(_gunPose, targetGun, blend);
            _viewPose = Blend(_viewPose, targetView, blend);

            _bodyRoot.localPosition = new Vector3(0f, feetLocalY, 0f) + _shift;
            _bodyRoot.localRotation = _lean;
            PlayerMockPose.Torso(_stanceScale, out var torsoPosition, out var torsoScale);
            _torso.localPosition = torsoPosition;
            _torso.localScale = torsoScale;
            _head.localPosition = PlayerMockPose.Head(_stanceScale);
            SetPose(_bodyGun, PlayerMockPose.WithKick(_gunPose, _kick, PlayerMockPose.BodyKickDistance, PlayerMockPose.BodyKickDegrees));
            SetPose(_viewModelGun, PlayerMockPose.WithKick(_viewPose, _kick, PlayerMockPose.ViewKickDistance, PlayerMockPose.ViewKickDegrees));

            ApplyView(state.ViewMode);
        }

        /// <summary>撃った反動を最大にする。武器の Fired から呼ばれる。</summary>
        public void AddKick()
        {
            _kick = 1f;
        }

        /// <summary>部品と作った材質を壊し、本体自身のレンダラーを元に戻す。組み立てていなければ何もしない。</summary>
        public void Dismantle()
        {
            UnsubscribeWeapon();
            DestroySafely(_bodyRoot != null ? _bodyRoot.gameObject : null);
            DestroySafely(_viewModelRoot != null ? _viewModelRoot.gameObject : null);
            DestroySafely(_bodyMaterial);
            DestroySafely(_gunMaterial);
            _bodyRoot = null;
            _torso = null;
            _head = null;
            _bodyGun = null;
            _viewModelRoot = null;
            _viewModelGun = null;
            _bodyMaterial = null;
            _gunMaterial = null;
            _bodyRenderers.Clear();
            _viewModelRenderers.Clear();
            _hasPose = false;
            _kick = 0f;
            RestoreHostRenderer();
        }

        // ViewRig は視点を変えるたびに子孫のレンダラーをまとめて影だけ・普通に描くへ書き換え、ビューモデルも巻き込むので、毎フレーム直す。
        private void ApplyView(ViewMode mode)
        {
            var firstPerson = mode == ViewMode.FirstPerson;
            if (_bodyRoot.gameObject.activeSelf != _showsBody)
            {
                _bodyRoot.gameObject.SetActive(_showsBody);
            }

            var bodyShadow = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var bodyRenderer in _bodyRenderers)
            {
                if (bodyRenderer != null && bodyRenderer.shadowCastingMode != bodyShadow)
                {
                    bodyRenderer.shadowCastingMode = bodyShadow;
                }
            }

            if (_viewModelRoot.gameObject.activeSelf != firstPerson)
            {
                _viewModelRoot.gameObject.SetActive(firstPerson);
            }

            foreach (var viewRenderer in _viewModelRenderers)
            {
                if (viewRenderer != null && viewRenderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    viewRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        private void SyncWeaponSubscription()
        {
            var weapon = _reader.Weapon;
            if (weapon == _subscribedWeapon)
            {
                return;
            }

            UnsubscribeWeapon();
            if (weapon != null)
            {
                weapon.Fired += OnFired;
                _subscribedWeapon = weapon;
            }
        }

        private void UnsubscribeWeapon()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_subscribedWeapon, null))
            {
                _subscribedWeapon.Fired -= OnFired;
            }

            _subscribedWeapon = null;
        }

        private void OnFired(Weapon _)
        {
            AddKick();
        }

        private void HideHostRenderer()
        {
            if (_hostRenderer != null)
            {
                return;
            }

            _hostRenderer = GetComponent<Renderer>();
            if (_hostRenderer != null)
            {
                _hostRendererWasEnabled = _hostRenderer.enabled;
                _hostRenderer.enabled = false;
            }
        }

        private void RestoreHostRenderer()
        {
            if (_hostRenderer != null)
            {
                _hostRenderer.enabled = _hostRendererWasEnabled;
            }

            _hostRenderer = null;
        }

        private static Transform CreateRoot(string rootName, Transform parent)
        {
            var root = new GameObject(rootName).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = parent.gameObject.layer;
            return root;
        }

        private Transform CreatePart(PrimitiveType type, string partName, Transform parent, bool isGun, List<Renderer> renderers)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName;

            // Destroy は次のフレームまで残り、その間に CharacterController や射撃の判定に当たるので、すぐ消す。
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);

            var partRenderer = part.GetComponent<Renderer>();
            partRenderer.sharedMaterial = isGun ? GunMaterialFrom(partRenderer) : BodyMaterialFrom(partRenderer);
            renderers.Add(partRenderer);
            return part.transform;
        }

        // プリミティブに付いてくる材質を元にすると、今の描画パイプラインのシェーダーをそのまま使える。
        private Material BodyMaterialFrom(Renderer source)
        {
            if (_bodyMaterial == null)
            {
                _bodyMaterial = CreateMaterial(source, "MockBody", BodyColor);
            }

            return _bodyMaterial;
        }

        private Material GunMaterialFrom(Renderer source)
        {
            if (_gunMaterial == null)
            {
                _gunMaterial = CreateMaterial(source, "MockGun", GunColor);
            }

            return _gunMaterial;
        }

        private static Material CreateMaterial(Renderer source, string materialName, Color color)
        {
            var template = source.sharedMaterial;
            if (template == null)
            {
                throw new InvalidOperationException($"the primitive '{source.name}' has no material to copy for {materialName}");
            }

            var material = new Material(template) { name = materialName, color = color };
            return material;
        }

        private static MockPartPose Blend(MockPartPose from, MockPartPose to, float t)
        {
            return new MockPartPose(Vector3.Lerp(from.Position, to.Position, t), Quaternion.Slerp(Normalized(from.Rotation), to.Rotation, t));
        }

        // default(Quaternion) は長さ 0 で Slerp が NaN を返すので、初回は単位回転として扱う。
        private static Quaternion Normalized(Quaternion rotation)
        {
            var lengthSquared = (rotation.x * rotation.x) + (rotation.y * rotation.y) + (rotation.z * rotation.z) + (rotation.w * rotation.w);
            return lengthSquared < 1e-8f ? Quaternion.identity : rotation;
        }

        private static void SetPose(Transform part, MockPartPose pose)
        {
            part.localPosition = pose.Position;
            part.localRotation = pose.Rotation;
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
