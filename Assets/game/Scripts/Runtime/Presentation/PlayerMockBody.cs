using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    // 部品と重ならないよう、本体自身のレンダラー (プリミティブのカプセル) は組み立てている間だけ消し、外すときに元へ戻す。
    [DisallowMultipleComponent]
    // PlayerActionReader (100) がそのフレームの状態を組み立て、ViewRig がカメラを置き終えてから姿勢を決める。
    [DefaultExecutionOrder(110)]
    public sealed class PlayerMockBody : MonoBehaviour
    {
        public const string LogTag = "[MockBody]";

        public const string BodyRootName = "MockBody";
        public const string ViewModelRootName = "MockViewModel";

        // 敵 (既定の白いカプセル) ともネット対戦のチーム色 (青・赤) とも見分けが付く緑にする。
        public static readonly Color BodyColor = new Color(0.2f, 0.75f, 0.45f);

        // 体と見分けが付くよう暗くする。
        public static readonly Color GunColor = new Color(0.15f, 0.15f, 0.17f);

        [SerializeField, Tooltip("見せるアクションの読み元。GameBootstrap が配線する")]
        private PlayerActionReader _reader;

        [SerializeField, Tooltip("胴・頭・銃の見た目。空の部品だけ仮の形を作る。GameBootstrap が配線する")]
        private PlayerMockBodyPrefabs _prefabs = new PlayerMockBodyPrefabs();

        private Transform _bodyRoot;
        private Transform _torso;
        private Transform _head;
        private Transform _bodyGun;
        private Transform _bodyGunDefault;
        private Transform _bodyGunModel;
        private string _bodyGunWeaponId;
        private IReadOnlyList<string> _bodyGunAttachments = WeaponAttachments.None;
        private WeaponExtrasCatalog _weaponModels;
        private bool _weaponModelsResolved;
        private Transform _viewModelRoot;
        private Transform _viewModelGun;
        private ViewmodelPresenter _viewmodel;
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
        private ViewRig _viewRig;

        public PlayerActionReader Reader => _reader;

        public PlayerMockBodyPrefabs Prefabs
        {
            get => _prefabs ?? (_prefabs = new PlayerMockBodyPrefabs());
            set => _prefabs = value ?? new PlayerMockBodyPrefabs();
        }

        public bool IsBuilt => _bodyRoot != null && _viewModelRoot != null;

        public Transform BodyRoot => _bodyRoot;

        public Transform Torso => _torso;
        public Transform Head => _head;
        public Transform BodyGun => _bodyGun;

        public Transform BodyGunModel => _bodyGunModel;

        public WeaponExtrasCatalog WeaponModels
        {
            get
            {
                if (!_weaponModelsResolved)
                {
                    _weaponModels = WeaponExtrasCatalog.Load();
                    _weaponModelsResolved = true;
                }

                return _weaponModels;
            }
            set
            {
                _weaponModels = value;
                _weaponModelsResolved = true;
                _bodyGunWeaponId = null;
            }
        }

        public Transform ViewModelRoot => _viewModelRoot;

        public Transform ViewModelGun => _viewModelGun;

        public float Kick => _kick;

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
            SyncBodyGunModel();

            var mover = _reader.Mover;
            var controller = mover.Controller;
            var velocity = controller != null ? controller.velocity : Vector3.zero;
            var feetY = controller != null ? controller.center.y - (controller.height / 2f) : -GameBootstrap.PlayerHeight / 2f;
            var eye = _reader.Rig.Eye;
            var pitch = eye != null ? eye.localRotation : Quaternion.identity;

            Apply(_reader.Current, velocity, _reader.Cover.CoverNormal, pitch, feetY, Time.deltaTime, mover.Stance);
            var armed = _reader.Weapon == null || !_reader.Weapon.IsHolstered;
            ShowGuns(armed, armed && !HasViewmodel());
        }

        // 仮の形は差し替えの見た目が無いときの代わりなので、武器のビューモデルが出ている間に両方を出すと銃が二重に見える。
        private bool HasViewmodel()
        {
            if (_viewmodel == null)
            {
                _viewmodel = GetComponent<ViewmodelPresenter>();
            }

            return _viewmodel != null && _viewmodel.Instance != null;
        }

        public void ShowGuns(bool shown)
        {
            ShowGuns(shown, shown);
        }

        private void ShowGuns(bool bodyGun, bool viewModelGun)
        {
            if (!IsBuilt)
            {
                return;
            }

            if (_bodyGun.gameObject.activeSelf != bodyGun)
            {
                _bodyGun.gameObject.SetActive(bodyGun);
            }

            if (_viewModelGun.gameObject.activeSelf != viewModelGun)
            {
                _viewModelGun.gameObject.SetActive(viewModelGun);
            }
        }

        public void Bind(PlayerActionReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            UnsubscribeWeapon();
            _reader = reader;
        }

        // camera をこの本体の子孫に限るのは、一人称の銃を視点と一緒に動かし、本体と一緒に消すため。
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
            var prefabs = Prefabs;
            try
            {
                _bodyRoot = CreateRoot(BodyRootName, transform);
                _torso = CreatePart(prefabs.Torso, PrimitiveType.Capsule, "Torso", _bodyRoot, isGun: false, _bodyRenderers);
                _head = CreatePart(prefabs.Head, PrimitiveType.Sphere, "Head", _bodyRoot, isGun: false, _bodyRenderers);
                _head.localScale = Vector3.one * PlayerMockPose.HeadDiameter;
                _bodyGun = CreateRoot("Gun", _bodyRoot);
                _bodyGunDefault = CreateGun(prefabs.Gun, _bodyGun, _bodyRenderers);

                _viewModelRoot = CreateRoot(ViewModelRootName, camera);
                _viewModelGun = CreateRoot("Gun", _viewModelRoot);
                CreateGun(prefabs.Gun, _viewModelGun, _viewModelRenderers);
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

        public void Apply(
            PlayerActionState state,
            Vector3 worldVelocity,
            Vector3 worldCoverNormal,
            Quaternion aimPitch,
            float feetLocalY,
            float deltaSeconds,
            StanceGeometry stance = null)
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
            var targetScale = PlayerMockPose.StanceScale(state, stance);
            var targetShift = PlayerMockPose.BodyShift(state, localNormal);
            var targetLean = PlayerMockPose.BodyLean(state, localMove, localNormal);
            var targetGun = PlayerMockPose.BodyGun(state, aimPitch, stance);
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

        public void AddKick()
        {
            _kick = 1f;
        }

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
            _bodyGunDefault = null;
            _bodyGunModel = null;
            _bodyGunWeaponId = null;
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

            // ViewRig は後から付くことがあるので、見つかるまで探し直す。
            if (_viewRig == null)
            {
                _viewRig = GetComponent<ViewRig>();
            }

            var hiddenBySight = _viewRig != null && _viewRig.IsBodyHiddenBySight;
            var bodyShadow = firstPerson || hiddenBySight ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
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

        private Transform CreateGun(GameObject prefab, Transform gunRoot, List<Renderer> renderers)
        {
            var box = CreatePart(prefab, PrimitiveType.Cube, "GunBox", gunRoot, isGun: true, renderers);
            if (prefab == null)
            {
                box.localScale = PlayerMockPose.GunSize;
            }

            return box;
        }

        public void SetBodyGunModel(GameObject model)
        {
            if (!IsBuilt)
            {
                return;
            }

            if (_bodyGunModel != null)
            {
                var old = _bodyGunModel;
                _bodyRenderers.RemoveAll(r => r == null || r.transform.IsChildOf(old));
                DestroySafely(old.gameObject);
                _bodyGunModel = null;
            }

            if (model != null)
            {
                var instance = VisualPrefab.Instantiate(model, _bodyGun, model.name, stripColliders: true);
                VisualPrefab.SetLayer(instance, _bodyGun.gameObject.layer);
                _bodyRenderers.AddRange(instance.GetComponentsInChildren<Renderer>(true));
                _bodyGunModel = instance.transform;
            }

            _bodyGunDefault.gameObject.SetActive(_bodyGunModel == null);
        }

        // 持ち替えたときと付けた物が変わったときだけ置き直す。毎フレーム作り直すと、プレハブの複製が積もる。
        private void SyncBodyGunModel()
        {
            var controller = _reader.Weapon;
            var weapon = controller != null ? controller.Weapon : null;
            var weaponId = weapon != null ? weapon.Spec.Id : null;
            var attachments = weaponId != null ? controller.AttachmentsOf(weaponId) : WeaponAttachments.None;
            if (string.Equals(weaponId, _bodyGunWeaponId, StringComparison.Ordinal))
            {
                if (!WeaponAttachments.SameAs(attachments, _bodyGunAttachments))
                {
                    _bodyGunAttachments = attachments;
                    WeaponAttachmentModels.Apply(_bodyGunModel, attachments, controller.AttachmentCatalog, AttachmentModelCatalog.Load());
                    AddBodyRenderers(_bodyGunModel);
                }

                return;
            }

            _bodyGunWeaponId = weaponId;
            _bodyGunAttachments = attachments;
            var catalog = weaponId != null ? WeaponModels : null;
            SetBodyGunModel(catalog != null && catalog.TryGetWeapon(weaponId, out var assets) ? assets.PickupModel : null);
            if (attachments.Count > 0)
            {
                WeaponAttachmentModels.Apply(_bodyGunModel, attachments, controller.AttachmentCatalog, AttachmentModelCatalog.Load());
                AddBodyRenderers(_bodyGunModel);
            }
        }

        // 付けた物の描画物も体の描画物として数え、一人称で体を隠すときに一緒に隠す
        private void AddBodyRenderers(Transform model)
        {
            if (model == null)
            {
                return;
            }

            _bodyRenderers.RemoveAll(r => r == null);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!_bodyRenderers.Contains(renderer))
                {
                    _bodyRenderers.Add(renderer);
                }
            }
        }

        // プレハブは名前の付いた空の根の子に置く。根の位置と大きさは姿勢 (Apply) が毎フレーム上書きするが、プレハブ自身の置き方は残すため。
        private Transform CreatePart(GameObject prefab, PrimitiveType type, string partName, Transform parent, bool isGun, List<Renderer> renderers)
        {
            if (prefab == null)
            {
                return CreatePart(type, partName, parent, isGun, renderers);
            }

            var root = CreateRoot(partName, parent);
            var model = VisualPrefab.Instantiate(prefab, root, prefab.name, stripColliders: true);
            VisualPrefab.SetLayer(model, parent.gameObject.layer);
            renderers.AddRange(model.GetComponentsInChildren<Renderer>(true));
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

    [Serializable]
    public sealed class PlayerMockBodyPrefabs
    {
        [SerializeField, Tooltip("胴。Unity の Capsule と同じく中心が原点で、幅 1m・高さ 2m の形にする (姿勢に合わせて拡大する)。空なら仮のカプセル")]
        private GameObject _torso;

        [SerializeField, Tooltip("頭。中心が原点で直径 1m の形にする (頭の大きさへ縮める)。空なら仮の球")]
        private GameObject _head;

        [SerializeField, Tooltip("銃。三人称の体と一人称の画面の両方に置く。中心が原点、前が +Z で、大きさはプレハブのまま。空なら仮の箱")]
        private GameObject _gun;

        public PlayerMockBodyPrefabs()
        {
        }

        public PlayerMockBodyPrefabs(GameObject torso, GameObject head, GameObject gun)
        {
            _torso = torso;
            _head = head;
            _gun = gun;
        }

        public GameObject Torso
        {
            get => _torso;
            set => _torso = value;
        }

        public GameObject Head
        {
            get => _head;
            set => _head = value;
        }

        public GameObject Gun
        {
            get => _gun;
            set => _gun = value;
        }
    }
}
