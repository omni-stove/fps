using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DryFireSound : MonoBehaviour
    {
        public const string LogTag = WeaponPresentation.LogTag;

        private static readonly HashSet<string> _warnedWeaponIds = new HashSet<string>(StringComparer.Ordinal);

        private WeaponExtrasCatalog _catalog;
        private bool _catalogResolved;
        private WeaponController _controller;
        private WeaponPresentation _presentation;
        private bool _resolved;
        private bool _subscribed;
        private AudioSource _source;

        public WeaponExtrasCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = WeaponExtrasCatalog.Load();
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

        // ネット対戦では NetV2LocalPlayer が WeaponPresentation.IsLocal に持ち主かどうかを代入しているので、自分では決めずにそれに従う。
        public bool IsLocal
        {
            get
            {
                EnsureResolved();
                return _presentation != null ? _presentation.IsLocal : _controller != null;
            }
        }

        public AudioSource Source => _source;

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            EnsureResolved();
            if (_controller != null && !_subscribed)
            {
                _controller.DryFired += HandleDryFired;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_subscribed && _controller != null)
            {
                _controller.DryFired -= HandleDryFired;
            }

            _subscribed = false;
        }

        public bool Play(string weaponId)
        {
            var catalog = Catalog;
            AudioClip clip = null;
            if (catalog == null)
            {
                WeaponExtrasCatalog.WarnMissingOnce(this);
            }
            else if (catalog.TryGetWeapon(weaponId, out var assets))
            {
                clip = assets.DryFireSound;
            }
            else if (_warnedWeaponIds.Add(weaponId ?? string.Empty))
            {
                Debug.LogWarning($"{LogTag} weapon '{weaponId}' has no dry-fire sound in the weapon extras catalog", this);
            }

            Debug.Log(FormatLog(weaponId, clip), this);
            if (clip == null)
            {
                return false;
            }

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.rolloffMode = AudioRolloffMode.Logarithmic;
                _source.minDistance = ShellEjector.LandSoundMinDistance;
                _source.maxDistance = ShellEjector.LandSoundMaxDistance;
                _source.dopplerLevel = 0f;
            }

            _source.spatialBlend = IsLocal ? 0f : 1f;
            _source.PlayOneShot(clip);
            return true;
        }

        public static string FormatLog(string weaponId, AudioClip sound)
        {
            return LogTag + " dry-fire weapon=" + (weaponId ?? string.Empty) + " sound=" + (sound != null ? sound.name : "none");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _warnedWeaponIds.Clear();
        }

        private void HandleDryFired(Weapon weapon)
        {
            if (weapon != null)
            {
                Play(weapon.Spec.Id);
            }
        }

        private void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _controller = GetComponentInChildren<WeaponController>(true);
            _presentation = GetComponent<WeaponPresentation>();
        }
    }
}
