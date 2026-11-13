using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// オンラインの HUD。自分の <see cref="NetPlayer"/> (体力・チーム)、その <see cref="NetPlayerWeapon"/> (弾数・リロード)、
    /// <see cref="NetTeamDeathmatch"/> (チームの点数・残り時間・試合の状態・次の試合までの秒数) の NetworkVariable の
    /// OnValueChanged を購読し、届いた値を <see cref="Hud.Model"/> の Set 系に流す。表示は <see cref="Hud"/> と同じ Hud.uxml を使う。
    /// </summary>
    /// <remarks>
    /// <see cref="NetPlayer"/> が自分のプレイヤーで画面があるときだけ <see cref="Create"/> で作り、消滅時に GameObject ごと消す。
    /// 試合は <see cref="NetTeamDeathmatch.CurrentChanged"/> で待つ (プレイヤーより後に Spawn されることがあるため)。
    /// <para>
    /// 倒れている間の案内はリスポーン方式 (<see cref="NetTeamDeathmatch.SyncedRules"/> の <see cref="NetMatchRules.RespawnMode"/>) で変わる。
    /// Individual の「リスポーンまで N 秒」だけは時間で進むので、倒れた値が届いたときに <see cref="RespawnCountdown"/> を
    /// 始め、<see cref="Tick"/> で数える。サーバーは個人の待ち時間を配らないので、同じプレハブの <see cref="NetTeamDeathmatch.RespawnSeconds"/>
    /// から数える。表示は秒の桁が変わったときだけ変わる。Wave ではサーバーが配る次のウェーブまでの秒数
    /// (<see cref="NetTeamDeathmatch.NextWaveSeconds"/>) を、None では次の試合まで戻らないことを出す。
    /// </para>
    /// <para>
    /// 予備弾は <see cref="NetMatchRules.InfiniteReserve"/> が true なら数の代わりに <see cref="HudFormat.InfiniteReserve"/> を出す。
    /// </para>
    /// <para>
    /// ダウン中は自分の <see cref="NetPlayer.DownedState"/> から「ダウン中 あと N 秒」と起こされている進み (<see cref="HudFormat.Downed(int, int)"/>) を、
    /// 味方を起こしている間は相手 (<see cref="NetPlayer.ReviveTarget"/>) の進み (<see cref="HudFormat.Reviving"/>) を案内の欄に出す。
    /// </para>
    /// <para>
    /// アクティブリロードの進みも時間で進むので、毎フレーム <see cref="RefreshReloadProgress"/> で、届いた <see cref="NetWeaponState"/> の
    /// 開始時刻と長さから求めて <see cref="HudModel.SetReloadProgress"/> に渡す。時刻は NetworkManager.LocalTime を使う。
    /// LocalTime はサーバーの時刻の尺で、今送った RPC がサーバーに着く頃を指すので、バーが「今押したらサーバーで判定される進み」になる。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Hud))]
    public sealed class NetHud : MonoBehaviour
    {
        /// <summary><see cref="Create"/> が作る GameObject の名前。</summary>
        public const string ObjectName = "NetHUD";

        private Hud _hud;
        private NetPlayer _player;
        private NetPlayerWeapon _weapon;
        private NetTeamDeathmatch _match;
        private bool _listeningForMatch;

        // 届いた値の控え。片方の値だけが届いたとき (点数など) にもう片方を NetworkVariable から読み直さないため
        private Team _team;
        private int _health;
        private int _alphaScore;
        private int _bravoScore;
        private int _nextMatchSeconds;
        private int _nextWaveSeconds;
        private NetMatchStatus _status = new NetMatchStatus(GameModeState.Waiting, null);
        private NetMatchRules _rules = NetMatchRules.Default;
        private NetWeaponState _weaponState;

        // 試合中に倒れてから、出現するか試合が終わるまで true。_respawn は Individual の表示に使う数え (待ち時間が不正なら null)
        private bool _isDown;
        private RespawnCountdown _respawn;

        // 自分のダウンの状態と、自分が起こしている味方の蘇生の進み (起こしていなければ null)
        private NetDownedState _downedState;
        private int? _revivingPercent;

        /// <summary>流し込み先の HUD。</summary>
        public Hud Hud => _hud;

        /// <summary>購読している自分のプレイヤー。</summary>
        public NetPlayer Player => _player;

        /// <summary>購読している試合。まだ無ければ null。</summary>
        public NetTeamDeathmatch Match => _match;

        /// <summary>試合中に倒れてから、出現するか試合が終わるまで true (None で次の試合を待っている間を含む)。</summary>
        public bool IsAwaitingRespawn => _isDown;

        /// <summary>
        /// HUD (<see cref="Hud.Create"/>) を作り、player とその武器、今の試合 (<see cref="NetTeamDeathmatch.Current"/>) を購読する。
        /// ヒットマーカー・キルフィードなどのビューと <see cref="PauseMenu"/> は、オフラインと同じく <see cref="Hud.Create"/> が
        /// (<see cref="Hud.AddViews"/> で) 付ける。
        /// 失敗したら作りかけのものは消して例外を投げる。
        /// </summary>
        /// <exception cref="InvalidOperationException">Resources に HUD のレイアウトかテーマが無い。</exception>
        public static NetHud Create(NetPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var hud = Hud.Create(ObjectName, null, null, null);
            try
            {
                var netHud = hud.gameObject.AddComponent<NetHud>();
                netHud.Initialize(player);
                netHud.ListenForMatch();
                ConfigurePauseMenu(hud.PauseMenu);
                return netHud;
            }
            catch
            {
                DestroySafely(hud.OwnedPanelSettings);
                DestroySafely(hud.gameObject);
                throw;
            }
        }

        /// <summary>
        /// オンラインのポーズ画面は時間を止めず (<see cref="PauseTimeMode.KeepRunning"/>)、「メニューに戻る」では
        /// シーンを読み込み直す前に試合から抜ける (<see cref="NetBootstrap.LeaveCurrent"/>)。
        /// 購読はポーズ画面と同じ GameObject の寿命なので外さない。
        /// </summary>
        internal static void ConfigurePauseMenu(PauseMenu pauseMenu)
        {
            if (pauseMenu == null)
            {
                throw new ArgumentNullException(nameof(pauseMenu));
            }

            pauseMenu.TimeMode = PauseTimeMode.KeepRunning;
            pauseMenu.ReturningToMenu -= NetBootstrap.LeaveCurrent;
            pauseMenu.ReturningToMenu += NetBootstrap.LeaveCurrent;
        }

        /// <summary>
        /// player とその武器 (<see cref="NetPlayer.Weapon"/>) の同期値を購読し、今の値をすぐ出す。前に渡したプレイヤーの購読は外す。
        /// チームの色は <see cref="NetPlayer.AlphaColor"/> / <see cref="NetPlayer.BravoColor"/> で塗る。
        /// </summary>
        internal void Initialize(NetPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            _hud = GetComponent<Hud>();
            if (_hud == null)
            {
                throw new InvalidOperationException(name + " has no Hud; create it with NetHud.Create");
            }

            UnbindPlayer();
            _hud.SetTeamColors(NetPlayer.AlphaColor, NetPlayer.BravoColor);

            _player = player;
            _team = player.TeamVariable.Value;
            _health = player.HealthVariable.Value;
            player.TeamVariable.OnValueChanged += HandleTeamChanged;
            player.HealthVariable.OnValueChanged += HandleHealthChanged;
            _downedState = player.DownedStateVariable.Value;
            _revivingPercent = null;
            player.DownedStateVariable.OnValueChanged += HandleDownedStateChanged;

            // NetPlayer.Weapon は Awake で解決するので、Awake を経ない Edit Mode でも見つかるよう自分で探す
            _weapon = player.GetComponent<NetPlayerWeapon>();
            _weaponState = _weapon != null ? _weapon.StateVariable.Value : default;
            if (_weapon != null)
            {
                _weapon.StateVariable.OnValueChanged += HandleWeaponChanged;
            }

            ShowAmmo();
            ShowHealth();
            ShowScores();
            ShowNotice();
            RefreshReloadProgressNow();
        }

        /// <summary>
        /// 自分が味方を起こしている進み (0〜100) を出す。null なら起こしていない。変わったときだけ案内を書き直す。
        /// Play 中は Update が、自分のプレイヤーの <see cref="NetPlayer.ReviveTarget"/> の <see cref="NetPlayer.DownedState"/> から呼ぶ。
        /// </summary>
        internal void ShowReviving(int? percent)
        {
            var clamped = percent.HasValue ? Math.Max(0, Math.Min(100, percent.Value)) : (int?)null;
            if (clamped == _revivingPercent)
            {
                return;
            }

            _revivingPercent = clamped;
            ShowNotice();
        }

        // 相手の進みは相手の NetworkVariable に届くので、購読せずに毎フレーム読む (起こす相手はフレームごとに変わりうる)
        private int? ReadRevivingPercent()
        {
            var target = _player != null ? _player.ReviveTarget : null;
            return target != null && target.IsDowned ? target.DownedState.RevivePercent : (int?)null;
        }

        /// <summary>
        /// 届いている武器の状態 (<see cref="NetWeaponState"/>) から、サーバーの時刻 serverTime でのリロードの進みを HUD に出す。
        /// 武器が無い・進みを出せない状態 (<see cref="NetWeaponState.HasReloadProgress"/> が false) なら消す。
        /// 判定を済ませた後 (成功になる幅が届かない) は、オフラインと同じく幅を 0 にして進みだけを出す。Play 中は Update が呼ぶ。
        /// </summary>
        internal void RefreshReloadProgress(double serverTime)
        {
            if (_weapon == null || !_weaponState.HasReloadProgress)
            {
                Model.ClearReloadProgress();
                return;
            }

            var progress = _weaponState.ReloadProgressAt(serverTime);
            if (_weaponState.HasActiveReloadWindow)
            {
                Model.SetReloadProgress(progress, _weaponState.ActiveReloadWindowStart, _weaponState.ActiveReloadWindowEnd);
            }
            else
            {
                Model.SetReloadProgress(progress, 0f, 0f);
            }
        }

        /// <summary>
        /// 試合の同期値を購読し、今の値をすぐ出す。前の試合の購読は外す。null なら点数・残り時間・結果を消す。
        /// </summary>
        internal void BindMatch(NetTeamDeathmatch match)
        {
            UnbindMatch();
            CancelRespawn();

            _match = match;
            if (_match == null)
            {
                _alphaScore = 0;
                _bravoScore = 0;
                _nextMatchSeconds = 0;
                _nextWaveSeconds = 0;
                _status = new NetMatchStatus(GameModeState.Waiting, null);
                _rules = NetMatchRules.Default;
                Model.ClearTime();
                Model.ClearScores();
                Model.ClearResult();
                ShowAmmo();
                ShowHealth();
                ShowNotice();
                return;
            }

            _alphaScore = _match.AlphaScoreVariable.Value;
            _bravoScore = _match.BravoScoreVariable.Value;
            _nextMatchSeconds = _match.NextMatchSecondsVariable.Value;
            _nextWaveSeconds = _match.NextWaveSecondsVariable.Value;
            _status = _match.StatusVariable.Value;
            _rules = _match.RulesVariable.Value;
            _match.AlphaScoreVariable.OnValueChanged += HandleAlphaScoreChanged;
            _match.BravoScoreVariable.OnValueChanged += HandleBravoScoreChanged;
            _match.RemainingSecondsVariable.OnValueChanged += HandleRemainingSecondsChanged;
            _match.NextMatchSecondsVariable.OnValueChanged += HandleNextMatchSecondsChanged;
            _match.NextWaveSecondsVariable.OnValueChanged += HandleNextWaveSecondsChanged;
            _match.StatusVariable.OnValueChanged += HandleStatusChanged;
            _match.RulesVariable.OnValueChanged += HandleRulesChanged;

            Model.SetTime(_match.RemainingSecondsVariable.Value);
            ShowAmmo();
            ShowHealth();
            ShowScores();
            ShowResult();
            ShowNotice();
        }

        /// <summary>リスポーンまでの秒数を deltaSeconds 進める。待っていなければ何もしない。Play 中は Update が呼ぶ。</summary>
        internal void Tick(float deltaSeconds)
        {
            if (_respawn == null)
            {
                return;
            }

            _respawn.Tick(deltaSeconds);
            ShowNotice();
        }

        private HudModel Model => _hud.Model;

        private void Update()
        {
            Tick(Time.deltaTime);
            ShowReviving(ReadRevivingPercent());
            RefreshReloadProgressNow();
        }

        // Spawn 前 (Edit Mode を含む) は時刻が無いので進みを出さない
        private void RefreshReloadProgressNow()
        {
            if (_weapon != null && _weapon.IsSpawned && _weapon.NetworkManager != null)
            {
                RefreshReloadProgress(_weapon.NetworkManager.LocalTime.Time);
            }
            else
            {
                Model.ClearReloadProgress();
            }
        }

        private void OnDestroy()
        {
            UnbindAll();
        }

        /// <summary>
        /// プレイヤー・武器・試合と <see cref="NetTeamDeathmatch.CurrentChanged"/> の購読をすべて外す。表示はそのまま残す。
        /// 破棄時に呼ぶ (Edit Mode では OnDestroy が走らないので、テストは破棄の前に呼ぶ)。
        /// </summary>
        internal void UnbindAll()
        {
            if (_listeningForMatch)
            {
                NetTeamDeathmatch.CurrentChanged -= BindMatch;
                _listeningForMatch = false;
            }

            UnbindMatch();
            UnbindPlayer();
        }

        private void ListenForMatch()
        {
            if (!_listeningForMatch)
            {
                NetTeamDeathmatch.CurrentChanged += BindMatch;
                _listeningForMatch = true;
            }

            BindMatch(NetTeamDeathmatch.Current);
        }

        // 破棄済みの NetworkBehaviour でも NetworkVariable は C# のオブジェクトとして残っているので、ReferenceEquals で外す
        private void UnbindPlayer()
        {
            if (!ReferenceEquals(_player, null))
            {
                _player.TeamVariable.OnValueChanged -= HandleTeamChanged;
                _player.HealthVariable.OnValueChanged -= HandleHealthChanged;
                _player.DownedStateVariable.OnValueChanged -= HandleDownedStateChanged;
                _player = null;
            }

            if (!ReferenceEquals(_weapon, null))
            {
                _weapon.StateVariable.OnValueChanged -= HandleWeaponChanged;
                _weapon = null;
            }
        }

        private void UnbindMatch()
        {
            if (ReferenceEquals(_match, null))
            {
                return;
            }

            _match.AlphaScoreVariable.OnValueChanged -= HandleAlphaScoreChanged;
            _match.BravoScoreVariable.OnValueChanged -= HandleBravoScoreChanged;
            _match.RemainingSecondsVariable.OnValueChanged -= HandleRemainingSecondsChanged;
            _match.NextMatchSecondsVariable.OnValueChanged -= HandleNextMatchSecondsChanged;
            _match.NextWaveSecondsVariable.OnValueChanged -= HandleNextWaveSecondsChanged;
            _match.StatusVariable.OnValueChanged -= HandleStatusChanged;
            _match.RulesVariable.OnValueChanged -= HandleRulesChanged;
            _match = null;
        }

        private void HandleTeamChanged(Team previous, Team current)
        {
            _team = current;
            ShowScores();
        }

        private void HandleHealthChanged(int previous, int current)
        {
            _health = current;
            ShowHealth();

            if (current > 0)
            {
                CancelRespawn();
            }
            else if (previous > 0)
            {
                StartRespawn();
            }

            ShowNotice();
        }

        private void HandleDownedStateChanged(NetDownedState previous, NetDownedState current)
        {
            _downedState = current;
            ShowNotice();
        }

        private void HandleWeaponChanged(NetWeaponState previous, NetWeaponState current)
        {
            _weaponState = current;
            ShowAmmo();

            // 次のフレームを待たずに、リロードが終わった・判定した時点で進みと幅を変えるため
            RefreshReloadProgressNow();
        }

        private void HandleAlphaScoreChanged(int previous, int current)
        {
            _alphaScore = current;
            ShowScores();
        }

        private void HandleBravoScoreChanged(int previous, int current)
        {
            _bravoScore = current;
            ShowScores();
        }

        private void HandleRemainingSecondsChanged(int previous, int current)
        {
            Model.SetTime(current);
        }

        private void HandleNextMatchSecondsChanged(int previous, int current)
        {
            _nextMatchSeconds = current;
            ShowNotice();
        }

        private void HandleNextWaveSecondsChanged(int previous, int current)
        {
            _nextWaveSeconds = current;
            ShowNotice();
        }

        private void HandleRulesChanged(NetMatchRules previous, NetMatchRules current)
        {
            _rules = current;
            ShowAmmo();
            ShowNotice();
        }

        private void HandleStatusChanged(NetMatchStatus previous, NetMatchStatus current)
        {
            _status = current;
            if (current.State == GameModeState.Ended)
            {
                // 終わっている間はリスポーンしない (次の試合の開始で全員が出現する)
                CancelRespawn();
            }

            ShowResult();
            ShowNotice();
        }

        private void StartRespawn()
        {
            if (_match == null || _status.State != GameModeState.Playing)
            {
                return;
            }

            _isDown = true;

            // ルールが後から届いて Individual になっても数えられるよう、方式によらず数え始める
            var delay = _match.RespawnSeconds;
            if (!(delay >= 0f) || float.IsInfinity(delay))
            {
                Debug.LogWarning("[Net] the match has an invalid respawn delay " + delay + "; the HUD shows no respawn countdown", this);
                return;
            }

            _respawn = new RespawnCountdown(delay);
            _respawn.Start();
        }

        private void CancelRespawn()
        {
            _isDown = false;
            _respawn = null;
        }

        private void ShowHealth()
        {
            var current = Math.Max(0, _health);

            // 試合がまだ届いていなければ最大体力が分からないので、今の値を最大として警告の色にしない
            var max = _match != null ? Math.Max(1, _match.MaxHealth) : Math.Max(1, current);
            Model.SetHealth(current, max);
        }

        // 無限予備弾は武器の状態ではなく試合のルールで届くので、どちらが後に届いても同じ表示になるよう両方の控えから出す
        private void ShowAmmo()
        {
            if (_weapon == null)
            {
                Model.ClearAmmo();
                return;
            }

            Model.SetAmmo(
                Math.Max(0, _weaponState.AmmoInMagazine),
                Math.Max(0, _weaponState.ReserveAmmo),
                _weaponState.IsReloading,
                _rules.InfiniteReserve);
        }

        private void ShowScores()
        {
            if (_match == null)
            {
                Model.ClearScores();
                return;
            }

            Model.SetScores(_team, Math.Max(0, _alphaScore), Math.Max(0, _bravoScore));
        }

        private void ShowResult()
        {
            if (_match != null && _status.State == GameModeState.Ended)
            {
                Model.SetResult(_status.Winner);
            }
            else
            {
                Model.ClearResult();
            }
        }

        /// <summary>
        /// 終わっていれば次の試合までの秒数を、ダウン中なら出血で倒れるまでの秒数と起こされている進みを、
        /// 倒れていればリスポーン方式に合わせた案内 (Individual は自分のリスポーンまで、Wave は次のウェーブまでの秒数、
        /// None は次の試合まで復活なし) を、味方を起こしていればその進みを出す。どれでもなければ消す。
        /// </summary>
        private void ShowNotice()
        {
            if (_match != null && _status.State == GameModeState.Ended)
            {
                Model.SetNotice(HudFormat.NextMatch(Math.Max(0, _nextMatchSeconds)));
                return;
            }

            if (_downedState.IsDowned)
            {
                Model.SetNotice(HudFormat.Downed(
                    Math.Max(0, _downedState.BleedoutSeconds), Math.Min(100, (int)_downedState.RevivePercent)));
                return;
            }

            if (!_isDown)
            {
                Model.SetNotice(_revivingPercent.HasValue ? HudFormat.Reviving(_revivingPercent.Value) : HudFormat.NoNotice);
                return;
            }

            switch (_rules.RespawnMode)
            {
                case RespawnMode.Individual:
                    if (_respawn != null)
                    {
                        Model.SetRespawnNotice(RespawnMode.Individual, HudFormat.DisplayedSeconds(_respawn.RemainingSeconds));
                    }
                    else
                    {
                        Model.SetNotice(HudFormat.NoNotice);
                    }

                    break;
                case RespawnMode.Wave:
                    Model.SetRespawnNotice(RespawnMode.Wave, Math.Max(0, _nextWaveSeconds));
                    break;
                case RespawnMode.None:
                    Model.SetRespawnNotice(RespawnMode.None, 0);
                    break;
                default:
                    // 古いサーバーなど知らない方式が届いても HUD を落とさない
                    Model.SetNotice(HudFormat.NoNotice);
                    break;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
