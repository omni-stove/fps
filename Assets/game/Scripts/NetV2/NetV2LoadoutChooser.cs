using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 断られた並びを次に自動で送らないよう、受け入れられた並びだけを保存する。
    // サーバーは武器のある要求にだけ送った順に答えるので、届いた答えは待っている先頭の並びへのものとして突き合わせる。
    // 見た目は相手から見える体のスキンだけを送る。迷彩は自分の一人称の武器にしか出さず、サーバーを通す必要が無いため。
    internal sealed class NetV2LoadoutChooser : IDisposable
    {
        private readonly NetV2Weapons _weapons;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<string> _cosmeticId;
        private readonly LoadoutPreferenceStore _store = new LoadoutPreferenceStore();
        private readonly LoadoutRequestMessage _request = new LoadoutRequestMessage();
        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private readonly UnityEngine.Object _context;
        private ILoadoutSelect _view;
        private IReadOnlyList<string> _equipped = Array.Empty<string>();
        private bool _awaiting;

        // 画面を開いた回数。前に開いた画面から送った並びの答えを、開き直した画面に出さないために答えと突き合わせる
        private int _screenSession;

        public NetV2LoadoutChooser(NetV2Weapons weapons, Func<IMessage, bool> send, UnityEngine.Object context)
            : this(weapons, send, context, () => CosmeticLooks.WornId(CosmeticKind.Skin))
        {
        }

        public NetV2LoadoutChooser(NetV2Weapons weapons, Func<IMessage, bool> send, UnityEngine.Object context, Func<string> cosmeticId)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _cosmeticId = cosmeticId ?? throw new ArgumentNullException(nameof(cosmeticId));
            _context = context;
        }

        public void Bind(ILoadoutSelect view)
        {
            Unbind();
            _view = view;
            if (_view != null)
            {
                _view.Confirmed += HandleConfirmed;
            }
        }

        public void SendSaved()
        {
            var saved = LoadSaved();
            if (saved == null)
            {
                SendCosmeticOnly();
                return;
            }

            if (!LoadoutSelectionAssets.TryCreate(_weapons.Loadout, _weapons.Catalog, saved.WeaponIds, null, out var selection, out var reason))
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " the saved loadout is not sent: " + reason, _context);
                SendCosmeticOnly();
                return;
            }

            reason = selection.RestoredPreference ? selection.Validate() : "the saved loadout has weapons this match does not offer in those slots";
            if (reason != null)
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " the saved loadout is not sent: " + reason, _context);
                SendCosmeticOnly();
                return;
            }

            Debug.Log(LoadoutChoiceLog.Prefix + " sending the saved loadout", _context);
            Send(selection.WeaponIds, saved.AbilityIds, 0);
        }

        public void Apply(SnapshotMessage snapshot, int playerId)
        {
            if (snapshot.HasOwner)
            {
                _equipped = Copy(snapshot.Owner.EquippedWeaponIds);
            }

            var own = snapshot.Players.IndexOf(playerId);
            var awaiting = own >= 0 && snapshot.Players[own].Health <= 0 && !snapshot.Players[own].Downed.IsDowned
                && snapshot.Match.State == GameModeState.Playing;
            if (awaiting && !_awaiting)
            {
                Open();
            }
            else if (!awaiting && _awaiting && _view != null)
            {
                _view.Close();
            }

            _awaiting = awaiting;
        }

        public void Answer(LoadoutAnswerMessage answer)
        {
            Debug.Log(LoadoutChoiceLog.Answer(answer.Accepted, answer.Reason), _context);
            if (_pending.Count == 0)
            {
                return;
            }

            var pending = _pending.Dequeue();
            if (answer.Accepted)
            {
                try
                {
                    // 能力はまだサーバーへ送らないが、画面で選んだ能力は残す。選べなかったときは保存済みの能力を消さない
                    _store.Save(new LoadoutChoice(pending.WeaponIds, pending.AbilityIds ?? LoadSaved()?.AbilityIds));
                }
                catch (ArgumentException e)
                {
                    Debug.LogError(LoadoutChoiceLog.Prefix + " could not save the accepted loadout: " + e.Message, _context);
                }
            }

            // 画面が待っているのは、今開いている画面から最後に送った並びの答えだけ
            if (pending.ScreenSession == _screenSession && _pending.Count == 0 && _view != null && _view.IsOpen)
            {
                _view.ShowAnswer(answer.Accepted, answer.Reason);
            }
        }

        public void Dispose()
        {
            Unbind();
            _pending.Clear();
        }

        private void Open()
        {
            if (_view == null)
            {
                return;
            }

            var saved = LoadSaved();
            if (!LoadoutSelectionAssets.TryCreate(_weapons.Loadout, _weapons.Catalog, saved?.WeaponIds, _equipped, out var selection, out var error))
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " cannot show the loadout screen: " + error, _context);
                return;
            }

            // 能力の一覧が壊れていても武器は選べるよう、能力を選ばせずに開く
            if (!LoadoutSelectionAssets.TryCreateAbilities(
                    _weapons.Loadout, GameContent.Pick(c => c.AbilityCatalog), saved?.AbilityIds, out var abilities, out error))
            {
                Debug.LogWarning(LoadoutChoiceLog.AbilitiesNotOffered(error), _context);
            }

            _screenSession++;
            _view.Open(selection, abilities);
        }

        private void HandleConfirmed(LoadoutChoice choice)
        {
            // 能力はまだ要求に載せないので、武器だけを送る
            if (!Send(choice.WeaponIds, choice.AbilityIds, _screenSession))
            {
                _view.ShowAnswer(false, "not connected to the match");
                return;
            }

            _view.ShowSending();
        }

        // 並びを送らない入り方でも相手に着けた体を見せるため、武器の無い要求で見た目だけを送る。サーバーはこの要求に答えないので待ちに積まない
        private void SendCosmeticOnly()
        {
            var cosmeticId = CosmeticToSend();
            if (cosmeticId.Length == 0)
            {
                return;
            }

            _request.WeaponIds.Clear();
            _request.CosmeticId = cosmeticId;
            if (_send(_request))
            {
                Debug.Log(LoadoutChoiceLog.RequestedCosmetic(cosmeticId), _context);
            }
        }

        // 送れない長さの Id はゲームのカタログの誤りなので、要求ごと落とさず見た目だけを諦める
        private string CosmeticToSend()
        {
            var id = _cosmeticId() ?? string.Empty;
            if (System.Text.Encoding.UTF8.GetByteCount(id) > MatchProtocol.MaxContentIdBytes)
            {
                Debug.LogWarning(LoadoutChoiceLog.CosmeticTooLong(id, MatchProtocol.MaxContentIdBytes), _context);
                return string.Empty;
            }

            return id;
        }

        private bool Send(IReadOnlyList<string> weaponIds, IReadOnlyList<string> abilityIds, int screenSession)
        {
            _request.CosmeticId = CosmeticToSend();
            _request.WeaponIds.Clear();
            foreach (var id in weaponIds)
            {
                if (_request.WeaponIds.Count == _request.WeaponIds.MaxCount)
                {
                    break;
                }

                _request.WeaponIds.Add(id);
            }

            if (!_send(_request))
            {
                return false;
            }

            Debug.Log(LoadoutChoiceLog.Requested(weaponIds), _context);
            _pending.Enqueue(new Pending(Copy(weaponIds), abilityIds == null ? null : Copy(abilityIds), screenSession));
            return true;
        }

        private LoadoutChoice LoadSaved()
        {
            try
            {
                return _store.Load();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " ignored the saved loadout: " + e.Message, _context);
                return null;
            }
        }

        private void Unbind()
        {
            if (_view != null)
            {
                _view.Confirmed -= HandleConfirmed;
            }

            _view = null;
        }

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> ids)
        {
            var copy = new List<string>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                copy.Add(ids[i]);
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyList<string> Copy(ContentIdList ids)
        {
            var copy = new List<string>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                copy.Add(ids[i]);
            }

            return copy.AsReadOnly();
        }

        private readonly struct Pending
        {
            public Pending(IReadOnlyList<string> weaponIds, IReadOnlyList<string> abilityIds, int screenSession)
            {
                WeaponIds = weaponIds;
                AbilityIds = abilityIds;
                ScreenSession = screenSession;
            }

            public IReadOnlyList<string> WeaponIds { get; }

            // 画面で選んだ能力。選べなかったときは null
            public IReadOnlyList<string> AbilityIds { get; }

            // 自動で送った並びは 0 (画面に出さない)
            public int ScreenSession { get; }
        }
    }
}
