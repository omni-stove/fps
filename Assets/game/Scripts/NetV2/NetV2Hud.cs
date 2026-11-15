using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Protocol;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.NetV2
{
    /// <summary>
    /// ネット対戦の HUD。表示はオフライン・Netcode のクライアントと同じ <see cref="Hud"/> (Hud.uxml) を使い、
    /// 届いたスナップショットの値をその <see cref="Hud.Model"/> に流すだけにする (Netcode の <c>NetHud</c> と同じ出し方)。
    /// </summary>
    /// <remarks>
    /// 値が変わったときだけ書き直す。毎 tick 届くスナップショットのたびに文を作り直さないため。
    /// 案内の欄の優先は <c>NetHud</c> と同じ: 試合の終わり、放置の警告、ダウン中、倒れている間のリスポーンの案内、出現直後の無敵。
    /// Individual のリスポーンまでの秒数はスナップショットに無いので、倒れている間は出さない。
    /// 拠点制圧の試合では、拠点ごとの状態を <see cref="ControlPointsView"/> に出す (Netcode の <c>NetHud</c> と同じ部品)。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Hud))]
    public sealed class NetV2Hud : MonoBehaviour
    {
        public const string ObjectName = "NetV2HUD";

        private readonly List<ScoreboardEntry> _scoreboard = new List<ScoreboardEntry>();
        private readonly ParticipantTable _participants = new ParticipantTable();
        private readonly List<ControlPointStatus> _controlPoints = new List<ControlPointStatus>();
        private ControlPointsView _controlPointsView;
        private Hud _hud;
        private int _localId;
        private int _maxHealth = 1;
        private bool _shown;
        private Team _team;
        private int _health;
        private int _alpha;
        private int _bravo;
        private int _remainingSeconds = -1;
        private HudDensity _density = HudDensity.Full;
        private WeaponSync _weapon;
        private HudNotice _notice;
        private bool _noticeShown;
        private float? _idleRemaining;
        private int _idleShownSeconds = -1;
        private SnapshotMessage _latest;
        private Func<string, WeaponSpec> _findWeapon;
        private WeaponSpec _weaponSpec;
        private float? _localCharge;
        private GrenadeCountView _grenadeCount;
        private bool _grenadesShown;
        private bool _grenadesEnabled;
        private string _grenadeTypeId;
        private int _grenadeRemaining = -1;
        private int _grenadeTypes = -1;

        public Hud Hud => _hud;

        /// <summary>
        /// 持ち主のグレネードの残りを出す。typeId はクライアントが選んだ投げる種類。種類を切り替えて持っているときは、
        /// 種類の名前を調整値の JSON (<see cref="KitTuning.Grenades"/>) から Id で引いて添える。pouch が null なら変えない。
        /// </summary>
        public void ShowGrenades(GrenadePouchSync pouch, string typeId)
        {
            if (pouch == null)
            {
                return;
            }

            var remaining = pouch.RemainingOf(typeId);
            if (_grenadesShown && pouch.Enabled == _grenadesEnabled && remaining == _grenadeRemaining && pouch.Count == _grenadeTypes
                && string.Equals(typeId, _grenadeTypeId, StringComparison.Ordinal))
            {
                return;
            }

            _grenadesShown = true;
            _grenadesEnabled = pouch.Enabled;
            _grenadeRemaining = remaining;
            _grenadeTypes = pouch.Count;
            _grenadeTypeId = typeId;
            if (_grenadeCount == null)
            {
                _grenadeCount = GrenadeCountView.AttachTo(_hud);
            }

            // 1 種類しか持たないなら、オフラインと同じく種類の名前を添えない
            LocalizedText name = default;
            if (pouch.Count > 1)
            {
                name = KitTuning.Grenades.TryFind(typeId, out var spec) ? spec.Name : LocalizedText.Literal(typeId);
            }

            _grenadeCount.ShowCount(pouch.Enabled, name, remaining);
        }

        public IParticipants Participants => _participants;

        /// <summary>HUD を作る。作れなければ作りかけを消して例外を投げる (Resources に HUD のレイアウトかテーマが無い)。</summary>
        // 武器の値を引く口を受け取るのは、溜め・熱・弾薬の種類を出すかを武器の値で決めるため (WeaponSync は無い項目にも 0 を運ぶ)。
        public static NetV2Hud Create(int localPlayerId, int maxHealth, Func<string, WeaponSpec> findWeapon)
        {
            if (findWeapon == null)
            {
                throw new ArgumentNullException(nameof(findWeapon));
            }

            var hud = Hud.Create(ObjectName, null, null, null);
            try
            {
                var netHud = hud.gameObject.AddComponent<NetV2Hud>();
                netHud._hud = hud;
                netHud._localId = localPlayerId;
                netHud._maxHealth = Math.Max(1, maxHealth);
                netHud._findWeapon = findWeapon;

                // 対戦では止めても相手は止まらない
                hud.PauseMenu.TimeMode = PauseTimeMode.KeepRunning;
                var killFeed = hud.KillFeed;
                if (killFeed != null)
                {
                    killFeed.Names = new Names(netHud);
                }

                return netHud;
            }
            catch
            {
                Destroy(hud.gameObject);
                throw;
            }
        }

        // 溜めは押している手元の引き金の値を毎フレーム出す。スナップショットの溜めは撃った後にしか変わらないため。
        public void ShowCharge(float? fraction)
        {
            _localCharge = fraction;
            if (fraction.HasValue && _weaponSpec?.Trigger.Charge != null)
            {
                _hud.Model.SetCharge(Math.Min(1f, Math.Max(0f, fraction.Value)));
            }
        }

        /// <summary>届いた最新のスナップショットを出す。</summary>
        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _latest = snapshot;
            var model = _hud.Model;
            var match = snapshot.Match;
            _participants.Clear();
            var own = -1;
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var playerId = snapshot.Players[i].PlayerId;
                _participants.Set(new Participant(playerId, BotRoster.KillFeedName(playerId), snapshot.Players[i].Team, playerId == _localId));
                if (snapshot.Players[i].PlayerId == _localId)
                {
                    own = i;
                }
            }

            var density = Enum.IsDefined(typeof(HudDensity), match.Rules.HudDensity) ? match.Rules.HudDensity : HudDensity.Full;
            if (!_shown || density != _density)
            {
                _density = density;
                _hud.ApplyDensity(density);
            }

            if (own >= 0)
            {
                var row = snapshot.Players[own];
                if (!_shown || row.Team != _team)
                {
                    _team = row.Team;
                    _hud.UseTeamPalette(_team);
                }

                if (!_shown || row.Health != _health)
                {
                    _health = row.Health;
                    model.SetHealth(Math.Max(0, _health), _maxHealth);
                }
            }

            var alpha = match.Objective == MatchObjective.None ? match.AlphaScore : match.AlphaPoints;
            var bravo = match.Objective == MatchObjective.None ? match.BravoScore : match.BravoPoints;
            if (!_shown || alpha != _alpha || bravo != _bravo)
            {
                _alpha = alpha;
                _bravo = bravo;
                model.SetScores(_team, Math.Max(0, alpha), Math.Max(0, bravo));
            }

            if (!_shown || match.RemainingSeconds != _remainingSeconds)
            {
                _remainingSeconds = match.RemainingSeconds;
                model.SetTime(Math.Max(0, _remainingSeconds));
            }

            var weaponSpec = snapshot.HasOwner ? _findWeapon(ClientFire.CurrentWeaponId(snapshot.Owner)) : null;
            if (snapshot.HasOwner && (!_shown || !snapshot.Owner.Weapon.SameAs(_weapon) || weaponSpec != _weaponSpec))
            {
                _weapon = snapshot.Owner.Weapon;
                _weaponSpec = weaponSpec;
                ClientWeaponGauges.Show(model, _weapon, weaponSpec, _localCharge);
                if (_weapon.WeaponCount == 0 || (weaponSpec != null && weaponSpec.Ammo.Kind == AmmoKind.Heat))
                {
                    model.ClearAmmo();
                }
                else
                {
                    model.SetAmmo(_weapon.AmmoInMagazine, _weapon.ReserveAmmo, _weapon.IsReloading, match.Rules.InfiniteReserve);
                }
            }

            if (match.State == GameModeState.Ended)
            {
                model.SetResult(match.HasWinner ? match.Winner : (Team?)null);
            }
            else
            {
                model.ClearResult();
            }

            _shown = true;
            ShowControlPoints(snapshot);
            ShowNotice(own >= 0 ? snapshot.Players[own] : default, own >= 0);
            ShowScoreboard(snapshot);
        }

        /// <summary>「あと remainingSeconds 秒で切断」の放置の警告を出し、ここから数える。</summary>
        public void ShowIdleWarning(float remainingSeconds)
        {
            _idleRemaining = float.IsNaN(remainingSeconds) ? 0f : Math.Max(0f, remainingSeconds);
            _idleShownSeconds = -1;
            Refresh();
        }

        public void ClearIdleWarning()
        {
            if (!_idleRemaining.HasValue)
            {
                return;
            }

            _idleRemaining = null;
            Refresh();
        }

        private void Update()
        {
            if (!_idleRemaining.HasValue)
            {
                return;
            }

            _idleRemaining = Math.Max(0f, _idleRemaining.Value - Time.unscaledDeltaTime);
            if (HudFormat.DisplayedSeconds(_idleRemaining.Value) != _idleShownSeconds)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_latest != null)
            {
                var own = _latest.Players.IndexOf(_localId);
                ShowNotice(own >= 0 ? _latest.Players[own] : default, own >= 0);
            }
        }

        private void ShowNotice(PlayerSnapshot own, bool present)
        {
            var match = _latest.Match;
            HudNotice notice;
            if (match.State == GameModeState.Ended)
            {
                notice = HudNotice.NextMatch(Math.Max(0, match.NextMatchSeconds));
            }
            else if (_idleRemaining.HasValue)
            {
                _idleShownSeconds = HudFormat.DisplayedSeconds(_idleRemaining.Value);
                notice = HudNotice.IdleWarning(_idleShownSeconds);
            }
            else if (present && own.Downed.IsDowned)
            {
                notice = HudNotice.Downed(Math.Max(0, own.Downed.BleedoutSeconds), Math.Min(100, (int)own.Downed.RevivePercent));
            }
            else if (present && own.Health <= 0 && match.State == GameModeState.Playing)
            {
                notice = match.Rules.RespawnMode == RespawnMode.Wave
                    ? HudNotice.ForRespawn(RespawnMode.Wave, Math.Max(0, match.NextWaveSeconds))
                    : match.Rules.RespawnMode == RespawnMode.None
                        ? HudNotice.ForRespawn(RespawnMode.None, 0)
                        : HudNotice.None;
            }
            else if (present && own.SpawnProtectionSeconds > 0)
            {
                notice = HudNotice.SpawnProtected(own.SpawnProtectionSeconds);
            }
            else
            {
                notice = default;
            }

            if (_noticeShown && notice == _notice)
            {
                return;
            }

            _notice = notice;
            _noticeShown = true;
            _hud.Model.SetNotice(notice);
        }

        private void ShowControlPoints(SnapshotMessage snapshot)
        {
            if (snapshot.Match.Objective != MatchObjective.Domination)
            {
                if (_controlPointsView != null)
                {
                    _controlPointsView.Clear();
                }

                return;
            }

            _controlPoints.Clear();
            var points = snapshot.ControlPoints;
            for (var index = 0; _controlPoints.Count < points.Count && index < SnapshotMessage.MaxControlPoints; index++)
            {
                var at = points.IndexOf(index);
                if (at < 0)
                {
                    // 番号が抜けた拠点は出さない (HUD の呼び名 A, B, C ... は並びの順に付くため、抜けたまま詰めると名前がずれる)
                    break;
                }

                var point = points[at];
                _controlPoints.Add(new ControlPointStatus(point.HasOwner ? point.Owner : (Team?)null, point.ControlPercent, point.Contested));
            }

            var view = EnsureControlPointsView();
            var alpha = _hud.TeamColor(Team.Alpha);
            var bravo = _hud.TeamColor(Team.Bravo);
            if (view.ColorOf(Team.Alpha) != alpha || view.ColorOf(Team.Bravo) != bravo)
            {
                view.SetTeamColors(alpha, bravo);
            }

            view.Show(_controlPoints);
        }

        // 拠点制圧のときだけ要るビューなので、Hud.AddViews では付けずに初めて要るときに付ける (Netcode の NetHud と同じ)
        private ControlPointsView EnsureControlPointsView()
        {
            if (_controlPointsView != null)
            {
                return _controlPointsView;
            }

            _controlPointsView = GetComponent<ControlPointsView>();
            if (_controlPointsView == null)
            {
                _controlPointsView = gameObject.AddComponent<ControlPointsView>();
            }

            // 木を作ったときの Bind には間に合っていないので、今の木があればここで要素を渡す (作り直したときは Hud が渡す)
            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (_controlPointsView.Element == null && root != null && root.childCount > 0)
            {
                _controlPointsView.Bind(root);
            }

            return _controlPointsView;
        }

        private void ShowScoreboard(SnapshotMessage snapshot)
        {
            var scoreboard = _hud.Scoreboard;
            if (scoreboard == null)
            {
                return;
            }

            _scoreboard.Clear();
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var row = snapshot.Players[i];
                _scoreboard.Add(new ScoreboardEntry(
                    row.PlayerId,
                    BotRoster.ScoreboardName(row.PlayerId),
                    row.Team,
                    Math.Max(0, row.Kills),
                    Math.Max(0, row.Deaths),
                    ScoreboardConnection.Connected,
                    row.PlayerId == _localId));
            }

            scoreboard.SetEntries(_scoreboard, _team);
        }

        /// <summary>キルフィードの名前。Id は .NET のサーバーのプレイヤー Id で、名前は Netcode のクライアントと同じく <see cref="BotRoster.KillFeedName"/>。</summary>
        private sealed class Names : IKillFeedNames
        {
            private readonly NetV2Hud _owner;

            public Names(NetV2Hud owner)
            {
                _owner = owner;
            }

            public KillFeedLine Describe(KillFeedEntry entry)
            {
                var killer = NameOf(entry.KillerId);
                var victim = entry.IsSuicide ? killer : NameOf(entry.VictimId);
                return new KillFeedLine(entry, killer, victim, default);
            }

            private KillFeedName NameOf(int playerId)
            {
                // 抜けた人もスナップショットから消えるので、表に無くても Id から名前を作り、チームだけ無しにする。
                return _owner._participants.TryGet(playerId, out var participant)
                    ? new KillFeedName(participant)
                    : new KillFeedName(BotRoster.KillFeedName(playerId), null, playerId == _owner._localId);
            }
        }
    }
}
