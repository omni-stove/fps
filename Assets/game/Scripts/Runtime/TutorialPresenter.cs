using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    // PlayerActionReader (100) が LateUpdate でそのフレームの状態を組み立ててから読む
    [DefaultExecutionOrder(110)]
    public sealed class TutorialPresenter : MonoBehaviour
    {
        public const string LogTag = "[Tutorial]";

        public const float CompletedDisplaySeconds = 4f;

        private static readonly string[] _deviceActionPaths =
        {
            InputActionPaths.Move,
            InputActionPaths.Look,
            InputActionPaths.Attack,
            InputActionPaths.Aim,
            InputActionPaths.Reload,
            InputActionPaths.Crouch,
            InputActionPaths.Cover,
            InputActionPaths.Jump,
            InputActionPaths.Sprint,
        };

        private readonly TutorialSteps _steps = new TutorialSteps();
        private TutorialProgressStore _store = new TutorialProgressStore();
        private PlayerActionReader _reader;
        private WeaponController _weapon;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction[] _deviceActions = Array.Empty<InputAction>();
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private int _pendingHits;
        private bool _reloadStarted;
        private bool _running;
        private bool _subscribed;
        private float _completedSeconds;
        private TutorialStatus? _status;
        private GameBootstrap _waitingBootstrap;

        public TutorialSteps Steps => _steps;

        public bool IsRunning => _running;

        public InputDeviceKind Device => _device;

        // 差し替えられるのは、テストで本物の記録を壊さないよう別のキーの store を使うため。
        public TutorialProgressStore Store
        {
            get => _store;
            set => _store = value ?? throw new ArgumentNullException(nameof(value));
        }

        public TutorialStatus? Status => _status;

        private void Start()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            var menu = GameUi.Menu;
            if (menu == null)
            {
                Debug.LogWarning(LogTag + " no menu navigator is registered with GameUi.Menu; treating the tutorial as not requested", this);
            }

            var requested = menu != null && menu.TakeTutorialRequest();
            if (!TutorialSteps.ShouldStart(_store.IsCompleted, requested))
            {
                Debug.Log(LogTag + " already completed; open it from the main menu to see it again", this);
                enabled = false;
                return;
            }

            var bootstrap = GetComponent<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError(LogTag + " needs a GameBootstrap on the same GameObject; rerun Shooter.Editor.BootstrapSceneBuilder.Build", this);
                enabled = false;
                return;
            }

            if (bootstrap.World != null)
            {
                BeginWith(bootstrap.World);
                return;
            }

            // GameBootstrap は Awake で作り終えるので、ここに来るのは作るのに失敗したか止められているとき
            if (!bootstrap.enabled)
            {
                Debug.LogError(LogTag + " the range was not built, so the tutorial cannot start", this);
                enabled = false;
                return;
            }

            _waitingBootstrap = bootstrap;
            bootstrap.Built += OnBuilt;
        }

        private void OnDestroy()
        {
            StopWaiting();
            Unsubscribe();
            ClearStatus();
        }

        private void OnEnable()
        {
            if (_running)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (!_running)
            {
                return;
            }

            if (_reader == null || !_reader.IsBound)
            {
                Debug.LogError(LogTag + " the player was destroyed; stopping the tutorial", this);
                Stop();
                return;
            }

            _device = DetectDevice(_deviceActions, _device);
            Tick(Time.deltaTime, _reader.Current, _reader.transform.position, _device);
        }

        private void OnBuilt(BootstrapWorld world)
        {
            StopWaiting();
            BeginWith(world);
        }

        private void BeginWith(BootstrapWorld world)
        {
            if (world.ActionReader == null)
            {
                Debug.LogError(LogTag + " the range has no player action reader; the tutorial cannot start", this);
                enabled = false;
                return;
            }

            _deviceActions = ResolveDeviceActions();
            _device = Gamepad.current != null && Keyboard.current == null ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse;
            Begin(world.ActionReader, world.Rules.Actions);
        }

        public void Begin(PlayerActionReader reader)
        {
            Begin(reader, null);
        }

        public void Begin(PlayerActionReader reader, ActionRules actions)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            Unsubscribe();
            _reader = reader;
            _weapon = reader.Weapon;
            if (actions != null)
            {
                _steps.Restart(actions);
            }
            else
            {
                _steps.Restart();
            }

            _hasLastPosition = false;
            _pendingHits = 0;
            _reloadStarted = false;
            _completedSeconds = 0f;
            _running = true;
            enabled = true;
            Subscribe();
            PublishStatus(_device);
            Debug.Log(LogTag + " started at " + _steps.Current, this);
        }

        public void Tick(float deltaSeconds, PlayerActionState state, Vector3 playerPosition, InputDeviceKind device)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_running)
            {
                return;
            }

            if (_steps.IsComplete)
            {
                _completedSeconds += deltaSeconds;
                if (_completedSeconds >= CompletedDisplaySeconds)
                {
                    Stop();
                }

                return;
            }

            var moved = 0f;
            if (_hasLastPosition)
            {
                var delta = playerPosition - _lastPosition;
                delta.y = 0f;
                moved = delta.magnitude;
            }

            _lastPosition = playerPosition;
            _hasLastPosition = true;

            var observation = new TutorialObservation(
                deltaSeconds,
                moved,
                state.IsAiming,
                _pendingHits,
                _reloadStarted || state.IsReloading,
                state.IsCrouching,
                state.IsInCover);
            _pendingHits = 0;
            _reloadStarted = false;

            var finished = _steps.Current;
            if (_steps.Observe(observation))
            {
                Debug.Log(LogTag + " finished " + finished + (_steps.IsComplete ? "; all done" : "; next " + _steps.Current), this);
                if (_steps.IsComplete)
                {
                    Complete();
                }
            }

            PublishStatus(device);
        }

        public void Stop()
        {
            Unsubscribe();
            _running = false;
            ClearStatus();
            enabled = false;
        }

        public static InputDeviceKind DetectDevice(InputAction[] actions, InputDeviceKind fallback)
        {
            if (actions == null)
            {
                return fallback;
            }

            foreach (var action in actions)
            {
                // スティックの小さな揺れで表記が行き来しないよう、押したと言える大きさのときだけ機器を見る
                if (action != null && action.enabled && action.IsPressed())
                {
                    return InputDeviceKinds.Of(action, fallback);
                }
            }

            return fallback;
        }

        public static string ActionPathFor(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Move:
                    return InputActionPaths.Move;
                case TutorialStep.Aim:
                    return InputActionPaths.Aim;
                case TutorialStep.Shoot:
                    return InputActionPaths.Attack;
                case TutorialStep.Reload:
                    return InputActionPaths.Reload;
                case TutorialStep.Crouch:
                    return InputActionPaths.Crouch;
                case TutorialStep.Cover:
                    return InputActionPaths.Cover;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "unknown tutorial step");
            }
        }

        private void Complete()
        {
            Unsubscribe();
            _completedSeconds = 0f;
            try
            {
                _store.MarkCompleted();
            }
            catch (Exception e)
            {
                // 書けなくても今回の完了は出す。次に射撃場へ入るとまた出るだけで済む
                Debug.LogError(LogTag + " could not save that the tutorial was completed: " + e.Message, this);
            }
        }

        private void PublishStatus(InputDeviceKind device)
        {
            var status = new TutorialStatus(_steps, _steps.IsComplete ? null : ActionPathFor(_steps.Current), device);
            if (_status == status)
            {
                return;
            }

            _status = status;
            GameSignals.Tutorial.Publish(status);
        }

        private void ClearStatus()
        {
            if (_status.HasValue)
            {
                // 後から別の側が出した段階まで消さない
                GameSignals.Tutorial.ClearIfCurrent(_status);
                _status = null;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _steps.IsComplete)
            {
                return;
            }

            CombatEvents.HitConfirmed += OnHitConfirmed;
            if (_weapon != null)
            {
                _weapon.ReloadStarted += OnReloadStarted;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            CombatEvents.HitConfirmed -= OnHitConfirmed;

            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる
            if (!ReferenceEquals(_weapon, null))
            {
                _weapon.ReloadStarted -= OnReloadStarted;
            }

            _subscribed = false;
        }

        private void StopWaiting()
        {
            if (!ReferenceEquals(_waitingBootstrap, null))
            {
                _waitingBootstrap.Built -= OnBuilt;
                _waitingBootstrap = null;
            }
        }

        private void OnHitConfirmed(HitConfirm confirm)
        {
            _pendingHits++;
        }

        private void OnReloadStarted(Weapon weapon)
        {
            _reloadStarted = true;
        }

        private InputAction[] ResolveDeviceActions()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(LogTag + " no project-wide Input Actions; the key hints stay on keyboard and mouse", this);
                return Array.Empty<InputAction>();
            }

            var resolved = new InputAction[_deviceActionPaths.Length];
            for (var i = 0; i < resolved.Length; i++)
            {
                resolved[i] = actions.FindAction(_deviceActionPaths[i]);
            }

            return resolved;
        }
    }
}
