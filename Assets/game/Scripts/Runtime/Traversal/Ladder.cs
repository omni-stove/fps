using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.AI;

// Shooter.Runtime に置くと、同じ名前のルールの列挙 (Shooter.Core.Ladder) を Shooter.Runtime の中から名前だけで指せなくなるので、名前空間を分ける。
namespace Shooter.Runtime.Traversal
{
    [DisallowMultipleComponent]
    public sealed class Ladder : MonoBehaviour
    {
        public const float DefaultHeight = 3f;

        public const float DefaultWidth = 0.8f;

        // 焼いた NavMesh は壁と縁から半径だけ削れているので、リンクの端をエージェントの半径にこれを足しただけ離し、端がその外に出ないようにする。
        public const float LinkMargin = 0.3f;

        private static readonly List<Ladder> _enabled = new List<Ladder>();

        [SerializeField, Min(0.01f), Tooltip("下の地面から上の足場までの高さ (m)")]
        private float _height = DefaultHeight;

        [SerializeField, Min(0.01f), Tooltip("つかまれる幅 (m)")]
        private float _width = DefaultWidth;

        [SerializeField, Min(0.01f), Tooltip("上り下りの速さ (m/s)")]
        private float _climbSpeed = LadderSpec.DefaultClimbSpeed;

        [SerializeField, Min(0f), Tooltip("体の表面からはしごの面までがこれ以内なら、はしごへ進むとつかまる (m)")]
        private float _grabDistance = LadderSpec.DefaultGrabDistance;

        [SerializeField, Min(0f), Tooltip("ジャンプで離れたときに、はしごから遠ざかる速さ (m/s)")]
        private float _jumpOffSpeed = LadderSpec.DefaultJumpOffSpeed;

        [SerializeField, Min(0f), Tooltip("離れてから次につかまれるまでの秒数")]
        private float _regrabSeconds = LadderSpec.DefaultRegrabSeconds;

        [SerializeField, Tooltip("敵と Bot が経路に使えるよう、有効な間 NavMesh のリンクを張るか")]
        private bool _createsNavMeshLinks = true;

        private readonly List<LinkRecord> _links = new List<LinkRecord>();
        private bool _warnedInvalid;

        public static IReadOnlyList<Ladder> Enabled => _enabled;

        public float Height
        {
            get => _height;
            set
            {
                _height = value;
                Relink();
            }
        }

        public float Width
        {
            get => _width;
            set => _width = value;
        }

        public float ClimbSpeed
        {
            get => _climbSpeed;
            set => _climbSpeed = value;
        }

        public float GrabDistance
        {
            get => _grabDistance;
            set => _grabDistance = value;
        }

        public float JumpOffSpeed
        {
            get => _jumpOffSpeed;
            set => _jumpOffSpeed = value;
        }

        public float RegrabSeconds
        {
            get => _regrabSeconds;
            set => _regrabSeconds = value;
        }

        public bool CreatesNavMeshLinks
        {
            get => _createsNavMeshLinks;
            set
            {
                _createsNavMeshLinks = value;
                Relink();
            }
        }

        public bool IsLinked => _links.Count > 0;

        public int LinkCount => _links.Count;

        public LadderSpan Span => new LadderSpan(transform.position.ToNumerics(), transform.forward.ToNumerics(), _height, _width);

        public LadderSpec CreateSpec()
        {
            return new LadderSpec(_climbSpeed, _grabDistance, _jumpOffSpeed, _regrabSeconds);
        }

        public bool TryGetShape(out LadderSpan span, out LadderSpec spec)
        {
            try
            {
                span = Span;
                spec = CreateSpec();
                return true;
            }
            catch (ArgumentException e)
            {
                if (!_warnedInvalid)
                {
                    _warnedInvalid = true;
                    Debug.LogError($"{LadderLog.LogTag} '{name}' の設定が不正なので、つかまれず NavMesh のリンクも張りません: {e.Message}", this);
                }

                span = default;
                spec = null;
                return false;
            }
        }

        public int Link()
        {
            Unlink();
            if (!TryGetShape(out var span, out _))
            {
                return 0;
            }

            var count = NavMesh.GetSettingsCount();
            for (var i = 0; i < count; i++)
            {
                var settings = NavMesh.GetSettingsByIndex(i);
                var (from, to) = LinkEnds(span, settings.agentRadius);
                var data = new NavMeshLinkData
                {
                    startPosition = from,
                    endPosition = to,
                    width = 0f,
                    costModifier = -1f,
                    bidirectional = true,
                    area = 0,
                    agentTypeID = settings.agentTypeID,
                };
                _links.Add(new LinkRecord(NavMesh.AddLink(data), from, to));
            }

            if (_links.Count > 0)
            {
                Debug.Log(LadderLog.Link(name, _links[0].From.ToNumerics(), _links[0].To.ToNumerics(), _links.Count), this);
            }

            return _links.Count;
        }

        public bool TryGetLinkEnds(float agentRadius, out Vector3 from, out Vector3 to)
        {
            if (!TryGetShape(out var span, out _))
            {
                from = default;
                to = default;
                return false;
            }

            (from, to) = LinkEnds(span, agentRadius);
            return true;
        }

        public void Unlink()
        {
            foreach (var link in _links)
            {
                NavMesh.RemoveLink(link.Instance);
            }

            _links.Clear();
        }

        public static Ladder FindLinked(Vector3 a, Vector3 b, float tolerance)
        {
            var limit = tolerance * tolerance;
            foreach (var ladder in _enabled)
            {
                if (ladder == null)
                {
                    continue;
                }

                foreach (var link in ladder._links)
                {
                    var forward = (a - link.From).sqrMagnitude <= limit && (b - link.To).sqrMagnitude <= limit;
                    var backward = (a - link.To).sqrMagnitude <= limit && (b - link.From).sqrMagnitude <= limit;
                    if (forward || backward)
                    {
                        return ladder;
                    }
                }
            }

            return null;
        }

        // Edit Mode では OnEnable が走らないので、試験から直接呼べるよう OnEnable の外に出している。
        public void Register()
        {
            if (!_enabled.Contains(this))
            {
                _enabled.Add(this);
            }

            if (_createsNavMeshLinks)
            {
                Link();
            }
        }

        public void Unregister()
        {
            _enabled.Remove(this);
            Unlink();
        }

        public bool IsRegistered => _enabled.Contains(this);

        private static (Vector3 From, Vector3 To) LinkEnds(LadderSpan span, float agentRadius)
        {
            var clearance = agentRadius + LinkMargin;
            return (span.LinkBottom(clearance).ToUnity(), span.LinkTop(clearance).ToUnity());
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Relink()
        {
            if (!IsRegistered)
            {
                return;
            }

            if (_createsNavMeshLinks)
            {
                Link();
            }
            else
            {
                Unlink();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetShapeQuietly(out var span))
            {
                return;
            }

            var bottom = span.Bottom.ToUnity();
            var top = span.Top.ToUnity();
            var right = span.Right.ToUnity() * (span.Width / 2f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(bottom - right, top - right);
            Gizmos.DrawLine(bottom + right, top + right);
            Gizmos.DrawLine(top, top + (span.Facing.ToUnity() * 0.5f));
        }

        // Gizmo は値を打ち込んでいる途中にも描くので、不正でもエラーを出さない。
        private bool TryGetShapeQuietly(out LadderSpan span)
        {
            try
            {
                span = Span;
                return true;
            }
            catch (ArgumentException)
            {
                span = default;
                return false;
            }
        }

        private readonly struct LinkRecord
        {
            public LinkRecord(NavMeshLinkInstance instance, Vector3 from, Vector3 to)
            {
                Instance = instance;
                From = from;
                To = to;
            }

            public NavMeshLinkInstance Instance { get; }

            public Vector3 From { get; }

            public Vector3 To { get; }
        }
    }
}
