using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 座席の武器に座っている間は、持ち物の武器が持ち替わっても HUD を座席の武器のままにするため、両方の知らせをここでまとめる。
    public sealed class HudWeaponBinding : IDisposable
    {
        private readonly HudModel _model;
        private readonly WeaponController _weapon;
        private readonly PlayerVehicleRider _rider;
        private bool _disposed;

        public HudWeaponBinding(HudModel model, WeaponController weapon, PlayerVehicleRider rider)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _weapon = weapon;
            _rider = rider;
            if (_weapon != null)
            {
                _weapon.WeaponChanged += OnChanged;
            }

            if (_rider != null)
            {
                _rider.SeatWeaponChanged += OnChanged;
            }

            Refresh();
        }

        public Weapon Current
        {
            get
            {
                var seatWeapon = _rider != null ? _rider.SeatWeapon : null;
                if (seatWeapon != null)
                {
                    return seatWeapon;
                }

                return _weapon != null ? _weapon.Weapon : null;
            }
        }

        public void Refresh()
        {
            if (!_disposed)
            {
                _model.BindWeapon(Current);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_weapon != null)
            {
                _weapon.WeaponChanged -= OnChanged;
            }

            if (_rider != null)
            {
                _rider.SeatWeaponChanged -= OnChanged;
            }
        }

        private void OnChanged(Weapon _)
        {
            Refresh();
        }
    }
}
