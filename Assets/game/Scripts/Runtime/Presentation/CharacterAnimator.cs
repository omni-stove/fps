using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラの状態を人型のリグ (CharacterRigBuilder が作るプレハブ) の Animator に写す。プレイヤー・NetPlayer・敵の本体に付ける。
    /// <list type="bullet">
    /// <item>読み元 (<see cref="TryBind"/>): 同じ GameObject に <see cref="PlayerController"/> があればプレイヤーとして、それと
    /// <see cref="WeaponController"/> (子のカメラに付く)・<see cref="AimDownSights"/>・<see cref="CoverController"/>・<see cref="Damageable"/> を読む。
    /// 無くて <see cref="EnemyAgent"/> があれば敵として、それと NavMeshAgent・Damageable を読む。
    /// どちらも無くて <see cref="ICharacterPoseSource"/> (ネット対戦の NetPlayer) があればそこから読み、水平の速さは本体の位置の変化から
    /// <see cref="HorizontalSpeedMeter"/> で求めて渡す。どれも無ければ何もしない。</item>
    /// <item>リグ (<see cref="BuildRig"/>): 本体の直下に <see cref="RigObjectName"/> の子 (根に Animator) が既にあれば
    /// (NetSceneBuilder がプレハブに置いたリグ) それを使う。無ければ <see cref="RigPrefab"/> (未設定なら Resources の <see cref="RigResourcePath"/>)
    /// を本体の子に置く。どちらも足元に合わせる。本体自身のレンダラー (プリミティブのカプセル) は消し、<see cref="PlayerMockBody"/> があればその体も出さない
    /// (一人称の銃は残す)。プレハブが無ければ警告を出してカプセルのまま止まる。</item>
    /// <item>毎フレーム (<see cref="Refresh"/>): 状態を <see cref="CharacterPose"/> で Animator のパラメーターにして渡す。
    /// この端末のプレイヤーの一人称 (<see cref="ViewRig"/>) ではリグを影だけにし、自分の体が視界に入らないようにする。</item>
    /// </list>
    /// 専用サーバーでは自分を止める (<c>Fps.Net.HeadlessPresentation</c> も止める)。
    /// </summary>
    [DisallowMultipleComponent]
    // 移動・射撃・カバー (Update) と PlayerActionReader (100)・PlayerMockBody (110) の後に、そのフレームの状態を読む。
    [DefaultExecutionOrder(120)]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public const string LogTag = "[CharacterAnimator]";

        /// <summary>リグのプレハブの Resources からのパス (どこかの Resources フォルダの Characters/CharacterRig.prefab)。</summary>
        public const string RigResourcePath = "Characters/CharacterRig";

        /// <summary>本体の子に置くリグの名前。</summary>
        public const string RigObjectName = "CharacterRig";

        /// <summary>Speed を寄せる秒数。歩きと走りの切り替わりで足の振りが跳ばないようにする。</summary>
        public const float SpeedDampSeconds = 0.1f;

        [SerializeField, Tooltip("置くリグのプレハブ。空なら Resources の Characters/CharacterRig を読む")]
        private GameObject _rigPrefab;

        private CharacterAnimatorSource _source;
        private PlayerController _mover;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private CoverController _cover;
        private Damageable _damageable;
        private EnemyAgent _enemy;
        private NavMeshAgent _navAgent;
        private ICharacterPoseSource _network;
        private readonly HorizontalSpeedMeter _speedMeter = new HorizontalSpeedMeter();
        private ViewRig _view;

        private CharacterPose _pose;
        private float _poseWalk;
        private float _poseSprint;
        private float _poseCrouch;
        private bool _warnedInvalidSpeeds;

        private Transform _rig;
        private bool _ownsRig;
        private Animator _animator;
        private Renderer[] _rigRenderers = Array.Empty<Renderer>();
        private Renderer _hostRenderer;
        private bool _hostRendererWasEnabled;

        /// <summary>リグに置き換えて隠したカプセルの Renderer。隠していなければ null。DeathBodyPresenter が抜け殻に写さない判定に使う。</summary>
        public Renderer HiddenHostRenderer => _hostRenderer;

        private PlayerMockBody _mockBody;
        private Damageable _resetSource;

        private readonly HashSet<int> _animatorParameters = new HashSet<int>();
        private bool _hasParameterTable;

        private CharacterPoseParameters _parameters;
        private bool _hasParameters;

        private static readonly int SpeedHash = Animator.StringToHash(CharacterPose.SpeedParameter);
        private static readonly int CrouchHash = Animator.StringToHash(CharacterPose.CrouchParameter);
        private static readonly int AimHash = Animator.StringToHash(CharacterPose.AimParameter);
        private static readonly int CoverHash = Animator.StringToHash(CharacterPose.CoverParameter);
        private static readonly int DownedHash = Animator.StringToHash(CharacterPose.DownedParameter);
        private static readonly int DeadHash = Animator.StringToHash(CharacterPose.DeadParameter);
        private static readonly int WeaponCategoryHash = Animator.StringToHash(CharacterPose.WeaponCategoryParameter);
        private static readonly int SlideHash = Animator.StringToHash(CharacterPose.SlideParameter);

        /// <summary>
        /// 置くリグのプレハブ。null なら <see cref="BuildRig"/> が Resources の <see cref="RigResourcePath"/> を読む。
        /// 置いた後に変えても今のリグは変わらない。
        /// </summary>
        public GameObject RigPrefab
        {
            get => _rigPrefab;
            set => _rigPrefab = value;
        }

        /// <summary>どの部品から状態を読むか。<see cref="TryBind"/> 前は <see cref="CharacterAnimatorSource.None"/>。</summary>
        public CharacterAnimatorSource Source => _source;

        /// <summary>リグを置いたか。</summary>
        public bool IsBuilt => _rig != null;

        /// <summary>置いたリグの根。無ければ null。</summary>
        public Transform Rig => _rig;

        /// <summary>リグを自分で置いたか (<see cref="Dismantle"/> で壊すか)。プレハブにあったリグを使っているなら false。</summary>
        public bool OwnsRig => _rig != null && _ownsRig;

        /// <summary><see cref="CharacterAnimatorSource.Network"/> で、位置の変化から求めた今の水平の速さ (m/s)。</summary>
        public float MeasuredSpeed => _speedMeter.Speed;

        /// <summary>リグの Animator。無ければ null。</summary>
        public Animator Animator => _animator;

        /// <summary>最後の <see cref="Refresh"/> で決めたパラメーター。まだ無ければ <see cref="HasParameters"/> が false。</summary>
        public CharacterPoseParameters Parameters => _parameters;

        public bool HasParameters => _hasParameters;

        /// <summary>今使っている姿勢の計算。<see cref="TryBind"/> 前や読み元が無ければ null。</summary>
        public CharacterPose Pose => _pose;

        private void Awake()
        {
            // 専用サーバーには画面が無く、見せる相手がいない。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
            }
        }

        // GameBootstrap は演出のコンポーネントを付けた後でカバーや武器を足すので、Awake ではまだ揃っていない。
        private void Start()
        {
            if (!TryBind())
            {
                enabled = false;
                return;
            }

            // 生成済みのシーンの敵にも銃を持たせるため、シーン生成スクリプトでなく実行時にここで付ける。
            if (_enemy != null && GetComponent<EnemyWeaponView>() == null)
            {
                gameObject.AddComponent<EnemyWeaponView>();
            }

            if (!BuildRig())
            {
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!IsBuilt)
            {
                enabled = false;
                return;
            }

            Refresh(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Dismantle();
        }

        /// <summary>
        /// 同じ GameObject から読み元を探す。<see cref="PlayerController"/> があればプレイヤー、無くて <see cref="EnemyAgent"/> があれば敵、
        /// どちらも無くて <see cref="ICharacterPoseSource"/> があればそれ (ネット対戦の NetPlayer)。
        /// どれも無ければ false (ログは出さない)。
        /// </summary>
        public bool TryBind()
        {
            _mover = GetComponent<PlayerController>();
            _enemy = _mover == null ? GetComponent<EnemyAgent>() : null;
            // GetComponent はエディタで、無い部品に null でない偽物を返すことがあり、インターフェイスでは == null で見分けられない
            _network = _mover == null && _enemy == null && TryGetComponent<ICharacterPoseSource>(out var network) ? network : null;
            _damageable = GetComponent<Damageable>();
            _view = GetComponent<ViewRig>();
            _speedMeter.Reset();

            if (_mover != null)
            {
                _source = CharacterAnimatorSource.Player;
                _weapon = GetComponentInChildren<WeaponController>(true);
                _ads = GetComponent<AimDownSights>();
                _cover = GetComponent<CoverController>();
                _navAgent = null;
            }
            else if (_enemy != null)
            {
                _source = CharacterAnimatorSource.Enemy;
                _navAgent = GetComponent<NavMeshAgent>();
                _weapon = null;
                _ads = null;
                _cover = null;
            }
            else if (_network != null)
            {
                _source = CharacterAnimatorSource.Network;
                _navAgent = null;
                _weapon = null;
                _ads = null;
                _cover = null;
            }
            else
            {
                _source = CharacterAnimatorSource.None;
                _weapon = null;
                _ads = null;
                _cover = null;
                _navAgent = null;
                _pose = null;
                return false;
            }

            _pose = null;
            EnsurePose();
            return true;
        }

        /// <summary>
        /// リグのプレハブを本体の子に置き、本体のカプセルと自機モックの体を隠す。置き済みなら何もせず true。
        /// 本体の直下に <see cref="RigObjectName"/> の子があって根に Animator があれば、置かずにそれを使う (<see cref="OwnsRig"/> は false)。
        /// プレハブが無ければ警告を、根に Animator が無ければエラーを出し、何も変えずに false を返す。
        /// </summary>
        public bool BuildRig()
        {
            if (IsBuilt)
            {
                return true;
            }

            if (TryAdoptRig())
            {
                return true;
            }

            var prefab = _rigPrefab != null ? _rigPrefab : Resources.Load<GameObject>(RigResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"{LogTag} '{name}': リグのプレハブ (Resources/{RigResourcePath}) がありません。カプセルの見た目のままにします。"
                    + " unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.CharacterRigBuilder.Build で作れます。",
                    this);
                return false;
            }

            if (prefab.GetComponent<Animator>() == null)
            {
                Debug.LogError($"{LogTag} '{name}': リグのプレハブ '{prefab.name}' の根に Animator がありません。カプセルの見た目のままにします。", this);
                return false;
            }

            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab, transform, false);
                instance.name = RigObjectName;
                StripColliders(instance);
                SetLayer(instance.transform, gameObject.layer);

                _rig = instance.transform;
                _ownsRig = true;
                _animator = instance.GetComponent<Animator>();
                _rigRenderers = instance.GetComponentsInChildren<Renderer>(true);
            }
            catch
            {
                DestroySafely(instance);
                _rig = null;
                _ownsRig = false;
                _animator = null;
                _rigRenderers = Array.Empty<Renderer>();
                throw;
            }

            FinishRig("built");
            return true;
        }

        // プレハブに置いたリグのコライダーは消さない。本体の部品 (NetPlayer) は Awake で子のコライダーを集めているので、
        // ここで消すと壊れた参照が残る。プレハブを作る側 (NetSceneBuilder) がコライダーを持たせない。
        private bool TryAdoptRig()
        {
            var existing = transform.Find(RigObjectName);
            if (existing == null)
            {
                return false;
            }

            var animator = existing.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"{LogTag} '{name}' の子 '{RigObjectName}' の根に Animator がないので、使わずにプレハブから置きます。", this);
                return false;
            }

            SetLayer(existing, gameObject.layer);
            _rig = existing;
            _ownsRig = false;
            _animator = animator;
            _rigRenderers = existing.GetComponentsInChildren<Renderer>(true);
            FinishRig("adopted");
            return true;
        }

        private void FinishRig(string how)
        {
            _animatorParameters.Clear();
            _hasParameterTable = false;
            _hasParameters = false;
            PlaceRig();
            HideHostRenderer();
            HideMockBody();
            SubscribeHealthReset();

            Debug.Log($"{LogTag} rig {how} for '{name}' source={_source} renderers={_rigRenderers.Length}", this);
        }

        /// <summary>
        /// 今の状態を読み、<see cref="CharacterPose"/> でパラメーターを決めて返す。リグがあれば Animator に渡し、足元へ置き、
        /// 一人称なら影だけにする。読み元が無ければ (<see cref="TryBind"/> 前・失敗) 例外を投げる。
        /// </summary>
        /// <param name="deltaSeconds">前の Refresh からの秒数。Speed を寄せるのに使う。0 以上の有限値。</param>
        public CharacterPoseParameters Refresh(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (_source == CharacterAnimatorSource.None)
            {
                throw new InvalidOperationException($"CharacterAnimator '{name}' has no state source; call TryBind first");
            }

            if (_source == CharacterAnimatorSource.Network)
            {
                var position = transform.position;
                _speedMeter.Sample(position.x, position.z, deltaSeconds);
            }

            EnsurePose();
            _parameters = _pose.Evaluate(ReadState());
            _hasParameters = true;

            if (IsBuilt)
            {
                PlaceRig();
                ApplyVisibility();
                ApplyToAnimator(_parameters, deltaSeconds);
            }

            return _parameters;
        }

        /// <summary>
        /// 読み元から今の状態を集める。読み元が無ければ例外を投げる。
        /// <see cref="CharacterAnimatorSource.Network"/> の速さは、最後の <see cref="Refresh"/> までの位置の変化から求めた値 (<see cref="MeasuredSpeed"/>)。
        /// </summary>
        public CharacterPoseInput ReadState()
        {
            switch (_source)
            {
                case CharacterAnimatorSource.Player:
                    return ReadPlayer();
                case CharacterAnimatorSource.Enemy:
                    return ReadEnemy();
                case CharacterAnimatorSource.Network:
                    return _network.ReadPose(_speedMeter.Speed);
                default:
                    throw new InvalidOperationException($"CharacterAnimator '{name}' has no state source; call TryBind first");
            }
        }

        /// <summary>
        /// リグを壊し、本体のカプセルと自機モックの体を元に戻す。置いていなければ何もしない。
        /// プレハブにあったリグ (<see cref="OwnsRig"/> が false) は壊さず、使うのをやめるだけにする。
        /// </summary>
        public void Dismantle()
        {
            UnsubscribeHealthReset();
            if (_rig != null && _ownsRig)
            {
                DestroySafely(_rig.gameObject);
            }

            _rig = null;
            _ownsRig = false;
            _animator = null;
            _rigRenderers = Array.Empty<Renderer>();
            _animatorParameters.Clear();
            _hasParameterTable = false;
            RestoreHostRenderer();
            RestoreMockBody();
        }

        private CharacterPoseInput ReadPlayer()
        {
            var controller = _mover.Controller != null ? _mover.Controller : GetComponent<CharacterController>();
            var velocity = controller != null && controller.enabled ? controller.velocity : Vector3.zero;

            CoverHeight? cover = null;
            if (_cover != null && _cover.IsInCover)
            {
                cover = _cover.CoverHeight;
            }

            WeaponCategory? category = null;
            if (_weapon != null && _weapon.Weapon != null)
            {
                category = _weapon.Weapon.Spec.Category;
            }

            return new CharacterPoseInput(
                HorizontalSpeed(velocity),
                _mover.IsGrounded,
                _mover.IsCrouched,
                _mover.IsSprinting,
                _ads != null && _ads.IsAiming,
                cover,
                _damageable != null && _damageable.IsDowned,
                _damageable != null && _damageable.IsDead,
                category,
                (_mover.MovementOverrideConditions & PlayerActionConditions.Sliding) != 0);
        }

        private CharacterPoseInput ReadEnemy()
        {
            var velocity = _navAgent != null && _navAgent.enabled ? _navAgent.velocity : Vector3.zero;
            var state = _enemy.State;
            var phase = _enemy.Brain != null ? _enemy.Brain.CoverPhase : EnemyCoverPhase.None;
            var aiming = EnemyGait.IsAiming(state, phase);

            WeaponCategory? category = null;
            if (_enemy.Weapon != null)
            {
                category = _enemy.Weapon.Spec.Category;
            }

            // 敵は NavMesh に沿って動くので宙に浮かない。
            return new CharacterPoseInput(
                HorizontalSpeed(velocity),
                isGrounded: true,
                isCrouching: _enemy.IsCrouching,
                isSprinting: _enemy.IsSprinting,
                isAiming: aiming,
                coverHeight: null,
                isDowned: _damageable != null && _damageable.IsDowned,
                isDead: state == EnemyState.Dead || (_damageable != null && _damageable.IsDead),
                weaponCategory: category);
        }

        // ルールや Inspector で速さが変わったら作り直す。
        private void EnsurePose()
        {
            float walk;
            float sprint;
            float crouch;
            if (_source == CharacterAnimatorSource.Player)
            {
                walk = _mover.WalkSpeed;
                sprint = _mover.SprintSpeed;
                crouch = _mover.CrouchSpeed;
            }
            else if (_source == CharacterAnimatorSource.Network)
            {
                walk = _network.WalkSpeed;
                sprint = _network.SprintSpeed;
                crouch = _network.CrouchSpeed;
            }
            else
            {
                // NavMeshAgent の速さは足の運びで毎フレーム変わるので、もとの歩きの速さから求める。
                walk = _enemy.WalkSpeed;
                sprint = walk * EnemyGait.SprintRatio;
                crouch = walk * EnemyGait.CrouchRatio;
            }

            if (_pose != null && walk.Equals(_poseWalk) && sprint.Equals(_poseSprint) && crouch.Equals(_poseCrouch))
            {
                return;
            }

            _poseWalk = walk;
            _poseSprint = sprint;
            _poseCrouch = crouch;
            try
            {
                _pose = new CharacterPose(walk, sprint, crouch);
            }
            catch (ArgumentOutOfRangeException e)
            {
                if (!_warnedInvalidSpeeds)
                {
                    Debug.LogWarning($"{LogTag} '{name}' の速さが姿勢の計算に使えないので、既定の速さで決めます: {e.Message}", this);
                    _warnedInvalidSpeeds = true;
                }

                _pose = new CharacterPose(PlayerController.DefaultWalkSpeed, PlayerController.DefaultSprintSpeed, PlayerController.DefaultCrouchSpeed);
            }
        }

        private static float HorizontalSpeed(Vector3 velocity)
        {
            var speed = new Vector2(velocity.x, velocity.z).magnitude;
            return speed >= 0f && !float.IsInfinity(speed) ? speed : 0f;
        }

        /// <summary>本体のローカルでの足元の高さ。リグの根は足元に置く。</summary>
        public float FeetLocalY()
        {
            var controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                return controller.center.y - (controller.height / 2f);
            }

            if (_navAgent != null)
            {
                // baseOffset はワールドの長さなので、本体の拡縮で割ってローカルに直す。
                var scaleY = Mathf.Abs(transform.lossyScale.y);
                return scaleY > 1e-4f ? -_navAgent.baseOffset / scaleY : 0f;
            }

            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null && capsule.direction == 1)
            {
                return capsule.center.y - (capsule.height / 2f);
            }

            return 0f;
        }

        private void PlaceRig()
        {
            _rig.localPosition = new Vector3(0f, FeetLocalY(), 0f);
            _rig.localRotation = Quaternion.identity;
        }

        // ViewRig は視点を変えたときにしか子孫のレンダラーを書き換えず、後から置いたリグは漏れるので、毎フレーム合わせる。
        private void ApplyVisibility()
        {
            var firstPerson = _view != null && _view.IsLocalView && _view.Mode == ViewMode.FirstPerson;
            var mode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var rigRenderer in _rigRenderers)
            {
                if (rigRenderer != null && rigRenderer.shadowCastingMode != mode)
                {
                    rigRenderer.shadowCastingMode = mode;
                }
            }
        }

        private void ApplyToAnimator(CharacterPoseParameters parameters, float deltaSeconds)
        {
            // Edit Mode では Animator が動いておらず、パラメーターを渡すと警告が出る。
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || !_animator.isInitialized)
            {
                return;
            }

            EnsureParameterTable();
            if (Has(SpeedHash))
            {
                _animator.SetFloat(SpeedHash, parameters.Speed, SpeedDampSeconds, deltaSeconds);
            }

            SetBool(CrouchHash, parameters.Crouch);
            SetBool(AimHash, parameters.Aim);
            SetInt(CoverHash, parameters.Cover);
            SetBool(DownedHash, parameters.Downed);
            SetBool(DeadHash, parameters.Dead);
            SetInt(WeaponCategoryHash, parameters.WeaponCategory);
            SetBool(SlideHash, parameters.Slide);
        }

        // 無いパラメーターを渡すと毎フレーム警告が出るので、あるものだけに渡し、足りなければ一度だけ知らせる。
        private void EnsureParameterTable()
        {
            if (_hasParameterTable)
            {
                return;
            }

            _hasParameterTable = true;
            foreach (var parameter in _animator.parameters)
            {
                _animatorParameters.Add(parameter.nameHash);
            }

            var missing = new List<string>();
            AddIfMissing(missing, CharacterPose.SpeedParameter, SpeedHash);
            AddIfMissing(missing, CharacterPose.CrouchParameter, CrouchHash);
            AddIfMissing(missing, CharacterPose.AimParameter, AimHash);
            AddIfMissing(missing, CharacterPose.CoverParameter, CoverHash);
            AddIfMissing(missing, CharacterPose.DownedParameter, DownedHash);
            AddIfMissing(missing, CharacterPose.DeadParameter, DeadHash);
            AddIfMissing(missing, CharacterPose.WeaponCategoryParameter, WeaponCategoryHash);
            AddIfMissing(missing, CharacterPose.SlideParameter, SlideHash);
            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"{LogTag} '{name}' のリグの AnimatorController に {string.Join(", ", missing)} がありません。CharacterRigBuilder で作り直してください。",
                    this);
            }
        }

        private void AddIfMissing(List<string> missing, string parameterName, int hash)
        {
            if (!_animatorParameters.Contains(hash))
            {
                missing.Add(parameterName);
            }
        }

        private bool Has(int hash)
        {
            return _animatorParameters.Contains(hash);
        }

        private void SetBool(int hash, bool value)
        {
            if (Has(hash))
            {
                _animator.SetBool(hash, value);
            }
        }

        private void SetInt(int hash, int value)
        {
            if (Has(hash))
            {
                _animator.SetInteger(hash, value);
            }
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

        private void HideMockBody()
        {
            _mockBody = GetComponent<PlayerMockBody>();
            if (_mockBody != null)
            {
                _mockBody.ShowsBody = false;
            }
        }

        private void RestoreMockBody()
        {
            if (_mockBody != null)
            {
                _mockBody.ShowsBody = true;
            }

            _mockBody = null;
        }

        // DeathBodyPresenter は倒れたときに体のレンダラーを止めるが、戻すのは持ち主に任せているので、体力を作り直したらリグを描き直す。
        private void SubscribeHealthReset()
        {
            UnsubscribeHealthReset();
            if (_damageable != null)
            {
                _damageable.HealthReset += OnHealthReset;
                _resetSource = _damageable;
            }
        }

        private void UnsubscribeHealthReset()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_resetSource, null))
            {
                _resetSource.HealthReset -= OnHealthReset;
            }

            _resetSource = null;
        }

        private void OnHealthReset(Health _)
        {
            foreach (var rigRenderer in _rigRenderers)
            {
                if (rigRenderer != null)
                {
                    rigRenderer.enabled = true;
                }
            }
        }

        private static void StripColliders(GameObject root)
        {
            // Destroy は次のフレームまで残り、その間に CharacterController や射撃の判定に当たるので、すぐ消す。
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(collider);
            }
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
            }
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
