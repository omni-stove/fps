using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    /// <summary>
    /// ネット対戦の、自分のロードアウトを選ぶ画面 (<see cref="GameUi.LoadoutSelect"/>) とサーバーへの送信をつなぐ。
    /// 振る舞いは Netcode のクライアントの <c>NetLoadoutChooser</c> と同じ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 倒されてリスポーンを待っている間に画面を開き、出現したら閉じる。並べる武器は入口のロードアウトと一覧 (<see cref="NetV2Weapons"/>) から作り、
    /// 最初に選んでおく並びは保存した並び、なければ今持たされている並び。受け入れられた並びだけを保存する (断られた並びを次に自動で送らないため)。
    /// </para>
    /// <para>
    /// 承認されたら保存した並びを 1 度だけ送る。サーバーは選んだ並びを次の出現から配るので、最初の出現は既定の並びになる。
    /// サーバーはどの要求にも送った順に答えるので、届いた答えは待っている先頭の並びへのもの。
    /// </para>
    /// </remarks>
    internal sealed class NetV2LoadoutChooser : IDisposable
    {
        private readonly NetV2Weapons _weapons;
        private readonly Func<IMessage, bool> _send;
        private readonly LoadoutPreferenceStore _store = new LoadoutPreferenceStore();
        private readonly LoadoutRequestMessage _request = new LoadoutRequestMessage();
        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private readonly UnityEngine.Object _context;
        private ILoadoutSelect _view;
        private IReadOnlyList<string> _equipped = Array.Empty<string>();
        private bool _awaiting;

        // 画面を開いた回数。前に開いた画面から送った並びの答えを、開き直した画面に出さないために答えと突き合わせる
        private int _screenSession;

        /// <param name="send">確実な届け方でサーバーへ送る。承認の前で送れなければ false。</param>
        public NetV2LoadoutChooser(NetV2Weapons weapons, Func<IMessage, bool> send, UnityEngine.Object context)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _context = context;
        }

        /// <summary>画面を使い始める。画面が無い (UI が無い) ときは null を渡し、自動で送るだけにする。</summary>
        public void Bind(ILoadoutSelect view)
        {
            Unbind();
            _view = view;
            if (_view != null)
            {
                _view.Confirmed += HandleConfirmed;
            }
        }

        /// <summary>保存した並びが今のアセットとルールで選べるなら送る。承認されたときに 1 度呼ぶ。</summary>
        public void SendSaved()
        {
            var saved = LoadSaved();
            if (saved == null)
            {
                return;
            }

            if (!LoadoutSelectionAssets.TryCreate(_weapons.Loadout, _weapons.Catalog, saved, null, out var selection, out var reason))
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " the saved loadout is not sent: " + reason, _context);
                return;
            }

            reason = selection.RestoredPreference ? selection.Validate() : "the saved loadout has weapons this match does not offer in those slots";
            if (reason != null)
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " the saved loadout is not sent: " + reason, _context);
                return;
            }

            Debug.Log(LoadoutChoiceLog.Prefix + " sending the saved loadout", _context);
            Send(selection.WeaponIds, 0);
        }

        /// <summary>届いたスナップショットで、リスポーン待ちに入った・抜けたら画面を開く・閉じる。</summary>
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
                    _store.Save(pending.WeaponIds);
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

            if (!LoadoutSelectionAssets.TryCreate(_weapons.Loadout, _weapons.Catalog, LoadSaved(), _equipped, out var selection, out var error))
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " cannot show the loadout screen: " + error, _context);
                return;
            }

            _screenSession++;
            _view.Open(selection);
        }

        private void HandleConfirmed(IReadOnlyList<string> weaponIds)
        {
            if (!Send(weaponIds, _screenSession))
            {
                _view.ShowAnswer(false, "not connected to the match");
                return;
            }

            _view.ShowSending();
        }

        private bool Send(IReadOnlyList<string> weaponIds, int screenSession)
        {
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
            _pending.Enqueue(new Pending(Copy(weaponIds), screenSession));
            return true;
        }

        private IReadOnlyList<string> LoadSaved()
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
            public Pending(IReadOnlyList<string> weaponIds, int screenSession)
            {
                WeaponIds = weaponIds;
                ScreenSession = screenSession;
            }

            public IReadOnlyList<string> WeaponIds { get; }

            // 自動で送った並びは 0 (画面に出さない)
            public int ScreenSession { get; }
        }
    }
}
