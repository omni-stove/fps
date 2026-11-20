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
    // 受け入れられた能力は、能力の枠を持つ部品 (NetV2DeployInputPart) をこのクラスに縛らないよう、イベントで外へ出す。
    internal sealed class NetV2LoadoutChooser : IDisposable
    {
        private readonly NetV2Weapons _weapons;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<string> _cosmeticId;
        private readonly LoadoutPreferenceStore _store;
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

        // store を渡せるのは、テストで本物の保存を壊さないよう別のキーを使うため
        public NetV2LoadoutChooser(
            NetV2Weapons weapons,
            Func<IMessage, bool> send,
            UnityEngine.Object context,
            Func<string> cosmeticId,
            LoadoutPreferenceStore store = null)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _cosmeticId = cosmeticId ?? throw new ArgumentNullException(nameof(cosmeticId));
            _context = context;
            _store = store ?? new LoadoutPreferenceStore();
        }

        // 能力を載せて送った並びが受け入れられたときだけ出す。能力を載せない要求ではサーバーがロードアウトの能力を使うので、枠を変えさせない
        public event Action<IReadOnlyList<string>> AbilitiesAccepted;

        // 能力の枠の順で、「なし」の枠は AbilitySelection.None。まだ受け入れられていなければ null
        public IReadOnlyList<string> AcceptedAbilityIds { get; private set; }

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
            Send(selection.WeaponIds, SavedAbilitiesToSend(saved.AbilityIds), 0);
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
                    // 能力を載せなかった並びは能力を null のまま保存し、次に送るときもロードアウトの能力に任せる
                    _store.Save(new LoadoutChoice(pending.WeaponIds, pending.AbilityIds));
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

            if (answer.Accepted && pending.AbilityIds != null)
            {
                AcceptedAbilityIds = pending.AbilityIds;
                AbilitiesAccepted?.Invoke(pending.AbilityIds);
            }
        }

        public void Dispose()
        {
            Unbind();
            _pending.Clear();
            AbilitiesAccepted = null;
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
            if (!LoadoutSelectionAssets.TryCreateAbilities(_weapons.Loadout, AbilityCatalog(), saved?.AbilityIds, out var abilities, out error))
            {
                Debug.LogWarning(LoadoutChoiceLog.AbilitiesNotOffered(error), _context);
            }

            _screenSession++;
            _view.Open(selection, abilities);
        }

        private void HandleConfirmed(LoadoutChoice choice)
        {
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
            _request.AbilityIds.Clear();
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

        // abilityIds が null なら能力を空の並びで送る。サーバーは空の並びをロードアウトの能力のままにする
        private bool Send(IReadOnlyList<string> weaponIds, IReadOnlyList<string> abilityIds, int screenSession)
        {
            _request.CosmeticId = CosmeticToSend();
            Fill(_request.WeaponIds, weaponIds);
            Fill(_request.AbilityIds, abilityIds ?? Array.Empty<string>());
            if (!_send(_request))
            {
                return false;
            }

            Debug.Log(LoadoutChoiceLog.Requested(weaponIds, abilityIds), _context);
            _pending.Enqueue(new Pending(Copy(weaponIds), abilityIds != null ? Copy(abilityIds) : null, screenSession));
            return true;
        }

        // 保存した能力が今の試合で選べないときは、武器まで止めずに能力だけを載せずに送る。断られる並びを自動で送らないため
        private IReadOnlyList<string> SavedAbilitiesToSend(IReadOnlyList<string> saved)
        {
            if (saved == null)
            {
                return null;
            }

            if (!LoadoutSelectionAssets.TryCreateAbilities(_weapons.Loadout, AbilityCatalog(), saved, out var abilities, out var reason))
            {
                Debug.LogWarning(LoadoutChoiceLog.SavedAbilitiesNotSent(reason), _context);
                return null;
            }

            // カタログが無いのは能力を選ばせないゲームなので、警告にしない
            if (abilities == null)
            {
                return null;
            }

            if (!abilities.RestoredPreference)
            {
                Debug.LogWarning(LoadoutChoiceLog.SavedAbilitiesNotSent("the saved abilities are not all offered in those slots"), _context);
                return null;
            }

            return abilities.AbilityIds;
        }

        private static AbilityCatalogAsset AbilityCatalog()
        {
            return GameContent.Pick(c => c.AbilityCatalog);
        }

        private static void Fill(ContentIdList list, IReadOnlyList<string> ids)
        {
            list.Clear();
            for (var i = 0; i < ids.Count && list.Count < list.MaxCount; i++)
            {
                list.Add(ids[i]);
            }
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

            // 能力を載せずに送った並びは null
            public IReadOnlyList<string> AbilityIds { get; }

            // 自動で送った並びは 0 (画面に出さない)
            public int ScreenSession { get; }
        }
    }
}
