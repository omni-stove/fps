using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // 輪を出すのは Player/Emote を押している間だけで、離すと GameSignals.Emotes から下ろす。選ぶ画面は輪が出ている間に Play を呼ぶ。
    // 再生は自分の体だけで、ネット対戦の相手には送らない。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class EmoteController : MonoBehaviour, IEmoteControl
    {
        public const string LogTag = EmoteLog.Tag;

        public const string EmoteActionPath = InputActionPaths.Emote;

        // 調整値の JSON に出さないのは、押し戻しや坂での小さなずれで止めないための遊びで、ゲームが動きとして選ぶ値ではないため。
        public const float MoveToleranceMeters = 0.05f;

        private IReadOnlyList<EmoteSpec> _emotes = EmoteSpec.DefaultEmotes;
        private IReadOnlyList<string> _ids = Array.Empty<string>();
        private bool _emotesApplied;
        private Emote _rule = Emote.Off;
        private bool _configured;

        private Damageable _self;
        private ViewRig _rig;
        private CharacterAnimator _animator;
        private PlayerVehicleRider _rider;
        private EmoteCatalog _catalog;
        private bool _catalogLoaded;
        private Damageable _diedSource;
        private bool _listeningToShots;

        private EmoteSpec _current;
        private float _startedAt;
        private Vector3 _startPosition;
        private bool _changedView;

        private bool _wheelOpen;
        private bool _wheelFromInput;

        [NonSerialized]
        private InputAction _emoteAction;
        private bool _enabledEmoteAction;
        private bool _resolvedAction;

        private EmoteRefusal _lastRefusal;
        private EmoteStopReason? _lastStop;

        public Emote Rule => _rule;

        public bool UsesEmotes => _configured && _rule == Emote.On;

        public IReadOnlyList<EmoteSpec> Emotes => _emotes;

        public IReadOnlyList<string> EmoteIds => UsesEmotes ? _ids : Array.Empty<string>();

        public EmoteSpec Current => _current;

        public bool IsPlaying => _current != null;

        public bool IsWheelOpen => _wheelOpen;

        public EmoteRefusal LastRefusal => _lastRefusal;

        public EmoteStopReason? LastStop => _lastStop;

        public bool ChangedView => _changedView;

        // 既定を Time.time にするのは、ポーズで時間が止まるとエモートの長さも止めるため。テストが時刻を差し込めるよう差し替えられるようにする。
        public Func<float> Clock { get; set; }

        // 空なら GameContent、それも無ければ Resources の EmoteCatalog を一度だけ読む。
        public EmoteCatalog Catalog
        {
            get
            {
                if (_catalog == null && !_catalogLoaded)
                {
                    _catalog = EmoteCatalog.Load();
                    _catalogLoaded = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogLoaded = true;
            }
        }

        public InputAction EmoteAction
        {
            get => _emoteAction;
            set
            {
                ReleaseEmoteAction();
                _emoteAction = value;
                _resolvedAction = true;
                AcquireEmoteAction();
            }
        }

        private float Now => Clock != null ? Clock() : Time.time;

        public void ApplyEmotes(IReadOnlyList<EmoteSpec> emotes)
        {
            if (emotes == null)
            {
                throw new ArgumentNullException(nameof(emotes));
            }

            var unique = EmoteSpec.UniqueById(emotes);
            var ids = new string[unique.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = unique[i].Id;
            }

            if (_current != null && EmoteSpec.Find(unique, _current.Id) == null)
            {
                StopCurrent(EmoteStopReason.Cancelled);
            }

            _emotes = unique;
            _ids = Array.AsReadOnly(ids);
            _emotesApplied = true;
        }

        private void Awake()
        {
            // 無効なまま置かれた本体は Awake が有効になるまで遅れるので、その間に Play 中のコードが ApplyEmotes で渡した一覧を JSON で消さない。
            if (Application.isPlaying && !_emotesApplied)
            {
                ApplyEmotes(KitTuning.Emotes);
            }
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (!_emotesApplied)
            {
                ApplyEmotes(KitTuning.Emotes);
            }

            _rule = rules.Actions.Emote;
            _configured = true;
            _lastRefusal = EmoteRefusal.None;
            _self = GetComponent<Damageable>();
            _rig = GetComponent<ViewRig>();
            _animator = GetComponent<CharacterAnimator>();
            _rider = GetComponent<PlayerVehicleRider>();

            if (UsesEmotes)
            {
                Listen();
            }
            else
            {
                StopCurrent(EmoteStopReason.RuleOff);
                CloseWheel();
                Unlisten();
            }

            Debug.Log(EmoteLog.Rule(_rule, _emotes), this);
        }

        public bool OpenWheel()
        {
            if (!UsesEmotes)
            {
                return false;
            }

            if (!_wheelOpen)
            {
                _wheelOpen = true;
                Debug.Log(EmoteLog.WheelOpened(_ids.Count), this);
            }

            GameSignals.Emotes.Publish(this);
            return true;
        }

        public void CloseWheel()
        {
            _wheelFromInput = false;
            if (!_wheelOpen)
            {
                return;
            }

            _wheelOpen = false;
            GameSignals.Emotes.ClearIfCurrent(this);
            Debug.Log(EmoteLog.WheelClosed(), this);
        }

        public bool Play(string emoteId)
        {
            if (!UsesEmotes)
            {
                return Refuse(emoteId, EmoteRefusal.RuleOff);
            }

            var emote = EmoteSpec.Find(_emotes, emoteId);
            if (emote == null)
            {
                return Refuse(emoteId, EmoteRefusal.UnknownId);
            }

            if (_self != null && _self.IsDead)
            {
                return Refuse(emoteId, EmoteRefusal.Dead);
            }

            if (_self != null && _self.IsDowned)
            {
                return Refuse(emoteId, EmoteRefusal.Downed);
            }

            if (_rider != null && _rider.IsRiding)
            {
                return Refuse(emoteId, EmoteRefusal.Riding);
            }

            if (_current != null)
            {
                StopCurrent(EmoteStopReason.Replaced);
            }

            // 演出の部品は取り付けの後に足されることがあるので、Configure で見つからなかったものは再生のたびに探し直す。
            _rig = _rig != null ? _rig : GetComponent<ViewRig>();
            _animator = _animator != null ? _animator : GetComponent<CharacterAnimator>();

            _current = emote;
            _startedAt = Now;
            _startPosition = transform.position;
            _lastRefusal = EmoteRefusal.None;
            EnterThirdPerson();

            var clip = Catalog != null ? Catalog.Find(emote.Id) : null;
            var animated = false;
            if (clip == null)
            {
                Debug.Log(EmoteLog.NoAnimation(emote.Id), this);
            }
            else if (_animator != null)
            {
                animated = _animator.PlayEmote(clip);
            }

            Debug.Log(EmoteLog.Played(emote, animated), this);
            return true;
        }

        public void Stop()
        {
            StopCurrent(EmoteStopReason.Cancelled);
        }

        public void Tick(float now)
        {
            if (_current == null)
            {
                return;
            }

            if (_self != null && _self.IsDead)
            {
                StopCurrent(EmoteStopReason.Died);
                return;
            }

            if (_self != null && _self.IsDowned)
            {
                StopCurrent(EmoteStopReason.Downed);
                return;
            }

            if (_rider != null && _rider.IsRiding)
            {
                StopCurrent(EmoteStopReason.Riding);
                return;
            }

            if (_current.StopsOnMove && HasMoved())
            {
                StopCurrent(EmoteStopReason.Moved);
                return;
            }

            if (_current.IsFinishedAt(now - _startedAt))
            {
                StopCurrent(EmoteStopReason.Finished);
            }
        }

        private void OnEnable()
        {
            if (UsesEmotes)
            {
                Listen();
            }

            AcquireEmoteAction();
        }

        private void OnDisable()
        {
            StopCurrent(EmoteStopReason.Cancelled);
            CloseWheel();
            Unlisten();
            ReleaseEmoteAction();
        }

        private void OnDestroy()
        {
            StopCurrent(EmoteStopReason.Cancelled);
            CloseWheel();
            Unlisten();
            ReleaseEmoteAction();
        }

        private void LateUpdate()
        {
            if (!_configured)
            {
                return;
            }

            ReadWheelInput();
            Tick(Now);
        }

        // 輪を開いたのがコード (画面やテスト) のときは、ボタンを押していないからといって閉じない。
        private void ReadWheelInput()
        {
            if (!_resolvedAction)
            {
                ResolveEmoteAction();
            }

            var held = _emoteAction != null && _emoteAction.enabled && _emoteAction.IsPressed() && !GameUi.IsPauseOpen;
            if (held && !_wheelOpen)
            {
                _wheelFromInput = OpenWheel();
            }
            else if (!held && _wheelOpen && _wheelFromInput)
            {
                CloseWheel();
            }
        }

        private void ResolveEmoteAction()
        {
            _resolvedAction = true;
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(EmoteActionPath) : null;
            if (action == null)
            {
                Debug.LogWarning(EmoteLog.NoInput(EmoteActionPath), this);
                return;
            }

            _emoteAction = action;
            AcquireEmoteAction();
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private void AcquireEmoteAction()
        {
            if (_emoteAction != null && !_enabledEmoteAction && !_emoteAction.enabled && isActiveAndEnabled)
            {
                _emoteAction.Enable();
                _enabledEmoteAction = true;
            }
        }

        private void ReleaseEmoteAction()
        {
            if (_enabledEmoteAction && _emoteAction != null)
            {
                _emoteAction.Disable();
            }

            _enabledEmoteAction = false;
        }

        private void StopCurrent(EmoteStopReason reason)
        {
            if (_current == null)
            {
                return;
            }

            var stopped = _current;
            var elapsed = Mathf.Max(0f, Now - _startedAt);
            _current = null;
            _lastStop = reason;
            if (_animator != null)
            {
                _animator.StopEmote();
            }

            RestoreView();
            Debug.Log(EmoteLog.Stopped(stopped.Id, reason, elapsed), this);
        }

        private bool HasMoved()
        {
            var offset = transform.position - _startPosition;
            offset.y = 0f;
            return offset.sqrMagnitude > MoveToleranceMeters * MoveToleranceMeters;
        }

        private void EnterThirdPerson()
        {
            if (_changedView || _rig == null || !_rig.enabled || _rig.Mode != ViewMode.FirstPerson)
            {
                return;
            }

            _rig.Mode = ViewMode.ThirdPerson;
            _changedView = true;
        }

        // 再生中にプレイヤーが自分で視点を替えたなら、その選んだ視点を残す。
        private void RestoreView()
        {
            if (!_changedView)
            {
                return;
            }

            _changedView = false;
            if (_rig != null && _rig.Mode == ViewMode.ThirdPerson)
            {
                _rig.Mode = ViewMode.FirstPerson;
            }
        }

        // Edit Mode では OnEnable が呼ばれないので、Configure でも購読する。
        private void Listen()
        {
            if (!_listeningToShots)
            {
                CombatEvents.ShotFired += OnShotFired;
                _listeningToShots = true;
            }

            if (!ReferenceEquals(_diedSource, _self))
            {
                UnlistenDied();
                if (_self != null)
                {
                    _self.Died += OnDied;
                    _diedSource = _self;
                }
            }
        }

        private void Unlisten()
        {
            if (_listeningToShots)
            {
                CombatEvents.ShotFired -= OnShotFired;
                _listeningToShots = false;
            }

            UnlistenDied();
        }

        private void UnlistenDied()
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_diedSource, null))
            {
                _diedSource.Died -= OnDied;
            }

            _diedSource = null;
        }

        private void OnShotFired(ShotFiredInfo shot)
        {
            if (_current == null || shot.Shooter == null)
            {
                return;
            }

            if (shot.Shooter == gameObject || shot.Shooter.transform.IsChildOf(transform))
            {
                StopCurrent(EmoteStopReason.Fired);
            }
        }

        private void OnDied(Damageable _)
        {
            StopCurrent(EmoteStopReason.Died);
        }

        private bool Refuse(string emoteId, EmoteRefusal reason)
        {
            _lastRefusal = reason;
            Debug.Log(EmoteLog.Refused(emoteId, reason), this);
            return false;
        }
    }
}
