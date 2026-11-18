using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    // オフラインの StreakController とネット対戦の NetV2StreakPart が同じ選び方を使えるよう、報酬を使うことは決めず、選んだ地点を知らせるだけにする。
    [DisallowMultipleComponent]
    public sealed class StreakTargeting : MonoBehaviour
    {
        public const string LogTag = StreakLog.LogTag;

        public const string CancelledByInput = "input";

        public const string CancelledByDeath = "died";

        public const string CancelledByDisable = "disabled";

        public const string CancelledByRules = "rules";

        public const float CodeLineWidth = 1.5f;

        public static readonly Color CodeLineColor = new Color(1f, 0.25f, 0.15f, 0.9f);

        // 見下ろすカメラはプレイヤーのカメラ (深さ 0 前後) より後に描き、画面を覆う。
        private const float _viewDepth = 50f;

        // 射程の円の端に置いた地点と線の先まで画面に入るよう、射程より少し広く映す。
        private const float _viewMarginFraction = 0.15f;

        // 矢じりの長さは線の長さに対する割合で決め、線を短くしても向きが読めるようにする。
        private const float _arrowFraction = 0.2f;

        private const string _viewObjectName = "StreakTargetingView";

        private const string _markerObjectName = "StreakTargetingMarker";

        private static Material _codeLineMaterial;

        [SerializeField, Tooltip("地点と向きを示す見た目。子に LineRenderer があれば線の端を地点と向きに合わせて置き直す。空ならコードで線を作る")]
        private GameObject _markerPrefab;

        private readonly List<ControlHandoff> _claimed = new List<ControlHandoff>();
        private readonly List<InputAction> _enabledActions = new List<InputAction>();

        private StreakAim _aim;
        private StreakTargetingSpec _spec;
        private StreakTargetingStatus? _published;
        private FirstPersonLook _look;
        private bool _lookWasSuspended;
        private AimDownSights _ads;
        private bool _adsWasEnabled;
        private Camera _camera;
        private GameObject _marker;
        private LineRenderer _markerLine;
        private int _begunFrame = -1;
        private int _restoreAfterFrame = -1;

        [NonSerialized]
        private InputAction _moveAction;
        [NonSerialized]
        private InputAction _attackAction;
        [NonSerialized]
        private InputAction _aimAction;
        [NonSerialized]
        private InputAction _useAction;
        [NonSerialized]
        private InputAction _leanLeftAction;
        [NonSerialized]
        private InputAction _leanRightAction;

        public event Action<StreakTargetingStatus> Confirmed;

        public event Action<string, string> Cancelled;

        public GameObject MarkerPrefab
        {
            get => _markerPrefab;
            set => _markerPrefab = value;
        }

        public string LogId { get; set; }

        public bool IsActive => _aim != null;

        public StreakAim Aim => _aim;

        public StreakTargetingStatus? Status => _aim?.Status;

        public Camera ViewCamera => _camera;

        public GameObject Marker => _marker;

        // 手放させる口は、オフラインでは PlayerController と WeaponController の、ネット対戦では NetV2LocalPlayer のものを渡す。
        public bool Begin(StreakAim aim, StreakTargetingSpec spec, IReadOnlyList<ControlHandoff> handoffs, FirstPersonLook look)
        {
            if (aim == null)
            {
                throw new ArgumentNullException(nameof(aim));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (IsActive)
            {
                return false;
            }

            _aim = aim;
            _spec = spec;
            _begunFrame = Time.frameCount;
            Hold(handoffs, look);
            ResolveActions();
            Ground();
            ShowView();
            Debug.Log(StreakLog.TargetingStarted(Id, aim.RewardId), this);
            Publish();
            return true;
        }

        public bool Confirm()
        {
            if (!IsActive)
            {
                return false;
            }

            var status = _aim.Status;
            End();
            Debug.Log(StreakLog.TargetingConfirmed(Id, status), this);
            Confirmed?.Invoke(status);
            return true;
        }

        public bool Cancel(string reason)
        {
            if (!IsActive)
            {
                return false;
            }

            var rewardId = _aim.RewardId;
            End(reason != CancelledByDeath && reason != CancelledByDisable);
            Debug.Log(StreakLog.TargetingCancelled(Id, rewardId, reason ?? string.Empty), this);
            Cancelled?.Invoke(rewardId, reason ?? string.Empty);
            return true;
        }

        public void Step(StreakTargetingInput input, float deltaSeconds)
        {
            if (!IsActive)
            {
                return;
            }

            if (input.Cancel)
            {
                Cancel(CancelledByInput);
                return;
            }

            _aim.Move(input.Move.ToNumerics(), _spec.CursorSpeed, deltaSeconds);
            if (input.TurnDegrees != 0f)
            {
                _aim.Turn(input.TurnDegrees);
            }

            Ground();
            PlaceMarker();
            Publish();

            if (input.Confirm)
            {
                Confirm();
            }
        }

        private string Id => string.IsNullOrEmpty(LogId) ? name : LogId;

        private void Update()
        {
            // 始めた押下と同じフレームの押下を、取消や決定として読まない。
            if (!IsActive || GameUi.IsPauseOpen || Time.frameCount == _begunFrame)
            {
                return;
            }

            Step(ReadInput(Time.deltaTime), Time.deltaTime);
        }

        // 決定や取消に使った押下で同じフレームに撃ったり覗き込んだりしないよう、返すのは押したフレームが終わってからにする。
        private void LateUpdate()
        {
            if (_restoreAfterFrame >= 0 && Time.frameCount > _restoreAfterFrame)
            {
                Restore();
            }
        }

        private void OnDisable()
        {
            Cancel(CancelledByDisable);
            Restore();
        }

        private void OnDestroy()
        {
            Cancel(CancelledByDisable);
            Restore();
            DestroyView();
        }

        private StreakTargetingInput ReadInput(float deltaSeconds)
        {
            var move = _moveAction != null ? Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            var turn = _look != null ? _look.ReadLookDegrees(deltaSeconds).x : 0f;
            var buttons = (Held(_leanRightAction) ? 1f : 0f) - (Held(_leanLeftAction) ? 1f : 0f);
            turn += buttons * _spec.TurnDegreesPerSecond * deltaSeconds;
            return new StreakTargetingInput(move, turn, Pressed(_attackAction), Pressed(_aimAction) || Pressed(_useAction));
        }

        private void Hold(IReadOnlyList<ControlHandoff> handoffs, FirstPersonLook look)
        {
            Restore();
            if (handoffs != null)
            {
                foreach (var handoff in handoffs)
                {
                    if (handoff != null && !_claimed.Contains(handoff) && handoff.Claim(this))
                    {
                        _claimed.Add(handoff);
                    }
                }
            }

            _look = look;
            if (_look != null)
            {
                _lookWasSuspended = _look.InputSuspended;
                _look.InputSuspended = true;
            }

            // 覗き込みは手放しの口を見ないので、取消に使う照準のボタンで覗き込まないよう、選んでいる間は止める。
            _ads = GetComponent<AimDownSights>();
            if (_ads != null)
            {
                _ads.Release();
                _adsWasEnabled = _ads.enabled;
                _ads.enabled = false;
            }
        }

        private void End(bool deferRestore = true)
        {
            ReleaseActions();
            HideView();
            _aim = null;
            _spec = null;
            Unpublish();

            // Edit Mode ではフレームが進まないので、すぐ返す。倒れた・無効になったときは押下が残らず、
            // 先送りするとデスカメラが覚えた値と食い違うので、その場で返す。
            if (deferRestore && Application.isPlaying)
            {
                _restoreAfterFrame = Time.frameCount;
            }
            else
            {
                Restore();
            }
        }

        private void Restore()
        {
            _restoreAfterFrame = -1;
            foreach (var handoff in _claimed)
            {
                handoff.Release(this);
            }

            _claimed.Clear();
            if (_look != null)
            {
                _look.InputSuspended = _lookWasSuspended;
            }

            _look = null;
            if (_ads != null)
            {
                _ads.enabled = _adsWasEnabled;
            }

            _ads = null;
        }

        private void ResolveActions()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                return;
            }

            _moveAction = Acquire(actions, InputActionPaths.Move);
            _attackAction = Acquire(actions, InputActionPaths.Attack);
            _aimAction = Acquire(actions, InputActionPaths.Aim);
            _useAction = Acquire(actions, InputActionPaths.UseStreak);
            _leanLeftAction = Acquire(actions, InputActionPaths.LeanLeft);
            _leanRightAction = Acquire(actions, InputActionPaths.LeanRight);
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private InputAction Acquire(InputActionAsset actions, string path)
        {
            var action = actions.FindAction(path);
            if (action != null && !action.enabled)
            {
                action.Enable();
                _enabledActions.Add(action);
            }

            return action;
        }

        private void ReleaseActions()
        {
            foreach (var action in _enabledActions)
            {
                action.Disable();
            }

            _enabledActions.Clear();
            _moveAction = null;
            _attackAction = null;
            _aimAction = null;
            _useAction = null;
            _leanLeftAction = null;
            _leanRightAction = null;
        }

        // 見下ろした地図では高さを選べないので、地点の真上から落として当たった面の高さを使う。
        private void Ground()
        {
            var target = _aim.Target.ToUnity();
            var height = _spec.ViewHeight;
            var top = new Vector3(target.x, _aim.Origin.Y + height, target.z);
            if (Hitscan.TryFirstHit(top, Vector3.down, height * 2f, transform, out var hit))
            {
                _aim.SetGroundHeight(hit.point.y);
            }
        }

        private void ShowView()
        {
            if (_camera == null)
            {
                var view = new GameObject(_viewObjectName);
                if (gameObject.scene.IsValid() && view.scene != gameObject.scene)
                {
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(view, gameObject.scene);
                }

                _camera = view.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.depth = _viewDepth;
                _camera.nearClipPlane = 0.3f;
            }

            var origin = _aim.Origin.ToUnity();
            var height = _spec.ViewHeight;
            _camera.transform.SetPositionAndRotation(origin + (Vector3.up * height), Quaternion.Euler(90f, 0f, 0f));
            _camera.orthographicSize = _aim.Range * (1f + _viewMarginFraction);
            _camera.farClipPlane = height * 2f;
            _camera.gameObject.SetActive(true);

            if (_marker == null)
            {
                _marker = _markerPrefab != null
                    ? VisualPrefab.Instantiate(_markerPrefab, null, _markerObjectName, stripColliders: true)
                    : CreateCodeMarker();
                _markerLine = _marker.GetComponentInChildren<LineRenderer>(true);
                if (_markerLine != null)
                {
                    // 端を動かすたびに世界の点で置き直すので、プレハブの設定に関わらず世界の座標で描く。
                    _markerLine.useWorldSpace = true;
                    _markerLine.positionCount = 5;
                }
            }

            _marker.SetActive(true);
            PlaceMarker();
        }

        private void HideView()
        {
            if (_camera != null)
            {
                _camera.gameObject.SetActive(false);
            }

            if (_marker != null)
            {
                _marker.SetActive(false);
            }
        }

        private void DestroyView()
        {
            if (_camera != null)
            {
                DestroyObject(_camera.gameObject);
                _camera = null;
            }

            if (_marker != null)
            {
                DestroyObject(_marker);
                _marker = null;
                _markerLine = null;
            }
        }

        // 線は後ろの端から前の端へ引き、前の端に矢じりを付けて、機体が来る向きを地図の上で読めるようにする。
        private void PlaceMarker()
        {
            if (_marker == null || _aim == null)
            {
                return;
            }

            var center = _aim.Target.ToUnity();
            var direction = AirstrikeLine.Direction(_aim.HeadingDegrees).ToUnity();
            var half = Mathf.Max(_aim.LineLength * 0.5f, CodeLineWidth);
            _marker.transform.SetPositionAndRotation(center, Quaternion.LookRotation(direction, Vector3.up));
            if (_markerLine == null)
            {
                return;
            }

            var lift = Vector3.up * 0.2f;
            var back = center - (direction * half) + lift;
            var front = center + (direction * half) + lift;
            var arrow = half * 2f * _arrowFraction;
            var side = Vector3.Cross(Vector3.up, direction);
            _markerLine.SetPosition(0, back);
            _markerLine.SetPosition(1, front);
            _markerLine.SetPosition(2, front - (direction * arrow) + (side * arrow * 0.6f));
            _markerLine.SetPosition(3, front);
            _markerLine.SetPosition(4, front - (direction * arrow) - (side * arrow * 0.6f));
        }

        private void Publish()
        {
            var status = _aim.Status;
            if (_published.HasValue && _published.Value.Equals(status))
            {
                return;
            }

            _published = status;
            GameSignals.StreakTargeting.Publish(status);
        }

        // 後から別の持ち主が出した値までは消さない。
        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.StreakTargeting.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private static GameObject CreateCodeMarker()
        {
            var marker = new GameObject(_markerObjectName);
            var line = marker.AddComponent<LineRenderer>();
            line.positionCount = 5;
            line.useWorldSpace = true;
            line.widthMultiplier = CodeLineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.startColor = CodeLineColor;
            line.endColor = CodeLineColor;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sharedMaterial = CodeLineMaterial();
            return marker;
        }

        // 頂点色で塗れて照明に左右されない組み込みのシェーダーを使う。どのパイプラインでも描け、ビルドにも常に入る。
        private static Material CodeLineMaterial()
        {
            if (_codeLineMaterial != null)
            {
                return _codeLineMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning($"{LogTag} the built-in shader 'Sprites/Default' is missing; the code-built targeting line uses the default material");
                return null;
            }

            _codeLineMaterial = new Material(shader) { name = "StreakTargetingLine", hideFlags = HideFlags.DontSave };
            return _codeLineMaterial;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static bool Pressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        private static bool Held(InputAction action)
        {
            return action != null && action.IsPressed();
        }
    }

    public readonly struct StreakTargetingInput
    {
        public StreakTargetingInput(Vector2 move, float turnDegrees, bool confirm, bool cancel)
        {
            Move = move;
            TurnDegrees = turnDegrees;
            Confirm = confirm;
            Cancel = cancel;
        }

        public Vector2 Move { get; }

        public float TurnDegrees { get; }

        public bool Confirm { get; }

        public bool Cancel { get; }
    }
}
