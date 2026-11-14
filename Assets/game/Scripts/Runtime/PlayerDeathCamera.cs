using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// オフラインのプレイヤー (<see cref="GameBootstrap"/> が作る) が倒されてから戻るまで (モードのリスポーン・キャンペーンの読み直し) のカメラ。
    /// <see cref="Mode"/> (ルールの <see cref="GameRules.DeathCamera"/>) が None 以外のとき、倒れてから体力が戻るまでルールに従ってカメラを動かす。
    /// <list type="bullet">
    /// <item><see cref="DeathCamera.Killer"/>: 倒れた位置の少し上から、倒した相手 (倒れたときの <see cref="CombatEvents.CurrentAttribution"/>) の方を向く。
    /// 相手がいない (自滅・相手が消えた) ときは倒れた位置を見下ろす。</item>
    /// <item><see cref="DeathCamera.Ally"/>: モード (<see cref="ModeRunnerBase.Members"/>) で同じ陣営の、生きている (ダウン中も含む) 参加者の肩越しに置く。
    /// どの味方を映すかは <see cref="SpectateTargets"/> で決め、Next / Previous で次・前の味方へ移る。
    /// モードが無い (キャンペーン) か、映せる味方がいなければ (<see cref="SpectateTargets.ResolveMode"/>) Free と同じにする。</item>
    /// <item><see cref="DeathCamera.Free"/>: 倒れた位置の上から、Move で向いている方へ飛び、Look で向きを変える。</item>
    /// </list>
    /// カメラの置き方と入力はネット対戦の NetDeathCamera と同じ <see cref="DeathCamView"/> に任せる。
    /// モードの試合が終わったら (<see cref="ModeRunnerBase.IsMatchOver"/>、<see cref="MatchRunner"/> の試合の終わり) 試合の終わりの見せ方に譲ってすぐ戻す。
    /// 武器と移動を止めるのはモード (<see cref="SideMember.SetActing"/>) とキャンペーンの役目で、ここは視点と覗き込みだけを止める。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed class PlayerDeathCamera : MonoBehaviour
    {
        public const string LogTag = DeathCamView.LogTag;

        [SerializeField, Tooltip("倒れてから戻るまでの演出。GameBootstrap とキャンペーンがルールの DeathCamera を入れる")]
        private DeathCamera _mode = DeathCamera.None;

        [SerializeField, Min(0f), Tooltip("倒れた目からカメラを持ち上げる高さ (m)")]
        private float _height = DeathCamFraming.DefaultHeight;

        [SerializeField, Min(0f), Tooltip("倒れた目から、相手と反対側へカメラを下げる水平の距離 (m)")]
        private float _backDistance = DeathCamFraming.DefaultBackDistance;

        [SerializeField, Min(0f), Tooltip("自由カメラ (Free) を倒し切ったときの速さ (m/s)")]
        private float _freeSpeed = DeathCamFraming.DefaultFreeSpeed;

        private readonly DeathCamSwitch _switch = new DeathCamSwitch();
        private readonly List<SpectateCandidate> _candidates = new List<SpectateCandidate>();

        private Damageable _damageable;
        private DeathCamView _view;
        private bool _resolved;

        // 試合の終わりと味方を読む相手。倒れたときに探す (Bootstrap がプレイヤーを作る時点ではモードがまだ始まっていないことがある)
        private ModeRunnerBase _modeRunner;
        private MatchRunner _matchRunner;

        // 倒れたときに記録した相手。消えたら null として扱う
        private GameObject _killer;
        private bool _killerResolved;

        // Ally で映している味方。_allyResolved は最初の選択をログに出すため
        private ulong? _allyId;
        private SideMember _ally;
        private bool _allyResolved;

        /// <summary>倒れてから戻るまでの演出。None なら自分の視点のまま。</summary>
        public DeathCamera Mode
        {
            get => _mode;
            set => _mode = value;
        }

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

        /// <summary>試合の終わりと味方を読むモード。未設定なら倒れたときにシーンから探す。</summary>
        public ModeRunnerBase ModeRunner
        {
            get => _modeRunner;
            set => _modeRunner = value;
        }

        /// <summary>いま倒されてからのカメラを映しているか。</summary>
        public bool IsActive => _switch.IsActive;

        /// <summary>いま自由カメラ (Free、または Ally で映せる味方がいないとき) で動かしているか。</summary>
        public bool IsFreeCamera => IsActive && _view != null && _view.IsFreeCamera;

        /// <summary>映している間、倒した相手。映していないか、相手がいない・消えたなら null。</summary>
        public GameObject Killer => IsActive && _killer != null ? _killer : null;

        /// <summary>肩越しに映している味方。映していないか、味方を映していなければ null。</summary>
        public SideMember Ally => IsActive && !IsFreeCamera && _ally != null ? _ally : null;

        /// <summary>rules の <see cref="GameRules.DeathCamera"/> を <see cref="Mode"/> にする。rules が null なら None。</summary>
        public void ApplyRules(GameRules rules)
        {
            _mode = rules != null ? rules.DeathCamera : DeathCamera.None;
        }

        private void Awake()
        {
            EnsureResolved();
        }

        private void LateUpdate()
        {
            EnsureResolved();
            Step(_mode, IsSelfAlive(), IsMatchEnded());
            if (IsActive)
            {
                Advance(_view.ReadSpectateStep(), _view.ReadMove(), _view.ReadLookDegrees(Time.deltaTime), Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            // シーンの読み直しやプレイヤーが消えるときも、カメラと入力を戻して次に有効になったときに持ち越さない
            if (_switch.Reset())
            {
                Restore();
                Debug.Log(LogTag + " stopped on " + name + " (disabled)", this);
            }
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }
        }

        /// <summary>
        /// 今の状態を渡して、カメラを始めるか止める。始めたらその場でカメラを倒れた位置の上へ置いて見下ろし (Free はここから飛び始める)、
        /// 止めたら元に戻す。オフラインのプレイヤーはいつもこの画面の視点を持つ。Play 中は LateUpdate が自分の体力と試合の終わりを渡して毎フレーム呼ぶ。
        /// </summary>
        public DeathCamTransition Step(DeathCamera mode, bool isAlive, bool matchEnded)
        {
            EnsureResolved();
            var transition = _switch.Update(mode, true, isAlive, matchEnded);
            switch (transition)
            {
                case DeathCamTransition.None:
                    break;
                case DeathCamTransition.Started:
                    ClearAlly();
                    _view.Begin(EyeOf(gameObject), transform.eulerAngles.y);
                    Debug.Log(LogTag + " started on " + name + " (" + mode + ") at " + DeathCamView.FormatPosition(_view.DeathEye), this);
                    Frame(null);
                    break;
                default:
                    Restore();
                    Debug.Log(LogTag + " stopped on " + name + " (" + transition + ")", this);
                    break;
            }

            return transition;
        }

        /// <summary>
        /// 映している間、今の <see cref="Mode"/> でカメラを 1 フレーム動かす。spectateStep は Next で +1、Previous で -1 (Ally で映す味方を替える)、
        /// move と lookDegrees は自由カメラの移動と回転 (度)。映していなければ何もしない。Play 中は LateUpdate がこのフレームの入力を渡して呼ぶ。
        /// </summary>
        public void Advance(int spectateStep, Vector2 move, Vector2 lookDegrees, float deltaSeconds)
        {
            if (!IsActive)
            {
                return;
            }

            switch (_mode)
            {
                case DeathCamera.Killer:
                    Frame(ResolveKillerEye());
                    break;
                case DeathCamera.Ally:
                    WatchAlly(spectateStep, move, lookDegrees, deltaSeconds);
                    break;
                default:
                    _view.MoveFree(move, lookDegrees, _freeSpeed, deltaSeconds);
                    break;
            }
        }

        private void Frame(Vector3? killerEye)
        {
            _view.Frame(killerEye, _height, _backDistance);
        }

        // モードの参加者から映す味方を選び、その肩越しへ置く。映せる味方がいなければ自由カメラにする
        private void WatchAlly(int step, Vector2 move, Vector2 lookDegrees, float deltaSeconds)
        {
            var self = GetComponent<SideMember>();
            var viewer = (ulong)ModeRunnerBase.LocalPlayerId;
            _candidates.Clear();
            if (_modeRunner != null && self != null)
            {
                var selfId = _modeRunner.IdOf(self);
                viewer = selfId >= 0 ? (ulong)selfId : viewer;
                var members = _modeRunner.Members;
                for (var i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (member != null && member.gameObject.activeInHierarchy)
                    {
                        // SpectateTargets はチームで味方を見分けるので、自分と同じ陣営を Alpha、それ以外を Bravo として渡す
                        var team = member.Side == self.Side ? Team.Alpha : Team.Bravo;
                        _candidates.Add(new SpectateCandidate((ulong)i, team, IsStanding(member.Damageable)));
                    }
                }
            }

            var allyId = SpectateTargets.Pick(_candidates, viewer, Team.Alpha, _allyId, step);
            if (!_allyResolved || allyId != _allyId)
            {
                _allyResolved = true;
                _allyId = allyId;
                _ally = allyId.HasValue ? _modeRunner.MemberOf((int)allyId.Value) : null;
                if (_ally != null)
                {
                    Debug.Log(LogTag + " watching ally " + _ally.name + " (id "
                        + allyId.Value.ToString(CultureInfo.InvariantCulture) + ") from " + name, this);
                }
                else
                {
                    Debug.Log(LogTag + " no ally to watch for " + name + "; using the free camera", this);
                }
            }

            if (SpectateTargets.ResolveMode(DeathCamera.Ally, _ally != null) == DeathCamera.Free)
            {
                _view.MoveFree(move, lookDegrees, _freeSpeed, deltaSeconds);
                return;
            }

            _view.FrameAlly(EyeOf(_ally.gameObject), _ally.transform.eulerAngles.y, _ally.transform);
        }

        /// <summary>
        /// go の目の位置。<see cref="EnemyAgent"/> ならその目、<see cref="ViewRig"/> があればその目、体力があれば狙う点、どれも無ければ Transform の位置。
        /// </summary>
        public static Vector3 EyeOf(GameObject go)
        {
            var agent = go.GetComponent<EnemyAgent>();
            if (agent != null)
            {
                return agent.EyePosition;
            }

            var eye = ViewRig.FindEye(go.transform);
            if (eye != null)
            {
                return eye.position;
            }

            var damageable = go.GetComponent<Damageable>();
            return damageable != null ? damageable.AimPoint : go.transform.position;
        }

        private void OnDied(Damageable _)
        {
            // 倒した相手は Died を出している間しか引けないので、ここで覚えておく。自分で倒れたなら相手はいない
            var attribution = CombatEvents.CurrentAttribution;
            var killer = attribution.HasValue ? CombatEvents.OfflineObject(attribution.Value.KillerId) : null;
            _killer = killer != null && killer != gameObject && !killer.transform.IsChildOf(transform) ? killer : null;
            _killerResolved = false;

            if (_modeRunner == null)
            {
                _modeRunner = FindAnyObjectByType<ModeRunnerBase>();
            }

            if (_matchRunner == null)
            {
                _matchRunner = FindAnyObjectByType<MatchRunner>();
            }
        }

        private Vector3? ResolveKillerEye()
        {
            var present = _killer != null && _killer.activeInHierarchy;
            if (!_killerResolved)
            {
                _killerResolved = true;
                Debug.Log(present
                    ? LogTag + " watching killer " + _killer.name + " from " + name
                    : LogTag + " no killer for " + name + "; looking down at the death spot", this);
            }
            else if (_killer != null && !present)
            {
                Debug.Log(LogTag + " killer " + _killer.name + " left; looking down at " + name, this);
                _killer = null;
            }

            return present ? EyeOf(_killer) : (Vector3?)null;
        }

        private bool IsSelfAlive()
        {
            return IsStanding(_damageable);
        }

        // ダウン中は倒れきっていない (起こされれば戻る) ので、観戦を始めない
        private static bool IsStanding(Damageable damageable)
        {
            return damageable == null || !damageable.IsDead || damageable.IsDowned;
        }

        private bool IsMatchEnded()
        {
            if (_modeRunner != null && _modeRunner.IsMatchOver)
            {
                return true;
            }

            return _matchRunner != null && _matchRunner.Match != null && _matchRunner.Match.IsOver;
        }

        private void Restore()
        {
            _view.Restore(true);
            ClearAlly();
            _killer = null;
            _killerResolved = false;
        }

        private void ClearAlly()
        {
            _allyId = null;
            _ally = null;
            _allyResolved = false;
        }

        // Awake を経ない (Edit Mode のテストなど) ときも、使う時点で解決する
        private void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _damageable = GetComponent<Damageable>();
            if (_damageable != null)
            {
                _damageable.Died += OnDied;
            }

            _view = new DeathCamView(this, "build the player with GameBootstrap");
        }
    }
}
