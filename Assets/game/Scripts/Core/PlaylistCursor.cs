using System;

namespace Shooter.Core
{
    public sealed class PlaylistCursor
    {
        private const long _modulus = 2147483647L;
        private const long _multiplier = 48271L;

        private readonly int[] _order;
        private long _randomState;
        private int _step;

        public PlaylistCursor(Playlist playlist, int seed)
        {
            Playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
            _randomState = ((uint)seed % (_modulus - 1L)) + 1L;
            _order = new int[playlist.Entries.Count];
            for (var i = 0; i < _order.Length; i++)
            {
                _order[i] = i;
            }

            if (playlist.Order == PlaylistOrder.Shuffle)
            {
                Shuffle();
            }
        }

        public Playlist Playlist { get; }

        public int Position => _order[_step];

        public int Round { get; private set; }

        public PlaylistEntry Current => Playlist.Entries[Position];

        public PlaylistEntry Next()
        {
            _step++;
            if (_step >= _order.Length)
            {
                _step = 0;
                Round++;
                if (Playlist.Order == PlaylistOrder.Shuffle)
                {
                    var last = _order[_order.Length - 1];
                    Shuffle();

                    // 混ぜ直した頭が直前の項目だと同じシーンが続くので、頭をほかの位置と入れ替える。
                    if (_order.Length > 1 && _order[0] == last)
                    {
                        var other = 1 + NextIndex(_order.Length - 1);
                        _order[0] = _order[other];
                        _order[other] = last;
                    }
                }
            }

            return Current;
        }

        private void Shuffle()
        {
            for (var i = _order.Length - 1; i > 0; i--)
            {
                var j = NextIndex(i + 1);
                var swap = _order[i];
                _order[i] = _order[j];
                _order[j] = swap;
            }
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、MapVote と同じ Lehmer の乗算合同法 (MINSTD) で混ぜる (同じ種から同じ並びにするため)。
        private int NextIndex(int count)
        {
            _randomState = _randomState * _multiplier % _modulus;
            return (int)(_randomState % count);
        }
    }
}
