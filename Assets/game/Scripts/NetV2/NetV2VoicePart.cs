using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    /// <summary>
    /// 試合中のボイスチャットを動かす <see cref="NetV2Client"/> の部品。マップに入ったら試合のロビーの RTC 部屋に入り、
    /// クライアントが消えるときに出る。押して話すボタン (<see cref="InputActionPaths.PushToTalk"/>) を押している間だけ送り、
    /// スナップショットのチームを見て敵の声は受けない (<see cref="ClientVoice"/>)。今の状態は <see cref="GameSignals.Voice"/> に出す。
    /// </summary>
    /// <remarks>
    /// マイクの入り切りと声の音量は <see cref="VoiceSettingsStore"/> から読み、設定の画面が保存したらすぐ効かせる。
    /// ポーズ画面を開いている間は押していないものとして扱う。
    /// </remarks>
    public sealed class NetV2VoicePart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly string _lobbyId;
        private readonly ClientVoice _voice;
        private readonly VoiceSettingsStore _store;
        private readonly Func<bool> _pushToTalkHeld;
        private readonly Action<string> _log;
        private InputAction _pushToTalk;
        private bool _enabledPushToTalk;
        private bool _started;
        private VoiceStatus? _published;
        private bool _disposed;

        /// <param name="client">つないだクライアント。自分のプレイヤーの Id をここから読む。</param>
        /// <param name="session">試合のボイスチャット。この部品が Dispose する。</param>
        /// <param name="lobbyId">
        /// 試合のロビーの Id。ロビーにはメンバーとして先に入っておく (<see cref="ILobbyBackend.JoinMatchLobby"/>)。
        /// null か空ならボイスを使わない。
        /// </param>
        /// <param name="roster">参加者 (ProductUserId) と試合のプレイヤーの対応。</param>
        /// <param name="store">マイクの入り切りと声の音量を読む設定。</param>
        /// <param name="log">ログの 1 行を受け取る。</param>
        /// <param name="pushToTalkHeld">押して話すボタンを押しているか。null なら入力のアクションを読む。</param>
        public NetV2VoicePart(
            MatchClient client,
            IVoiceSession session,
            string lobbyId,
            VoiceRoster roster,
            VoiceSettingsStore store,
            Action<string> log,
            Func<bool> pushToTalkHeld = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _voice = new ClientVoice(session ?? throw new ArgumentNullException(nameof(session)), roster ?? throw new ArgumentNullException(nameof(roster)));
            _lobbyId = string.IsNullOrEmpty(lobbyId) ? null : lobbyId;
            _pushToTalkHeld = pushToTalkHeld ?? ReadPushToTalk;
            _voice.StatusChanged += Show;
            _voice.ApplySettings(_store.Load());
            VoiceSettingsStore.Saved += HandleSettingsSaved;
            if (pushToTalkHeld == null)
            {
                FindPushToTalk();
            }
        }

        /// <summary>試合のボイスの判断。</summary>
        public ClientVoice Voice => _voice;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            if (!_started)
            {
                _started = true;
                if (_lobbyId == null)
                {
                    _log(VoiceLog.Off("the match has no lobby"));
                }
                else
                {
                    _voice.Enter(_lobbyId);
                }
            }

            _voice.Apply(snapshot, _client.PlayerId);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            _voice.SetTalking(!GameUi.IsPauseOpen && _pushToTalkHeld());
            _voice.Tick();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            VoiceSettingsStore.Saved -= HandleSettingsSaved;
            _voice.Dispose();
            _voice.StatusChanged -= Show;
            if (_published.HasValue)
            {
                GameSignals.Voice.ClearIfCurrent(_published);
                _published = null;
            }

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ戻す
            if (_enabledPushToTalk)
            {
                _pushToTalk.Disable();
                _enabledPushToTalk = false;
            }
        }

        private void Show(VoiceStatus? status)
        {
            if (status.HasValue)
            {
                _published = status;
                GameSignals.Voice.Publish(status);
                return;
            }

            if (_published.HasValue)
            {
                GameSignals.Voice.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private void HandleSettingsSaved(string key, VoiceSettings settings)
        {
            if (!_disposed && string.Equals(key, _store.Key, StringComparison.Ordinal))
            {
                _voice.ApplySettings(settings);
            }
        }

        private void FindPushToTalk()
        {
            var actions = InputSystem.actions;
            _pushToTalk = actions != null ? actions.FindAction(InputActionPaths.PushToTalk) : null;
            if (_pushToTalk == null)
            {
                _log(VoiceLog.PushToTalkMissing(InputActionPaths.PushToTalk));
                return;
            }

            if (!_pushToTalk.enabled)
            {
                _pushToTalk.Enable();
                _enabledPushToTalk = true;
            }
        }

        private bool ReadPushToTalk()
        {
            return _pushToTalk != null && _pushToTalk.IsPressed();
        }
    }
}
