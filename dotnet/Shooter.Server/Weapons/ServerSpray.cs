using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    /// <summary>
    /// 火炎のような吹き付け (<see cref="RoundDelivery.Spray"/>)。押した・離したの要求で吹き付けを持ち、毎 tick 円錐の中の相手にダメージを出す。
    /// </summary>
    /// <remarks>
    /// 噴射の間合いはビームと同じ数え方 (<see cref="TriggerModels"/> が火炎に使う <see cref="BeamState"/>) で数え、噴射のたびに弾 (熱) を 1 発使う。
    /// 見た目はビームと同じ <see cref="BeamStateMessage"/> で送り、クライアントは武器の届け方で火炎として出す。
    /// 始点と向きは押したときの要求にしか載らないので、その後の噴射は撃った人の体の今の位置と yaw に合わせて回す (上下の向きは押したときのまま)。
    /// </remarks>
    internal sealed class ServerSpray
    {
        private readonly Dictionary<int, Spray> _sprays = new Dictionary<int, Spray>();
        private readonly List<int> _scratch = new List<int>();
        private readonly List<ShotHit> _hits = new List<ShotHit>();
        private readonly BeamStateMessage _message = new BeamStateMessage();

        public ServerSpray(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        public int FiringCount
        {
            get
            {
                var count = 0;
                foreach (var spray in _sprays.Values)
                {
                    if (spray.State.IsFiring)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 受け入れた射撃 shot の弾 1 本。押した知らせ (<see cref="ServerShot.Trigger"/>) なら始める。
        /// 押した・離したを送らない撃ち手 (Bot) の射撃は、噴射 1 回ぶんとして当てる。
        /// </summary>
        public void Fire(in ServerShot shot, PlannedRound planned)
        {
            var spec = shot.Round.Spray ?? throw new ArgumentException("the round needs RoundSpec.Spray", nameof(shot));
            if (!Combat.Context.Bodies.TryGet(shot.ShooterId, out var body))
            {
                return;
            }

            var spray = SprayFor(shot.ShooterId, spec);
            if (shot.Trigger == TriggerEdge.None)
            {
                // 押していた噴射の間に届いた 1 回ぶんは、噴射の間合いより速く当てられないよう捨てる。弾は Fire の検査で使われている。
                if (spray.State.IsFiring)
                {
                    return;
                }

                spray.Aim(shot, planned, body, LastRecordedTick());
                spray.Ticks = 0;
                Emit(spray, body, shot.Tick);
                return;
            }

            if (shot.Trigger != TriggerEdge.Pressed)
            {
                return;
            }

            var restarted = spray.State.IsFiring;
            spray.Aim(shot, planned, body, LastRecordedTick());

            // 押した瞬間の検査 (ServerCombat.Fire) で 1 発使っているので、最初の噴射はそれで払う。押し直しの連打で間合いが来ていなければ次の噴射に回す。
            var ticks = spray.State.Start();
            if (!restarted)
            {
                Log(spray, "start", ShotRejection.None);
            }

            spray.Prepaid = ticks == 0;
            if (ticks > 0)
            {
                Emit(spray, body, shot.Tick);
            }
        }

        /// <summary>shooterId が引き金を離した。</summary>
        public void Release(int shooterId)
        {
            if (_sprays.TryGetValue(shooterId, out var spray) && spray.State.IsFiring)
            {
                Stop(spray, ShotRejection.None);
            }
        }

        public void Tick(float deltaSeconds)
        {
            _scratch.Clear();
            _scratch.AddRange(_sprays.Keys);
            foreach (var shooterId in _scratch)
            {
                // 噴射のダメージで試合が終わると Forget で並びから外れるので、そのたびに引き直す
                if (!_sprays.TryGetValue(shooterId, out var spray))
                {
                    continue;
                }

                var ticks = spray.State.Tick(deltaSeconds);
                if (!spray.State.IsFiring)
                {
                    continue;
                }

                if (!Combat.Context.Bodies.TryGet(shooterId, out var body))
                {
                    Stop(spray, ShotRejection.None);
                    continue;
                }

                for (var i = 0; i < ticks && spray.State.IsFiring; i++)
                {
                    if (!Pay(spray, out var rejection))
                    {
                        Stop(spray, rejection);
                        break;
                    }

                    Emit(spray, body, spray.ViewTick(LastRecordedTick()));
                }
            }
        }

        /// <summary>playerId が抜けた・倒れた・試合が始まった。</summary>
        public void Forget(int playerId)
        {
            if (!_sprays.TryGetValue(playerId, out var spray))
            {
                return;
            }

            if (spray.State.IsFiring)
            {
                Stop(spray, ShotRejection.None);
            }

            _sprays.Remove(playerId);
        }

        private Spray SprayFor(int shooterId, SpraySpec spec)
        {
            if (_sprays.TryGetValue(shooterId, out var spray) && ReferenceEquals(spray.Spec, spec))
            {
                return spray;
            }

            if (spray != null && spray.State.IsFiring)
            {
                Stop(spray, ShotRejection.WeaponMismatch);
            }

            spray = new Spray(shooterId, spec);
            _sprays[shooterId] = spray;
            return spray;
        }

        private bool Pay(Spray spray, out ShotRejection rejection)
        {
            var prepaid = spray.Prepaid;
            spray.Prepaid = false;
            return Combat.TryContinueFire(spray.ShooterId, spray.Shot.Weapon.Id, !prepaid, out rejection);
        }

        // 噴射 1 回。今の向きで円錐の中の相手を集め直し、相手ごとに着弾を渡して、火炎の届いた所までを全員に送る
        private void Emit(Spray spray, ServerBody body, float tick)
        {
            spray.Pose(body, out var origin, out var direction);
            var shot = spray.Shot;
            var round = shot.Round;
            var end = Combat.Context.World.Raycast(origin, direction, round.Range, ServerHitboxes.RaycastLayers, out var wall)
                ? wall.Point
                : origin + (direction * round.Range);
            _hits.Clear();
            Combat.Hitboxes.Spray(origin, direction, spray.Spec, round.Range, tick, spray.ShooterId, Combat.IsHittable, _hits);
            spray.Ticks++;
            Send(spray, origin, end, true);
            foreach (var hit in _hits)
            {
                // 火炎は相手ごとに向きが違うので、軸でなく相手へ向かう向き (ServerHitboxes.Spray が法線の逆に置いた向き) で当てる
                Combat.Impact.Apply(shot, -hit.Normal, hit);
            }
        }

        private void Stop(Spray spray, ShotRejection reason)
        {
            spray.State.Stop();
            spray.Prepaid = false;
            Send(spray, spray.LastStart, spray.LastEnd, false);
            Log(spray, "stop", reason);
            spray.Ticks = 0;
        }

        private void Send(Spray spray, Vector3 start, Vector3 end, bool active)
        {
            spray.LastStart = start;
            spray.LastEnd = end;
            _message.ShooterId = spray.ShooterId;
            _message.WeaponId = spray.Shot.Weapon?.Id ?? "";
            _message.Start = start;
            _message.End = end;
            _message.Active = active;

            // 撃った本人にも送るのは、熱や死亡でサーバーが噴射を止めたことを、予測で出している火炎に知らせるため
            Combat.SendToAll(_message);
        }

        private void Log(Spray spray, string what, ShotRejection reason)
        {
            Combat.Log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net spray {1} shooter={2} weapon={3} ticks={4} seconds={5:F2} reason={6}",
                ServerCombat.LogTag,
                what,
                spray.ShooterId,
                spray.Shot.Weapon?.Id ?? "",
                spray.Ticks,
                spray.State.FiringSeconds,
                reason));
        }

        private float LastRecordedTick()
        {
            var hitboxes = Combat.Hitboxes;
            return hitboxes.HasHistory ? hitboxes.LastRecordedTick : float.NaN;
        }

        private sealed class Spray : HeldFire
        {
            // 押した・離したと間合いの数え方はビームと同じなので、TriggerModels と同じく火炎の間隔を持つビームとして数える
            public Spray(int shooterId, SpraySpec spec)
                : base(shooterId, new BeamState(new BeamSpec(spec.TickSeconds)))
            {
                Spec = spec;
            }

            public SpraySpec Spec { get; }
        }
    }
}
