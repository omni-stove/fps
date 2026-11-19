using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // GameSignals.Vision は画面 1 つに 1 つの値なので、この画面で操作するプレイヤーにだけ付ける。
    [DisallowMultipleComponent]
    public sealed class VisionModeController : MonoBehaviour
    {
        public const string LogTag = VisionLog.Tag;

        private VisionVisualCatalog _catalog;
        private bool _catalogResolved;
        private AimDownSights _ads;
        private Damageable _damageable;
        private bool _damageableResolved;
        private VisionStatus? _published;
        private AudioSource _audio;

        public event Action<VisionStatus> Changed;

        public VisionVisualCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = VisionVisualCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
            }
        }

        // テストや、AimDownSights を持たない体が差し込む。null なら同じ物に付いた AimDownSights の照準を読む。
        public Func<SightView> SightSource { get; set; }

        public VisionMode Goggles { get; private set; }

        public bool GogglesOn => Goggles != VisionMode.None;

        public VisionStatus Current { get; private set; }

        public VisionStatus? Published => _published;

        public AudioClip LastSwitchSound { get; private set; }

        public Damageable Damageable
        {
            get
            {
                if (!_damageableResolved)
                {
                    BindDamageable(GetComponent<Damageable>());
                }

                return _damageable;
            }
        }

        public bool IsDead
        {
            get
            {
                var damageable = Damageable;
                return damageable != null && damageable.IsDead;
            }
        }

        // 照準器の見え方を優先するのは、覗いている間は照準の中の景色がゴーグル越しの景色より手前にあるため。
        public static VisionStatus Resolve(VisionMode scope, VisionMode goggles, bool dead)
        {
            RequireDefined(scope, nameof(scope));
            RequireDefined(goggles, nameof(goggles));
            if (dead)
            {
                return VisionStatus.None;
            }

            if (scope != VisionMode.None)
            {
                return new VisionStatus(scope, VisionSource.Scope);
            }

            return goggles != VisionMode.None ? new VisionStatus(goggles, VisionSource.Goggles) : VisionStatus.None;
        }

        public static bool SoundsOn(VisionStatus previous, VisionStatus next)
        {
            return next.IsActive && (!previous.IsActive || previous.Mode != next.Mode);
        }

        public static bool SoundsOff(VisionStatus previous, VisionStatus next)
        {
            return previous.IsActive && !next.IsActive;
        }

        public void BindDamageable(Damageable damageable)
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_damageable, null))
            {
                _damageable.Died -= HandleDied;
            }

            _damageable = damageable;
            _damageableResolved = true;
            if (!ReferenceEquals(_damageable, null))
            {
                _damageable.Died += HandleDied;
            }
        }

        // 入れたら true。死んでいる間は入れない。
        public bool SetGoggles(VisionMode mode)
        {
            RequireDefined(mode, nameof(mode));
            if (mode != VisionMode.None && IsDead)
            {
                return false;
            }

            if (mode == Goggles)
            {
                return mode != VisionMode.None;
            }

            var previous = Goggles;
            Goggles = mode;
            Debug.Log(VisionLog.Goggles(OwnerId(), mode != VisionMode.None ? mode : previous, mode != VisionMode.None), this);
            Refresh();
            return mode != VisionMode.None;
        }

        // 別の見え方のゴーグルを掛けていたら、切らずに掛け替える。
        public bool ToggleGoggles(VisionMode mode)
        {
            RequireDefined(mode, nameof(mode));
            if (mode == VisionMode.None)
            {
                throw new ArgumentException("toggle a goggles mode, not None", nameof(mode));
            }

            return SetGoggles(Goggles == mode ? VisionMode.None : mode);
        }

        public VisionStatus Refresh()
        {
            var next = Resolve(ReadSight().VisionMode, Goggles, IsDead);
            Apply(next);
            return next;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Apply(VisionStatus.None);
        }

        private void OnDestroy()
        {
            BindDamageable(null);
            Apply(VisionStatus.None);
            Changed = null;
        }

        private void HandleDied(Damageable _)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                BindDamageable(null);
                return;
            }

            if (GogglesOn)
            {
                Goggles = VisionMode.None;
                Debug.Log(VisionLog.GogglesOffOnDeath(OwnerId()), this);
            }

            Refresh();
        }

        private SightView ReadSight()
        {
            if (SightSource != null)
            {
                return SightSource();
            }

            // 組み立てる側が AimDownSights を後から付けることがあるので、見つかるまで探し直す。
            if (_ads == null)
            {
                _ads = GetComponent<AimDownSights>();
            }

            return _ads != null ? _ads.SightView : SightView.None;
        }

        private void Apply(VisionStatus next)
        {
            var previous = Current;
            if (next == previous)
            {
                return;
            }

            Current = next;
            Debug.Log(VisionLog.Changed(previous, next), this);
            Publish(next);
            PlaySwitch(previous, next);
            Changed?.Invoke(next);
        }

        private void Publish(VisionStatus status)
        {
            if (status.IsActive)
            {
                _published = status;
                GameSignals.Vision.Publish(status);
                return;
            }

            if (_published.HasValue)
            {
                GameSignals.Vision.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private void PlaySwitch(VisionStatus previous, VisionStatus next)
        {
            bool on;
            if (SoundsOn(previous, next))
            {
                on = true;
            }
            else if (SoundsOff(previous, next))
            {
                on = false;
            }
            else
            {
                return;
            }

            var clip = VisionVisualCatalog.SwitchSoundFor(Catalog, on);
            LastSwitchSound = clip;
            if (clip == null || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
                _audio.dopplerLevel = 0f;
            }

            _audio.PlayOneShot(clip, VisionVisualCatalog.SwitchVolumeOf(Catalog));
        }

        private string OwnerId()
        {
            var damageable = Damageable;
            return damageable != null ? damageable.Id : name;
        }

        private static void RequireDefined(VisionMode mode, string name)
        {
            if (!Enum.IsDefined(typeof(VisionMode), mode))
            {
                throw new ArgumentOutOfRangeException(name, mode, "mode is not a defined VisionMode");
            }
        }
    }
}
