using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    // 移動・射撃・カバー (Update) と PlayerActionReader (100)・PlayerMockBody (110) の後に、そのフレームの状態を読む。
    [DefaultExecutionOrder(120)]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public const string LogTag = "[CharacterAnimator]";

        public const string RigResourcePath = "Characters/CharacterRig";

        public const string RigObjectName = "CharacterRig";

        // Speed を寄せるのは、歩きと走りの切り替わりで足の振りが跳ばないようにするため。
        public const float SpeedDampSeconds = 0.1f;

        [SerializeField, Tooltip("置くリグのプレハブ。空なら入口 (GameContent) のキャラクターのリグ、それも無ければ Resources の Characters/CharacterRig を読む")]
        private GameObject _rigPrefab;

        [SerializeField, Tooltip("リーン・水泳・壁走りの向きへリグの根を傾けるか。AnimatorController のクリップがそれらの姿勢を体の傾きごと持つなら切る (二重に傾くので)")]
        private bool _tiltsRigWithMotor = true;

        private CharacterAnimatorSource _source;
        private PlayerController _mover;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private CoverController _cover;
        private PlayerVehicleRider _rider;
        private Damageable _damageable;
        private EnemyAgent _enemy;
        private NavMeshAgent _navAgent;
        private ICharacterPoseSource _network;
        private ICharacterMotorStateSource _motorSource;
        private MotorSpec _fallbackMotorSpec;
        private bool _warnedInvalidMotor;
        private readonly HorizontalSpeedMeter _speedMeter = new HorizontalSpeedMeter();
        private ViewRig _view;
        private Quaternion _rigTilt = Quaternion.identity;
        private bool _snapRigTilt;

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

        public Renderer HiddenHostRenderer => _hostRenderer;

        private PlayerMockBody _mockBody;
        private Damageable _resetSource;

        private readonly HashSet<int> _animatorParameters = new HashSet<int>();
        private bool _hasParameterTable;

        private CharacterPoseParameters _parameters;
        private bool _hasParameters;

        private PlayableGraph _emoteGraph;
        private AnimationClip _emoteClip;

        private static readonly int SpeedHash = Animator.StringToHash(CharacterPose.SpeedParameter);
        private static readonly int CrouchHash = Animator.StringToHash(CharacterPose.CrouchParameter);
        private static readonly int AimHash = Animator.StringToHash(CharacterPose.AimParameter);
        private static readonly int CoverHash = Animator.StringToHash(CharacterPose.CoverParameter);
        private static readonly int DownedHash = Animator.StringToHash(CharacterPose.DownedParameter);
        private static readonly int DeadHash = Animator.StringToHash(CharacterPose.DeadParameter);
        private static readonly int WeaponCategoryHash = Animator.StringToHash(CharacterPose.WeaponCategoryParameter);
        private static readonly int SlideHash = Animator.StringToHash(CharacterPose.SlideParameter);
        private static readonly int LeanHash = Animator.StringToHash(CharacterPose.LeanParameter);
        private static readonly int SwimmingHash = Animator.StringToHash(CharacterPose.SwimmingParameter);
        private static readonly int WallRunHash = Animator.StringToHash(CharacterPose.WallRunParameter);

        public GameObject RigPrefab
        {
            get => _rigPrefab;
            set => _rigPrefab = value;
        }

        public bool TiltsRigWithMotor
        {
            get => _tiltsRigWithMotor;
            set => _tiltsRigWithMotor = value;
        }

        public ICharacterMotorStateSource MotorSource
        {
            get => CharacterMotorStateSources.IsMissing(_motorSource) ? null : _motorSource;
            set => _motorSource = value;
        }

        public Quaternion RigTilt => _rigTilt;

        public CharacterAnimatorSource Source => _source;

        public bool IsBuilt => _rig != null;

        public Transform Rig => _rig;

        public bool OwnsRig => _rig != null && _ownsRig;

        public float MeasuredSpeed => _speedMeter.Speed;

        public Animator Animator => _animator;

        public CharacterPoseParameters Parameters => _parameters;

        public bool HasParameters => _hasParameters;

        public CharacterPose Pose => _pose;

        public bool IsPlayingEmote => _emoteGraph.IsValid();

        public AnimationClip EmoteClip => IsPlayingEmote ? _emoteClip : null;

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

        private void OnDisable()
        {
            StopEmote();
        }

        private void OnDestroy()
        {
            Dismantle();
        }

        // AnimatorController に状態を足さずに別のグラフで上書きするのは、ゲームが差し替えるリグのコントローラーにエモートごとの状態を作らせないため。
        // グラフを壊せば Animator は自分のコントローラーの姿勢へ戻る。
        public bool PlayEmote(AnimationClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            StopEmote();
            if (_animator == null || !_animator.isActiveAndEnabled)
            {
                return false;
            }

            AnimationPlayableUtilities.PlayClip(_animator, clip, out _emoteGraph);
            _emoteClip = clip;
            Debug.Log(EmoteLog.ClipStarted(name, clip.name), this);
            return true;
        }

        public void StopEmote()
        {
            if (_emoteGraph.IsValid())
            {
                _emoteGraph.Destroy();
            }

            _emoteGraph = default;
            _emoteClip = null;
        }

        public bool TryBind()
        {
            _mover = GetComponent<PlayerController>();
            _enemy = _mover == null ? GetComponent<EnemyAgent>() : null;
            // GetComponent はエディタで、無い部品に null でない偽物を返すことがあり、インターフェイスでは == null で見分けられない
            _network = _mover == null && _enemy == null && TryGetComponent<ICharacterPoseSource>(out var network) ? network : null;
            _damageable = GetComponent<Damageable>();
            _view = GetComponent<ViewRig>();
            if (TryGetComponent<ICharacterMotorStateSource>(out var motorSource))
            {
                _motorSource = motorSource;
            }

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

            var prefab = GameContent.Pick(_rigPrefab, c => c.CharacterRig);
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(RigResourcePath);
            }
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"{LogTag} '{name}': リグのプレハブ (Resources/{RigResourcePath}) がありません。カプセルの見た目のままにします。"
                    + " unity -batchmode -quit -projectPath . -executeMethod Shooter.Editor.CharacterRigBuilder.Build で作れます。",
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
            _rigTilt = Quaternion.identity;
            _snapRigTilt = true;
            PlaceRig();
            HideHostRenderer();
            HideMockBody();
            SubscribeHealthReset();

            Debug.Log($"{LogTag} rig {how} for '{name}' source={_source} renderers={_rigRenderers.Length}", this);
        }

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
                var targetTilt = _tiltsRigWithMotor
                    ? PlayerMockPose.TraversalLean(_parameters, _parameters.Lean != 0f ? LeanAngleDegrees() : 0f)
                    : Quaternion.identity;
                // 置いた直後は前の傾きが無いので、泳いでいる最中に出てきたキャラが起き上がった姿から倒れ込まないよう、そのまま置く。
                _rigTilt = _snapRigTilt ? targetTilt : Quaternion.Slerp(_rigTilt, targetTilt, PlayerMockPose.BlendFactor(deltaSeconds));
                _snapRigTilt = false;
                PlaceRig();
                ApplyVisibility();
                ApplyToAnimator(_parameters, deltaSeconds);
            }

            return _parameters;
        }

        public CharacterPoseInput ReadState()
        {
            CharacterPoseInput input;
            switch (_source)
            {
                case CharacterAnimatorSource.Player:
                    input = ReadPlayer();
                    break;
                case CharacterAnimatorSource.Enemy:
                    input = ReadEnemy();
                    break;
                case CharacterAnimatorSource.Network:
                    input = _network.ReadPose(_speedMeter.Speed);
                    break;
                default:
                    throw new InvalidOperationException($"CharacterAnimator '{name}' has no state source; call TryBind first");
            }

            return TryReadMotor(out var motor) ? input.WithMotor(motor) : input;
        }

        private bool TryReadMotor(out CharacterMotorState motor)
        {
            motor = default;
            var source = MotorSource;
            if (source == null || !source.TryGetMotorState(out motor))
            {
                return false;
            }

            // 壊れた状態で毎フレーム例外を投げると、ほかの姿勢まで止まるので、リーン・水泳・壁走りだけを諦める。
            if (!motor.Lean.IsValid || !motor.WallRun.IsValid)
            {
                if (!_warnedInvalidMotor)
                {
                    Debug.LogWarning($"{LogTag} '{name}' の移動の状態のリーンか壁走りが範囲の外なので、それらを読まずに姿勢を決めます。", this);
                    _warnedInvalidMotor = true;
                }

                return false;
            }

            return true;
        }

        // 移動の状態の読み元・プレイヤーの調整値の順に探し、どちらも無ければ (敵・ネット対戦) 設定された既定の調整値を一度だけ読む。
        private float LeanAngleDegrees()
        {
            var source = MotorSource;
            var spec = source != null ? source.Spec : null;
            if (spec == null && _mover != null)
            {
                spec = _mover.Spec;
            }

            if (spec == null)
            {
                _fallbackMotorSpec = _fallbackMotorSpec ?? MotorSettings.ResolveOrDefault(null);
                spec = _fallbackMotorSpec;
            }

            return spec.LeanAngleDegrees;
        }

        public void Dismantle()
        {
            StopEmote();
            UnsubscribeHealthReset();
            if (_rig != null && _ownsRig)
            {
                DestroySafely(_rig.gameObject);
            }

            _rig = null;
            _ownsRig = false;
            _animator = null;
            _rigRenderers = Array.Empty<Renderer>();
            _rigTilt = Quaternion.identity;
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

            // 座席では乗り物と一緒に動くだけなので、その速さで走る姿にしない。
            var seated = Rider() != null && Rider().IsRiding;
            return new CharacterPoseInput(
                seated ? 0f : HorizontalSpeed(velocity),
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
            var aiming = _enemy.IsAiming;

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
                sprint = _enemy.SprintSpeed;
                crouch = _enemy.CrouchSpeed;
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

                _pose = FallbackPose();
            }
        }

        // MotorSpec は歩きと走りが同じ速さでも受け付けるが CharacterPose は受け付けないので、設定された値でも決められなければコードの既定値で決める。
        private static CharacterPose FallbackPose()
        {
            try
            {
                return CharacterPose.FromMotor(MotorSettings.ResolveOrDefault(null));
            }
            catch (ArgumentOutOfRangeException)
            {
                return CharacterPose.FromMotor(MotorSpec.Default);
            }
        }

        private static float HorizontalSpeed(Vector3 velocity)
        {
            var speed = new Vector2(velocity.x, velocity.z).magnitude;
            return speed >= 0f && !float.IsInfinity(speed) ? speed : 0f;
        }

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
            var seatFacing = SeatFacing();
            _rig.localRotation = seatFacing.HasValue
                ? Quaternion.Inverse(transform.rotation) * seatFacing.Value * _rigTilt
                : _rigTilt;
        }

        // 座席では本体の向きが視点 (追うカメラの回り込み) に付いて回るので、自分の武器で撃てない席では見た目だけ座席の前へ向け直す。
        private Quaternion? SeatFacing()
        {
            var rider = Rider();
            var seat = rider != null ? rider.Seat : null;
            if (seat == null || rider.Firing == VehicleSeatFiring.OwnWeapon)
            {
                return null;
            }

            return Quaternion.Euler(0f, seat.SitYawDegrees, 0f);
        }

        private PlayerVehicleRider Rider()
        {
            if (_source != CharacterAnimatorSource.Player)
            {
                return null;
            }

            return _rider != null ? _rider : (_rider = GetComponent<PlayerVehicleRider>());
        }

        // ViewRig は視点を変えたときにしか子孫のレンダラーを書き換えず、後から置いたリグは漏れるので、毎フレーム合わせる。
        private void ApplyVisibility()
        {
            var firstPerson = _view != null && _view.IsLocalView && (_view.Mode == ViewMode.FirstPerson || _view.IsBodyHiddenBySight);
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
            if (Has(LeanHash))
            {
                _animator.SetFloat(LeanHash, parameters.Lean);
            }

            SetBool(SwimmingHash, parameters.Swimming);
            SetInt(WallRunHash, parameters.WallRun);
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
            AddIfMissing(missing, CharacterPose.LeanParameter, LeanHash);
            AddIfMissing(missing, CharacterPose.SwimmingParameter, SwimmingHash);
            AddIfMissing(missing, CharacterPose.WallRunParameter, WallRunHash);
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
