using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 入力でなく Rigidbody の動きから歩き方を出すのは、入力の届かない他人の馬 (オンラインで位置だけ受け取る馬) でも同じ見た目にするため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HorseGaitAnimator : MonoBehaviour
    {
        public const string DefaultGaitParameter = "gait";
        public const string DefaultSpeedParameter = "speed";
        public const string DefaultJumpTrigger = "jump";

        // 坂を駆け上がるときの上向きの速さで跳んだとみなさないよう、地面を離れたうえでこれより速く昇るときだけ跳んだとする。
        public const float DefaultJumpRiseSpeed = 1f;

        [SerializeField, Tooltip("歩き方を渡す Animator。空なら何もしない")]
        private Animator _animator;

        [SerializeField, Tooltip("歩き方 (Idle=0, Walk=1, Trot=2, Canter=3) を渡す int の引数名。空なら渡さない")]
        private string _gaitParameter = DefaultGaitParameter;

        [SerializeField, Tooltip("前向きの速さ (m/s、後ずさりは負) を渡す float の引数名。空なら渡さない")]
        private string _speedParameter = DefaultSpeedParameter;

        [SerializeField, Tooltip("跳んだときに引く trigger の名前。空なら引かない")]
        private string _jumpTrigger = DefaultJumpTrigger;

        [SerializeField, Min(0f), Tooltip("地面を離れてこの速さ (m/s) より速く昇り始めたら跳んだとみなす")]
        private float _jumpRiseSpeed = DefaultJumpRiseSpeed;

        private static readonly HorseSpec FallbackSpec = HorseSpec.Default;

        private readonly List<string> _missing = new List<string>();

        private Rigidbody _body;
        private SimpleHorseMotor _motor;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private Vector3 _velocity;
        private float _forwardSpeed;
        private HorseGait _gait;
        private bool _wasRising;
        private bool _jumped;

        private Animator _checkedAnimator;
        private RuntimeAnimatorController _checkedController;
        private bool _hasGait;
        private bool _hasSpeed;
        private bool _hasJump;
        private int _gaitHash;
        private int _speedHash;
        private int _jumpHash;
        private bool _warnedMissing;

        public Animator Animator
        {
            get => _animator;
            set
            {
                _animator = value;
                ForgetParameters();
            }
        }

        public string GaitParameter
        {
            get => _gaitParameter;
            set
            {
                _gaitParameter = value;
                ForgetParameters();
            }
        }

        public string SpeedParameter
        {
            get => _speedParameter;
            set
            {
                _speedParameter = value;
                ForgetParameters();
            }
        }

        public string JumpTrigger
        {
            get => _jumpTrigger;
            set
            {
                _jumpTrigger = value;
                ForgetParameters();
            }
        }

        public float JumpRiseSpeed
        {
            get => _jumpRiseSpeed;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "JumpRiseSpeed must be a non-negative finite number");
                }

                _jumpRiseSpeed = value;
            }
        }

        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());

        // 同じ GameObject の SimpleHorseMotor と同じ境目で歩き方を分けるのは、馬の動きと見た目の歩き方の段階を食い違わせないため。
        public HorseSpec Spec
        {
            get
            {
                // 見つからなくても毎回探し直すのは、ゲームが後から SimpleHorseMotor を足しても境目をそちらへ合わせるため。
                if (_motor == null)
                {
                    _motor = GetComponent<SimpleHorseMotor>();
                }

                return _motor != null ? _motor.Spec : FallbackSpec;
            }
        }

        public HorseGait Gait => _gait;

        public int GaitValue => (int)_gait;

        public float ForwardSpeed => _forwardSpeed;

        public Vector3 Velocity => _velocity;

        public bool Jumped => _jumped;

        public bool IsMissingParameters => _missing.Count > 0;

        private void Awake()
        {
            // 専用サーバーには画面が無く、見せる相手がいない。
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
            }
        }

        private void Reset()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        private void OnValidate()
        {
            ForgetParameters();
        }

        private void OnEnable()
        {
            _hasLastPosition = false;
            _wasRising = false;
        }

        private void Update()
        {
            Refresh(Time.deltaTime);
        }

        // Update から呼ぶ。Animator の進まない Edit Mode のテストからも直接呼べるよう public にする。
        public void Refresh(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var body = Body;
            _velocity = MeasureVelocity(body, deltaSeconds);
            _forwardSpeed = Vector3.Dot(_velocity, Heading(transform.rotation));
            _gait = HorseStride.GaitOf(Spec, _forwardSpeed);

            var grounded = _motor != null && _motor.IsGrounded;
            var rising = !grounded && _velocity.y > _jumpRiseSpeed;
            // 昇り始めの一度だけ引く。昇っている間ずっと引くと、着地のあとに跳ぶ姿勢をもう一度始めてしまうため。
            _jumped = rising && !_wasRising;
            _wasRising = rising;

            Apply();
        }

        // 他人の馬は Rigidbody を kinematic にして位置だけ置かれ、Rigidbody の速さが 0 のままなので、置かれた位置の差から速さを測る。
        private Vector3 MeasureVelocity(Rigidbody body, float deltaSeconds)
        {
            var position = transform.position;
            var velocity = _velocity;
            if (!body.isKinematic)
            {
                velocity = body.linearVelocity;
            }
            else if (_hasLastPosition && deltaSeconds > 0f)
            {
                velocity = (position - _lastPosition) / deltaSeconds;
            }
            else if (!_hasLastPosition)
            {
                velocity = Vector3.zero;
            }

            _lastPosition = position;
            _hasLastPosition = true;
            return velocity;
        }

        private void Apply()
        {
            // Edit Mode や組み立て途中では Animator が動いておらず、引数を渡すと警告が出る。
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || !_animator.isInitialized)
            {
                return;
            }

            EnsureParameters();
            if (_hasGait)
            {
                _animator.SetInteger(_gaitHash, (int)_gait);
            }

            if (_hasSpeed)
            {
                _animator.SetFloat(_speedHash, _forwardSpeed);
            }

            if (_hasJump && _jumped)
            {
                _animator.SetTrigger(_jumpHash);
            }
        }

        // 無い引数を渡すと毎フレーム警告が出るので、型まで合う引数だけに渡し、足りなければ一度だけ知らせる。
        private void EnsureParameters()
        {
            var controller = _animator.runtimeAnimatorController;
            if (_checkedAnimator == _animator && _checkedController == controller)
            {
                return;
            }

            _checkedAnimator = _animator;
            _checkedController = controller;
            _missing.Clear();
            var parameters = _animator.parameters;
            _hasGait = Find(parameters, _gaitParameter, AnimatorControllerParameterType.Int, out _gaitHash);
            _hasSpeed = Find(parameters, _speedParameter, AnimatorControllerParameterType.Float, out _speedHash);
            _hasJump = Find(parameters, _jumpTrigger, AnimatorControllerParameterType.Trigger, out _jumpHash);
            if (_missing.Count > 0 && !_warnedMissing)
            {
                _warnedMissing = true;
                Debug.LogWarning(VehicleLog.MissingAnimatorParameters(name, controller.name, _missing), this);
            }
        }

        private bool Find(AnimatorControllerParameter[] parameters, string parameterName, AnimatorControllerParameterType type, out int hash)
        {
            hash = 0;
            if (string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            hash = Animator.StringToHash(parameterName);
            foreach (var parameter in parameters)
            {
                if (parameter.nameHash == hash && parameter.type == type)
                {
                    return true;
                }
            }

            _missing.Add(parameterName);
            return false;
        }

        private void ForgetParameters()
        {
            _checkedAnimator = null;
            _checkedController = null;
            _hasGait = false;
            _hasSpeed = false;
            _hasJump = false;
            _warnedMissing = false;
            _missing.Clear();
        }

        // SimpleHorseMotor と同じく、鼻先が真上か真下を向くと水平の向きが無いので、そのときは背の向きを進む向きにする。
        private static Vector3 Heading(Quaternion rotation)
        {
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f)
            {
                return forward.normalized;
            }

            var up = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            return up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.forward;
        }
    }
}
