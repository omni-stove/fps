using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="EnemyAgent"/> のうち、NavMesh のリンク (はしご <see cref="Traversal.Ladder"/> が張るものなど) を渡る処理。
    /// NavMeshAgent に任せるとリンクの端と端をまっすぐ結んで動き、はしごでは足場の角を突き抜けるので、自分で動かす
    /// (<see cref="NavMeshAgent.autoTraverseOffMeshLink"/> を切る)。
    /// はしごのリンクなら、はしごの下 (上の足場) まで歩き、<see cref="Traversal.Ladder.ClimbSpeed"/> で上り下りしてから、リンクの先へ歩く。
    /// はしごでないリンクは、端から端へ歩きの速さでまっすぐ動く。渡っている間は判断を止め、撃たない。
    /// はしごを渡り始めたら <c>[AI] id=.. ladder=.. climb=Up|Down</c>、渡り終えたら <c>climb=Done</c> をログに出す。
    /// </summary>
    public sealed partial class EnemyAgent
    {
        /// <summary>渡っているリンクの端と、はしごの張ったリンクの端がこれ以内なら、そのはしごのリンクとみなす (m)。</summary>
        public const float LadderLinkTolerance = 1f;

        private bool _traversingLink;
        private Traversal.Ladder _linkLadder;
        private Vector3[] _linkPath;
        private int _linkIndex;
        private float _linkClimbSpeed;
        private Vector3 _linkFacing;

        /// <summary>NavMesh のリンクを渡っている最中か。</summary>
        public bool IsTraversingLink => _traversingLink;

        /// <summary>今渡っているはしご。はしごを渡っていなければ null。</summary>
        public Traversal.Ladder ClimbingLadder => _traversingLink ? _linkLadder : null;

        /// <summary>はしごを渡るときのログ。例: <c>[AI] id=enemy-1 ladder=Ladder climb=Up</c>。</summary>
        public static string FormatLadderLog(string id, string ladder, string climb)
        {
            return $"{LogTag} id={id} ladder={(string.IsNullOrEmpty(ladder) ? "-" : ladder)} climb={climb}";
        }

        /// <summary>
        /// NavMesh のリンクの上にいれば、このフレームの分だけ渡って true を返す (呼んだ側は判断と移動を飛ばす)。
        /// リンクの上にいなければ、渡っている途中の状態を捨てて false。
        /// </summary>
        private bool TraverseOffMeshLink(float deltaSeconds)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh || !_agent.isOnOffMeshLink)
            {
                if (_traversingLink)
                {
                    ClearLinkTraversal();
                }

                return false;
            }

            if (!_traversingLink)
            {
                BeginLinkTraversal();
            }

            var offset = Vector3.up * _agent.baseOffset;
            var ground = GroundPosition;
            var target = _linkPath[_linkIndex];
            var toTarget = target - ground;
            var vertical = Mathf.Abs(toTarget.y);
            toTarget.y = 0f;
            var climbing = _linkLadder != null && vertical > toTarget.magnitude;
            var speed = climbing ? _linkClimbSpeed : _walkSpeed;
            transform.position = Vector3.MoveTowards(ground, target, speed * deltaSeconds) + offset;
            if (climbing)
            {
                transform.rotation = Quaternion.LookRotation(_linkFacing, Vector3.up);
            }

            if ((GroundPosition - target).sqrMagnitude > 1e-6f)
            {
                return true;
            }

            _linkIndex++;
            if (_linkIndex < _linkPath.Length)
            {
                return true;
            }

            _agent.CompleteOffMeshLink();
            if (_linkLadder != null)
            {
                Debug.Log(FormatLadderLog(Id, _linkLadder.name, "Done"), this);
            }

            ClearLinkTraversal();

            // リンクの手前で測った基準のままだと、渡った後も進めていないとみなされる。
            _stuck.Reset();
            _destination = null;
            return true;
        }

        private void BeginLinkTraversal()
        {
            var data = _agent.currentOffMeshLinkData;
            var start = data.startPos;
            var end = data.endPos;
            _traversingLink = true;
            _linkIndex = 0;
            _linkLadder = Traversal.Ladder.FindLinked(start, end, LadderLinkTolerance);
            if (_linkLadder != null && _linkLadder.TryGetShape(out var span, out var spec))
            {
                var ascending = end.y > start.y;
                var climb = span.ClimbPath(ascending, _agent.radius);
                _linkPath = new[] { climb[0].ToUnity(), climb[1].ToUnity(), climb[2].ToUnity(), end };
                _linkClimbSpeed = spec.ClimbSpeed;
                _linkFacing = span.Facing.ToUnity();
                Debug.Log(FormatLadderLog(Id, _linkLadder.name, ascending ? "Up" : "Down"), this);
                return;
            }

            _linkLadder = null;
            _linkPath = new[] { end };
        }

        private void ClearLinkTraversal()
        {
            _traversingLink = false;
            _linkLadder = null;
            _linkPath = null;
            _linkIndex = 0;
        }
    }
}
