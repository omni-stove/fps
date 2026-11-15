using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Runtime
{
    public sealed class ProjectileBody : MonoBehaviour
    {
        public const float DefaultStuckSeconds = 10f;

        public const string LogTag = "[Combat]";

        public const string RodName = "ProjectileRod";

        public const float RodLength = 0.6f;

        public const float RodThickness = 0.02f;

        private ShotRequest _request;
        private ShotWorldCollision _world;
        private BallisticFlight _flight;
        private AudioSource _audio;
        private Collider _stuckIn;
        private float _stuckLeft;

        public BallisticFlight Flight => _flight;

        public bool IsStuck => _flight != null && _flight.State == BallisticState.Stuck;

        public static ProjectileBody Launch(ShotRequest request, PlannedRound round)
        {
            var projectile = request.Round.Projectile
                ?? throw new ArgumentException("the round needs RoundSpec.Projectile", nameof(request));
            var world = new ShotWorldCollision(request.ShooterRoot);

            // 的かどうかは Unity の Collider から引くので、直前の Raycast の当たり (ShotWorldCollision.LastHit) を見る。
            var flight = new BallisticFlight(
                projectile,
                request.Round.Homing,
                world,
                request.LayerMask,
                round.Origin,
                round.Direction,
                Physics.gravity.ToNumerics(),
                _ => world.LastHit.collider != null && Hitbox.FindFor(world.LastHit.collider) != null);

            var look = request.ProjectileLook;
            var visual = look != null && look.Visual != null ? Instantiate(look.Visual) : BuildRod();
            visual.name = "Projectile-" + request.WeaponId;

            // 見た目の当たりが残ると、次に撃った弾や周りの Raycast がそれに当たるので切る。
            foreach (var collider in visual.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            var body = visual.AddComponent<ProjectileBody>();
            body._request = request;
            body._world = world;
            body._flight = flight;
            body.Place();
            if (look != null && look.Sound != null)
            {
                body._audio = visual.AddComponent<AudioSource>();
                body._audio.clip = look.Sound;
                body._audio.loop = true;
                body._audio.spatialBlend = 1f;
                body._audio.Play();
            }

            return body;
        }

        private void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        internal void Tick(float deltaSeconds)
        {
            if (_flight == null)
            {
                return;
            }

            if (_flight.IsDone)
            {
                _stuckLeft -= deltaSeconds;

                // 刺さった物が壊れて消えたら、宙に浮いた弾を残さない。
                if (_stuckLeft <= 0f || _stuckIn == null || !_stuckIn.enabled)
                {
                    Despawn();
                }

                return;
            }

            var step = _flight.Step(deltaSeconds);
            Place();
            switch (step.State)
            {
                case BallisticState.Flying:
                    if (step.HasHit)
                    {
                        var bounce = _world.LastHit;
                        CombatEvents.RaiseImpact(bounce.point, bounce.normal, bounce.collider);
                    }

                    break;
                case BallisticState.HitTarget:
                case BallisticState.Stopped:
                    Land();
                    Despawn();
                    break;
                case BallisticState.Stuck:
                    Land();
                    StopSound();
                    _stuckIn = _world.LastHit.collider;
                    _stuckLeft = _request.ProjectileLook != null ? _request.ProjectileLook.StuckSeconds : DefaultStuckSeconds;
                    if (_stuckLeft <= 0f)
                    {
                        Despawn();
                    }

                    break;
                case BallisticState.Expired:
                    Debug.Log(FormatLog("Expired", null, 0), this);
                    Despawn();
                    break;
            }
        }

        private void Land()
        {
            var hit = _world.LastHit;

            // 距離減衰は撃った所から飛んだ道のりで決めるので、最後の 1 区間の長さを置き換える。
            hit.distance = _flight.Distance;
            var velocity = _flight.Velocity.ToUnity();
            var direction = velocity.sqrMagnitude > 1e-12f ? velocity.normalized : transform.forward;
            var outcome = HitscanDelivery.Land(_request, hit, direction);
            Debug.Log(FormatLog(_flight.State.ToString(), outcome.Owner, outcome.Damage), this);
            _request.Landed?.Invoke(outcome);
        }

        private string FormatLog(string end, Damageable owner, int damage)
        {
            var position = _flight.Position;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} projectile weapon={1} end={2} pos=({3:F2},{4:F2},{5:F2}) dist={6:F1} age={7:F2} hit={8} dmg={9}",
                LogTag,
                _request.WeaponId,
                end,
                position.X,
                position.Y,
                position.Z,
                _flight.Distance,
                _flight.Age,
                owner != null ? owner.Id : "none",
                damage);
        }

        private void Place()
        {
            transform.position = _flight.Position.ToUnity();
            var velocity = _flight.Velocity.ToUnity();
            if (velocity.sqrMagnitude > 1e-8f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }
        }

        private void StopSound()
        {
            if (_audio != null)
            {
                _audio.Stop();
            }
        }

        private void Despawn()
        {
            _flight = null;
            DestroySafely(gameObject);
        }

        // 見た目の参照が空のときだけ使う。円柱の軸 (Y) を飛ぶ向き (Z) に寝かせた細い棒にする。
        private static GameObject BuildRod()
        {
            var root = new GameObject();
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = RodName;
            DestroySafely(rod.GetComponent<Collider>());
            rod.transform.SetParent(root.transform, false);
            rod.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rod.transform.localPosition = new Vector3(0f, 0f, -RodLength / 2f);
            rod.transform.localScale = new Vector3(RodThickness, RodLength / 2f, RodThickness);
            return root;
        }

        // 再生していない EditMode のテストでも弾を消せるよう、Destroy が使えないときは即座に消す。
        private static void DestroySafely(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
