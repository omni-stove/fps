using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class WindUpSound : MonoBehaviour
    {
        public const string LogTag = "[Presentation]";

        private WeaponController _weapon;
        private AudioSource _loop;
        private AudioSource _oneShot;
        private bool _wasFull;
        private bool _subscribed;

        public AudioSource LoopSource => _loop;

        public static WindUpSound Attach(GameObject player, WeaponController weapon)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            var sound = player.AddComponent<WindUpSound>();
            sound.Bind(weapon);
            return sound;
        }

        public void Bind(WeaponController weapon)
        {
            Unsubscribe();
            _weapon = weapon;
            Subscribe();
        }

        // 回転と溜めを両方持つ武器では、撃てるようになるまでの待ちを決めるのは外側の溜めなので、溜めの音を優先する。
        public void Tick(WeaponDefinition definition, float spinFraction, float chargeFraction)
        {
            WindUpSoundSettings settings = null;
            var fraction = 0f;
            if (definition != null && definition.Charge.Enabled)
            {
                settings = definition.Charge.Sound;
                fraction = chargeFraction;
            }
            else if (definition != null && definition.SpinUp.Enabled)
            {
                settings = definition.SpinUp.Sound;
                fraction = spinFraction;
            }

            var full = settings != null && fraction >= 1f;
            if (full && !_wasFull && settings.ReadySound != null)
            {
                Debug.Log($"{LogTag} wind-up ready weapon={definition.Id} sound={settings.ReadySound.name}", this);
                PlayOnce(settings.ReadySound);
            }

            _wasFull = full;
            PlayLoop(settings, fraction);
        }

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _wasFull = false;
            if (_loop != null)
            {
                _loop.Stop();
            }
        }

        private void Update()
        {
            if (_weapon != null)
            {
                Tick(_weapon.Definition, _weapon.SpinFraction, _weapon.ChargeFraction);
            }
        }

        private void Subscribe()
        {
            if (!_subscribed && _weapon != null && isActiveAndEnabled)
            {
                _weapon.Fired += HandleFired;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed && _weapon != null)
            {
                _weapon.Fired -= HandleFired;
            }

            _subscribed = false;
        }

        private void HandleFired(Weapon weapon)
        {
            var definition = _weapon != null ? _weapon.Definition : null;
            if (definition == null || !definition.Cycle.Enabled || definition.Cycle.Sound == null)
            {
                return;
            }

            Debug.Log($"{LogTag} cycle weapon={definition.Id} sound={definition.Cycle.Sound.name}", this);
            PlayOnce(definition.Cycle.Sound);
        }

        private void PlayLoop(WindUpSoundSettings settings, float fraction)
        {
            var clip = settings?.Loop;
            if (clip == null || fraction <= 0f)
            {
                if (_loop != null && _loop.isPlaying)
                {
                    _loop.Stop();
                }

                return;
            }

            if (_loop == null)
            {
                _loop = CreateSource();
                _loop.loop = true;
            }

            _loop.pitch = settings.PitchAt(fraction);
            _loop.volume = settings.VolumeAt(fraction);
            if (_loop.clip != clip || !_loop.isPlaying)
            {
                _loop.clip = clip;
                _loop.Play();
            }
        }

        private void PlayOnce(AudioClip clip)
        {
            if (_oneShot == null)
            {
                _oneShot = CreateSource();
            }

            _oneShot.PlayOneShot(clip);
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
