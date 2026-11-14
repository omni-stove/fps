using System;
using Fps.Core;
using UnityEngine;
using LadderRule = Fps.Core.Ladder;

namespace Fps.Runtime.Traversal
{
    /// <summary>
    /// プレイヤーをはしご (<see cref="Ladder"/>) で上り下りさせる移動の横取り。<see cref="Bind"/> で <see cref="PlayerController"/> に登録する
    /// (オフラインのプレイヤーには <see cref="LadderActionInstaller"/> が付ける)。
    /// <list type="bullet">
    /// <item><see cref="Rule"/> が On で、有効なはしごの前で、はしごへ進んでいる (<see cref="PlayerMotor.Velocity"/> の水平成分がはしごへ向く) とつかまる
    /// (<see cref="LadderSpan.Probe"/>)。上の足場から縁へ進んでもつかまる。</item>
    /// <item>つかまっている間は前後の入力で上り下りし (<see cref="LadderClimb"/>)、Jump で離れる。上の端では上の足場に、下の端では地面に降りる。</item>
    /// <item>つかまっている間は <see cref="PlayerActionConditions.OnLadder"/> を立て、武器をしまわせ (<see cref="WeaponController.Holster"/>) 撃たせない。</item>
    /// <item>つかまったら <c>[Ladder] grab ...</c>、離れたら <c>[Ladder] leave ... reason=..</c> をログに出す (<see cref="LadderLog"/>)。</item>
    /// </list>
    /// つかまっている途中で Rule が Off になる・はしごが無効になる・このコンポーネントが無効になると、その場で離す (<see cref="LadderOutcome.Released"/>)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadderMovement : MonoBehaviour, IMovementOverride
    {
        /// <summary><see cref="Priority"/>。はしごにつかまっている間は、ほかの横取りより先に選ばれる。</summary>
        public const int MovementPriority = 200;

        /// <summary>上の端・下の端で降りた足元を床から浮かせて置く高さ (m)。置いた Tick の <see cref="LandingDrop"/> で床に着く。</summary>
        public const float LandingLift = 0.02f;

        /// <summary>降りた Tick に下へ動かす量 (m)。床に触れさせて接地にする。</summary>
        public const float LandingDrop = 0.1f;

        private LadderRule _rule = LadderRule.Off;
        private PlayerController _mover;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private Ladder _pendingLadder;
        private LadderEntry _pendingEntry;
        private Ladder _ladder;
        private LadderClimb _climb;
        private bool _placed;
        private float _regrabRemaining;

        /// <summary>はしごにつかまったとき、つかまったはしごと所を渡して呼ばれる。</summary>
        public event Action<Ladder, LadderEntry> Grabbed;

        /// <summary>はしごを離れたとき、離れたはしごと理由を渡して呼ばれる。</summary>
        public event Action<Ladder, LadderOutcome> Left;

        /// <summary>はしごにつかまれるか。Off にすると、つかまっていれば次の Tick で離す。</summary>
        public LadderRule Rule
        {
            get => _rule;
            set => _rule = value;
        }

        /// <summary><see cref="Bind"/> で渡したプレイヤー。未設定なら null。</summary>
        public PlayerController Mover => _mover;

        /// <summary>つかまっている間にしまわせる武器。無ければ null。</summary>
        public WeaponController Weapon => _weapon;

        /// <summary>はしごにつかまっているか。</summary>
        public bool IsClimbing => _climb != null;

        /// <summary>つかまっているはしご。つかまっていなければ null。</summary>
        public Ladder CurrentLadder => _climb != null ? _ladder : null;

        /// <summary>今の上り下り。つかまっていなければ null。</summary>
        public LadderClimb Climb => _climb;

        /// <summary>次につかまれるまでの残りの秒数。</summary>
        public float RegrabRemaining => _regrabRemaining;

        public int Priority => MovementPriority;

        public PlayerActionConditions Conditions => PlayerActionConditions.OnLadder;

        /// <summary>
        /// 今つかまっている (または、この Tick でつかまる) か。つかまっていなければ、有効なはしごのうちつかまれるものを探して覚え、
        /// <see cref="Begin"/> でそれにつかまる。つかまっている途中で続けられなくなっていたら、ここで離して false を返す。
        /// </summary>
        public bool IsActive
        {
            get
            {
                if (_climb != null)
                {
                    if (CanKeepClimbing())
                    {
                        return true;
                    }

                    Leave(LadderOutcome.Released);
                    return false;
                }

                _pendingLadder = null;
                if (_rule != LadderRule.On || _mover == null || !_mover.IsInitialized || !IsLive || _regrabRemaining > 0f)
                {
                    return false;
                }

                return FindLadder(out _pendingLadder, out _pendingEntry);
            }
        }

        /// <summary>
        /// mover に横取りとして登録し、つかまっている間に weapon をしまわせ、ads の覗き込みを解く。何度呼んでも登録は 1 つ。
        /// 別の mover に付け替えるなら、前の mover からは外す。
        /// </summary>
        /// <param name="mover">動かすプレイヤー。null 不可。</param>
        /// <param name="weapon">しまわせる武器。無ければ null。</param>
        /// <param name="ads">つかまったときに覗き込みを解く。無ければ null。</param>
        public void Bind(PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (_mover != null && _mover != mover)
            {
                _mover.UnregisterMovementOverride(this);
            }

            if (_climb != null && _weapon != weapon)
            {
                ReleaseWeapon();
                if (weapon != null)
                {
                    weapon.Holster.Claim(this);
                }
            }

            _mover = mover;
            _weapon = weapon;
            _ads = ads;
            mover.RegisterMovementOverride(this);
        }

        /// <summary>次につかまれるまでの待ちを deltaSeconds 進める。Play 中は Update が呼ぶ。</summary>
        public void AdvanceCooldown(float deltaSeconds)
        {
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _regrabRemaining = Mathf.Max(0f, _regrabRemaining - deltaSeconds);
            }
        }

        public void Begin(PlayerController mover)
        {
            var ladder = _pendingLadder;
            _pendingLadder = null;
            if (ladder == null || !ladder.TryGetShape(out var span, out var spec))
            {
                return;
            }

            _ladder = ladder;
            _climb = new LadderClimb(span, spec, _pendingEntry, span.HeightOf(FeetOf(mover).ToNumerics()));
            _placed = false;
            if (_weapon != null)
            {
                _weapon.Holster.Claim(this);
            }

            if (_ads != null)
            {
                _ads.Release();
            }

            Debug.Log(LadderLog.Grab(ladder.name, _climb.Entry, _climb.Height), this);
            Grabbed?.Invoke(ladder, _climb.Entry);
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            var climb = _climb;
            if (climb == null)
            {
                // Begin がつかまれなかった Tick。次の Tick で IsActive が false になり、ふつうの移動に戻る。
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            var span = climb.Span;
            var radius = RadiusOf(mover);

            // 置く前の接地は、つかまる前に立っていた床 (上の足場など) のものなので読まない。
            var outcome = climb.Step(input.Move.y, input.JumpPressed, input.IsGrounded && _placed, deltaSeconds);
            switch (outcome)
            {
                case LadderOutcome.ExitTop:
                {
                    var feet = span.TopExit(radius).ToUnity() + (Vector3.up * LandingLift);
                    Leave(LadderOutcome.ExitTop);
                    return new MovementOverrideStep(Vector3.down * LandingDrop, Vector3.zero, RootFor(mover, feet), crouched: false);
                }

                case LadderOutcome.ExitBottom:
                {
                    var feet = climb.Feet(radius).ToUnity() + (Vector3.up * LandingLift);
                    Leave(LadderOutcome.ExitBottom);
                    return new MovementOverrideStep(Vector3.down * LandingDrop, Vector3.zero, RootFor(mover, feet), crouched: false);
                }

                case LadderOutcome.JumpOff:
                {
                    var velocity = span.JumpOffVelocity(climb.Spec.JumpOffSpeed).ToUnity();
                    Leave(LadderOutcome.JumpOff);
                    return new MovementOverrideStep(velocity * deltaSeconds, velocity, crouched: false);
                }

                default:
                {
                    var target = climb.Feet(radius).ToUnity();
                    var velocity = Vector3.up * climb.VerticalSpeed;
                    if (!_placed)
                    {
                        _placed = true;
                        return new MovementOverrideStep(
                            Vector3.zero, velocity, RootFor(mover, target), FacingYaw(span), crouched: false);
                    }

                    return new MovementOverrideStep(target - FeetOf(mover), velocity, crouched: false);
                }
            }
        }

        public void End(PlayerController mover)
        {
            _pendingLadder = null;
            if (_climb != null)
            {
                Leave(LadderOutcome.Released);
            }
        }

        /// <summary>mover の足元 (CharacterController のカプセルの底)。</summary>
        public static Vector3 FeetOf(PlayerController mover)
        {
            var controller = mover.Controller;
            var center = controller.transform.TransformPoint(controller.center);
            return center - (Vector3.up * (controller.height / 2f));
        }

        private static float RadiusOf(PlayerController mover)
        {
            return mover.Controller.radius;
        }

        // 足元を feet に合わせたときの本体の位置。足元と本体の差は姿勢を変えても保たれる (しゃがみは足元を残して縮む)。
        private static Vector3 RootFor(PlayerController mover, Vector3 feet)
        {
            return feet + (mover.transform.position - FeetOf(mover));
        }

        private static float FacingYaw(LadderSpan span)
        {
            return Mathf.Atan2(span.Facing.X, span.Facing.Z) * Mathf.Rad2Deg;
        }

        // isActiveAndEnabled は Edit Mode では OnEnable が走らないため常に false になり、テストから動かせないので、enabled と階層の有効で見る。
        private bool IsLive => enabled && gameObject.activeInHierarchy;

        private bool CanKeepClimbing()
        {
            return _rule == LadderRule.On && _ladder != null && _ladder.IsRegistered && IsLive;
        }

        private bool FindLadder(out Ladder found, out LadderEntry entry)
        {
            var feet = FeetOf(_mover).ToNumerics();
            var velocity = _mover.Motor.Velocity;
            var radius = RadiusOf(_mover);
            var ladders = Ladder.Enabled;
            for (var i = 0; i < ladders.Count; i++)
            {
                var ladder = ladders[i];
                if (ladder == null || !ladder.IsRegistered || !ladder.TryGetShape(out var span, out var spec))
                {
                    continue;
                }

                entry = span.Probe(feet, velocity, radius, spec.GrabDistance);
                if (entry != LadderEntry.None)
                {
                    found = ladder;
                    return true;
                }
            }

            found = null;
            entry = LadderEntry.None;
            return false;
        }

        private void Leave(LadderOutcome reason)
        {
            var ladder = _ladder;
            var climb = _climb;
            _climb = null;
            _ladder = null;
            _placed = false;
            _regrabRemaining = climb != null ? climb.Spec.RegrabSeconds : 0f;
            ReleaseWeapon();
            Debug.Log(LadderLog.Leave(ladder != null ? ladder.name : null, reason, climb != null ? climb.Height : 0f), this);
            Left?.Invoke(ladder, reason);
        }

        private void ReleaseWeapon()
        {
            if (_weapon != null)
            {
                _weapon.Holster.Release(this);
            }
        }

        private void Update()
        {
            AdvanceCooldown(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (_climb != null)
            {
                Leave(LadderOutcome.Released);
            }
        }

        private void OnDestroy()
        {
            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }
        }
    }
}
