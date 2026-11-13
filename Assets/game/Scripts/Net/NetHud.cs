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
    /// 倒れている間の「リスポーンまで N 秒」だけは時間で進むので、倒れた値が届いたときに <see cref="RespawnCountdown"/> を
    /// 始め、<see cref="Tick"/> で数える。サーバーは待ち時間を配らないので、同じプレハブの <see cref="NetTeamDeathmatch.RespawnSeconds"/>
    /// から数える。表示は秒の桁が変わったときだけ変わる。
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
        private NetMatchStatus _status = new NetMatchStatus(GameModeState.Waiting, null);
        private RespawnCountdown _respawn;

        /// <summary>流し込み先の HUD。</summary>
        public Hud Hud => _hud;

        /// <summary>購読している自分のプレイヤー。</summary>
        public NetPlayer Player => _player;

        /// <summary>購読している試合。まだ無ければ null。</summary>
        public NetTeamDeathmatch Match => _match;

        /// <summary>倒れてからリスポーンを待っている間 true。</summary>
        public bool IsAwaitingRespawn => _respawn != null;

        /// <summary>
        /// HUD (<see cref="Hud.Create"/>) を作り、player とその武器、今の試合 (<see cref="NetTeamDeathmatch.Current"/>) を購読する。
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

            // NetPlayer.Weapon は Awake で解決するので、Awake を経ない Edit Mode でも見つかるよう自分で探す
            _weapon = player.GetComponent<NetPlayerWeapon>();
            if (_weapon != null)
            {
                _weapon.StateVariable.OnValueChanged += HandleWeaponChanged;
                ShowAmmo(_weapon.StateVariable.Value);
            }
            else
            {
                Model.ClearAmmo();
            }

            ShowHealth();
            ShowScores();
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
                _status = new NetMatchStatus(GameModeState.Waiting, null);
                Model.ClearTime();
                Model.ClearScores();
                Model.ClearResult();
                ShowHealth();
                ShowNotice();
                return;
            }

            _alphaScore = _match.AlphaScoreVariable.Value;
            _bravoScore = _match.BravoScoreVariable.Value;
            _nextMatchSeconds = _match.NextMatchSecondsVariable.Value;
            _status = _match.StatusVariable.Value;
            _match.AlphaScoreVariable.OnValueChanged += HandleAlphaScoreChanged;
            _match.BravoScoreVariable.OnValueChanged += HandleBravoScoreChanged;
            _match.RemainingSecondsVariable.OnValueChanged += HandleRemainingSecondsChanged;
            _match.NextMatchSecondsVariable.OnValueChanged += HandleNextMatchSecondsChanged;
            _match.StatusVariable.OnValueChanged += HandleStatusChanged;

            Model.SetTime(_match.RemainingSecondsVariable.Value);
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
            _match.StatusVariable.OnValueChanged -= HandleStatusChanged;
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

        private void HandleWeaponChanged(NetWeaponState previous, NetWeaponState current)
        {
            ShowAmmo(current);
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
            _respawn = null;
        }

        private void ShowHealth()
        {
            var current = Math.Max(0, _health);

            // 試合がまだ届いていなければ最大体力が分からないので、今の値を最大として警告の色にしない
            var max = _match != null ? Math.Max(1, _match.MaxHealth) : Math.Max(1, current);
            Model.SetHealth(current, max);
        }

        private void ShowAmmo(NetWeaponState state)
        {
            Model.SetAmmo(Math.Max(0, state.AmmoInMagazine), Math.Max(0, state.ReserveAmmo), state.IsReloading);
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

        /// <summary>終わっていれば次の試合まで、倒れていればリスポーンまでの秒数を案内に出す。どちらでもなければ消す。</summary>
        private void ShowNotice()
        {
            if (_match != null && _status.State == GameModeState.Ended)
            {
                Model.SetNotice(HudFormat.NextMatch(Math.Max(0, _nextMatchSeconds)));
            }
            else if (_respawn != null)
            {
                Model.SetNotice(HudFormat.Respawn(HudFormat.DisplayedSeconds(_respawn.RemainingSeconds)));
            }
            else
            {
                Model.SetNotice(HudFormat.NoNotice);
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
