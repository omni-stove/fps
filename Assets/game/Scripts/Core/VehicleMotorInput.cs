using System;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 軸に意味を持たせないのは、車・飛行機・船などモーターごとに要る軸が違い、どの操作をどの軸に当てるかを使う側 (IVehicleControlScheme) が決めるため。
    // 軸を配列でなく欄で持つのは、毎フレーム作る入力でごみを出さないため。
    public readonly struct VehicleMotorInput : IEquatable<VehicleMotorInput>
    {
        public const int AxisCount = 8;

        private readonly float _axis0;
        private readonly float _axis1;
        private readonly float _axis2;
        private readonly float _axis3;
        private readonly float _axis4;
        private readonly float _axis5;
        private readonly float _axis6;
        private readonly float _axis7;

        public VehicleMotorInput(
            float axis0 = 0f,
            float axis1 = 0f,
            float axis2 = 0f,
            float axis3 = 0f,
            float axis4 = 0f,
            float axis5 = 0f,
            float axis6 = 0f,
            float axis7 = 0f)
        {
            _axis0 = Checked(axis0, 0);
            _axis1 = Checked(axis1, 1);
            _axis2 = Checked(axis2, 2);
            _axis3 = Checked(axis3, 3);
            _axis4 = Checked(axis4, 4);
            _axis5 = Checked(axis5, 5);
            _axis6 = Checked(axis6, 6);
            _axis7 = Checked(axis7, 7);
        }

        public static VehicleMotorInput Idle => default;

        public bool IsIdle => Equals(Idle);

        public float this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0: return _axis0;
                    case 1: return _axis1;
                    case 2: return _axis2;
                    case 3: return _axis3;
                    case 4: return _axis4;
                    case 5: return _axis5;
                    case 6: return _axis6;
                    case 7: return _axis7;
                    default: throw AxisOutOfRange(axis);
                }
            }
        }

        public VehicleMotorInput With(int axis, float value)
        {
            if (axis < 0 || axis >= AxisCount)
            {
                throw AxisOutOfRange(axis);
            }

            return new VehicleMotorInput(
                axis == 0 ? value : _axis0,
                axis == 1 ? value : _axis1,
                axis == 2 ? value : _axis2,
                axis == 3 ? value : _axis3,
                axis == 4 ? value : _axis4,
                axis == 5 ? value : _axis5,
                axis == 6 ? value : _axis6,
                axis == 7 ? value : _axis7);
        }

        public static bool operator ==(VehicleMotorInput left, VehicleMotorInput right) => left.Equals(right);

        public static bool operator !=(VehicleMotorInput left, VehicleMotorInput right) => !left.Equals(right);

        public bool Equals(VehicleMotorInput other)
        {
            return _axis0.Equals(other._axis0) && _axis1.Equals(other._axis1) && _axis2.Equals(other._axis2) && _axis3.Equals(other._axis3)
                && _axis4.Equals(other._axis4) && _axis5.Equals(other._axis5) && _axis6.Equals(other._axis6) && _axis7.Equals(other._axis7);
        }

        public override bool Equals(object obj) => obj is VehicleMotorInput other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < AxisCount; i++)
                {
                    hash = (hash * 397) ^ this[i].GetHashCode();
                }

                return hash;
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder("(");
            for (var i = 0; i < AxisCount; i++)
            {
                if (i > 0)
                {
                    text.Append(' ');
                }

                text.Append(this[i].ToString("0.##", CultureInfo.InvariantCulture));
            }

            return text.Append(')').ToString();
        }

        private static float Checked(float value, int axis)
        {
            if (!(Math.Abs(value) <= 1f))
            {
                throw new ArgumentOutOfRangeException("axis" + axis.ToString(CultureInfo.InvariantCulture), value, "each axis must be between -1 and 1");
            }

            return value;
        }

        private static ArgumentOutOfRangeException AxisOutOfRange(int axis)
        {
            return new ArgumentOutOfRangeException(nameof(axis), axis, "axis must be between 0 and " + (AxisCount - 1).ToString(CultureInfo.InvariantCulture));
        }
    }
}
