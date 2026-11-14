using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke に足す、爆発する置き物 (<see cref="ExplosivePropRangeFixture"/>) の手順。
    /// <list type="number">
    /// <item>樽 A の正面から、プレイヤーの今の武器の値で弾を撃つ (<see cref="Hitscan.Fire"/>)。爆発するまで 1 フレームに 1 発ずつ撃つ。</item>
    /// <item>樽 A が連鎖でなく爆発し (<c>[Explosive] exploded id=Barrel_A chained=false</c>)、隣の標的ダミーが倒れる (<c>[Combat] target died</c>)。</item>
    /// <item>樽 B が連鎖として、樽 A から <see cref="ExplosivePropSpec.ChainDelaySeconds"/> 以上遅れて爆発する。</item>
    /// <item>ガソリン缶が燃え始め (<c>[Explosive] ignited id=GasCan</c>)、<see cref="ExplosivePropSpec.BurnSeconds"/> 以上燃えてから爆発する。</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// batchmode では照準を樽へ向けられないので、プレイヤーの <see cref="WeaponController.Fire"/> ではなく、敵の弾と同じ
    /// <see cref="Hitscan.Fire"/> で撃つ。弾が置き物に届くまでの道筋 (Raycast → <see cref="StructureDamage.Apply"/>) は同じ。
    /// </remarks>
    public sealed class ExplosivePropSmokeStep : ITpsActionSmokeStep
    {
        /// <summary>樽 A の正面のどれだけ手前から撃つか (m)。</summary>
        public const float ShotDistance = 4f;

        /// <summary>樽 A が爆発するまでに撃つ弾の数の上限。</summary>
        public const int MaxShots = 10;

        // 爆発までの時間はフレームの deltaTime を足して数えるので、Time.time の差は浮動小数の誤差だけ短く見えることがある。
        private const float TimeTolerance = 0.01f;

        private static readonly string _dummyDiedLog = $"{Damageable.LogTag} target died id={ExplosivePropRangeFixture.DummyId}";
        private static readonly string _firstExplodedLog =
            $"{ExplosivePropLog.ExplodedPrefix}id={ExplosivePropRangeFixture.FirstBarrelName} chained=false ";
        private static readonly string _secondExplodedLog =
            $"{ExplosivePropLog.ExplodedPrefix}id={ExplosivePropRangeFixture.SecondBarrelName} chained=true ";
        private static readonly string _gasCanIgnitedLog = $"{ExplosivePropLog.Tag} ignited id={ExplosivePropRangeFixture.GasCanName} ";
        private static readonly string _gasCanExplodedLog = $"{ExplosivePropLog.ExplodedPrefix}id={ExplosivePropRangeFixture.GasCanName} ";

        private ExplosiveProp _first;
        private ExplosiveProp _second;
        private ExplosiveProp _gasCan;
        private Damageable _dummy;
        private bool _started;
        private int _shots;
        private float _firstExplodedAt;
        private float _secondExplodedAt;
        private float _gasCanTriggeredAt;
        private float _gasCanExplodedAt;
        private bool _secondChained;
        private bool _dummyDiedInFirstBlast;
        private bool _sawDummyDied;
        private bool _sawFirstExploded;
        private bool _sawSecondExploded;
        private bool _sawGasCanIgnited;
        private bool _sawGasCanExploded;

        public int Order => 0;

        public string Name => "explosive props";

        /// <summary>撃ってからガソリン缶が燃え尽きるまでを見込む。</summary>
        public float TimeoutSeconds => 10f;

        public void Reset()
        {
            Unsubscribe();
            _first = null;
            _second = null;
            _gasCan = null;
            _dummy = null;
            _started = false;
            _shots = 0;
            _firstExplodedAt = -1f;
            _secondExplodedAt = -1f;
            _gasCanTriggeredAt = -1f;
            _gasCanExplodedAt = -1f;
            _secondChained = false;
            _dummyDiedInFirstBlast = false;
            _sawDummyDied = false;
            _sawFirstExploded = false;
            _sawSecondExploded = false;
            _sawGasCanIgnited = false;
            _sawGasCanExploded = false;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (string.Equals(condition, _dummyDiedLog, StringComparison.Ordinal))
            {
                _sawDummyDied = true;
            }
            else if (condition.StartsWith(_firstExplodedLog, StringComparison.Ordinal))
            {
                _sawFirstExploded = true;
            }
            else if (condition.StartsWith(_secondExplodedLog, StringComparison.Ordinal))
            {
                _sawSecondExploded = true;
            }
            else if (condition.StartsWith(_gasCanIgnitedLog, StringComparison.Ordinal))
            {
                _sawGasCanIgnited = true;
            }
            else if (condition.StartsWith(_gasCanExplodedLog, StringComparison.Ordinal))
            {
                _sawGasCanExploded = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            if (!_started)
            {
                problem = Start();
                return false;
            }

            if (_firstExplodedAt < 0f)
            {
                problem = Shoot(world);
                return false;
            }

            if (_secondExplodedAt < 0f || _gasCanExplodedAt < 0f)
            {
                problem = null;
                return false;
            }

            problem = Check();
            return problem == null;
        }

        public void Cleanup()
        {
            Unsubscribe();
        }

        private string Start()
        {
            var root = GameObject.Find(ExplosivePropRangeFixture.RootName);
            if (root == null)
            {
                return $"the scene has no '{ExplosivePropRangeFixture.RootName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _first = FindProp(root, ExplosivePropRangeFixture.FirstBarrelName);
            _second = FindProp(root, ExplosivePropRangeFixture.SecondBarrelName);
            _gasCan = FindProp(root, ExplosivePropRangeFixture.GasCanName);
            var dummy = root.transform.Find(ExplosivePropRangeFixture.DummyId);
            _dummy = dummy != null ? dummy.GetComponent<Damageable>() : null;
            if (_first == null || _second == null || _gasCan == null || _dummy == null)
            {
                return $"'{ExplosivePropRangeFixture.RootName}' is missing a prop or the dummy; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (_first.IsTriggered || _second.IsTriggered || _gasCan.IsTriggered || _dummy.IsDead)
            {
                return "a prop was set off or the dummy was dead before the shot";
            }

            _first.Exploded += OnFirstExploded;
            _second.Exploded += OnSecondExploded;
            _gasCan.Triggered += OnGasCanTriggered;
            _gasCan.Exploded += OnGasCanExploded;
            _started = true;
            return null;
        }

        private string Shoot(BootstrapWorld world)
        {
            if (_first == null)
            {
                return $"'{ExplosivePropRangeFixture.FirstBarrelName}' disappeared without exploding";
            }

            // 撃った弾で耐久が 0 になっても爆発は次のフレームなので、待っている間は撃ち足さない。
            if (_first.IsTriggered)
            {
                return null;
            }

            if (_shots >= MaxShots)
            {
                return $"'{ExplosivePropRangeFixture.FirstBarrelName}' did not go off after {MaxShots.ToString(CultureInfo.InvariantCulture)} shots (hp {_first.Health.ToString(CultureInfo.InvariantCulture)})";
            }

            var spec = world.Weapon != null && world.Weapon.Weapon != null ? world.Weapon.Weapon.Spec : null;
            if (spec == null || spec.Damage <= 0)
            {
                return "the player has no weapon that deals damage to shoot the barrel with";
            }

            var target = _first.transform.position;
            var origin = target + (Vector3.back * ShotDistance);
            var result = Hitscan.Fire(
                origin,
                target - origin,
                Mathf.Max(spec.Range, ShotDistance * 2f),
                spec.Damage,
                spec.Falloff,
                world.Player.transform,
                0f,
                spec.Id);
            _shots++;
            var collider = result.RaycastHit.collider;
            if (!result.Hit || collider == null || collider.GetComponentInParent<ExplosiveProp>() != _first)
            {
                return $"the shot from {origin} did not hit '{ExplosivePropRangeFixture.FirstBarrelName}'";
            }

            return null;
        }

        private string Check()
        {
            if (!_dummyDiedInFirstBlast)
            {
                return $"'{ExplosivePropRangeFixture.DummyId}' next to '{ExplosivePropRangeFixture.FirstBarrelName}' survived its explosion";
            }

            if (!_secondChained)
            {
                return $"'{ExplosivePropRangeFixture.SecondBarrelName}' exploded but not as a chain";
            }

            var chainDelay = _secondExplodedAt - _firstExplodedAt;
            var expectedChain = ExplosivePropRangeFixture.BarrelSpec.ChainDelaySeconds;
            if (chainDelay < expectedChain - TimeTolerance)
            {
                return $"'{ExplosivePropRangeFixture.SecondBarrelName}' exploded {Seconds(chainDelay)} after '{ExplosivePropRangeFixture.FirstBarrelName}' (expected at least {Seconds(expectedChain)})";
            }

            if (_gasCanTriggeredAt < 0f)
            {
                return $"'{ExplosivePropRangeFixture.GasCanName}' exploded without catching fire first";
            }

            var burned = _gasCanExplodedAt - _gasCanTriggeredAt;
            var expectedBurn = ExplosivePropRangeFixture.GasCanSpec.BurnSeconds;
            if (burned < expectedBurn - TimeTolerance)
            {
                return $"'{ExplosivePropRangeFixture.GasCanName}' burned for {Seconds(burned)} (expected at least {Seconds(expectedBurn)})";
            }

            if (!_sawFirstExploded || !_sawSecondExploded || !_sawDummyDied || !_sawGasCanIgnited || !_sawGasCanExploded)
            {
                return $"missing logs (first exploded={_sawFirstExploded}, second chained={_sawSecondExploded}, dummy died={_sawDummyDied}, gas can ignited={_sawGasCanIgnited}, gas can exploded={_sawGasCanExploded})";
            }

            return null;
        }

        private void OnFirstExploded(ExplosiveProp prop, ExplosivePropExplosion explosion)
        {
            _firstExplodedAt = Time.time;
            _dummyDiedInFirstBlast = _dummy != null && _dummy.IsDead && explosion.Killed > 0;
        }

        private void OnSecondExploded(ExplosiveProp prop, ExplosivePropExplosion explosion)
        {
            _secondExplodedAt = Time.time;
            _secondChained = explosion.Chained;
        }

        private void OnGasCanTriggered(ExplosiveProp prop, ExplosiveChargeStep step)
        {
            if (step == ExplosiveChargeStep.Ignited)
            {
                _gasCanTriggeredAt = Time.time;
            }
        }

        private void OnGasCanExploded(ExplosiveProp prop, ExplosivePropExplosion explosion)
        {
            _gasCanExplodedAt = Time.time;
        }

        private void Unsubscribe()
        {
            // 爆発した置き物は破棄済み (Unity の == では null) でも、C# のイベントは外せる。
            if (!ReferenceEquals(_first, null))
            {
                _first.Exploded -= OnFirstExploded;
            }

            if (!ReferenceEquals(_second, null))
            {
                _second.Exploded -= OnSecondExploded;
            }

            if (!ReferenceEquals(_gasCan, null))
            {
                _gasCan.Triggered -= OnGasCanTriggered;
                _gasCan.Exploded -= OnGasCanExploded;
            }
        }

        private static ExplosiveProp FindProp(GameObject root, string name)
        {
            var child = root.transform.Find(name);
            return child != null ? child.GetComponent<ExplosiveProp>() : null;
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";
        }
    }
}
