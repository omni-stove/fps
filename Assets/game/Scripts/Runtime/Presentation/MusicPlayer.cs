using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // シーンをまたいで曲をつなぐため、シーンには置かず起動時に 1 つだけ作って常駐させる。
    // 場面はメインメニューのシーンにいるかと GameSignals.ModeStatus から決め、モードやメニューの画面には手を入れない。
    [DisallowMultipleComponent]
    public sealed class MusicPlayer : MonoBehaviour
    {
        public const string ObjectName = "MusicPlayer";

        private static MusicPlayer _instance;

        private AudioSource _source;
        private MusicCatalog _catalog;
        private bool _catalogResolved;
        private MenuScenes _scenes;
        private bool _inMenu;
        private bool _attached;
        private MusicCue _cue = MusicCue.None;
        private AudioClip _target;
        private float _level;

        public static MusicPlayer Instance => _instance;

        public MusicCue Cue => _cue;

        public AudioClip TargetClip => _target;

        public AudioSource Source => EnsureSource();

        public float Level => _level;

        public bool InMenu => _inMenu;

        public bool IsAttached => _attached;

        public MusicCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalogResolved = true;
                    _catalog = MusicCatalog.Load();
                    Debug.Log(_catalog != null ? MusicPlayerLog.UsingCatalog(_catalog) : MusicPlayerLog.NoCatalog(), this);
                }

                return _catalog;
            }

            set
            {
                _catalog = value;
                _catalogResolved = true;
                Retarget();
            }
        }

        // ドメインの再読み込みを切った Play で、前の Play やテストのものへの参照が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnLoad()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            Install();
        }

        public static MusicPlayer Install()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject(ObjectName);
            // Edit Mode では DontDestroyOnLoad が例外を投げるので、Play の間だけ常駐させる。
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            var player = go.AddComponent<MusicPlayer>();
            _instance = player;
            // Edit Mode では OnEnable が呼ばれないので、入口から聞き始める。Play では OnEnable と重なっても Attach が二度目を無視する。
            player.Attach();
            return player;
        }

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            _attached = true;
            _scenes = MenuSceneSettings.LoadScenes();
            GameSignals.ModeStatus.Changed += OnModeStatusChanged;
            AppliedControlSettings.Changed += OnSettingsChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            _inMenu = MusicCues.IsMenuScene(SceneManager.GetActiveScene().name, _scenes);
            Refresh();
        }

        public void Detach()
        {
            if (!_attached)
            {
                return;
            }

            _attached = false;
            GameSignals.ModeStatus.Changed -= OnModeStatusChanged;
            AppliedControlSettings.Changed -= OnSettingsChanged;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        public void SetInMenu(bool inMenu)
        {
            _inMenu = inMenu;
            Refresh();
        }

        public void Refresh()
        {
            var cue = MusicCues.Pick(_inMenu, GameSignals.ModeStatus.Value);
            if (cue == _cue)
            {
                return;
            }

            var previous = _cue;
            _cue = cue;
            Retarget();
            Debug.Log(MusicPlayerLog.CueChanged(previous, cue, _target), this);
        }

        // 2 つの音源で重ねず、1 つで下げきってから替えるのは、拍の合わない曲同士が重なって濁らないようにするため。
        public void Tick(float deltaSeconds)
        {
            var source = EnsureSource();
            var fadeSeconds = FadeSeconds();
            var step = fadeSeconds > 0f ? Mathf.Max(0f, deltaSeconds) / fadeSeconds : 1f;
            if (source.clip != _target)
            {
                _level = source.clip == null ? 0f : Mathf.Max(0f, _level - step);
                if (_level <= 0f)
                {
                    SwitchClip(source);
                }
            }
            else if (_target != null && _level < 1f)
            {
                _level = Mathf.Min(1f, _level + step);
            }

            ApplyVolume();
        }

        public void ApplyVolume()
        {
            var catalog = Catalog;
            var volume = catalog != null ? catalog.Volume * AppliedControlSettings.Current.MusicVolume * _level : 0f;
            EnsureSource().volume = volume;
        }

        private void OnEnable()
        {
            if (_instance == this)
            {
                Attach();
            }
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnModeStatusChanged(ModeStatus status)
        {
            Refresh();
        }

        private void OnSettingsChanged(ControlSettings settings)
        {
            ApplyVolume();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            SetInMenu(MusicCues.IsMenuScene(next.name, _scenes ?? MenuScenes.Default));
        }

        private void Retarget()
        {
            var catalog = Catalog;
            _target = catalog != null ? catalog.ClipFor(_cue) : null;
            var source = EnsureSource();
            if (source.clip == _target && catalog != null)
            {
                // 勝ちと終わりに同じ曲を入れたときなど、曲を替えずに場面だけ変わっても、くり返すかは新しい場面に合わせる。
                source.loop = catalog.Loops(_cue);
            }

            ApplyVolume();
        }

        private void SwitchClip(AudioSource source)
        {
            source.Stop();
            source.clip = _target;
            var catalog = Catalog;
            source.loop = catalog == null || catalog.Loops(_cue);
            if (_target == null)
            {
                _level = 0f;
                return;
            }

            _level = FadeSeconds() > 0f ? 0f : 1f;
            source.Play();
        }

        private float FadeSeconds()
        {
            var catalog = Catalog;
            return catalog != null ? catalog.FadeSeconds : 0f;
        }

        private AudioSource EnsureSource()
        {
            if (_source != null)
            {
                return _source;
            }

            _source = GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.priority = 0;
            // ポーズで AudioListener.pause を立てても、メニューの曲は流し続ける。
            _source.ignoreListenerPause = true;
            _source.volume = 0f;
            return _source;
        }
    }
}
