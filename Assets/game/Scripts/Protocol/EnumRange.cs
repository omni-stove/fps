using System;

namespace Fps.Protocol
{
    /// <summary>
    /// 1 バイトで送る列挙の値の数。値が 0 から隙間なく並ぶ列挙だけを扱い、読むときは数より小さい値だけを受け取る
    /// (<see cref="MessageReader.ReadByteBelow"/>)。
    /// </summary>
    /// <remarks>
    /// 値を毎回 <see cref="Enum.IsDefined(Type, object)"/> で確かめると箱詰めで GC を起こすので、型ごとに一度だけ数える。
    /// 列挙の値はゲームが足せるので、数は定数にせず型から数える。
    /// </remarks>
    internal static class EnumRange<T>
        where T : struct, Enum
    {
        public static readonly int Count = CountValues();

        private static int CountValues()
        {
            var values = Enum.GetValues(typeof(T));
            var seen = new bool[values.Length];
            foreach (var value in values)
            {
                var number = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                if (number < 0 || number >= values.Length || number > byte.MaxValue)
                {
                    throw new InvalidOperationException(
                        typeof(T).Name + " must number its values 0, 1, 2, ... below 256 to be sent as a byte");
                }

                seen[number] = true;
            }

            foreach (var hit in seen)
            {
                if (!hit)
                {
                    throw new InvalidOperationException(typeof(T).Name + " has duplicate values");
                }
            }

            return values.Length;
        }
    }
}
