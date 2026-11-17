using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ParticipantTable : IParticipants
    {
        // Dictionary だけにしないのは、スコアボードが一覧を取るたびに同じ順で並ぶよう、入れた順を残すため。
        private readonly List<Participant> _order = new List<Participant>();
        private readonly Dictionary<int, int> _indexById = new Dictionary<int, int>();

        public IReadOnlyList<Participant> All => _order;

        public void Set(Participant participant)
        {
            if (_indexById.TryGetValue(participant.Id, out var index))
            {
                _order[index] = participant;
                return;
            }

            _indexById.Add(participant.Id, _order.Count);
            _order.Add(participant);
        }

        public bool Remove(int id)
        {
            if (!_indexById.TryGetValue(id, out var index))
            {
                return false;
            }

            _order.RemoveAt(index);
            _indexById.Remove(id);
            for (var i = index; i < _order.Count; i++)
            {
                _indexById[_order[i].Id] = i;
            }

            return true;
        }

        public void Clear()
        {
            _order.Clear();
            _indexById.Clear();
        }

        public bool TryGet(int id, out Participant participant)
        {
            if (_indexById.TryGetValue(id, out var index))
            {
                participant = _order[index];
                return true;
            }

            participant = default;
            return false;
        }
    }
}
