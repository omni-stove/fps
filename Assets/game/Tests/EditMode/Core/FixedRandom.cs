using System;

namespace Shooter.Core.Tests
{
    // 種から出る列は実装 (Mono と .NET) で変わりうるので、期待値を決め打つテストは引く値そのものを渡す。
    internal sealed class FixedRandom : Random
    {
        private readonly double[] _values;

        public FixedRandom(params double[] values)
        {
            _values = values;
        }

        public int Used { get; private set; }

        public override double NextDouble()
        {
            if (Used >= _values.Length)
            {
                throw new InvalidOperationException("the test gave only " + _values.Length + " values");
            }

            return _values[Used++];
        }
    }
}
