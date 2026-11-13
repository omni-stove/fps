using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 射撃場のチュートリアル。Bootstrap シーン (Fps.Editor.BootstrapSceneBuilder が生成) の <see cref="GameBootstrap"/> と同じ GameObject に置く。
    /// Start で、まだ一度も終えていないか (<see cref="TutorialProgressStore"/>)、メニューのチュートリアルから来たとき
    /// (<see cref="MenuNavigator.TakeTutorialRequest"/>) だけ始め (<see cref="TutorialSteps.ShouldStart"/>)、そうでなければ自分を無効にする。
    /// 毎フレーム、プレイヤーの今のアクション (<see cref="PlayerActionReader.Current"/>: ADS・リロード・しゃがみ・カバー)・
    /// 歩いた距離・標的に当てた回数 (<see cref="CombatEvents.HitConfirmed"/>) から <see cref="TutorialSteps"/> を進め、
    /// HUD の <see cref="TutorialPromptView"/> に今の手順と使うキーを出す。キーの表記は最後に操作した入力機器と今の割り当てで決める
    /// (<see cref="TutorialKeyLabels"/>)。すべて終えたら記録して完了を <see cref="CompletedDisplaySeconds"/> 秒出し、隠す。
    /// </summary>
    [DisallowMultipleComponent]
    // PlayerActionReader (100) が LateUpdate でそのフレームの状態を組み立ててから読む
    [DefaultExecutionOrder(110)]
    public sealed class TutorialPresenter : MonoBehaviour
    {
        public const string LogTag = "[Tutorial]";

        /// <summary>すべて終えてから完了を出しておく秒数。</summary>
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
        private TutorialPromptView _view;
        private InputAction[] _deviceActions = Array.Empty<InputAction>();
        private InputDeviceKind _device = InputDeviceKind.KeyboardMouse;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private int _pendingHits;
        private bool _reloadStarted;
        private bool _running;
        private bool _subscribed;
        private float _completedSeconds;
        private string _shownPrompt;
        private GameBootstrap _waitingBootstrap;

        /// <summary>進みの数え。</summary>
        public TutorialSteps Steps => _steps;

        /// <summary>案内を出しているか (始めてから、完了を出し終えるまで)。</summary>
        public bool IsRunning => _running;

        /// <summary>案内のキーの表記に使っている入力機器。</summary>
        public InputDeviceKind Device => _device;

        /// <summary>終えたことを読み書きする先。テストでは本物の記録を壊さないよう別のキーの store を渡す。</summary>
        public TutorialProgressStore Store
        {
            get => _store;
            set => _store = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>今出している文。出していなければ null。</summary>
        public string Prompt => _running ? _shownPrompt : null;

        private void Start()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            var requested = MenuNavigator.TakeTutorialRequest();
            if (!TutorialSteps.ShouldStart(_store.IsCompleted, requested))
            {
                Debug.Log(LogTag + " already completed; open it from the main menu to see it again", this);
                enabled = false;
                return;
            }

            var bootstrap = GetComponent<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError(LogTag + " needs a GameBootstrap on the same GameObject; rerun Fps.Editor.BootstrapSceneBuilder.Build", this);
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
            // 専用サーバー以外で HUD が無いのは作るのに失敗したときで、そのエラーは GameBootstrap が出している
            var view = world.Hud != null ? world.Hud.TutorialPrompt : null;
            if (world.ActionReader == null || view == null)
            {
                Debug.LogError(LogTag + " the range has no player action reader or HUD tutorial view; the tutorial cannot start", this);
                enabled = false;
                return;
            }

            _deviceActions = ResolveDeviceActions();
            _device = Gamepad.current != null && Keyboard.current == null ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse;
            Begin(world.ActionReader, view);
        }

        /// <summary>
        /// reader のプレイヤーで最初の手順から始め、view に案内を出す。始めていれば最初からやり直す。
        /// 武器の <see cref="WeaponController.ReloadStarted"/> と <see cref="CombatEvents.HitConfirmed"/> を購読する。
        /// </summary>
        public void Begin(PlayerActionReader reader, TutorialPromptView view)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            Unsubscribe();
            _reader = reader;
            _weapon = reader.Weapon;
            _view = view;
            _steps.Restart();
            _hasLastPosition = false;
            _pendingHits = 0;
            _reloadStarted = false;
            _completedSeconds = 0f;
            _shownPrompt = null;
            _running = true;
            enabled = true;
            Subscribe();
            ShowPrompt(_device);
            Debug.Log(LogTag + " started at " + _steps.Current, this);
        }

        /// <summary>
        /// 1 フレーム分進める。前のフレームからの位置の差を歩いた距離に、その間に届いた命中とリロードの開始を state に足して
        /// <see cref="TutorialSteps.Observe"/> に渡し、device のキーで案内を出し直す。すべて終えたら記録して完了を出し、
        /// <see cref="CompletedDisplaySeconds"/> 秒経ったら隠して止める。Play 中は <see cref="LateUpdate"/> から呼ぶ。
        /// </summary>
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

            ShowPrompt(device);
        }

        /// <summary>案内を隠して止める。終えたかの記録は変えない。</summary>
        public void Stop()
        {
            Unsubscribe();
            _running = false;
            _shownPrompt = null;
            if (_view != null)
            {
                _view.Hide();
            }

            enabled = false;
        }

        /// <summary>
        /// 最後に操作した入力機器。actions のうち押されている (しきい値を超えて動かしている) ものの機器を、並びの先のものから選ぶ。
        /// 何も押されていなければ fallback (直前の機器) のまま。
        /// </summary>
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

        /// <summary>step で使うアクションのパス (<see cref="InputActionPaths"/>)。</summary>
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

        /// <summary>
        /// step を device で行うキーの表記。プロジェクト全体のアクション (<see cref="InputSystem.actions"/>) から引き、
        /// アクションか device の割り当てが無ければアクションの名前 (Move など) を返す。
        /// </summary>
        public static string KeyLabelFor(TutorialStep step, InputDeviceKind device)
        {
            var path = ActionPathFor(step);
            var actionName = path.Substring(path.LastIndexOf('/') + 1);
            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(path) : null;
            if (action == null)
            {
                return actionName;
            }

            var label = TutorialKeyLabels.For(action, device);
            return string.IsNullOrWhiteSpace(label) ? actionName : label;
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

        private void ShowPrompt(InputDeviceKind device)
        {
            var text = _steps.IsComplete ? TutorialSteps.CompletedText : _steps.Prompt(KeyLabelFor(_steps.Current, device));
            if (text == _shownPrompt)
            {
                return;
            }

            _shownPrompt = text;
            _view.Show(text, _steps.IsComplete);
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
