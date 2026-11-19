using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 中心は自分の付いた物でなく GameSignals.LocalPlayer から取る。どこに付けても、操作するプレイヤーが替わっても追えるようにするため。
    [DisallowMultipleComponent]
    public sealed class MinimapPublisher : MonoBehaviour
    {
        public const string LogTag = MinimapLog.LogTag;

        private readonly List<MinimapContact> _contacts = new List<MinimapContact>();
        private readonly List<Damageable> _revealed = new List<Damageable>();
        private readonly List<IMinimapObjective> _objectives = new List<IMinimapObjective>();
        private MinimapSpec _spec;
        private MinimapStatus? _published;
        private bool _due = true;
        private float _nextAt;
        private int[] _loggedCounts;

        // ログの関数を渡す形の Parse なので、KitTuning.Load にはそのまま渡せず包む。
        public static MinimapSpec Tuning =>
            KitTuning.Load(c => c.MinimapTuning, MinimapJson.ResourcePath, (text, source) => MinimapJson.Parse(text, source, Debug.LogWarning), MinimapSpec.Default);

        public MinimapSpec Spec => _spec ?? (_spec = Tuning);

        // テストが時刻を進めるために差し込む。null なら Time.time で、ポーズの間は間隔も止まる。
        public Func<float> Clock { get; set; }

        public MinimapStatus? Published => _published;

        private float Now => Clock != null ? Clock() : Time.time;

        public void ApplySpec(MinimapSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _due = true;
        }

        // 毎フレームでなく間隔ごとにするのは、シーンの目標と陣営を探し直すのが毎フレームには重いため。
        public bool Tick(float now)
        {
            if (!_due && now < _nextAt)
            {
                return false;
            }

            _due = false;
            _nextAt = now + Spec.UpdateIntervalSeconds;
            Refresh();
            return true;
        }

        public MinimapStatus? Refresh()
        {
            var local = GameSignals.LocalPlayer.Value;
            var player = local?.Player;
            if (player == null)
            {
                Unpublish();
                return null;
            }

            var mine = SideMember.Of(player);
            Team? myTeam = mine != null && mine.Side.TryGetTeam(out var team) ? team : (Team?)null;

            _contacts.Clear();
            var allies = AddAllies(player, mine);
            var enemies = AddRevealedEnemies(player, mine);
            var pings = AddPings(myTeam);
            var objectives = AddObjectives(player, mine);

            var selfPosition = local.Damageable != null ? local.Damageable.GroundPoint : player.transform.position;
            var status = new MinimapStatus(selfPosition.ToNumerics(), HeadingOf(player.transform), _contacts);
            _published = status;
            // 同じ中身を出し直さないのは、止まっている間に UI が間隔ごとに描き直さないようにするため。
            if (!(GameSignals.Minimap.Value is MinimapStatus current) || current != status)
            {
                GameSignals.Minimap.Publish(status);
            }

            LogCountsIfChanged(allies, enemies, pings, objectives);
            return status;
        }

        private void OnEnable()
        {
            _due = true;
            Debug.Log(MinimapLog.Configured(Spec), this);
        }

        private void OnDisable()
        {
            Unpublish();
        }

        private void LateUpdate()
        {
            Tick(Now);
        }

        private int AddAllies(GameObject player, SideMember mine)
        {
            // 陣営の無いプレイヤーには味方がいないので、誰も味方として載せない。
            if (mine == null)
            {
                return 0;
            }

            var added = 0;
            foreach (var member in SideMember.FindAll())
            {
                if (member == null || member == mine || IsPartOf(member.transform, player) || !member.IsAlive || !mine.IsAllyOf(member))
                {
                    continue;
                }

                _contacts.Add(new MinimapContact(MinimapContactKind.Ally, member.GroundPosition.ToNumerics(), HeadingOf(member.transform), TeamOf(member.Side)));
                added++;
            }

            return added;
        }

        private int AddRevealedEnemies(GameObject player, SideMember mine)
        {
            _revealed.Clear();
            MinimapReveals.CopyRevealedTo(_revealed);
            var added = 0;
            foreach (var target in _revealed)
            {
                // 見つけた後に陣営が替わった者は、もう敵ではないので載せない。
                var theirs = SideMember.Of(target.gameObject);
                if (IsPartOf(target.transform, player) || (mine != null && mine.IsAllyOf(theirs)))
                {
                    continue;
                }

                var position = theirs != null ? theirs.GroundPosition : target.GroundPoint;
                Team? team = theirs != null ? TeamOf(theirs.Side) : null;
                _contacts.Add(new MinimapContact(MinimapContactKind.Enemy, position.ToNumerics(), HeadingOf(target.transform), team));
                added++;
            }

            _revealed.Clear();
            return added;
        }

        private int AddPings(Team? myTeam)
        {
            var markers = GameSignals.Pings.Value;
            if (markers == null)
            {
                return 0;
            }

            foreach (var marker in markers)
            {
                _contacts.Add(new MinimapContact(MinimapContactKind.Ping, marker.Position, 0f, myTeam));
            }

            return markers.Count;
        }

        private int AddObjectives(GameObject player, SideMember mine)
        {
            var added = 0;
            _objectives.Clear();
            MinimapObjectives.CopyShownTo(_objectives);
            foreach (var objective in _objectives)
            {
                added += AddObjective(objective.Position, null);
            }

            _objectives.Clear();

            foreach (var point in FindObjectsByType<ControlPointZone>(FindObjectsInactive.Exclude))
            {
                if (point.isActiveAndEnabled)
                {
                    added += AddObjective(point.transform.position, point.Owner);
                }
            }

            foreach (var flag in FindObjectsByType<FlagObject>(FindObjectsInactive.Exclude))
            {
                if (flag.IsVisible)
                {
                    added += AddObjective(flag.transform.position, TeamOf(flag.Side));
                }
            }

            foreach (var site in FindObjectsByType<BombSite>(FindObjectsInactive.Exclude))
            {
                if (site.isActiveAndEnabled)
                {
                    added += AddObjective(site.transform.position, null);
                }
            }

            foreach (var bomb in FindObjectsByType<BombObject>(FindObjectsInactive.Exclude))
            {
                // 敵が運んでいる爆弾を載せると運び手の居場所が分かってしまうので、地面にあるか自分か味方が運んでいるときだけ載せる。
                if (bomb.IsVisible && (bomb.Carrier == null || IsFriendly(bomb.Carrier, player, mine)))
                {
                    added += AddObjective(bomb.transform.position, null);
                }
            }

            return added;
        }

        private int AddObjective(Vector3 position, Team? team)
        {
            _contacts.Add(new MinimapContact(MinimapContactKind.Objective, position.ToNumerics(), 0f, team));
            return 1;
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                // 後から別の部品が出した値までは消さない。
                GameSignals.Minimap.ClearIfCurrent(_published);
            }

            _published = null;
            _loggedCounts = null;
        }

        private void LogCountsIfChanged(int allies, int enemies, int pings, int objectives)
        {
            if (_loggedCounts != null
                && _loggedCounts[0] == allies
                && _loggedCounts[1] == enemies
                && _loggedCounts[2] == pings
                && _loggedCounts[3] == objectives)
            {
                return;
            }

            _loggedCounts = new[] { allies, enemies, pings, objectives };
            Debug.Log(MinimapLog.Contacts(allies, enemies, pings, objectives), this);
        }

        private static bool IsFriendly(Transform carrier, GameObject player, SideMember mine)
        {
            return IsPartOf(carrier, player) || (mine != null && mine.IsAllyOf(SideMember.Of(carrier.gameObject)));
        }

        private static bool IsPartOf(Transform candidate, GameObject player)
        {
            return candidate.IsChildOf(player.transform);
        }

        private static Team? TeamOf(Side side)
        {
            return side.TryGetTeam(out var team) ? team : (Team?)null;
        }

        // 真上か真下を向いて方位が出ないときは、北を向いているとみなす。
        private static float HeadingOf(Transform transform)
        {
            return MinimapProjection.TryHeadingDegrees(transform.forward.ToNumerics(), out var degrees) ? degrees : 0f;
        }
    }
}
