using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾切れでトリガーを引いたときの空撃ちの音を鳴らす。撃つ人の本体に付ける。音は <see cref="WeaponExtrasCatalog"/> から武器の Id で引く。
    /// オフラインでは同じ階層の <see cref="WeaponController.DryFired"/> を購読し、ネット対戦では <c>Fps.Net.NetPlayer</c> が
    /// <see cref="Play"/> を呼ぶ。手元のプレイヤー (<see cref="IsLocal"/>) は 2D、それ以外は本体の位置から 3D で鳴らす。
    /// 専用サーバーでは有効になったときに自分を止める (<c>Fps.Net.HeadlessPresentation</c> も止める)。
    /// </summary>
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

        /// <summary>
        /// 音を引く設定。初めて読んだときに <see cref="WeaponExtrasCatalog.Load"/> で探し、無ければ null (鳴らさない)。
        /// 代入すると探さずにそれを使う。
        /// </summary>
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

        /// <summary>
        /// 2D で鳴らすか。同じ本体の <see cref="WeaponPresentation.IsLocal"/> に従い (ネット対戦では NetPlayer が持ち主かどうかを代入している)、
        /// それが無ければ同じ階層に <see cref="WeaponController"/> があるときだけ true。
        /// </summary>
        public bool IsLocal
        {
            get
            {
                EnsureResolved();
                return _presentation != null ? _presentation.IsLocal : _controller != null;
            }
        }

        /// <summary>空撃ちの音を鳴らす音源。初めて鳴らすまでは null。</summary>
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

        /// <summary>
        /// weaponId の武器の空撃ちの音を鳴らし、鳴らしたら true。素材の有無にかかわらず <see cref="FormatLog"/> の形でログに出す。
        /// カタログが無い・武器が無い (どちらも 1 度だけ警告)・音の枠が空なら鳴らさずに false。
        /// </summary>
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

        /// <summary>空撃ちの音を鳴らすときのログ。例: <c>[Presentation] dry-fire weapon=rifle-id sound=DryFire</c>。音が無ければ <c>sound=none</c>。</summary>
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
