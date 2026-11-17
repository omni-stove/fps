using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    /// <summary>
    /// ネット対戦の HUD に出す値。届いたスナップショットの値を自分の <see cref="Model"/> に書き、それを <see cref="GameSignals.HudModel"/> に出す。
    /// 拠点・スコアボード・グレネードの残り・持ち物・目くらまし・参加者・HUD の量・ポーズ中の時間の扱い・色の組の視点も <see cref="GameSignals"/> に出し、
    /// 消すときに自分の出したものだけを消す。どう出すかは UI が決める (UI が無ければ HUD 無しで進む)。
    /// </summary>
    /// <remarks>
    /// 値が変わったときだけ書き直す。毎 tick 届くスナップショットのたびに出し直さないため。
    /// 案内の欄の優先は <c>NetHud</c> と同じ: 試合の終わり、放置の警告、ダウン中、倒れている間のリスポーンの案内、出現直後の無敵。
    /// Individual のリスポーンまでの秒数はスナップショットに無いので、倒れている間は出さない。
    /// 拠点制圧の試合では、拠点ごとの状態を <see cref="GameSignals.Domination"/> に出す。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetV2Hud : MonoBehaviour
    {
        public const string ObjectName = "NetV2HUD";

        private readonly HudModel _model = new HudModel();
        private readonly ParticipantTable _participants = new ParticipantTable();
        private readonly List<ControlPointStatus> _controlPoints = new List<ControlPointStatus>();
        private readonly List<ScoreboardEntry> _scoreboard = new List<ScoreboardEntry>();
        private Roster _roster;
        private int _localId;
        private int _maxHealth = 1;
        private bool _shown;
        private Team _team;
        private bool _teamPublished;
        private int _health;
        private int _alpha;
        private int _bravo;
        private int _remainingSeconds = -1;
        private HudDensity _density = HudDensity.Full;
        private bool _densityPublished;
        private bool _pausePublished;
        private WeaponSync _weapon;
        private HudNotice _notice;
        private bool _noticeShown;
        private float? _idleRemaining;
        private int _idleShownSeconds = -1;
        private ClientStatusEffects _statusEffects;
        private string _statusKind;
        private int _statusShownSeconds;
        private SnapshotMessage _latest;
        private Func<string, WeaponSpec> _findWeapon;
        private WeaponSpec _weaponSpec;
        private float? _localCharge;
        private GrenadeCountStatus? _grenades;
        private DominationStatus _domination;
        private ScoreboardEntry[] _publishedScoreboard;
        private Func<float> _blindSeconds;
        private IInventoryControl _inventory;

        public HudModel Model => _model;

        public IParticipants Participants => _roster;

        /// <summary>
        /// 持ち主のグレネードの残りを <see cref="GameSignals.GrenadeCount"/> に出す。typeId はクライアントが選んだ投げる種類。
        /// 種類を切り替えて持っているときだけ種類の Id を添える。pouch が null なら変えない。
        /// </summary>
        public void ShowGrenades(GrenadePouchSync pouch, string typeId)
        {
            if (pouch == null)
            {
                return;
            }

            // 1 種類しか持たないなら、オフラインと同じく種類の名前を添えない
            var status = new GrenadeCountStatus(pouch.Enabled, pouch.RemainingOf(typeId), pouch.Count > 1 ? typeId : null);
            if (_grenades == status)
            {
                return;
            }

            _grenades = status;
            GameSignals.GrenadeCount.Publish(status);
        }

        public void BindInventory(IInventoryControl inventory)
        {
            if (_inventory != null)
            {
                GameSignals.Inventory.ClearIfCurrent(_inventory);
            }

            _inventory = inventory;
            if (_inventory != null)
            {
                GameSignals.Inventory.Publish(_inventory);
            }
        }

        /// <summary>値を出し始める。ポーズを開いても対戦は止まらないので、<see cref="GameSignals.PauseTimeMode"/> は <see cref="PauseTimeMode.KeepRunning"/> にする。</summary>
        // 武器の値を引く口を受け取るのは、溜め・熱・弾薬の種類を出すかを武器の値で決めるため (WeaponSync は無い項目にも 0 を運ぶ)。
        public static NetV2Hud Create(int localPlayerId, int maxHealth, Func<string, WeaponSpec> findWeapon)
        {
            if (findWeapon == null)
            {
                throw new ArgumentNullException(nameof(findWeapon));
            }

            var netHud = new GameObject(ObjectName).AddComponent<NetV2Hud>();
            netHud._localId = localPlayerId;
            netHud._maxHealth = Math.Max(1, maxHealth);
            netHud._findWeapon = findWeapon;
            netHud._roster = new Roster(netHud);
            GameSignals.HudModel.Publish(netHud._model);
            GameSignals.Participants.Publish(netHud._roster);
            GameSignals.PauseTimeMode.Publish(PauseTimeMode.KeepRunning);
            netHud._pausePublished = true;
            return netHud;
        }

        // 溜めは押している手元の引き金の値を毎フレーム出す。スナップショットの溜めは撃った後にしか変わらないため。
        public void ShowCharge(float? fraction)
        {
            _localCharge = fraction;
            if (fraction.HasValue && _weaponSpec?.Trigger.Charge != null)
            {
                _model.SetCharge(Math.Min(1f, Math.Max(0f, fraction.Value)));
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
            var model = _model;
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
            if (!_densityPublished || density != _density)
            {
                _density = density;
                _densityPublished = true;
                GameSignals.HudDensity.Publish(density);
            }

            if (own >= 0)
            {
                var row = snapshot.Players[own];
                if (!_teamPublished || row.Team != _team)
                {
                    _team = row.Team;
                    _teamPublished = true;
                    GameSignals.PaletteViewer.Publish(_team);
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

        /// <summary>
        /// 自分にかかっている状態異常の残りを、effects から読んで出す。目くらましの残りを読む関数は <see cref="GameSignals.LocalBlindSeconds"/> に出す。
        /// </summary>
        public void BindStatusEffects(ClientStatusEffects effects)
        {
            _statusEffects = effects;
            if (_blindSeconds != null)
            {
                GameSignals.LocalBlindSeconds.ClearIfCurrent(_blindSeconds);
            }

            _blindSeconds = effects != null ? () => effects.RemainingSeconds(_localId, StatusEffectKinds.Blind) : (Func<float>)null;
            if (_blindSeconds != null)
            {
                GameSignals.LocalBlindSeconds.Publish(_blindSeconds);
            }

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
            var changed = false;
            if (_idleRemaining.HasValue)
            {
                _idleRemaining = Math.Max(0f, _idleRemaining.Value - Time.unscaledDeltaTime);
                changed = HudFormat.DisplayedSeconds(_idleRemaining.Value) != _idleShownSeconds;
            }

            ReadStatus(out var statusKind, out var statusSeconds);
            changed |= statusSeconds != _statusShownSeconds || !string.Equals(statusKind, _statusKind, StringComparison.Ordinal);
            if (changed)
            {
                Refresh();
            }
        }

        private void ReadStatus(out string kindId, out int seconds)
        {
            if (_statusEffects != null && _statusEffects.TryGetLongest(_localId, out kindId, out var remaining))
            {
                seconds = HudFormat.DisplayedSeconds(remaining);
                return;
            }

            kindId = null;
            seconds = 0;
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
            ReadStatus(out _statusKind, out _statusShownSeconds);
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
            else if (present && _statusKind != null && own.Health > 0)
            {
                notice = HudNotice.StatusEffect(_statusKind, _statusShownSeconds);
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
            _model.SetNotice(notice);
        }

        private void ShowControlPoints(SnapshotMessage snapshot)
        {
            var match = snapshot.Match;
            if (match.Objective != MatchObjective.Domination)
            {
                if (_domination != null)
                {
                    GameSignals.Domination.ClearIfCurrent(_domination);
                    _domination = null;
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

            var ended = match.State == GameModeState.Ended;
            var winner = ended && match.HasWinner ? match.Winner : (Team?)null;
            var remaining = Math.Max(0, match.RemainingSeconds);
            if (_domination != null && SameDomination(_domination, match.AlphaPoints, match.BravoPoints, remaining, ended, winner, _controlPoints))
            {
                return;
            }

            _domination = new DominationStatus(match.AlphaPoints, match.BravoPoints, remaining, _controlPoints.ToArray(), ended, winner);
            GameSignals.Domination.Publish(_domination);
        }

        private static bool SameDomination(
            DominationStatus status, float alpha, float bravo, float remaining, bool ended, Team? winner, List<ControlPointStatus> points)
        {
            if (status.AlphaScore != alpha || status.BravoScore != bravo || status.RemainingSeconds != remaining || status.IsEnded != ended
                || status.Winner != winner || status.Points.Count != points.Count)
            {
                return false;
            }

            for (var i = 0; i < points.Count; i++)
            {
                if (!status.Points[i].Equals(points[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void ShowScoreboard(SnapshotMessage snapshot)
        {
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

            if (_publishedScoreboard != null && SameEntries(_publishedScoreboard, _scoreboard))
            {
                return;
            }

            // 出すたびに新しい配列にするのは、受けた側が持っている前の一覧を書き換えないため。
            _publishedScoreboard = _scoreboard.ToArray();
            GameSignals.Scoreboard.Publish(_publishedScoreboard);
        }

        private static bool SameEntries(ScoreboardEntry[] published, List<ScoreboardEntry> entries)
        {
            if (published.Length != entries.Count)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (!published[i].Equals(entries[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDestroy()
        {
            ClearSignals();
        }

        /// <summary>
        /// 出した値を <see cref="GameSignals"/> から消す。後から別の側が出した値は消さない。破棄時に呼ばれ、何度呼んでもよい。
        /// </summary>
        public void ClearSignals()
        {
            GameSignals.HudModel.ClearIfCurrent(_model);
            if (_roster != null)
            {
                GameSignals.Participants.ClearIfCurrent(_roster);
            }

            if (_pausePublished)
            {
                GameSignals.PauseTimeMode.ClearIfCurrent(PauseTimeMode.KeepRunning);
                _pausePublished = false;
            }

            if (_densityPublished)
            {
                GameSignals.HudDensity.ClearIfCurrent(_density);
                _densityPublished = false;
            }

            if (_teamPublished)
            {
                GameSignals.PaletteViewer.ClearIfCurrent(_team);
                _teamPublished = false;
            }

            if (_domination != null)
            {
                GameSignals.Domination.ClearIfCurrent(_domination);
                _domination = null;
            }

            if (_publishedScoreboard != null)
            {
                GameSignals.Scoreboard.ClearIfCurrent(_publishedScoreboard);
                _publishedScoreboard = null;
            }

            if (_grenades.HasValue)
            {
                GameSignals.GrenadeCount.ClearIfCurrent(_grenades);
                _grenades = null;
            }

            if (_blindSeconds != null)
            {
                GameSignals.LocalBlindSeconds.ClearIfCurrent(_blindSeconds);
                _blindSeconds = null;
            }

            if (_inventory != null)
            {
                GameSignals.Inventory.ClearIfCurrent(_inventory);
                _inventory = null;
            }
        }

        // 抜けた人もスナップショットから消えるので、表に無くても Id から名前を作り、チームだけ無しにする。
        private sealed class Roster : IParticipants
        {
            private readonly NetV2Hud _owner;

            public Roster(NetV2Hud owner)
            {
                _owner = owner;
            }

            public bool TryGet(int id, out Participant participant)
            {
                if (!_owner._participants.TryGet(id, out participant))
                {
                    participant = new Participant(id, BotRoster.KillFeedName(id), null, id == _owner._localId);
                }

                return true;
            }
        }
    }
}
