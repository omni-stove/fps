using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// オンラインで、自分のロードアウトを選ぶ画面 (<see cref="LoadoutSelectView"/>) とサーバーへの送信をつなぐ。
    /// <see cref="NetHud"/> と同じ GameObject に <see cref="NetHud.Create"/> が付ける。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 倒されてリスポーンを待っている間 (<see cref="NetHud.IsAwaitingRespawn"/>) に画面を開き、出現したら閉じる。
    /// 並べる武器は試合のプレハブが持つアセット (<see cref="NetTeamDeathmatch.Loadout"/>・<see cref="NetTeamDeathmatch.Catalog"/>) から作り、
    /// 最初に選んでおく並びは保存した並び、なければ今持たされている並び (<see cref="NetPlayerWeapon.EquippedWeaponIds"/>)。
    /// 決めた並びは <see cref="NetPlayerWeapon.RequestLoadout"/> で送り、答え (<see cref="NetPlayerWeapon.LoadoutAnswered"/>) を画面に出す。
    /// 受け入れられた並びだけを <see cref="Store"/> に保存する (断られた並びを次に自動で送らないため)。
    /// </para>
    /// <para>
    /// 試合に入ったら (自分が <see cref="NetTeamDeathmatch.Players"/> に載ったら)、保存した並びを 1 度だけ自動で送る。
    /// サーバーは選んだ並びを次の出現から配るので、最初の出現は既定の並びで、次のリスポーンから保存した並びになる。
    /// 保存した並びが今の試合のルールと一覧に合わなければ送らない。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetHud))]
    public sealed class NetLoadoutChooser : MonoBehaviour
    {
        private NetHud _netHud;
        private LoadoutSelectView _view;
        private NetPlayerWeapon _weapon;
        private LoadoutPreferenceStore _store = new LoadoutPreferenceStore();

        // 保存した並びを送った試合。同じ試合に 2 度送らない (次の試合のオブジェクトになったら送り直す)
        private NetTeamDeathmatch _sentFor;

        // 送って答えを待っている並びを送った順に持つ。サーバーはどの送信にも送った順に答えるので、届いた答えは先頭のものへの答え。
        // 画面を閉じても消さないのは、閉じた後に届いた答えでも受け入れられた並びを保存するため
        private readonly Queue<PendingRequest> _pending = new Queue<PendingRequest>();
        private bool _wasAwaiting;

        // 画面を開いた回数。前に開いた画面から送った並びの答えを、開き直した画面に出さないために答えと突き合わせる
        private int _screenSession;

        /// <summary>選んだ並びの保存先。null を入れると既定のキーに戻す (テストで差し替える)。</summary>
        public LoadoutPreferenceStore Store
        {
            get => _store;
            set => _store = value ?? new LoadoutPreferenceStore();
        }

        /// <summary>送った並び (画面から・自動のどちらも) の答えを待っているか。</summary>
        public bool IsAwaitingAnswer => _pending.Count > 0;

        /// <summary>netHud の HUD の <see cref="LoadoutSelectView"/> を購読する。前に購読したものは外す。</summary>
        /// <exception cref="InvalidOperationException">HUD にロードアウトの選択のビューが無い。</exception>
        internal void Initialize(NetHud netHud)
        {
            if (netHud == null)
            {
                throw new ArgumentNullException(nameof(netHud));
            }

            UnbindAll();
            _netHud = netHud;
            _view = netHud.Hud != null ? netHud.Hud.LoadoutSelect : null;
            if (_view == null)
            {
                throw new InvalidOperationException(netHud.name + " has no LoadoutSelectView; create the HUD with Hud.Create");
            }

            _view.Confirmed += HandleConfirmed;
            BindWeapon(FindWeapon());
        }

        private void Update()
        {
            Tick();
        }

        private void OnDestroy()
        {
            UnbindAll();
        }

        /// <summary>
        /// 武器の差し替えを拾い、保存した並びを送れるなら送り、リスポーン待ちに入った・抜けたら画面を開く・閉じる。Play 中は Update が呼ぶ。
        /// </summary>
        internal void Tick()
        {
            if (_netHud == null)
            {
                return;
            }

            var weapon = FindWeapon();
            if (!ReferenceEquals(weapon, _weapon))
            {
                BindWeapon(weapon);
            }

            var match = _netHud.Match;
            TrySendSaved(match);

            var awaiting = _netHud.IsAwaitingRespawn && match != null;
            if (awaiting && !_wasAwaiting)
            {
                Open(match);
            }
            else if (!awaiting && _wasAwaiting)
            {
                _view.Close();
            }

            _wasAwaiting = awaiting;
        }

        /// <summary>
        /// 保存した並び saved を、試合のアセット (loadout・catalog) で作った選択に当てはめる。全スロットでそのまま選べて
        /// ルールに合えば送る並びを返す。保存していない・アセットが使えない・合わないなら null と理由 (保存していないときは null)。
        /// </summary>
        internal static IReadOnlyList<string> SavedLoadoutToSend(
            IReadOnlyList<string> saved, LoadoutDefinition loadout, WeaponCatalogAsset catalog, out string reason)
        {
            if (saved == null)
            {
                reason = null;
                return null;
            }

            if (!LoadoutSelectionAssets.TryCreate(loadout, catalog, saved, null, out var selection, out reason))
            {
                return null;
            }

            if (!selection.RestoredPreference)
            {
                reason = "the saved loadout has weapons this match does not offer in those slots";
                return null;
            }

            reason = selection.Validate();
            return reason == null ? selection.WeaponIds : null;
        }

        private NetPlayerWeapon FindWeapon()
        {
            var player = _netHud != null ? _netHud.Player : null;
            if (player == null)
            {
                return null;
            }

            // NetPlayer.Weapon は Awake で解決するので、Awake を経ない Edit Mode でも見つかるよう自分で探す
            return player.Weapon != null ? player.Weapon : player.GetComponent<NetPlayerWeapon>();
        }

        private void Open(NetTeamDeathmatch match)
        {
            var fallback = _weapon != null ? _weapon.EquippedWeaponIds : null;
            if (!LoadoutSelectionAssets.TryCreate(match.EffectiveLoadout, match.EffectiveCatalog, _store.Load(), fallback, out var selection, out var error))
            {
                Debug.LogWarning(LoadoutChoiceLog.Prefix + " cannot show the loadout screen: " + error, this);
                return;
            }

            _screenSession++;
            _view.Open(selection);
        }

        private void TrySendSaved(NetTeamDeathmatch match)
        {
            if (match == null || ReferenceEquals(match, _sentFor) || _weapon == null || !_weapon.IsSpawned || !_weapon.IsOwner
                || !match.IsSpawned || !HasJoined(match, _weapon.OwnerClientId))
            {
                return;
            }

            _sentFor = match;
            var ids = SavedLoadoutToSend(_store.Load(), match.EffectiveLoadout, match.EffectiveCatalog, out var reason);
            if (ids == null)
            {
                if (reason != null)
                {
                    Debug.LogWarning(LoadoutChoiceLog.Prefix + " the saved loadout is not sent: " + reason, this);
                }

                return;
            }

            Debug.Log(LoadoutChoiceLog.Prefix + " sending the saved loadout", this);
            if (_weapon.RequestLoadout(ids))
            {
                _pending.Enqueue(new PendingRequest(ids, 0));
            }
        }

        // サーバーは試合に入っていない人の選択を断るので、自分が Players に載るまで待つ
        private static bool HasJoined(NetTeamDeathmatch match, ulong clientId)
        {
            var players = match.Players;
            if (players == null)
            {
                return false;
            }

            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == clientId)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleConfirmed(IReadOnlyList<string> weaponIds)
        {
            if (_weapon == null || !_weapon.RequestLoadout(weaponIds))
            {
                _view.ShowAnswer(false, "not connected to the match");
                return;
            }

            _pending.Enqueue(new PendingRequest(new List<string>(weaponIds).AsReadOnly(), _screenSession));
            _view.ShowSending();
        }

        private void HandleAnswered(bool accepted, string reason)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            var pending = _pending.Dequeue();
            if (accepted)
            {
                try
                {
                    _store.Save(pending.WeaponIds);
                }
                catch (ArgumentException e)
                {
                    Debug.LogError(LoadoutChoiceLog.Prefix + " could not save the accepted loadout: " + e.Message, this);
                }
            }

            // 画面が待っているのは、今開いている画面から最後に送った並びの答えだけ
            if (pending.ScreenSession == _screenSession && _pending.Count == 0 && _view != null && _view.IsOpen)
            {
                _view.ShowAnswer(accepted, reason);
            }
        }

        private void BindWeapon(NetPlayerWeapon weapon)
        {
            if (!ReferenceEquals(_weapon, null))
            {
                _weapon.LoadoutAnswered -= HandleAnswered;
            }

            _weapon = weapon;
            _pending.Clear();
            if (_weapon != null)
            {
                _weapon.LoadoutAnswered += HandleAnswered;
            }
        }

        private void UnbindAll()
        {
            BindWeapon(null);
            if (_view != null)
            {
                _view.Confirmed -= HandleConfirmed;
            }

            _view = null;
            _netHud = null;
        }

        private readonly struct PendingRequest
        {
            public PendingRequest(IReadOnlyList<string> weaponIds, int screenSession)
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
