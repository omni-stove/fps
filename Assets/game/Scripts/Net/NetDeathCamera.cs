using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// 倒されてからリスポーンするまでのカメラ。試合のルール (<see cref="NetTeamDeathmatch.SyncedRules"/>) の <see cref="DeathCamera"/> が
    /// None 以外のとき、自分のプレイヤー (<see cref="NetPlayer.HasLocalView"/>) が倒れてからリスポーンするまで、ルールに従ってカメラを動かす。
    /// <list type="bullet">
    /// <item><see cref="DeathCamera.Killer"/>: カメラを倒れた位置の少し上へ置き、倒した相手 (<see cref="NetPlayerStats.Killer"/>) の方を向ける。
    /// 相手がいない (自滅・相手が抜けた) ときは倒れた位置を見下ろす。置き場所と向きは <see cref="DeathCamFraming"/>。</item>
    /// <item><see cref="DeathCamera.Ally"/>: 生きている味方の肩越し (三人称のカメラと同じオフセット) に置き、味方の体の向きを向く。
    /// 味方の位置と向きは他のクライアントに同期されている値 (<see cref="NetworkPlayerMotor"/> が補間した本体) を読む。
    /// どの味方を映すかは <see cref="SpectateTargets"/> で決め、Next / Previous で次・前の味方へ移る。
    /// 映せる味方がいなければ (<see cref="SpectateTargets.ResolveMode"/>) Free と同じにし、味方が現れたらその視点へ移る。</item>
    /// <item><see cref="DeathCamera.Free"/>: 倒れた位置の上から、Move で向いている方へ飛び、Look で向きを変える。
    /// 壁の中には入らない (<see cref="ViewRig.ResolveFreeMove"/>)。</item>
    /// <item><see cref="DeathCamera.KillCam"/>: 倒した相手の倒す直前を、このクライアントがその相手の補間に使っている履歴
    /// (<see cref="NetworkPlayerMotor"/> の <see cref="HitboxHistory"/>) から切り出して肩越しで再生 (<see cref="KillCamReplay"/>) し、終えたら Killer と同じにする。
    /// 履歴が無い (相手を自分で動かしている・サーバーの本体・相手が見つからない) ならすぐ Killer にする。</item>
    /// </list>
    /// カメラの置き方と入力はオフラインの <see cref="PlayerDeathCamera"/> と同じ <see cref="DeathCamView"/> に任せる。
    /// 映している間にルールが別の演出に変わったら、次のフレームからそちらで映す。
    /// <para>
    /// 映している間は <see cref="ViewRig"/> を三人称にして止め (カメラはこちらが置く)、視点操作 (<see cref="FirstPersonLook.InputSuspended"/>) と
    /// 覗き込み (<see cref="AimDownSights"/>) と射撃・蘇生 (<see cref="NetPlayer"/> が <see cref="IsActive"/> を見る) を止める。
    /// リスポーンしたらカメラの位置・ViewRig の視点・入力を元に戻す。試合が終わったときは試合の終わりの見せ方
    /// (<see cref="NetSpectatorCamera"/> など) を優先してすぐ戻す。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetPlayer))]
    public sealed class NetDeathCamera : MonoBehaviour
    {
        public const string LogTag = DeathCamView.LogTag;

        [SerializeField, Min(0f), Tooltip("倒れた目からカメラを持ち上げる高さ (m)")]
        private float _height = DeathCamFraming.DefaultHeight;

        [SerializeField, Min(0f), Tooltip("倒れた目から、相手と反対側へカメラを下げる水平の距離 (m)")]
        private float _backDistance = DeathCamFraming.DefaultBackDistance;

        [SerializeField, Min(0f), Tooltip("自由カメラ (Free) を倒し切ったときの速さ (m/s)")]
        private float _freeSpeed = DeathCamFraming.DefaultFreeSpeed;

        [SerializeField, Min(_minKillCamSeconds), Tooltip("キルカム (KillCam) で再生する、倒される直前の長さ (秒)。相手の履歴より長い分は再生されない")]
        private float _killCamSeconds = KillCamBuffer.DefaultSeconds;

        private const float _minKillCamSeconds = 0.1f;

        private readonly DeathCamSwitch _switch = new DeathCamSwitch();

        private NetPlayer _player;
        private DeathCamView _view;
        private KillCamReplay _replay;
        private bool _resolved;

        // この倒れ方で再生を始めた (か、記録が無くて飛ばした) か。倒した相手の行が後から変わったら再生をやめるために相手も覚える
        private bool _killCamResolved;
        private ulong? _killCamKillerId;

        // 映す相手。Id が変わったときだけ探し直す (見つからない Id を毎フレーム探さない)
        private ulong? _killerId;
        private NetPlayer _killer;
        private bool _hasKiller;
        private bool _targetResolved;

        // Ally で映している味方。_allyResolved は最初の選択をログに出すため
        private readonly List<SpectateCandidate> _candidates = new List<SpectateCandidate>();
        private ulong? _allyId;
        private NetPlayer _ally;
        private bool _allyResolved;

        /// <summary>いま倒されてからのカメラを映しているか。</summary>
        public bool IsActive => _switch.IsActive;

        /// <summary>映している相手のクライアント Id。映していないか、相手がいなければ null。</summary>
        public ulong? KillerClientId => IsActive && _hasKiller ? _killerId : null;

        /// <summary>肩越しに映している味方のクライアント Id。映していないか、味方を映していなければ null。</summary>
        public ulong? AllyClientId => IsActive && !IsFreeCamera && _ally != null ? _allyId : null;

        /// <summary>いま自由カメラ (Free、または Ally で映せる味方がいないとき) で動かしているか。</summary>
        public bool IsFreeCamera => IsActive && _view != null && _view.IsFreeCamera;

        public float Height
        {
            get => _height;
            set => _height = Mathf.Max(0f, value);
        }

        public float BackDistance
        {
            get => _backDistance;
            set => _backDistance = Mathf.Max(0f, value);
        }

        public float FreeSpeed
        {
            get => _freeSpeed;
            set => _freeSpeed = Mathf.Max(0f, value);
        }

        /// <summary>キルカム (<see cref="DeathCamera.KillCam"/>) で再生する、倒される直前の長さ (秒)。</summary>
        public float KillCamSeconds
        {
            get => _killCamSeconds;
            set => _killCamSeconds = float.IsNaN(value) || float.IsInfinity(value) ? KillCamBuffer.DefaultSeconds : Mathf.Max(_minKillCamSeconds, value);
        }

        /// <summary>いまキルカムで倒した相手の記録を再生しているか。</summary>
        public bool IsReplayingKillCam => IsActive && _replay != null && _replay.IsPlaying;

        private void Awake()
        {
            EnsureResolved();
        }

        private void LateUpdate()
        {
            EnsureResolved();
            var match = NetTeamDeathmatch.Current;
            var mode = match != null ? match.SyncedRules.DeathCamera : DeathCamera.None;
            var matchEnded = match != null && match.State == GameModeState.Ended;
            Step(mode, _player.IsAlive, matchEnded);

            if (!IsActive)
            {
                return;
            }

            if (mode != DeathCamera.KillCam && _replay.IsPlaying)
            {
                _replay.Stop("rule changed to " + mode);
            }

            switch (mode)
            {
                case DeathCamera.Killer:
                    Frame(ResolveKillerEye(match));
                    break;
                case DeathCamera.KillCam:
                    if (!WatchKillCam(match, Time.deltaTime))
                    {
                        Frame(ResolveKillerEye(match));
                    }

                    break;
                case DeathCamera.Ally:
                    WatchAlly(_view.ReadSpectateStep());
                    break;
                default:
                    MoveFree(_view.ReadMove(), _view.ReadLookDegrees(Time.deltaTime), Time.deltaTime);
                    break;
            }
        }

        private void OnDisable()
        {
            // プレイヤーが消えるときも、カメラと入力を戻して次に有効になったときに持ち越さない
            if (_switch.Reset())
            {
                Restore("disabled");
                Debug.Log(LogTag + " stopped on " + name + " (disabled)", this);
            }
        }

        /// <summary>
        /// 今の状態を渡して、カメラを始めるか止める。始めたらその場でカメラを倒れた位置の上へ置き (相手はまだ分からないので見下ろす。
        /// Free はここから飛び始める)、止めたら元に戻す。視点の有無は <see cref="NetPlayer.HasLocalView"/> を見る。
        /// </summary>
        internal DeathCamTransition Step(DeathCamera mode, bool isAlive, bool matchEnded)
        {
            EnsureResolved();
            var transition = _switch.Update(mode, _player.HasLocalView, isAlive, matchEnded);
            switch (transition)
            {
                case DeathCamTransition.None:
                    break;
                case DeathCamTransition.Started:
                    ClearTargets();
                    _view.Begin(_player.AimOrigin(), _player.transform.eulerAngles.y);
                    Debug.Log(LogTag + " started on " + name + " (" + mode + ") at " + DeathCamView.FormatPosition(_view.DeathEye), this);
                    Frame(null);
                    break;
                default:
                    Restore(transition.ToString());
                    Debug.Log(LogTag + " stopped on " + name + " (" + transition + ")", this);
                    break;
            }

            return transition;
        }

        /// <summary>
        /// 映している間、カメラを倒れた目から <see cref="DeathCamFraming.BoomOffset"/> の先 (壁があれば手前) へ置き、
        /// killerEye (null なら倒れた目) を向ける。映していなければ何もしない。
        /// </summary>
        internal void Frame(Vector3? killerEye)
        {
            if (IsActive)
            {
                _view.Frame(killerEye, _height, _backDistance);
            }
        }

        /// <summary>
        /// 映している間、カメラを味方の目から肩越し (<see cref="DeathCamFraming.ShoulderPosition"/>、壁があれば手前) へ置き、
        /// 味方の体の向き (allyYaw、度) を水平に向ける。allyRoot は味方の本体で、カメラを縮める判定でその体を除く。映していなければ何もしない。
        /// </summary>
        internal void FrameAlly(Vector3 allyEye, float allyYaw, Transform allyRoot)
        {
            if (IsActive)
            {
                _view.FrameAlly(allyEye, allyYaw, allyRoot);
            }
        }

        /// <summary>
        /// 映している間、自由カメラを 1 フレーム動かす。直前まで自由カメラでなければ、今のカメラの位置と向きから始める。
        /// lookDegrees (x は正で右、y は正で上) だけ回してから、move (Move 入力) で向いている方へ <see cref="FreeSpeed"/> で進め、
        /// 壁の手前で止める (<see cref="ViewRig.ResolveFreeMove"/>)。映していなければ何もしない。
        /// </summary>
        internal void MoveFree(Vector2 move, Vector2 lookDegrees, float deltaSeconds)
        {
            if (IsActive)
            {
                _view.MoveFree(move, lookDegrees, _freeSpeed, deltaSeconds);
            }
        }

        // 同期されている各プレイヤーのチームと生死から映す味方を選び、その肩越しへ置く。映せる味方がいなければ自由カメラにする
        private void WatchAlly(int step)
        {
            _candidates.Clear();
            var players = NetPlayer.SpawnedPlayers;
            for (var i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate != null && candidate.IsSpawned)
                {
                    // Bot はみなサーバーが持ち主なので、持ち主のクライアント Id では 1 人に決まらない
                    _candidates.Add(new SpectateCandidate(candidate.RosterId, candidate.Team, candidate.IsAlive));
                }
            }

            var allyId = SpectateTargets.Pick(_candidates, _player.RosterId, _player.Team, _allyId, step);
            if (!_allyResolved || allyId != _allyId || (allyId.HasValue && _ally == null))
            {
                _allyResolved = true;
                _allyId = allyId;
                _ally = allyId.HasValue ? FindSpawnedPlayer(allyId.Value) : null;
                if (_ally != null)
                {
                    Debug.Log(LogTag + " watching ally client " + allyId.Value.ToString(CultureInfo.InvariantCulture) + " from " + name, this);
                }
                else
                {
                    Debug.Log(LogTag + " no ally to watch for " + name + "; using the free camera", this);
                }
            }

            if (SpectateTargets.ResolveMode(DeathCamera.Ally, _ally != null) == DeathCamera.Free)
            {
                MoveFree(_view.ReadMove(), _view.ReadLookDegrees(Time.deltaTime), Time.deltaTime);
                return;
            }

            FrameAlly(_ally.AimOrigin(), _ally.transform.eulerAngles.y, _ally.transform);
        }

        private NetPlayer FindSpawnedPlayer(ulong clientId)
        {
            var players = NetPlayer.SpawnedPlayers;
            for (var i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate != null && candidate != _player && candidate.IsSpawned && candidate.RosterId == clientId)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// キルカムの再生を 1 フレーム進める。この倒れ方で初めて呼ばれたら、倒した相手を試合の行から引いて記録を切り出し、再生を始める
        /// (相手がいない・記録が無ければ始めない)。再生中に試合の行の相手が変わったら (行が倒れた後に届いたとき) 再生をやめる。
        /// まだ再生中なら true、呼び出し側は false の間 Killer と同じに映す。
        /// </summary>
        internal bool WatchKillCam(NetTeamDeathmatch match, float deltaSeconds)
        {
            if (!IsActive)
            {
                return false;
            }

            var killerId = FindKillerId(match, _player.RosterId);
            if (!_killCamResolved)
            {
                _killCamResolved = true;
                _killCamKillerId = killerId;
                BeginKillCam(killerId);
            }
            else if (_replay.IsPlaying && killerId != _killCamKillerId)
            {
                _replay.Stop("killer changed");
            }

            return _replay.Advance(_view, deltaSeconds);
        }

        private void BeginKillCam(ulong? killerId)
        {
            if (!killerId.HasValue)
            {
                Debug.Log(KillCamLog.NoKiller(name), this);
                return;
            }

            var killerName = "client " + killerId.Value.ToString(CultureInfo.InvariantCulture);
            var killer = FindSpawnedPlayer(killerId.Value);
            var motor = killer != null ? killer.GetComponent<NetworkPlayerMotor>() : null;
            var clip = motor != null
                ? motor.CaptureKillCam(_killCamSeconds, killer.AimOrigin() - killer.transform.position)
                : KillCamClip.Empty;
            var caption = KillCamText.Caption(BotRoster.ScoreboardName((int)killerId.Value));
            _replay.Begin(clip, killerName, caption, killer != null ? killer.transform : null, _view);
        }

        private void Restore(string reason)
        {
            _replay.Stop(reason);

            // 映している間に視点が無くなっていたら (プレイヤーが消えるときなど)、NetPlayer が止めた部品を有効に戻さない
            _view.Restore(_player != null && _player.HasLocalView);
            ClearTargets();
        }

        private void ClearTargets()
        {
            _killCamResolved = false;
            _killCamKillerId = null;
            _killerId = null;
            _killer = null;
            _hasKiller = false;
            _targetResolved = false;
            _allyId = null;
            _ally = null;
            _allyResolved = false;
        }

        // 試合の行から自分を倒した相手を引き、その目の位置を返す。相手がいない・見つからない・抜けたなら null (倒れた位置を見下ろす)
        private Vector3? ResolveKillerEye(NetTeamDeathmatch match)
        {
            var killerId = FindKillerId(match, _player.RosterId);
            if (!_targetResolved || killerId != _killerId)
            {
                _targetResolved = true;
                _killerId = killerId;
                _killer = killerId.HasValue ? FindSpawnedPlayer(killerId.Value) : null;
                _hasKiller = _killer != null;
                if (_hasKiller)
                {
                    Debug.Log(LogTag + " watching killer client " + killerId.Value.ToString(CultureInfo.InvariantCulture) + " from " + name, this);
                }
                else if (killerId.HasValue)
                {
                    Debug.Log(LogTag + " killer client " + killerId.Value.ToString(CultureInfo.InvariantCulture)
                        + " is gone; looking down at " + name, this);
                }
                else
                {
                    Debug.Log(LogTag + " no killer for " + name + "; looking down at the death spot", this);
                }
            }
            else if (_hasKiller && (_killer == null || !_killer.IsSpawned))
            {
                _hasKiller = false;
                _killer = null;
                Debug.Log(LogTag + " killer client " + (_killerId ?? 0).ToString(CultureInfo.InvariantCulture)
                    + " left; looking down at " + name, this);
            }

            return _hasKiller ? _killer.AimOrigin() : (Vector3?)null;
        }

        /// <summary>
        /// match の <see cref="NetTeamDeathmatch.Players"/> から、victim を最後に倒した相手のクライアント Id を引く。
        /// 試合が無い・行が無い・倒した相手がいない・自分自身なら null。
        /// </summary>
        internal static ulong? FindKillerId(NetTeamDeathmatch match, ulong victim)
        {
            if (match == null || !match.IsSpawned)
            {
                return null;
            }

            var players = match.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var row = players[i];
                if (row.ClientId == victim)
                {
                    var killer = row.Killer;
                    return killer.HasValue && killer.Value != victim ? killer : null;
                }
            }

            return null;
        }

        // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で解決する
        private void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _player = GetComponent<NetPlayer>();
            _view = new DeathCamView(this, "run Fps.Editor.NetSceneBuilder.Build");
            _replay = new KillCamReplay(this);
        }
    }
}
