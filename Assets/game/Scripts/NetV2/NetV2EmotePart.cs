using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Shooter.NetV2
{
    // 出せるか・いつ止めるかはサーバー (ServerEmotes) が決めるので、ここは輪から選んだ Id を要求として送り、
    // 配られた始めと止めを相手の体のアニメーションに当てるだけにする。
    // 輪はオフラインの EmoteController と同じく Player/Emote を押している間だけ GameSignals.Emotes に出す。
    public sealed class NetV2EmotePart : INetV2ClientPart
    {
        public const string EmoteActionPath = InputActionPaths.Emote;

        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, false);

        private readonly MatchClient _client;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<int, GameObject> _playerObject;
        private readonly Func<bool> _held;
        private readonly Action<string> _log;
        private readonly Wheel _wheel;
        private readonly Dictionary<int, Shown> _shown = new Dictionary<int, Shown>();
        private readonly List<int> _scratch = new List<int>();
        private readonly EmoteRequestMessage _request = new EmoteRequestMessage();
        private IReadOnlyList<EmoteSpec> _emotes;
        private IReadOnlyList<string> _ids;
        private EmoteCatalog _catalog;
        private bool _catalogLoaded;
        private InputAction _action;
        private bool _resolvedAction;
        private Emote _rule = Emote.Off;
        private bool _canAct;
        private bool _wheelOpen;
        private bool _disposed;

        // emotes が null ならオフラインと同じ調整値 (KitTuning.Emotes) を使い、サーバーの server-data/tuning/emotes.json と揃える。
        // catalog が null なら EmoteCatalog.Load で、オフラインと同じ見た目を引く。
        // playerObject は相手の体を引く口で、null なら今の NetV2Client から引く。held は輪のボタンを押しているかで、null なら Player/Emote を読む
        public NetV2EmotePart(
            MatchClient client,
            Func<IMessage, bool> send,
            IReadOnlyList<EmoteSpec> emotes = null,
            EmoteCatalog catalog = null,
            Func<int, GameObject> playerObject = null,
            Func<bool> held = null,
            Action<string> log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _playerObject = playerObject ?? (id => NetV2Client.Current != null ? NetV2Client.Current.RemoteObject(id) : null);
            _held = held ?? ReadHeld;
            _log = log ?? (line => Debug.Log(line));
            _wheel = new Wheel(this);
            ApplyEmotes(emotes ?? KitTuning.Emotes);
            if (catalog != null)
            {
                Catalog = catalog;
            }
        }

        public IReadOnlyList<EmoteSpec> Emotes => _emotes;

        public Emote Rule => _rule;

        public bool CanEmote => !_disposed && _rule == Emote.On && _canAct;

        public bool IsWheelOpen => _wheelOpen;

        // 輪の画面に渡す口。部品そのものを渡すと画面から Handle や Dispose まで呼べてしまうので、選ぶ口だけの包みを渡す
        public IEmoteControl Control => _wheel;

        // サーバーが自分のエモートを始めたと配ってきた Id。止めたら null
        public string LocalEmoteId { get; private set; }

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

        public void ApplyEmotes(IReadOnlyList<EmoteSpec> emotes)
        {
            var unique = EmoteSpec.UniqueById(emotes ?? throw new ArgumentNullException(nameof(emotes)));
            var ids = new string[unique.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = unique[i].Id;
            }

            _emotes = unique;
            _ids = Array.AsReadOnly(ids);
        }

        // 相手のエモートを見せているか。見せる体が無い (見えていない) 相手でも、配られた Id は残す
        public bool TryGetShown(int playerId, out string emoteId, out bool animated)
        {
            if (_shown.TryGetValue(playerId, out var shown))
            {
                emoteId = shown.EmoteId;
                animated = shown.Graph.IsValid();
                return true;
            }

            emoteId = null;
            animated = false;
            return false;
        }

        public bool Handle(IMessage message)
        {
            if (!(message is EmoteStateMessage state))
            {
                return false;
            }

            if (_disposed)
            {
                return true;
            }

            if (state.Playing)
            {
                ShowPlaying(state.PlayerId, state.EmoteId);
            }
            else
            {
                ShowStopped(state.PlayerId, state.StopReason);
            }

            return true;
        }

        // 送れるか (ルールで On・試合中・生きていてダウンしていない) は、サーバーが同じ値で判定するスナップショットの値で決める
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            _rule = snapshot.Match.Rules.Emote;
            var canAct = snapshot.Match.State == GameModeState.Playing;
            var own = snapshot.Players.IndexOf(_client.PlayerId);
            if (own < 0)
            {
                canAct = false;
            }
            else
            {
                var row = snapshot.Players[own];
                canAct &= row.Health > 0 && !row.Downed.IsDowned;
            }

            _canAct = canAct;
            if (!CanEmote)
            {
                CloseWheel();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            // 輪は長押しで開くので、押した瞬間に読む相手 (key-sharing.json で Emote を prefer にした組) にも譲らせるよう、使える間はずっと名乗る (EmoteController と同じ)
            KeySharing.SetClaim(EmoteActionPath, this, CanEmote);
            var held = CanEmote && !GameUi.IsPauseOpen && _held();
            if (held && !_wheelOpen)
            {
                OpenWheel();
            }
            else if (!held && _wheelOpen)
            {
                CloseWheel();
            }

            RebindLostBodies();
        }

        // 輪を通さずに選ぶ画面 (ゲームが足すショートカットなど) のための口。輪から選んだときもここを通る
        public bool Play(string emoteId)
        {
            if (_disposed)
            {
                return false;
            }

            var reason = Check(emoteId);
            if (reason != null)
            {
                _log(EmoteLog.NetNotSent(emoteId, reason));
                return false;
            }

            _request.Action = EmoteAction.Play;
            _request.EmoteId = emoteId;
            if (!_send(_request))
            {
                _log(EmoteLog.NetNotSent(emoteId, "NotConnected"));
                return false;
            }

            _log(EmoteLog.NetRequested(emoteId));
            return true;
        }

        public bool Stop()
        {
            if (_disposed || LocalEmoteId == null)
            {
                return false;
            }

            _request.Action = EmoteAction.Stop;
            _request.EmoteId = "";
            if (!_send(_request))
            {
                return false;
            }

            _log(EmoteLog.NetStopRequested());
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CloseWheel();
            KeySharing.Release(EmoteActionPath, this);
            foreach (var shown in _shown.Values)
            {
                StopGraph(shown);
            }

            _shown.Clear();
            LocalEmoteId = null;
        }

        // サーバーも同じ理由で断るが、断られると分かっている要求を送らず、理由を手元のログに残す
        private string Check(string emoteId)
        {
            if (_rule != Emote.On)
            {
                return nameof(EmoteRefusal.RuleOff);
            }

            if (string.IsNullOrEmpty(emoteId) || EmoteSpec.Find(_emotes, emoteId) == null)
            {
                return nameof(EmoteRefusal.UnknownId);
            }

            if (!_canAct)
            {
                return nameof(EmoteRefusal.Dead);
            }

            // 送る形に収まらない Id はサーバーへ届かないので、一覧に書けても送らない
            return _utf8.GetByteCount(emoteId) > MatchProtocol.MaxContentIdBytes ? "IdTooLong" : null;
        }

        private void OpenWheel()
        {
            _wheelOpen = true;
            GameSignals.Emotes.Publish(_wheel);
            _log(EmoteLog.WheelOpened(_ids.Count));
        }

        private void CloseWheel()
        {
            if (!_wheelOpen)
            {
                return;
            }

            _wheelOpen = false;
            GameSignals.Emotes.ClearIfCurrent(_wheel);
            _log(EmoteLog.WheelClosed());
        }

        private void ShowPlaying(int playerId, string emoteId)
        {
            if (playerId == _client.PlayerId)
            {
                // 自分の体は一人称で見えないので、アニメーションは当てずに Id だけを持つ
                LocalEmoteId = emoteId;
                _log(EmoteLog.NetPlayed(playerId, emoteId, false));
                return;
            }

            // 同じ人の次のエモートは前のものを止めて替える (サーバーは替えたときに止めたことを配らない)
            if (_shown.TryGetValue(playerId, out var previous))
            {
                StopGraph(previous);
            }

            var shown = previous ?? new Shown();
            shown.EmoteId = emoteId;
            _shown[playerId] = shown;
            var animated = Bind(playerId, shown);
            _log(EmoteLog.NetPlayed(playerId, emoteId, animated));
        }

        private void ShowStopped(int playerId, EmoteStopReason reason)
        {
            if (playerId == _client.PlayerId)
            {
                var own = LocalEmoteId;
                LocalEmoteId = null;
                _log(EmoteLog.NetStopped(playerId, own, reason));
                return;
            }

            if (!_shown.TryGetValue(playerId, out var shown))
            {
                return;
            }

            StopGraph(shown);
            _shown.Remove(playerId);
            _log(EmoteLog.NetStopped(playerId, shown.EmoteId, reason));
        }

        // 体の Animator にクリップを流す。AnimatorController に状態を足さずに別のグラフで上書きするのは、オフラインの CharacterAnimator と同じく、
        // ゲームが差し替えるリグのコントローラーにエモートごとの状態を作らせないため
        private bool Bind(int playerId, Shown shown)
        {
            // 当てられなかった体も覚えるのは、同じ体へ毎フレーム当て直してログを埋めないため。体が替わったときだけ当て直す
            var body = _playerObject(playerId);
            shown.Body = body;
            shown.Animator = null;
            if (body == null)
            {
                _log(EmoteLog.NetNoBody(playerId, shown.EmoteId));
                return false;
            }

            var clip = Catalog != null ? Catalog.Find(shown.EmoteId) : null;
            if (clip == null)
            {
                _log(EmoteLog.NoAnimation(shown.EmoteId));
                return false;
            }

            var animator = body.GetComponentInChildren<Animator>();
            shown.Animator = animator;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            AnimationPlayableUtilities.PlayClip(animator, clip, out var graph);
            shown.Graph = graph;
            _log(EmoteLog.ClipStarted(body.name, clip.name));
            return true;
        }

        // 見た目の差し替え (NetV2RemotePlayer.ShowCosmetic) で体が作り直されたり、見えなかった相手が現れたりしたら、続いているエモートを新しい体に当て直す
        private void RebindLostBodies()
        {
            if (_shown.Count == 0)
            {
                return;
            }

            _scratch.Clear();
            foreach (var pair in _shown)
            {
                var shown = pair.Value;
                var lost = shown.Graph.IsValid() && shown.Animator == null;
                var replaced = shown.Body == null && _playerObject(pair.Key) != null;
                if (lost || replaced)
                {
                    _scratch.Add(pair.Key);
                }
            }

            foreach (var playerId in _scratch)
            {
                var shown = _shown[playerId];
                StopGraph(shown);
                Bind(playerId, shown);
            }

            _scratch.Clear();
        }

        private static void StopGraph(Shown shown)
        {
            if (shown.Graph.IsValid())
            {
                shown.Graph.Destroy();
            }

            shown.Graph = default;
        }

        private bool ReadHeld()
        {
            if (!_resolvedAction)
            {
                _resolvedAction = true;
                AppliedInputBindings.EnsureLoaded();
                var actions = InputSystem.actions;
                _action = actions != null ? actions.FindAction(EmoteActionPath) : null;
                if (_action == null)
                {
                    _log(EmoteLog.NoInput(EmoteActionPath));
                }
            }

            return _action != null && _action.enabled && _action.IsPressed();
        }

        private sealed class Shown
        {
            public string EmoteId { get; set; }

            public GameObject Body { get; set; }

            public Animator Animator { get; set; }

            public PlayableGraph Graph { get; set; }
        }

        private sealed class Wheel : IEmoteControl
        {
            private readonly NetV2EmotePart _part;

            public Wheel(NetV2EmotePart part)
            {
                _part = part;
            }

            public IReadOnlyList<string> EmoteIds => _part.CanEmote ? _part._ids : Array.Empty<string>();

            public bool Play(string emoteId)
            {
                return _part.Play(emoteId);
            }
        }
    }
}
