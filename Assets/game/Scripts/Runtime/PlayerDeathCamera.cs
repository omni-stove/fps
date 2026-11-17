using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 倒れた主観 (DeathCamera.Fallen) では、床の近くのカメラに自分の脚や銃が映らないよう、自分の体も倒れた抜け殻も描かず影も落とさず、武器はしまわせる。
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

        [SerializeField, Min(_minKillCamSeconds), Tooltip("キルカム (KillCam) で再生する、倒される直前の長さ (秒)")]
        private float _killCamSeconds = KillCamBuffer.DefaultSeconds;

        [SerializeField, Min(0.01f), Tooltip("キルカム (KillCam) の再生の速さ (1 で記録どおり、0.5 で半分の速さ)")]
        private float _killCamSpeed = 1f;

        [SerializeField, Min(0f), Tooltip("倒れた主観 (Fallen) で倒れ切るまでの秒数")]
        private float _fallSeconds = FallenView.DefaultSeconds;

        [SerializeField, Min(0f), Tooltip("倒れた主観 (Fallen) で倒れた後の目の、足元からの高さ (m)")]
        private float _fallenEyeHeight = FallenView.DefaultEyeHeight;

        [SerializeField, Range(-FallenView.MaxDegrees, FallenView.MaxDegrees), Tooltip("倒れた主観 (Fallen) で倒れた後の横の傾き (度、正が左)")]
        private float _fallenRollDegrees = FallenView.DefaultRollDegrees;

        [SerializeField, Range(-FallenView.MaxDegrees, FallenView.MaxDegrees), Tooltip("倒れた主観 (Fallen) で倒れた後の上下の向き (度、正が下)")]
        private float _fallenPitchDegrees = FallenView.DefaultPitchDegrees;

        public const float FallenGroundProbeDistance = 50f;

        // 後から出てきた相手を拾うため、キルカムの姿勢をためる相手をこの間隔で場から探し直す。
        public const float KillCamScanSeconds = 1f;

        private const float _minKillCamSeconds = 0.1f;

        private readonly DeathCamSwitch _switch = new DeathCamSwitch();

        // キルカムのために姿勢をためている相手。倒れたときに倒した相手の分を切り出す
        private readonly List<KillCamSubject> _killCamSubjects = new List<KillCamSubject>();
        private double _lastKillCamRecordTime;
        private bool _hasKillCamRecord;
        private double _nextKillCamScanTime;
        private KillCamClip _killCamClip = KillCamClip.Empty;
        private KillCamReplay _replay;
        private bool _shotsSubscribed;
        private readonly List<SpectateCandidate> _candidates = new List<SpectateCandidate>();

        // Fallen で倒れ始めてからの秒数と、倒れた足元の高さ
        private float _fallenElapsed;
        private float _fallenGroundY;
        private bool _fallenSettled;
        private WeaponController _holsteredWeapon;
        private readonly List<Renderer> _hiddenOwnRenderers = new List<Renderer>();

        private Damageable _damageable;
        private DeathCamView _view;
        private bool _resolved;

        // 試合の終わりと味方を読む相手。倒れたときに探す (Bootstrap がプレイヤーを作る時点ではモードがまだ始まっていないことがある)
        private ModeRunnerBase _modeRunner;
        private MatchRunner _matchRunner;

        private GameObject _killer;
        private string _killerWeaponId = string.Empty;
        private bool _killerResolved;

        // Ally で映している味方。_allyResolved は最初の選択をログに出すため
        private ulong? _allyId;
        private SideMember _ally;
        private bool _allyResolved;

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

        public float FallSeconds
        {
            get => _fallSeconds;
            set => _fallSeconds = FiniteOr(Mathf.Max(0f, value), FallenView.DefaultSeconds);
        }

        public float FallenEyeHeight
        {
            get => _fallenEyeHeight;
            set => _fallenEyeHeight = FiniteOr(Mathf.Max(0f, value), FallenView.DefaultEyeHeight);
        }

        public float FallenRollDegrees
        {
            get => _fallenRollDegrees;
            set => _fallenRollDegrees = FiniteOr(Mathf.Clamp(value, -FallenView.MaxDegrees, FallenView.MaxDegrees), FallenView.DefaultRollDegrees);
        }

        public float FallenPitchDegrees
        {
            get => _fallenPitchDegrees;
            set => _fallenPitchDegrees = FiniteOr(Mathf.Clamp(value, -FallenView.MaxDegrees, FallenView.MaxDegrees), FallenView.DefaultPitchDegrees);
        }

        public float FallenGroundY => IsActive ? _fallenGroundY : 0f;

        public float FallenElapsed => IsActive ? _fallenElapsed : 0f;

        public FallenView FallenMotion => new FallenView(
            FiniteOr(Mathf.Max(0f, _fallSeconds), FallenView.DefaultSeconds),
            FiniteOr(Mathf.Max(0f, _fallenEyeHeight), FallenView.DefaultEyeHeight),
            FiniteOr(Mathf.Clamp(_fallenRollDegrees, -FallenView.MaxDegrees, FallenView.MaxDegrees), FallenView.DefaultRollDegrees),
            FiniteOr(Mathf.Clamp(_fallenPitchDegrees, -FallenView.MaxDegrees, FallenView.MaxDegrees), FallenView.DefaultPitchDegrees));

        // 変えるとためておく長さが変わるので、それまでにためた姿勢は捨てる。
        public float KillCamSeconds
        {
            get => _killCamSeconds;
            set
            {
                var seconds = float.IsNaN(value) || float.IsInfinity(value) ? KillCamBuffer.DefaultSeconds : Mathf.Max(_minKillCamSeconds, value);
                if (seconds != _killCamSeconds)
                {
                    _killCamSeconds = seconds;
                    ClearKillCamSubjects();
                }
            }
        }

        public float KillCamSpeed
        {
            get => _killCamSpeed;
            set => _killCamSpeed = value;
        }

        public bool IsReplayingKillCam => IsActive && _replay != null && _replay.IsPlaying;

        public int KillCamSubjectCount => _killCamSubjects.Count;

        public ModeRunnerBase ModeRunner
        {
            get => _modeRunner;
            set => _modeRunner = value;
        }

        public bool IsActive => _switch.IsActive;

        public bool IsFreeCamera => IsActive && _view != null && _view.IsFreeCamera;

        public GameObject Killer => IsActive && _killer != null ? _killer : null;

        public SideMember Ally => IsActive && !IsFreeCamera && _ally != null ? _ally : null;

        public void ApplyRules(GameRules rules)
        {
            _mode = rules != null ? rules.DeathCamera : DeathCamera.None;
        }

        private void Awake()
        {
            EnsureResolved();
        }

        private void OnEnable()
        {
            if (!_shotsSubscribed)
            {
                CombatEvents.ShotFired += OnShotFired;
                _shotsSubscribed = true;
            }
        }

        private void LateUpdate()
        {
            EnsureResolved();
            var alive = IsSelfAlive();
            if (_mode == DeathCamera.KillCam && alive && !IsActive)
            {
                RecordKillCam(Time.timeAsDouble);
            }
            else
            {
                // 記録が途切れた前後を補間でつながないよう、次に記録し始めるときは空から始める
                ClearKillCamRecords();
            }

            Step(_mode, alive, IsMatchEnded());
            if (IsActive)
            {
                Advance(_view.ReadSpectateStep(), _view.ReadMove(), _view.ReadLookDegrees(Time.deltaTime), Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            if (_shotsSubscribed)
            {
                CombatEvents.ShotFired -= OnShotFired;
                _shotsSubscribed = false;
            }

            ClearKillCamSubjects();

            // シーンの読み直しやプレイヤーが消えるときも、カメラと入力を戻して次に有効になったときに持ち越さない
            if (_switch.Reset())
            {
                Restore("disabled");
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

        public void RecordKillCam(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time), time, "time must be finite");
            }

            EnsureResolved();
            if (_hasKillCamRecord && time < _lastKillCamRecordTime)
            {
                // 時計が戻った (Play のやり直しなど) ので、前の記録とはつながらない
                ClearKillCamRecords();
            }

            if (!_hasKillCamRecord || time >= _nextKillCamScanTime)
            {
                ScanKillCamSubjects();
                _nextKillCamScanTime = time + KillCamScanSeconds;
            }

            for (var i = _killCamSubjects.Count - 1; i >= 0; i--)
            {
                var subject = _killCamSubjects[i];
                if (subject.Target == null)
                {
                    _killCamSubjects.RemoveAt(i);
                    continue;
                }

                if (!subject.Target.activeInHierarchy || !TryPoseOf(subject.Target, out var pose))
                {
                    subject.Buffer.Clear();
                    continue;
                }

                subject.Buffer.Record(time, pose);
            }

            _lastKillCamRecordTime = time;
            _hasKillCamRecord = true;
        }

        private void OnShotFired(ShotFiredInfo shot)
        {
            // 自分の弾や、場を探す前に撃ってきた相手 (砲台など Damageable を持たないものも) を拾う
            if (_mode == DeathCamera.KillCam && shot.Shooter != null && IsStanding(_damageable))
            {
                AddKillCamSubject(shot.Shooter);
            }
        }

        private void ScanKillCamSubjects()
        {
            foreach (var candidate in FindObjectsByType<Damageable>())
            {
                AddKillCamSubject(candidate.gameObject);
            }
        }

        private void AddKillCamSubject(GameObject target)
        {
            if (target == null || target == gameObject || target.transform.IsChildOf(transform))
            {
                return;
            }

            for (var i = 0; i < _killCamSubjects.Count; i++)
            {
                if (ReferenceEquals(_killCamSubjects[i].Target, target))
                {
                    return;
                }
            }

            _killCamSubjects.Add(new KillCamSubject(target, new KillCamBuffer(_killCamSeconds)));
        }

        // 倒した者は撃った者 (照準のルートなど体の子のこともある) なので、同じものが無ければ親子の関係でも探す
        private KillCamSubject FindKillCamSubject(GameObject killer)
        {
            KillCamSubject related = null;
            for (var i = 0; i < _killCamSubjects.Count; i++)
            {
                var subject = _killCamSubjects[i];
                if (subject.Target == null)
                {
                    continue;
                }

                if (ReferenceEquals(subject.Target, killer))
                {
                    return subject;
                }

                if (related == null
                    && (killer.transform.IsChildOf(subject.Target.transform) || subject.Target.transform.IsChildOf(killer.transform)))
                {
                    related = subject;
                }
            }

            return related;
        }

        private KillCamClip CaptureKillCam(GameObject killer)
        {
            if (killer == null || !_hasKillCamRecord)
            {
                return KillCamClip.Empty;
            }

            var subject = FindKillCamSubject(killer);
            return subject != null ? subject.Buffer.Capture(_lastKillCamRecordTime) : KillCamClip.Empty;
        }

        private void BeginKillCam()
        {
            var clip = _killCamClip;
            _killCamClip = KillCamClip.Empty;
            if (_killer == null)
            {
                Debug.Log(KillCamLog.NoKiller(name), this);
                return;
            }

            _replay.Begin(clip, _killer.name, _killerWeaponId, _killer.transform, _view, _killCamSpeed);
        }

        private static bool TryPoseOf(GameObject target, out KillCamPose pose)
        {
            var eye = EyeOf(target);
            var yaw = target.transform.eulerAngles.y;
            if (!IsFinite(eye.x) || !IsFinite(eye.y) || !IsFinite(eye.z) || !IsFinite(yaw))
            {
                pose = default;
                return false;
            }

            pose = new KillCamPose(eye.ToNumerics(), yaw);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ClearKillCamRecords()
        {
            if (!_hasKillCamRecord)
            {
                return;
            }

            for (var i = 0; i < _killCamSubjects.Count; i++)
            {
                _killCamSubjects[i].Buffer.Clear();
            }

            _hasKillCamRecord = false;
        }

        private void ClearKillCamSubjects()
        {
            _killCamSubjects.Clear();
            _hasKillCamRecord = false;
        }

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
                    var fallen = mode == DeathCamera.Fallen;
                    var eye = EyeOf(gameObject);
                    _view.Begin(eye, transform.eulerAngles.y, fallen);
                    Debug.Log(LogTag + " started on " + name + " (" + mode + ") at " + DeathCamView.FormatPosition(_view.DeathEye), this);
                    _fallenElapsed = 0f;
                    _fallenSettled = false;
                    _fallenGroundY = ResolveFallenGroundY(eye);
                    if (fallen)
                    {
                        HideOwnBodyFromFallenView();
                        _view.FrameFallen(FallenMotion, _fallenGroundY, 0f);
                    }
                    else
                    {
                        Frame(null);
                    }

                    if (mode == DeathCamera.KillCam)
                    {
                        BeginKillCam();
                    }

                    break;
                default:
                    Restore(transition.ToString());
                    Debug.Log(LogTag + " stopped on " + name + " (" + transition + ")", this);
                    break;
            }

            return transition;
        }

        public void Advance(int spectateStep, Vector2 move, Vector2 lookDegrees, float deltaSeconds)
        {
            if (!IsActive)
            {
                return;
            }

            if (_mode != DeathCamera.KillCam && _replay.IsPlaying)
            {
                _replay.Stop("rule changed to " + _mode);
            }

            switch (_mode)
            {
                case DeathCamera.Killer:
                    Frame(ResolveKillerEye());
                    break;
                case DeathCamera.KillCam:
                    // 再生は倒れていても進め、リスポーンまでの待ちとは別に終わる。終えたフレームから Killer と同じに映す
                    if (!_replay.Advance(_view, deltaSeconds))
                    {
                        Frame(ResolveKillerEye());
                    }

                    break;
                case DeathCamera.Ally:
                    WatchAlly(spectateStep, move, lookDegrees, deltaSeconds);
                    break;
                case DeathCamera.Fallen:
                    AdvanceFallen(deltaSeconds);
                    break;
                default:
                    _view.MoveFree(move, lookDegrees, _freeSpeed, deltaSeconds);
                    break;
            }
        }

        private void AdvanceFallen(float deltaSeconds)
        {
            var fallen = FallenMotion;
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _fallenElapsed += deltaSeconds;
            }

            _view.FrameFallen(fallen, _fallenGroundY, _fallenElapsed);
            if (!_fallenSettled && fallen.IsSettled(_fallenElapsed) && _view.Camera != null)
            {
                _fallenSettled = true;
                Debug.Log(LogTag + " fallen view settled on " + name + " at " + DeathCamView.FormatPosition(_view.Camera.transform.position), this);
            }
        }

        // 足元の高さだけでは、跳んでいる途中に倒れると空中で止まってしまうので、目の真下の床を探す
        private float ResolveFallenGroundY(Vector3 eye)
        {
            var feet = _damageable != null ? _damageable.GroundPoint.y : transform.position.y;
            var hits = Physics.RaycastAll(eye, Vector3.down, FallenGroundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            var ground = feet;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                ground = hit.point.y;
            }

            return ground;
        }

        private void HideOwnBodyFromFallenView()
        {
            var weapon = GetComponentInChildren<WeaponController>();
            if (weapon != null && weapon.Holster.Claim(this))
            {
                _holsteredWeapon = weapon;
            }

            // 一人称の体は影だけ描かれていて、カメラが床へ倒れても立ったままの影が残るので、影ごと描かない
            foreach (var ownRenderer in GetComponentsInChildren<Renderer>(false))
            {
                if (ownRenderer.enabled)
                {
                    ownRenderer.enabled = false;
                    _hiddenOwnRenderers.Add(ownRenderer);
                }
            }

            // 抜け殻は倒れたとき (Died) に作られ、リスポーンで消えるので、戻さなくてよい
            var body = GetComponent<DeathBodyPresenter>();
            var shell = body != null ? body.Shell : null;
            if (shell != null)
            {
                foreach (var shellRenderer in shell.GetComponentsInChildren<Renderer>(true))
                {
                    shellRenderer.enabled = false;
                }
            }
        }

        private void ShowOwnBody()
        {
            foreach (var ownRenderer in _hiddenOwnRenderers)
            {
                if (ownRenderer != null)
                {
                    ownRenderer.enabled = true;
                }
            }

            _hiddenOwnRenderers.Clear();
        }

        private void ReleaseHolster()
        {
            if (_holsteredWeapon != null)
            {
                _holsteredWeapon.Holster.Release(this);
            }

            _holsteredWeapon = null;
        }

        private static float FiniteOr(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private void Frame(Vector3? killerEye)
        {
            _view.Frame(killerEye, _height, _backDistance);
        }

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
            _killerWeaponId = _killer != null ? attribution.Value.WeaponId : string.Empty;
            _killerResolved = false;

            // 倒した相手の姿勢はこの後も記録が進むので、倒れた時点までをここで切り出しておく
            _killCamClip = _mode == DeathCamera.KillCam ? CaptureKillCam(_killer) : KillCamClip.Empty;

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

        private void Restore(string reason)
        {
            _replay.Stop(reason);
            _killCamClip = KillCamClip.Empty;
            ReleaseHolster();
            ShowOwnBody();
            _view.Restore(true);
            ClearAlly();
            _killer = null;
            _killerWeaponId = string.Empty;
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
            _replay = new KillCamReplay(this);
        }

        private sealed class KillCamSubject
        {
            public KillCamSubject(GameObject target, KillCamBuffer buffer)
            {
                Target = target;
                Buffer = buffer;
            }

            public GameObject Target { get; }

            public KillCamBuffer Buffer { get; }
        }
    }
}
