using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// 飛ぶ弾 (<see cref="RoundDelivery.Projectile"/>) の見た目。当たりはサーバーが決めるので、ここでは見せるだけ。
    /// 発射の知らせ (<see cref="ProjectileSpawnedMessage"/>) か、それが落ちたときはスナップショットで弾を出し、
    /// 知らせの間は <see cref="BallisticFlight"/> で世界の当たりまで進め、スナップショットとのずれは少しずつ寄せる。
    /// 終わりの知らせ (<see cref="ProjectileEndedMessage"/>) で消すか刺さったままにする。
    /// </summary>
    /// <remarks>
    /// 撃った本人は撃った瞬間に予測の弾を出し、同じ武器のサーバーの弾がスナップショットに載ったら古い順に位置と速さが最も近い予測の弾がその Id を引き継ぐ。
    /// 撃った本人には発射の知らせが来ないので、一度も知らない Id の終わりの知らせは、まだ引き継いでいない自分の予測の弾の終わりとみなす。
    /// 予測の弾は撃った時から進めているので、往復の分だけ遅れたスナップショットには寄せない (寄せると毎回引き戻される)。
    /// 誘導する弾だけは手元で曲がり方が分からないので、引き継いだ後はスナップショットに寄せる。
    /// </remarks>
    public sealed class ClientProjectiles
    {
        /// <summary>予測の弾がサーバーの弾を待つ秒数と、手元で止まった弾が終わりの知らせを待つ秒数。</summary>
        public const float DefaultPendingSeconds = 1f;

        /// <summary>スナップショットとのずれを寄せ切る目安の秒数。</summary>
        public const float DefaultCorrectionSeconds = 0.1f;

        /// <summary>これより離れていたら寄せずに跳ぶ。</summary>
        public const float DefaultSnapDistance = 4f;

        /// <summary>武器が分からない弾を、終わりの知らせもスナップショットも無いまま残しておく最長の秒数。</summary>
        public const float UnknownWeaponLifetimeSeconds = 10f;

        private const int _retiredCapacity = 256;

        private readonly Func<string, WeaponSpec> _findWeapon;
        private readonly float _pendingSeconds;
        private readonly float _correctionSeconds;
        private readonly float _snapDistance;
        private readonly Dictionary<int, Projectile> _byId = new Dictionary<int, Projectile>();
        private readonly List<Projectile> _predicted = new List<Projectile>();

        // 終わりの知らせの後に、まだその弾が載った古いスナップショットが届いても出し直さないよう、スナップショットから消えるまで覚えておく。
        private readonly HashSet<int> _ended = new HashSet<int>();
        private readonly HashSet<int> _listed = new HashSet<int>();
        private readonly List<int> _scratch = new List<int>();

        // 見せ終えた弾の Id。遅れて届いた終わりの知らせを、自分の予測の弾の終わりと取り違えないために覚える。
        private readonly HashSet<int> _retired = new HashSet<int>();
        private readonly Queue<int> _retiredOrder = new Queue<int>();
        private IClientProjectileViews _views;
        private IWorldCollision _world;
        private int _layerMask;
        private Vector3 _gravity;
        private int _localPlayerId;

        public ClientProjectiles(
            Func<string, WeaponSpec> findWeapon,
            float pendingSeconds = DefaultPendingSeconds,
            float correctionSeconds = DefaultCorrectionSeconds,
            float snapDistance = DefaultSnapDistance)
        {
            _findWeapon = findWeapon ?? throw new ArgumentNullException(nameof(findWeapon));
            _pendingSeconds = Positive(pendingSeconds, nameof(pendingSeconds));
            _correctionSeconds = Positive(correctionSeconds, nameof(correctionSeconds));
            _snapDistance = Positive(snapDistance, nameof(snapDistance));
        }

        public bool IsAttached => _views != null;

        public int Count => _byId.Count + _predicted.Count;

        public void Attach(IClientProjectileViews views, IWorldCollision world, int layerMask, Vector3 gravity, int localPlayerId)
        {
            Detach();
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _layerMask = layerMask;
            _gravity = gravity;
            _localPlayerId = localPlayerId;
        }

        public void Detach()
        {
            foreach (var projectile in _byId.Values)
            {
                projectile.Remove();
            }

            foreach (var projectile in _predicted)
            {
                projectile.Remove();
            }

            _byId.Clear();
            _predicted.Clear();
            _ended.Clear();
            _retired.Clear();
            _retiredOrder.Clear();
            _views = null;
            _world = null;
        }

        public void Spawned(ProjectileSpawnedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!IsAttached || _ended.Contains(message.ProjectileId) || _byId.ContainsKey(message.ProjectileId))
            {
                return;
            }

            Adopt(message.ProjectileId, message.HasShooter, message.ShooterId, message.WeaponId, message.Position, message.Velocity);
        }

        public void Ended(ProjectileEndedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!IsAttached)
            {
                return;
            }

            var id = message.ProjectileId;
            _ended.Add(id);
            if (_byId.TryGetValue(id, out var projectile))
            {
                Retire(id);
            }
            else if (!_retired.Contains(id) && _predicted.Count > 0)
            {
                projectile = _predicted[0];
                _predicted.RemoveAt(0);
                Remember(id);
            }
            else
            {
                return;
            }

            if (message.Reason == ProjectileEndReason.HitWorld && projectile.Sticks)
            {
                projectile.Stick(message.Position);
            }
            else
            {
                projectile.Remove();
            }
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!IsAttached || !snapshot.IsComplete)
            {
                return;
            }

            _listed.Clear();
            var rows = snapshot.Projectiles;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                _listed.Add(row.ProjectileId);
                if (_ended.Contains(row.ProjectileId))
                {
                    continue;
                }

                if (_byId.TryGetValue(row.ProjectileId, out var projectile))
                {
                    projectile.Listed = true;
                    if (projectile.FollowsServer)
                    {
                        projectile.Correct(row.Position, row.Velocity, _snapDistance);
                    }

                    continue;
                }

                var adopted = Adopt(row.ProjectileId, row.HasShooter, row.ShooterId, row.WeaponId, row.Position, row.Velocity);
                if (adopted != null)
                {
                    adopted.Listed = true;
                }
            }

            _ended.IntersectWith(_listed);

            // 一度スナップショットに載った弾が消えたら、サーバーではもう飛んでいない。
            // ただしスナップショットは Unreliable、終わりの知らせは Reliable で再送だと後から届くので、その場では手放さず Tick で待ってから手放す。
            foreach (var pair in _byId)
            {
                if (pair.Value.Listed && !_listed.Contains(pair.Key))
                {
                    pair.Value.Missing = true;
                }
                else if (_listed.Contains(pair.Key))
                {
                    pair.Value.Missing = false;
                    pair.Value.MissingFor = 0f;
                }
            }
        }

        public void LocalFired(FireRequestMessage request, WeaponSpec spec)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var round = RoundFor(spec, request.AmmoTypeIndex);
            if (!IsAttached || round.Delivery != RoundDelivery.Projectile || request.Trigger != TriggerEdge.None)
            {
                return;
            }

            // サーバーと同じ並べ方でペレットごとに出す。腰だめや移動の散らばりは手元で同じに出せないので掛けない。
            var plan = ShotPlanner.Plan(round, request.Origin, request.Direction, 0f, _localPlayerId, request.ShotIndex);
            foreach (var planned in plan.Rounds)
            {
                var velocity = SafeDirection(planned.Direction) * round.Projectile.Speed;
                var projectile = Create(spec.Id, round, planned.Origin, velocity, predicted: true);
                if (projectile != null)
                {
                    _predicted.Add(projectile);
                }
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsAttached || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _scratch.Clear();
            foreach (var pair in _byId)
            {
                var alive = pair.Value.Tick(deltaSeconds, _correctionSeconds);
                if (pair.Value.Missing)
                {
                    pair.Value.MissingFor += deltaSeconds;
                }

                if (!alive || (pair.Value.Missing && pair.Value.MissingFor >= _pendingSeconds))
                {
                    _scratch.Add(pair.Key);
                }
            }

            foreach (var id in _scratch)
            {
                _byId[id].Release();
                Retire(id);
            }

            for (var i = _predicted.Count - 1; i >= 0; i--)
            {
                var projectile = _predicted[i];
                projectile.Unclaimed += deltaSeconds;

                // サーバーに弾かれた射撃の弾をいつまでも飛ばさない。
                if (!projectile.Tick(deltaSeconds, _correctionSeconds) || projectile.Unclaimed >= _pendingSeconds)
                {
                    projectile.Release();
                    _predicted.RemoveAt(i);
                }
            }
        }

        private void Retire(int id)
        {
            _byId.Remove(id);
            Remember(id);
        }

        private void Remember(int id)
        {
            if (!_retired.Add(id))
            {
                return;
            }

            _retiredOrder.Enqueue(id);
            if (_retiredOrder.Count > _retiredCapacity)
            {
                _retired.Remove(_retiredOrder.Dequeue());
            }
        }

        private Projectile Adopt(int id, bool hasShooter, int shooterId, string weaponId, Vector3 position, Vector3 velocity)
        {
            if (hasShooter && shooterId == _localPlayerId)
            {
                // 本人にはサーバーが発射の知らせを送らず、サーバーの弾がスナップショットに載らないまま終わることもある。
                // 古い順に渡すと、そうして残った前の射撃の予測の弾に次の射撃の Id を渡してしまうので、位置と速さが最も近い予測の弾に渡す。
                var best = -1;
                var bestScore = float.MaxValue;
                for (var i = 0; i < _predicted.Count; i++)
                {
                    var candidate = _predicted[i];
                    if (!string.Equals(candidate.WeaponId, weaponId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var score = candidate.Mismatch(position, velocity);
                    if (score < bestScore)
                    {
                        best = i;
                        bestScore = score;
                    }
                }

                if (best >= 0)
                {
                    var predicted = _predicted[best];
                    _predicted.RemoveAt(best);
                    _byId.Add(id, predicted);
                    if (predicted.FollowsServer)
                    {
                        predicted.Correct(position, velocity, _snapDistance);
                    }

                    return predicted;
                }
            }

            var spec = _findWeapon(weaponId);
            var round = spec?.Round;
            var projectile = Create(weaponId ?? "", round != null && round.Delivery == RoundDelivery.Projectile ? round : null, position, velocity, predicted: false);
            if (projectile != null)
            {
                _byId.Add(id, projectile);
            }

            return projectile;
        }

        private Projectile Create(string weaponId, RoundSpec round, Vector3 position, Vector3 velocity, bool predicted)
        {
            var projectile = new Projectile(
                weaponId,
                round?.Projectile,
                round?.Homing != null || !predicted,
                _world,
                _layerMask,
                _gravity,
                _pendingSeconds);
            if (!projectile.Launch(position, velocity))
            {
                return null;
            }

            projectile.Show(_views);
            return projectile;
        }

        private static float Positive(float value, string paramName)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be positive");
            }

            return value;
        }

        private static RoundSpec RoundFor(WeaponSpec spec, int ammoTypeIndex)
        {
            var types = spec.AmmoTypes;
            return ammoTypeIndex < types.Count ? types[ammoTypeIndex].RoundOr(spec.Round) : spec.Round;
        }

        private static Vector3 SafeDirection(Vector3 direction)
        {
            var length = direction.Length();
            return length > 1e-6f ? direction / length : Vector3.UnitZ;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }

        private sealed class Projectile
        {
            private const float _minSpeed = 1e-4f;

            private readonly ProjectileSpec _spec;
            private readonly IWorldCollision _world;
            private readonly int _layerMask;
            private readonly Vector3 _gravity;
            private readonly float _landedSeconds;
            private BallisticFlight _flight;
            private IClientProjectileView _view;

            // 寄せ直した弾が跳ばないよう、見た目は飛び方の位置にこのずれを足した所に置き、ずれは時間で 0 へ縮める。
            private Vector3 _offset;
            private float _age;
            private float _landedFor;
            private bool _stuck;

            public Projectile(
                string weaponId,
                ProjectileSpec spec,
                bool followsServer,
                IWorldCollision world,
                int layerMask,
                Vector3 gravity,
                float landedSeconds)
            {
                WeaponId = weaponId;
                _spec = spec;
                FollowsServer = followsServer;
                _world = world;
                _layerMask = layerMask;
                _gravity = gravity;
                _landedSeconds = landedSeconds;
            }

            public string WeaponId { get; }

            public bool FollowsServer { get; }

            public bool Sticks => _spec != null && _spec.OnWorldHit == WorldHitResponse.Stick;

            public bool Listed { get; set; }

            public bool Missing { get; set; }

            public float MissingFor { get; set; }

            public float Unclaimed { get; set; }

            private float Lifetime => _spec != null ? _spec.LifetimeSeconds : UnknownWeaponLifetimeSeconds;

            private Vector3 Shown => _flight.Position + _offset;

            public float Mismatch(Vector3 position, Vector3 velocity)
            {
                return Vector3.Distance(Shown, position) + Vector3.Distance(_flight.Velocity, velocity);
            }

            public bool Launch(Vector3 position, Vector3 velocity)
            {
                if (!IsFinite(position) || !IsFinite(velocity))
                {
                    return false;
                }

                var speed = velocity.Length();
                var left = Lifetime - _age;
                if (speed < _minSpeed || !(left > 0f))
                {
                    return false;
                }

                // 溜めや弾薬で速さが武器の値と違うことがあるので、速さはサーバーの値を使う。武器が分からない弾はまっすぐ飛ばす。
                var flight = new ProjectileSpec(
                    speed,
                    _spec != null ? _spec.GravityMultiplier : 0f,
                    left,
                    _spec != null ? _spec.OnWorldHit : WorldHitResponse.Stop);
                _flight = new BallisticFlight(flight, null, _world, _layerMask, position, velocity, _gravity);
                _landedFor = 0f;
                _stuck = false;
                return true;
            }

            public void Show(IClientProjectileViews views)
            {
                _view = views.Show(WeaponId, _flight.Position, _flight.Velocity);
            }

            public void Correct(Vector3 position, Vector3 velocity, float snapDistance)
            {
                var shown = Shown;
                var age = _age;
                if (!Launch(position, velocity))
                {
                    return;
                }

                _age = age;
                var offset = shown - position;
                _offset = offset.Length() <= snapDistance ? offset : Vector3.Zero;
                _view?.Move(Shown, _flight.Velocity);
            }

            public bool Tick(float deltaSeconds, float correctionSeconds)
            {
                if (_flight.IsDone)
                {
                    _landedFor += deltaSeconds;
                    return _landedFor < _landedSeconds;
                }

                _age += deltaSeconds;
                var step = _flight.Step(deltaSeconds);
                _offset *= MathF.Exp(-deltaSeconds / correctionSeconds);
                switch (step.State)
                {
                    case BallisticState.Flying:
                        _view?.Move(Shown, _flight.Velocity);
                        return true;
                    case BallisticState.Stuck:
                        _offset = Vector3.Zero;
                        _stuck = true;
                        _view?.Stick(_flight.Position);
                        return true;
                    case BallisticState.Expired:
                        return false;
                    default:
                        // 止まる弾は当たった所で待ち、サーバーの終わりの知らせ (爆発などはその後に届く) を待つ。
                        _offset = Vector3.Zero;
                        _view?.Move(_flight.Position, _flight.Velocity);
                        return true;
                }
            }

            public void Stick(Vector3 position)
            {
                _stuck = true;
                _view?.Stick(position);
                _view = null;
            }

            public void Remove()
            {
                _view?.Remove();
                _view = null;
            }

            // 刺さった弾は見た目の側が残す長さを決めているので、知らせを待ち切れずに手放すときは消さない。
            public void Release()
            {
                if (!_stuck)
                {
                    _view?.Remove();
                }

                _view = null;
            }
        }
    }
}
