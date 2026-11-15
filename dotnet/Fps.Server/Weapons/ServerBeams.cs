using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// ビーム (<see cref="RoundDelivery.Beam"/>)。押した・離したの要求で照射を持ち、毎 tick 判定してダメージを出す。
    /// 照射の間合いはサーバーの <see cref="BeamState"/> が数え、照射のたびに弾 (熱) を 1 発使い、当てた所までの線を <see cref="BeamStateMessage"/> で送る。
    /// </summary>
    /// <remarks>
    /// 始点と向きは押したときの要求にしか載らないので、その後の照射は撃った人の体の今の位置と yaw に合わせて回す (上下の向きは押したときのまま)。
    /// 当たりは押したときの遅れ (サーバーの tick と撃った人が見ていた tick の差) だけ巻き戻した相手に当てる。
    /// </remarks>
    internal sealed class ServerBeams
    {
        private readonly Dictionary<int, HeldFire> _beams = new Dictionary<int, HeldFire>();
        private readonly List<int> _scratch = new List<int>();
        private readonly List<ShotHit> _hits = new List<ShotHit>();
        private readonly BeamStateMessage _message = new BeamStateMessage();

        public ServerBeams(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        public int FiringCount
        {
            get
            {
                var count = 0;
                foreach (var beam in _beams.Values)
                {
                    if (beam.State.IsFiring)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 受け入れた射撃 shot の弾 1 本。押した知らせ (<see cref="ServerShot.Trigger"/>) なら始める。
        /// 押した・離したを送らない撃ち手 (Bot) の射撃は、照射 1 回ぶんとして当てる。
        /// </summary>
        public void Fire(in ServerShot shot, PlannedRound planned)
        {
            var spec = shot.Round.Beam ?? throw new ArgumentException("the round needs RoundSpec.Beam", nameof(shot));
            if (!Combat.Context.Bodies.TryGet(shot.ShooterId, out var body))
            {
                return;
            }

            var beam = BeamFor(shot.ShooterId, spec);
            if (shot.Trigger == TriggerEdge.None)
            {
                // 押していた照射の間に届いた 1 回ぶんは、照射の間合いより速く当てられないよう捨てる。弾は Fire の検査で使われている。
                if (beam.State.IsFiring)
                {
                    return;
                }

                // 押した・離したの間の待ちを数える BeamState を通すのは、離した直後に None の要求を重ねて間合いより速く当てられないようにするため
                var allowed = beam.State.Start();
                beam.State.Stop();
                if (allowed == 0)
                {
                    return;
                }

                beam.Aim(shot, planned, body, LastRecordedTick());
                beam.Ticks = 0;
                Emit(beam, body, shot.Tick);
                return;
            }

            if (shot.Trigger != TriggerEdge.Pressed)
            {
                return;
            }

            var restarted = beam.State.IsFiring;
            beam.Aim(shot, planned, body, LastRecordedTick());

            // 押した瞬間の検査 (ServerCombat.Fire) で 1 発使っているので、最初の照射はそれで払う。押し直しの連打で間合いが来ていなければ次の照射に回す。
            var ticks = beam.State.Start();
            if (!restarted)
            {
                Log(beam, "start", ShotRejection.None);
            }

            beam.Prepaid = ticks == 0;
            if (ticks > 0)
            {
                Emit(beam, body, shot.Tick);
            }
        }

        /// <summary>shooterId が引き金を離した。</summary>
        public void Release(int shooterId)
        {
            if (_beams.TryGetValue(shooterId, out var beam) && beam.State.IsFiring)
            {
                Stop(beam, ShotRejection.None);
            }
        }

        public void Tick(float deltaSeconds)
        {
            _scratch.Clear();
            _scratch.AddRange(_beams.Keys);
            foreach (var shooterId in _scratch)
            {
                // 照射のダメージで試合が終わると Forget で並びから外れるので、そのたびに引き直す
                if (!_beams.TryGetValue(shooterId, out var beam))
                {
                    continue;
                }

                var ticks = beam.State.Tick(deltaSeconds);
                if (!beam.State.IsFiring)
                {
                    continue;
                }

                if (!Combat.Context.Bodies.TryGet(shooterId, out var body))
                {
                    Stop(beam, ShotRejection.None);
                    continue;
                }

                for (var i = 0; i < ticks && beam.State.IsFiring; i++)
                {
                    if (!Pay(beam, out var rejection))
                    {
                        Stop(beam, rejection);
                        break;
                    }

                    Emit(beam, body, beam.ViewTick(LastRecordedTick()));
                }
            }
        }

        /// <summary>playerId が抜けた・倒れた・試合が始まった。</summary>
        public void Forget(int playerId)
        {
            if (!_beams.TryGetValue(playerId, out var beam))
            {
                return;
            }

            if (beam.State.IsFiring)
            {
                Stop(beam, ShotRejection.None);
            }

            _beams.Remove(playerId);
        }

        private HeldFire BeamFor(int shooterId, BeamSpec spec)
        {
            if (_beams.TryGetValue(shooterId, out var beam) && ReferenceEquals(beam.State.Spec, spec))
            {
                return beam;
            }

            if (beam != null && beam.State.IsFiring)
            {
                Stop(beam, ShotRejection.WeaponMismatch);
            }

            beam = new HeldFire(shooterId, new BeamState(spec));
            _beams[shooterId] = beam;
            return beam;
        }

        private bool Pay(HeldFire beam, out ShotRejection rejection)
        {
            var prepaid = beam.Prepaid;
            beam.Prepaid = false;
            return Combat.TryContinueFire(beam.ShooterId, beam.Shot.Weapon.Id, !prepaid, out rejection);
        }

        // 照射 1 回。今の向きで当たり直し、当たりを着弾に渡して、当てた所までの線を全員に送る
        private void Emit(HeldFire beam, ServerBody body, float tick)
        {
            beam.Pose(body, out var origin, out var direction);

            // 押したときの始点のずれを残して回すので、振り向きや壁寄りで始点が壁の向こうに回り込む。照射のたびに押したときと同じ確かめをやり直す
            if (Combat.IsOriginBlocked(body.EyePosition, origin))
            {
                if (beam.State.IsFiring)
                {
                    Stop(beam, ShotRejection.OriginBlocked);
                }

                return;
            }

            var shot = beam.Shot;
            var round = shot.Round;
            _hits.Clear();
            var found = Combat.Hitboxes.Cast(origin, direction, round.Radius, round.Range, 1, tick, beam.ShooterId, Combat.IsHittable, _hits);
            var end = found ? _hits[0].Point : origin + (direction * round.Range);
            beam.Ticks++;
            Send(beam, origin, end, true);
            if (found)
            {
                var hit = _hits[0];
                Combat.Impact.Apply(shot, direction, hit);
            }
        }

        private void Stop(HeldFire beam, ShotRejection reason)
        {
            beam.State.Stop();
            beam.Prepaid = false;
            Send(beam, beam.LastStart, beam.LastEnd, false);
            Log(beam, "stop", reason);
            beam.Ticks = 0;
        }

        private void Send(HeldFire beam, Vector3 start, Vector3 end, bool active)
        {
            beam.LastStart = start;
            beam.LastEnd = end;
            _message.ShooterId = beam.ShooterId;
            _message.WeaponId = beam.Shot.Weapon?.Id ?? "";
            _message.Start = start;
            _message.End = end;
            _message.Active = active;

            // 撃った本人にも送るのは、熱や死亡でサーバーが照射を止めたことを、予測で出している線に知らせるため
            Combat.SendToAll(_message);
        }

        private void Log(HeldFire beam, string what, ShotRejection reason)
        {
            Combat.Log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net beam {1} shooter={2} weapon={3} ticks={4} seconds={5:F2} reason={6}",
                ServerCombat.LogTag,
                what,
                beam.ShooterId,
                beam.Shot.Weapon?.Id ?? "",
                beam.Ticks,
                beam.State.FiringSeconds,
                reason));
        }

        private float LastRecordedTick()
        {
            var hitboxes = Combat.Hitboxes;
            return hitboxes.HasHistory ? hitboxes.LastRecordedTick : float.NaN;
        }
    }
}
