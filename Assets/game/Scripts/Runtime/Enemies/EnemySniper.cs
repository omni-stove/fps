using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    // EnemyAgent の Update で狙いと撃つかが決まった後に、同じフレームで光線を引き直す。
    [DefaultExecutionOrder(10)]
    public sealed class EnemySniper : MonoBehaviour
    {
        public const string BeamPlaceholderName = "SniperAimBeam";
        public const float DefaultBeamWidth = 0.04f;

        [SerializeField, Tooltip("撃った後に移る足場 (ワールド座標の足元)。シーン生成スクリプトが配線する。空なら移らない")]
        private Vector3[] _perches = Array.Empty<Vector3>();

        [SerializeField, Tooltip("照準の光線。+Z に長さ 1 の形を、目から目標へ伸ばす。空なら実行時に仮の線を作る")]
        private Transform _aimBeam;

        [SerializeField, Min(0.001f), Tooltip("照準の光線の太さ (m)")]
        private float _beamWidth = DefaultBeamWidth;

        private EnemyAgent _agent;
        private SniperEnemyBehavior _behavior;
        private EnemyBrain _attachedBrain;
        private float _lastAimElapsed;
        private bool _confirming;
        private bool _active;

        public SniperEnemyBehavior Behavior => EnsureBehavior();

        public IReadOnlyList<Vector3> Perches => _perches;

        public Transform AimBeam
        {
            get => _aimBeam;
            set => _aimBeam = value;
        }

        public bool IsBeamVisible => _aimBeam != null && _aimBeam.gameObject.activeSelf;

        public bool IsAttached => _attachedBrain != null;

        private string Id => _agent != null ? _agent.Id : name;

        public void SetPerches(IReadOnlyList<Vector3> perches)
        {
            if (perches == null)
            {
                throw new ArgumentNullException(nameof(perches));
            }

            var copy = new Vector3[perches.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = perches[i];
            }

            _perches = copy;
            ApplyPerches();
        }

        private void Awake()
        {
            EnsureInitialized();
            HideBeam();
        }

        // Edit Mode のテストでは Awake が走らないので、Tick からも呼ぶ。
        private void EnsureInitialized()
        {
            if (_agent != null)
            {
                return;
            }

            _agent = GetComponent<EnemyAgent>();
            EnsureBehavior();
            _agent.Fired += OnFired;
            if (_aimBeam == null)
            {
                _aimBeam = CreateBeamPlaceholder(null);
            }
        }

        private void OnDisable()
        {
            HideBeam();
            Detach();
        }

        private void OnDestroy()
        {
            if (_agent != null)
            {
                _agent.Fired -= OnFired;
            }

            if (_behavior != null)
            {
                _behavior.PhaseChanged -= OnPhaseChanged;
            }

            Detach();

            // 光線は持ち主の子にしていない (持ち主の向きや大きさに引きずられないように) ので、仮に作ったものは一緒に消す。
            if (_aimBeam != null && _aimBeam.name == BeamPlaceholderName && !_aimBeam.IsChildOf(transform))
            {
                if (Application.isPlaying)
                {
                    Destroy(_aimBeam.gameObject);
                }
                else
                {
                    DestroyImmediate(_aimBeam.gameObject);
                }
            }
        }

        private void Update()
        {
            Tick();
        }

        public void Tick()
        {
            EnsureInitialized();
            if (!TryAttach())
            {
                HideBeam();
                return;
            }

            var spec = _attachedBrain.Spec;
            _active = spec.Archetype == EnemyArchetype.Sniper && !_attachedBrain.IsDead;
            _agent.ShotDamageMultiplier = _active ? spec.Sniper.ShotDamageMultiplier : 1f;

            // 怯みの間は判断が進まず、光線を出したまま止まってしまう。解けたら狙い直させる。
            if (_active && _agent.IsInterrupted)
            {
                _behavior.CancelAim();
            }

            var aiming = _behavior.Phase == SniperPhase.Aiming || _behavior.Phase == SniperPhase.Firing;
            if (aiming)
            {
                _lastAimElapsed = _behavior.AimElapsed;
            }

            var target = _agent.Target;
            if (_active && aiming && target != null && !target.IsDead)
            {
                ShowBeam(_agent.EyePosition, target.AimPoint);
            }
            else
            {
                HideBeam();
            }
        }

        public static Transform CreateBeamPlaceholder(Transform parent)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = BeamPlaceholderName;

            // 光線が弾や視線を遮らないよう、当たり判定は持たせない。
            var collider = beam.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            var renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (parent != null)
            {
                beam.transform.SetParent(parent, false);
            }

            beam.SetActive(false);
            return beam.transform;
        }

        private void ShowBeam(Vector3 from, Vector3 to)
        {
            if (_aimBeam == null)
            {
                return;
            }

            var offset = to - from;
            var length = offset.magnitude;
            if (length < 1e-3f)
            {
                HideBeam();
                return;
            }

            _aimBeam.SetPositionAndRotation(from + (offset / 2f), Quaternion.LookRotation(offset / length, Vector3.up));
            var parent = _aimBeam.parent;
            var world = new Vector3(_beamWidth, _beamWidth, length);
            _aimBeam.localScale = parent != null ? Divide(world, parent.lossyScale) : world;
            if (!_aimBeam.gameObject.activeSelf)
            {
                _aimBeam.gameObject.SetActive(true);
            }
        }

        private void HideBeam()
        {
            if (_aimBeam != null && _aimBeam.gameObject.activeSelf)
            {
                _aimBeam.gameObject.SetActive(false);
            }
        }

        private static Vector3 Divide(Vector3 value, Vector3 by)
        {
            return new Vector3(
                Mathf.Abs(by.x) > 1e-6f ? value.x / by.x : value.x,
                Mathf.Abs(by.y) > 1e-6f ? value.y / by.y : value.y,
                Mathf.Abs(by.z) > 1e-6f ? value.z / by.z : value.z);
        }

        // 撃てたかは連射間隔や弾切れで決まるので、判断が Fire を出したことではなく、実際に撃ったことで位置を変え始める。
        private void OnFired(Weapon weapon)
        {
            if (!_active || _behavior.Phase != SniperPhase.Firing)
            {
                return;
            }

            Debug.Log(SniperLog.Fire(Id, _agent.ShotDamageMultiplier), this);
            _confirming = true;
            try
            {
                _behavior.ConfirmShot(_agent.GroundPosition.ToNumerics());
            }
            finally
            {
                _confirming = false;
            }
        }

        private void OnPhaseChanged(SniperPhase previous, SniperPhase next)
        {
            Debug.Log(SniperLog.Phase(Id, previous, next), this);
            switch (next)
            {
                case SniperPhase.Aiming:
                    var target = _agent != null ? _agent.Target : null;
                    var seconds = _attachedBrain != null ? _attachedBrain.Spec.Sniper.AimSeconds : SniperSpec.DefaultAimSeconds;
                    Debug.Log(SniperLog.Aim(Id, target != null ? target.Id : "none", seconds), this);
                    break;
                case SniperPhase.Relocating:
                    var index = _behavior.PerchIndex;
                    Debug.Log(SniperLog.Relocate(Id, index, _behavior.Perches[index]), this);
                    break;
                case SniperPhase.Holding when previous == SniperPhase.Relocating:
                    Debug.Log(SniperLog.Arrived(Id, _behavior.PerchIndex, _behavior.LastRelocationGaveUp), this);
                    break;
                case SniperPhase.Holding when _confirming:
                    Debug.Log(SniperLog.Stay(Id), this);
                    break;
                case SniperPhase.Holding:
                    Debug.Log(SniperLog.AimCancelled(Id, _lastAimElapsed), this);
                    break;
            }
        }

        // EnemyAgent は Awake で判断を作るが、同じ GameObject の Awake の順は決まらないので、使うときに付ける。
        private bool TryAttach()
        {
            var brain = _agent != null ? _agent.Brain : null;
            if (brain == null)
            {
                return false;
            }

            if (ReferenceEquals(brain, _attachedBrain))
            {
                return true;
            }

            Detach();
            brain.AddBehavior(EnsureBehavior());
            brain.StateChanged += OnBrainStateChanged;
            _attachedBrain = brain;
            return true;
        }

        private void Detach()
        {
            if (_attachedBrain == null)
            {
                return;
            }

            _attachedBrain.RemoveBehavior(_behavior);
            _attachedBrain.StateChanged -= OnBrainStateChanged;
            _attachedBrain = null;
        }

        // 倒れて生き返ったら、前の命の狙いや移る先を持ち越さない。
        private void OnBrainStateChanged(EnemyState previous, EnemyState next)
        {
            if (next == EnemyState.Dead)
            {
                _behavior.Reset();
            }
        }

        private SniperEnemyBehavior EnsureBehavior()
        {
            if (_behavior == null)
            {
                _behavior = new SniperEnemyBehavior();
                _behavior.PhaseChanged += OnPhaseChanged;
                ApplyPerches();
            }

            return _behavior;
        }

        private void ApplyPerches()
        {
            if (_behavior == null)
            {
                return;
            }

            var perches = new System.Numerics.Vector3[_perches.Length];
            for (var i = 0; i < perches.Length; i++)
            {
                perches[i] = _perches[i].ToNumerics();
            }

            _behavior.SetPerches(perches);
        }
    }
}
