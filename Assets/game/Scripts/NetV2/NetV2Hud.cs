using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 毎 tick 届くスナップショットのたびに出し直さないよう、値が変わったときだけ書き直す。
    // 案内の欄の優先は NetHud と揃える: 試合の終わり、放置の警告、ダウン中、倒れている間のリスポーンの案内、出現直後の無敵。
    // Individual のリスポーンまでの秒数はスナップショットに無いので、倒れている間は出さない。
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
        private ClientCaptureTheFlagStatus _captureTheFlag;
        private ModeStatus _modeStatus;
        private ScoreboardEntry[] _publishedScoreboard;
        private Func<float> _blindSeconds;
        private IInventoryControl _inventory;
        private ClientSearchAndDestroyStatus _searchAndDestroy;
        private ClientHardpointStatus _hardpoint;
        private ClientKillConfirmedStatus _killConfirmed;
        private ClientGunGameStatus _gunGame;
        private bool _gunGameMissingLogged;
        private bool _gunGameMismatchLogged;
        private ModeStatus _perSnapshotStatus;
        private long _perSnapshotKey;

        public HudModel Model => _model;

        public IParticipants Participants => _roster;

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

        // ポーズを開いても対戦は止まらないので、GameSignals.PauseTimeMode は PauseTimeMode.KeepRunning にする。
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
            netHud._captureTheFlag = new ClientCaptureTheFlagStatus(localPlayerId);
            netHud._searchAndDestroy = new ClientSearchAndDestroyStatus(localPlayerId);
            netHud._hardpoint = new ClientHardpointStatus(localPlayerId);
            netHud._killConfirmed = new ClientKillConfirmedStatus(localPlayerId);
            netHud._gunGame = ConfiguredGunGame();
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

            var alpha = TeamScore(match, match.AlphaScore, match.AlphaPoints);
            var bravo = TeamScore(match, match.BravoScore, match.BravoPoints);
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
            ShowModeStatus(snapshot);
            ShowNotice(own >= 0 ? snapshot.Players[own] : default, own >= 0);
            ShowScoreboard(snapshot);
        }

        public void ShowIdleWarning(float remainingSeconds)
        {
            _idleRemaining = float.IsNaN(remainingSeconds) ? 0f : Math.Max(0f, remainingSeconds);
            _idleShownSeconds = -1;
            Refresh();
        }

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

        // FFA は AlphaPoints・BravoPoints に 1 キルの点とキル数の上限を載せていてチームの点ではないので、GunGame と同じく 0 を出す
        private static int TeamScore(MatchSnapshot match, int score, int points)
        {
            switch (match.Objective)
            {
                case MatchObjective.None:
                    return score;
                case MatchObjective.FreeForAll:
                    return 0;
                default:
                    return points;
            }
        }

        // 武器の表示名の鍵は梯子の定義にしか無いので、サーバーの段の並びではなく手元の入口 (GameContent) の梯子で作る。無ければ Gun Game の状態は出さない
        private static ClientGunGameStatus ConfiguredGunGame()
        {
            var definition = GameContent.Pick(c => c.GunGameLadder);
            if (definition == null || definition.Tiers.Count == 0)
            {
                return null;
            }

            try
            {
                return new ClientGunGameStatus(definition.ToLadder());
            }
            catch (Exception e) when (e is WeaponDefinitionException || e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogWarning(ClientGunGameLog.LadderNotShown(definition.name, e.Message));
                return null;
            }
        }

        private void PublishModeStatus(ModeStatus status)
        {
            if (_modeStatus != null)
            {
                GameSignals.ModeStatus.ClearIfCurrent(_modeStatus);
            }

            _modeStatus = status;
            if (_modeStatus != null)
            {
                GameSignals.ModeStatus.Publish(_modeStatus);
            }
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

        private void ShowModeStatus(SnapshotMessage snapshot)
        {
            // || だと先の Apply で後の Apply が飛ばされ、目的が変わったときに後の側の状態が残るので | ですべてに渡す
            var changed = (_captureTheFlag != null && _captureTheFlag.Apply(snapshot))
                | (_searchAndDestroy != null && _searchAndDestroy.Apply(snapshot))
                | (_hardpoint != null && _hardpoint.Apply(snapshot))
                | (_killConfirmed != null && _killConfirmed.Apply(snapshot))
                | ApplyPerSnapshot(snapshot);
            if (changed)
            {
                // 1 つが出したあとに、別のものが消えたことで null を出して上書きしないように、残っているものを出す
                PublishModeStatus(_captureTheFlag?.Status
                    ?? _searchAndDestroy?.Status
                    ?? _hardpoint?.Status
                    ?? _killConfirmed?.Status
                    ?? _perSnapshotStatus);
            }
        }

        // FFA と Gun Game の状態はスナップショットから毎回作り直すので、出す値 (残り秒・状態・勝者・各人のキル数と段) が変わったときだけ差し替える
        private bool ApplyPerSnapshot(SnapshotMessage snapshot)
        {
            var match = snapshot.Match;
            ModeStatus status = null;
            if (match.Objective == MatchObjective.FreeForAll)
            {
                status = ClientFreeForAllStatus.From(snapshot, _localId);
            }
            else if (match.Objective == MatchObjective.GunGame)
            {
                status = GunGameStatusOf(snapshot);
            }

            if (status == null)
            {
                if (_perSnapshotStatus == null)
                {
                    return false;
                }

                _perSnapshotStatus = null;
                _perSnapshotKey = 0;
                return true;
            }

            var key = PerSnapshotKey(snapshot);
            if (_perSnapshotStatus != null && key == _perSnapshotKey)
            {
                return false;
            }

            _perSnapshotStatus = status;
            _perSnapshotKey = key;
            return true;
        }

        private ModeStatus GunGameStatusOf(SnapshotMessage snapshot)
        {
            if (_gunGame == null)
            {
                if (!_gunGameMissingLogged)
                {
                    _gunGameMissingLogged = true;
                    Debug.LogWarning(ClientGunGameLog.LadderMissing());
                }

                return null;
            }

            var status = _gunGame.Update(snapshot, _localId);
            if (_gunGame.LadderMismatch && !_gunGameMismatchLogged)
            {
                _gunGameMismatchLogged = true;
                Debug.LogWarning(ClientGunGameLog.LadderMismatch());
            }

            return status;
        }

        public static long PerSnapshotKey(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var match = snapshot.Match;
            unchecked
            {
                long key = 17;
                key = (key * 31) + (int)match.Objective;
                key = (key * 31) + (int)match.State;
                key = (key * 31) + match.RemainingSeconds;
                key = (key * 31) + (match.HasWinner ? 1 + (int)match.Winner : 0);
                key = (key * 31) + match.AlphaPoints;
                var players = snapshot.Players;
                for (var i = 0; i < players.Count; i++)
                {
                    var row = players[i];
                    key = (key * 31) + row.PlayerId;
                    key = (key * 31) + row.Kills;
                    key = (key * 31) + row.GunGameTier;
                }

                return key;
            }
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
                    Math.Max(0, row.Assists),
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

            PublishModeStatus(null);
            _captureTheFlag?.Clear();
            _searchAndDestroy?.Reset();
            _hardpoint?.Clear();
            _killConfirmed?.Reset();
            _perSnapshotStatus = null;
            _perSnapshotKey = 0;

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
