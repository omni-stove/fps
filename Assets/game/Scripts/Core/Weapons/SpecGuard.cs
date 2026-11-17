using System;

namespace Shooter.Core
{
    internal static class SpecGuard
    {
        public static float NonNegativeFinite(float value, string paramName)
        {
            if (!IsNonNegativeFinite(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be a non-negative finite number");
            }

            return value;
        }

        public static float PositiveFinite(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be positive and finite");
            }

            return value;
        }

        public static float Angle(float degrees, string paramName)
        {
            if (!(degrees >= 0f && degrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(paramName, degrees, paramName + " must be between 0 and 90");
            }

            return degrees;
        }

        public static float PositiveAngle(float degrees, string paramName)
        {
            if (!(degrees > 0f && degrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(paramName, degrees, paramName + " must be greater than 0 and at most 90");
            }

            return degrees;
        }

        public static float Fraction(float value, string paramName)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be between 0 and 1");
            }

            return value;
        }

        public static int NonNegative(int value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must not be negative");
            }

            return value;
        }

        public static int AtLeast(int value, int min, string paramName)
        {
            if (value < min)
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be at least " + min);
            }

            return value;
        }

        public static TEnum Defined<TEnum>(TEnum value, string paramName)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " is not a defined " + typeof(TEnum).Name);
            }

            return value;
        }

        public static string ReferenceId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        public static string NameKey(string nameKey)
        {
            return string.IsNullOrWhiteSpace(nameKey) ? string.Empty : nameKey.Trim();
        }

        public static string LowercaseUuid(string id, string paramName)
        {
            if (id == null)
            {
                throw new ArgumentNullException(paramName);
            }

            var trimmed = id.Trim();
            if (!IsLowercaseUuid(trimmed))
            {
                throw new ArgumentException(
                    paramName + " must be a lowercase hyphenated UUID (e.g. 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13): '" + trimmed + "'", paramName);
            }

            return trimmed;
        }

        public static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }

        // Guid.TryParseExact(value, "D", ...) は大文字も通すので使わない
        private static bool IsLowercaseUuid(string value)
        {
            if (value.Length != 36)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (i == 8 || i == 13 || i == 18 || i == 23)
                {
                    if (c != '-')
                    {
                        return false;
                    }
                }
                else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
