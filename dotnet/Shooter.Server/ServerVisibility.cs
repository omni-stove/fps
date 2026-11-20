using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 見えるかは人ではなく見る側のまとまり (チーム、FFA なら本人だけ) で決める。観戦している人は味方と同じ行を受け取るので、見る側を人ごとに分ける要は無い。
    // 生きている人を選ぶのは呼ぶ側に任せ、ここは目の位置だけを受け取る。誰が生きているかはルールのモジュールが決めるので、ここで二重に持たないため。
    internal sealed class ServerVisibility
    {
        // 真上・真下を見る線では横の向きが決まらないので、横に広げた点を見ない長さの下限
        private const float _minHorizontal = 1e-4f;

        private readonly Dictionary<(bool Solo, int Id, int Target), double> _lastSeen =
            new Dictionary<(bool Solo, int Id, int Target), double>();

        private readonly List<(bool Solo, int Id, int Target)> _forgotten = new List<(bool Solo, int Id, int Target)>();
        private IWorldCollision _world;

        public ServerVisibility(IWorldCollision world, AntiCheatTuning.ConcealSettings conceal)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Conceal = conceal ?? throw new ArgumentNullException(nameof(conceal));
        }

        public AntiCheatTuning.ConcealSettings Conceal { get; }

        public bool CanSee(Team team, IReadOnlyList<Vector3> eyes, int targetId, Vector3 targetCenter, Vector3 targetEye, double now)
        {
            ArgumentNullException.ThrowIfNull(eyes);
            var seen = false;
            for (var i = 0; i < eyes.Count && !seen; i++)
            {
                seen = SeenFrom(eyes[i], targetCenter, targetEye);
            }

            return Remember((false, (int)team, targetId), seen, now);
        }

        public bool CanSeeAlone(int viewerId, Vector3 eye, int targetId, Vector3 targetCenter, Vector3 targetEye, double now)
        {
            return Remember((true, viewerId, targetId), SeenFrom(eye, targetCenter, targetEye), now);
        }

        // 抜けた人の番号は後から入る人に使い回されるので、見る側としても見られる側としても覚えを消す
        public void Forget(int playerId)
        {
            _forgotten.Clear();
            foreach (var key in _lastSeen.Keys)
            {
                if (key.Target == playerId || (key.Solo && key.Id == playerId))
                {
                    _forgotten.Add(key);
                }
            }

            foreach (var key in _forgotten)
            {
                _lastSeen.Remove(key);
            }
        }

        // マップが替わると前のマップで見えた覚えは意味を持たないので、当たりと一緒に捨てる
        public void UseWorld(IWorldCollision world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lastSeen.Clear();
        }

        private bool Remember((bool Solo, int Id, int Target) key, bool seen, double now)
        {
            if (seen)
            {
                _lastSeen[key] = now;
                return true;
            }

            return _lastSeen.TryGetValue(key, out var last) && now - last <= Conceal.MarginSeconds;
        }

        private bool SeenFrom(Vector3 eye, Vector3 targetCenter, Vector3 targetEye)
        {
            var margin = Conceal.MarginDistance;
            if (Vector3.Distance(eye, targetCenter) - margin > Conceal.MaxDistance)
            {
                return false;
            }

            if (IsClear(eye, targetEye) || IsClear(eye, targetCenter))
            {
                return true;
            }

            if (margin <= 0f)
            {
                return false;
            }

            // 角から出てくる相手を出る前に送り始めるため、見る線に横と上へ余裕の距離だけずらした点も見る
            var toTarget = targetCenter - eye;
            var horizontal = new Vector3(toTarget.X, 0f, toTarget.Z);
            if (horizontal.Length() >= _minHorizontal)
            {
                var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, horizontal)) * margin;
                if (IsClear(eye, targetCenter + side) || IsClear(eye, targetCenter - side)
                    || IsClear(eye, targetEye + side) || IsClear(eye, targetEye - side))
                {
                    return true;
                }
            }

            return IsClear(eye, targetEye + new Vector3(0f, margin, 0f));
        }

        private bool IsClear(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var distance = offset.Length();
            return distance < 1e-4f || !_world.Raycast(from, offset / distance, distance, ServerHitboxes.RaycastLayers, out _);
        }
    }
}
