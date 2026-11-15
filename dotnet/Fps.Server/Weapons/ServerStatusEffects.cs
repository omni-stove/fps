using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 燃焼などの状態異常。相手ごとに Core の <see cref="StatusEffects"/> を持ち、毎 tick 進めてダメージを出す。
    /// かけた・延ばした・切れたときに <see cref="StatusEffectMessage"/> を全員に送り (切れたら残り 0)、クライアントは燃えている見た目を出す。
    /// </summary>
    internal sealed class ServerStatusEffects
    {
        public const string LogTag = "[StatusEffect]";

        private readonly SortedDictionary<int, StatusEffects> _byPlayer = new SortedDictionary<int, StatusEffects>();
        private readonly List<PendingDamage> _pending = new List<PendingDamage>();
        private readonly List<StatusEffectTick> _ticks = new List<StatusEffectTick>();
        private readonly List<string> _ended = new List<string>();
        private readonly List<int> _emptied = new List<int>();
        private readonly StatusEffectMessage _message = new StatusEffectMessage();

        public ServerStatusEffects(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        /// <summary>shot の弾で victimId に effect をかける。倒れている相手・かけてはいけない味方には何もしない。</summary>
        public void Afflict(in ServerShot shot, int victimId, StatusEffectSpec effect)
        {
            ArgumentNullException.ThrowIfNull(effect);

            if (!Combat.IsHittable(victimId) || !Combat.CanAfflict(shot.ShooterId, victimId))
            {
                return;
            }

            if (!_byPlayer.TryGetValue(victimId, out var effects))
            {
                effects = new StatusEffects();
                _byPlayer.Add(victimId, effects);
            }

            var active = effects.Afflict(effect, shot.ShooterId, shot.Weapon.Id);
            Combat.Context.Log.WriteLine(LogTag + " afflict source=" + Text(shot.ShooterId) + " victim=" + Text(victimId)
                + " kind=" + active.KindId + " weapon=" + active.WeaponId + " remaining=" + Text(active.RemainingSeconds));
            Send(victimId, active.KindId, active.RemainingSeconds);
        }

        /// <summary>playerId にかかっている種類 kindId の残り秒数。かかっていなければ 0。</summary>
        public float RemainingSeconds(int playerId, string kindId)
        {
            return _byPlayer.TryGetValue(playerId, out var effects) ? effects.RemainingSeconds(kindId) : 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (_byPlayer.Count == 0)
            {
                return;
            }

            // ダメージで倒れた人の状態異常を消すと並びが変わるので、進め終えてからまとめて与える。
            _pending.Clear();
            _emptied.Clear();
            foreach (var pair in _byPlayer)
            {
                var victimId = pair.Key;
                var effects = pair.Value;
                _ended.Clear();
                if (!Combat.IsHittable(victimId))
                {
                    effects.Clear(_ended);
                }
                else
                {
                    _ticks.Clear();
                    effects.Tick(deltaSeconds, _ticks, _ended);
                    foreach (var tick in _ticks)
                    {
                        _pending.Add(new PendingDamage(victimId, tick));
                    }
                }

                SendEnded(victimId);
                if (effects.Count == 0)
                {
                    _emptied.Add(victimId);
                }
            }

            foreach (var id in _emptied)
            {
                _byPlayer.Remove(id);
            }

            foreach (var damage in _pending)
            {
                if (!Combat.IsHittable(damage.VictimId))
                {
                    continue;
                }

                var tick = damage.Tick;
                var outcome = Combat.DamageOverTime(tick.SourceId, damage.VictimId, tick.Damage, tick.WeaponId);
                Combat.Context.Log.WriteLine(LogTag + " tick source=" + (tick.SourceId.HasValue ? Text(tick.SourceId.Value) : "none")
                    + " victim=" + Text(damage.VictimId) + " kind=" + tick.KindId + " dmg=" + Text(tick.Damage) + " outcome=" + outcome);
                if (!Combat.IsHittable(damage.VictimId))
                {
                    Forget(damage.VictimId);
                }
            }
        }

        /// <summary>playerId にかかっている状態異常を消す (抜けた・倒れた)。</summary>
        public void Forget(int playerId)
        {
            if (!_byPlayer.TryGetValue(playerId, out var effects))
            {
                return;
            }

            _byPlayer.Remove(playerId);
            _ended.Clear();
            effects.Clear(_ended);
            SendEnded(playerId);
        }

        /// <summary>試合の始まりに、すべての状態異常を消す。</summary>
        public void Clear()
        {
            foreach (var pair in _byPlayer)
            {
                _ended.Clear();
                pair.Value.Clear(_ended);
                SendEnded(pair.Key);
            }

            _byPlayer.Clear();
        }

        private void SendEnded(int playerId)
        {
            foreach (var kindId in _ended)
            {
                Send(playerId, kindId, 0f);
            }
        }

        private void Send(int targetId, string kindId, float remainingSeconds)
        {
            _message.TargetId = targetId;
            _message.KindId = kindId;
            _message.RemainingSeconds = remainingSeconds;
            Combat.SendToAll(_message);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private readonly struct PendingDamage
        {
            public PendingDamage(int victimId, StatusEffectTick tick)
            {
                VictimId = victimId;
                Tick = tick;
            }

            public int VictimId { get; }

            public StatusEffectTick Tick { get; }
        }
    }
}
