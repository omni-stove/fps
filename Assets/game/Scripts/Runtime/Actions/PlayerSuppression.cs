using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SuppressionReceiver))]
    public sealed class PlayerSuppression : MonoBehaviour
    {
        private SuppressionReceiver _receiver;
        private SuppressionWeaponModifier _modifier;
        private WeaponController _weapon;
        private ISuppressionPresenter _presenter;

        public SuppressionReceiver Receiver => _receiver != null ? _receiver : (_receiver = GetComponent<SuppressionReceiver>());

        public WeaponController Weapon => _weapon;

        public SuppressionWeaponModifier Modifier => _modifier ??= new SuppressionWeaponModifier(Receiver);

        public bool IsOn => Receiver != null && Receiver.Rule == Suppression.On;

        // 差し替えの口。Configure で On にしたときに空なら既定の SuppressionVignette を入れるので、ゲームはその前後どちらで入れてもよい。
        // 外れる演出には 0 を渡し、画面に残らないようにする。
        public ISuppressionPresenter Presenter
        {
            get => IsAlive(_presenter) ? _presenter : null;
            set
            {
                var previous = Presenter;
                if (ReferenceEquals(previous, value))
                {
                    return;
                }

                previous?.Present(0f, Receiver != null ? Receiver.Spec : SuppressionSpec.Default);
                _presenter = value;
            }
        }

        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public void Configure(Suppression rule, SuppressionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Receiver.Configure(rule, spec);

            // WeaponController はプレイヤーのカメラ (子) に付いている。
            BindWeapon(GetComponentInChildren<WeaponController>(true));

            var on = rule == Suppression.On;
            if (on && Presenter == null)
            {
                Presenter = PlayerActions.GetOrAdd<SuppressionVignette>(gameObject);
            }

            enabled = on;

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、照準の乱れと演出はここでも揃える。
            SyncModifier();
            Present();
        }

        public void BindWeapon(WeaponController weapon)
        {
            if (_weapon == weapon)
            {
                SyncModifier();
                return;
            }

            if (_weapon != null)
            {
                _weapon.RemoveModifier(Modifier);
            }

            _weapon = weapon;
            SyncModifier();
        }

        private void OnEnable()
        {
            SyncModifier();
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.RemoveModifier(Modifier);
            }

            Presenter?.Present(0f, Receiver != null ? Receiver.Spec : SuppressionSpec.Default);
        }

        private void LateUpdate()
        {
            Present();
        }

        private void SyncModifier()
        {
            if (_weapon == null)
            {
                return;
            }

            if (enabled && IsOn)
            {
                _weapon.AddModifier(Modifier);
            }
            else
            {
                _weapon.RemoveModifier(Modifier);
            }
        }

        private void Present()
        {
            var receiver = Receiver;
            var presenter = Presenter;
            if (presenter == null || receiver == null)
            {
                return;
            }

            presenter.Present(enabled && receiver.IsActive ? receiver.Level : 0f, receiver.Spec);
        }

        // 演出が MonoBehaviour なら、消された後も参照が残るので Unity の null 比較で見る。
        private static bool IsAlive(ISuppressionPresenter presenter)
        {
            return presenter != null && !(presenter is UnityEngine.Object unityObject && unityObject == null);
        }
    }
}
