using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

// Fps.Runtime に置くと、同じ名前のルールの列挙 (Fps.Core.Ladder) を Fps.Runtime の中から名前だけで指せなくなるので、名前空間を分ける。
namespace Fps.Runtime.Traversal
{
    /// <summary>
    /// はしご 1 本。この GameObject の位置を、はしごの面が地面と交わる線の真ん中 (<see cref="LadderSpan.Bottom"/>)、
    /// 前 (transform.forward の水平成分) を、つかまる人が向く向き (上の足場のある側) とし、そこから <see cref="Height"/> 上がった所を上の足場とする。
    /// プレイヤーは <see cref="LadderMovement"/> がこれを見つけてつかまる。
    /// 有効な間は、下の地面と上の足場を結ぶ NavMesh のリンクを NavMesh のエージェントの種類ごとに 1 本ずつ張り (<see cref="Link"/>)、
    /// 敵と Bot がはしごを経路に使えるようにする (渡り方は <see cref="EnemyAgent"/> が受け持つ)。
    /// 張ったら <c>[Ladder] link ...</c> をログに出す。見た目は持たない (シーンを作るスクリプトが置く)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Ladder : MonoBehaviour
    {
        /// <summary><see cref="Height"/> の既定値 (m)。</summary>
        public const float DefaultHeight = 3f;

        /// <summary><see cref="Width"/> の既定値 (m)。</summary>
        public const float DefaultWidth = 0.8f;

        /// <summary>
        /// NavMesh のリンクの端を、はしごの面と上の足場の縁から、エージェントの半径にこれを足しただけ離す (m)。
        /// 焼いた NavMesh は壁と縁から半径だけ削れているので、端がその外に出ないようにする。
        /// </summary>
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

        /// <summary>つかまれるはしご (有効にしたか <see cref="Register"/> したもの)。載せた順。</summary>
        public static IReadOnlyList<Ladder> Enabled => _enabled;

        /// <summary>下の地面から上の足場までの高さ (m)。0 より大きい。変えたらリンクを張り直す。</summary>
        public float Height
        {
            get => _height;
            set
            {
                _height = value;
                Relink();
            }
        }

        /// <summary>つかまれる幅 (m)。0 より大きい。</summary>
        public float Width
        {
            get => _width;
            set => _width = value;
        }

        /// <summary>上り下りの速さ (m/s)。</summary>
        public float ClimbSpeed
        {
            get => _climbSpeed;
            set => _climbSpeed = value;
        }

        /// <summary>体の表面からはしごの面までの、つかまれる距離 (m)。</summary>
        public float GrabDistance
        {
            get => _grabDistance;
            set => _grabDistance = value;
        }

        /// <summary>ジャンプで離れたときに、はしごから遠ざかる速さ (m/s)。</summary>
        public float JumpOffSpeed
        {
            get => _jumpOffSpeed;
            set => _jumpOffSpeed = value;
        }

        /// <summary>離れてから次につかまれるまでの秒数。</summary>
        public float RegrabSeconds
        {
            get => _regrabSeconds;
            set => _regrabSeconds = value;
        }

        /// <summary>有効な間 NavMesh のリンクを張るか。変えたら、有効なら張るか外す。</summary>
        public bool CreatesNavMeshLinks
        {
            get => _createsNavMeshLinks;
            set
            {
                _createsNavMeshLinks = value;
                Relink();
            }
        }

        /// <summary>NavMesh のリンクを張っているか。</summary>
        public bool IsLinked => _links.Count > 0;

        /// <summary>張っているリンクの数 (エージェントの種類の数)。</summary>
        public int LinkCount => _links.Count;

        /// <summary>
        /// 今の位置・向き・値から作った形。値が不正なら <see cref="ArgumentException"/>。
        /// </summary>
        public LadderSpan Span => new LadderSpan(transform.position.ToNumerics(), transform.forward.ToNumerics(), _height, _width);

        /// <summary>今の値から作った調整値。値が不正なら <see cref="ArgumentException"/>。</summary>
        public LadderSpec CreateSpec()
        {
            return new LadderSpec(_climbSpeed, _grabDistance, _jumpOffSpeed, _regrabSeconds);
        }

        /// <summary>
        /// 形と調整値を作る。値が不正なら最初の 1 度だけエラーに出して false (はしごを使わない)。
        /// </summary>
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

        /// <summary>
        /// NavMesh のリンクを、NavMesh の設定にあるエージェントの種類ごとに 1 本ずつ張り直し、張った数を返す。
        /// 端は <see cref="LadderSpan.LinkBottom"/> と <see cref="LadderSpan.LinkTop"/> で、両向きに渡れる。
        /// 有効にすると (<see cref="CreatesNavMeshLinks"/> が true なら) 呼ばれる。形が不正なら何も張らずに 0。
        /// </summary>
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
                var clearance = settings.agentRadius + LinkMargin;
                var from = span.LinkBottom(clearance).ToUnity();
                var to = span.LinkTop(clearance).ToUnity();
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

        /// <summary>張ったリンクをすべて外す。張っていなければ何もしない。</summary>
        public void Unlink()
        {
            foreach (var link in _links)
            {
                NavMesh.RemoveLink(link.Instance);
            }

            _links.Clear();
        }

        /// <summary>
        /// 端が a と b (どちらの向きでもよい) のリンクを張っている、有効なはしごを探す。端の距離が tolerance (m) 以内なら同じとみなす。
        /// NavMeshAgent が渡っているリンク (<see cref="OffMeshLinkData"/>) がはしごかを見分けるのに使う。
        /// </summary>
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

        /// <summary>
        /// つかまれるはしごの一覧 (<see cref="Enabled"/>) に載せ、<see cref="CreatesNavMeshLinks"/> なら NavMesh のリンクを張る。
        /// Play 中は OnEnable が呼ぶ。Edit Mode では OnEnable が走らないので、試すときは直接呼ぶ。
        /// </summary>
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

        /// <summary>一覧から外し、リンクを外す。つかまっている人は次の Tick で離れる。Play 中は OnDisable が呼ぶ。</summary>
        public void Unregister()
        {
            _enabled.Remove(this);
            Unlink();
        }

        /// <summary>つかまれるはしごの一覧 (<see cref="Enabled"/>) に載っているか。</summary>
        public bool IsRegistered => _enabled.Contains(this);

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
