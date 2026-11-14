using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンの敵をシーン上で動かす。判断は <see cref="EnemyBrain"/> に任せ、ここでは
    /// 毎フレーム知覚 (<see cref="EnemySenses"/>) を組み立てて渡し、返ってきた <see cref="EnemyDecision"/> を
    /// <see cref="NavMeshAgent.SetDestination"/>・向きの変更・射撃 (<see cref="Hitscan.Fire"/>) に変換するだけをする。
    /// 参照はコードで解決する: NavMeshAgent と自分の <see cref="Damageable"/> は同じ GameObject、
    /// 目標は <see cref="TargetTag"/> のタグが付いた GameObject の Damageable。
    /// 同じ GameObject に <see cref="SideMember"/> があれば、タグの代わりに自分と違う陣営で生きている、いちばん近い相手を
    /// <see cref="TargetSearchInterval"/> ごとに (目標が倒れたらすぐに) 選び直す (<see cref="EnemySenses.SelectHostile"/>)。
    /// モードが倒れた敵を戻すときは <see cref="Respawn"/> を呼ぶ。
    /// 武器は 1 本だけ持ち、自分では持ち替えない (ガンゲームのようにモードが <see cref="EquipWeapon"/> で替えることはある)。その値は <see cref="WeaponDefinition"/> アセットから作り、このコンポーネントには持たない
    /// (シーン生成スクリプトが、敵の配置ごとに指定した Id の武器を武器の一覧 <see cref="WeaponCatalogAsset"/> から引いて配線する)。
    /// 状態が変わるたびに <c>[AI] id=.. state=..</c> をログに出す。
    /// 撃たれたら (<see cref="Damageable.Damaged"/>) 撃った者の位置を次の知覚 1 回だけ物音として渡し、
    /// 視界の外から撃たれても <see cref="EnemyBrain"/> がその位置を Search するようにする。
    /// 動こうとしているのに進めなければ (<see cref="StuckMonitor"/>)、経路の引き直し → 寄り道 → 目的地をあきらめる →
    /// 瞬間移動の順に立て直し、<c>[AI] id=.. stuck recovery=..</c> をログに出す。NavMesh から外れたら近くの NavMesh に戻し
    /// (近くに無ければ瞬間移動の段階で出現地点へ戻し)、位置が NaN などになったら最後に正常だった位置に戻す。判断の良し悪しはここでは直さない (<see cref="EnemyBrain"/> 側で調整する)。
    /// <para>
    /// <see cref="CoverRegistry"/> を配線していれば、その selector を <see cref="EnemyBrain.UseCover"/> に渡し、撃たれたときや体力が減ったときに
    /// 遮蔽点 (<see cref="CoverPoint"/>) へ NavMesh で移る (TakeCover)。低い壁 (<see cref="CoverHeight.Low"/>) の点で隠れている間は
    /// しゃがみ、当たり判定のカプセルと目の高さを <see cref="CrouchHeight"/>・<see cref="CrouchEyeHeight"/> まで下げる。
    /// 覗くときは立ち上がり、点に覗く位置 (<see cref="CoverPoint.PeekPosition"/>) があればそこへ出て撃ち、隠れ直すときに点へ戻る。
    /// 使う点や段階が変わるたびに <c>[AI] id=.. cover phase=.. point=..</c> をログに出す。
    /// </para>
    /// <para>
    /// モードは <see cref="Disarm"/> で武器を持たせずに始められる (撃たない)。拾える物 (<see cref="Pickup"/>) から武器を拾う (<see cref="AddWeapon"/>) と持つ。
    /// モードは <see cref="SetErrand"/> で戦いのほかの行き先 (縮むエリアの中・拾える物など) を渡せる。巡回・待機・捜索の間はその行き先へ動き、
    /// 戦いより優先する行き先 (overridesCombat が true) か武器を持っていない間は、追跡・攻撃・隠れる最中でもそこへ動く (向きと射撃は判断のまま)。
    /// </para>
    /// <para>
    /// 人は分隊の指示 (<see cref="OrderHold"/>・<see cref="OrderAttack"/>・<see cref="OrderFollow"/>) を出せる。指示は判断と行き先 (<see cref="SetErrand"/>) より優先する。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Damageable))]
    public sealed partial class EnemyAgent : MonoBehaviour, ICharacterMotionSource, IWeaponHolder
    {
        public const string LogTag = "[AI]";

        /// <summary>目標を探すタグ。<see cref="GameBootstrap"/> がプレイヤーに付ける。</summary>
        public const string TargetTag = "Player";

        // キットを使うゲームがこの名前で読んでいても壊れないよう、EnemySpec に移した後も別名として残す。
        [Obsolete("Use EnemySpec.DefaultSightRange")]
        public const float DefaultSightRange = EnemySpec.DefaultSightRange;

        [Obsolete("Use EnemySpec.DefaultAttackRange")]
        public const float DefaultAttackRange = EnemySpec.DefaultAttackRange;

        [Obsolete("Use EnemySpec.DefaultLoseSightSeconds")]
        public const float DefaultLoseSightSeconds = EnemySpec.DefaultLoseSightSeconds;

        [Obsolete("Use EnemySpec.DefaultSearchSeconds")]
        public const float DefaultSearchSeconds = EnemySpec.DefaultSearchSeconds;

        [Obsolete("Use EnemySpec.DefaultReactionSeconds")]
        public const float DefaultReactionSeconds = EnemySpec.DefaultReactionSeconds;

        [Obsolete("Use EnemySpec.DefaultOpeningSpreadDegrees")]
        public const float DefaultOpeningSpreadDegrees = EnemySpec.DefaultOpeningSpreadDegrees;

        [Obsolete("Use EnemySpec.DefaultSettledSpreadDegrees")]
        public const float DefaultSettledSpreadDegrees = EnemySpec.DefaultSettledSpreadDegrees;

        [Obsolete("Use EnemySpec.DefaultOpeningShots")]
        public const int DefaultOpeningShots = EnemySpec.DefaultOpeningShots;

        /// <summary>足元からの目の高さ (メートル)。</summary>
        public const float DefaultEyeHeight = 1.6f;

        /// <summary>LookAt に向き直る速さ (度/秒)。</summary>
        public const float DefaultTurnSpeed = 360f;

        /// <summary>しゃがんだときの当たり判定の高さ (メートル)。低い壁 (<see cref="CoverProbe.LowWallHeight"/> 未満) に隠れる高さ。</summary>
        public const float DefaultCrouchHeight = 1f;

        /// <summary>しゃがんだときの足元からの目の高さ (メートル)。</summary>
        public const float DefaultCrouchEyeHeight = 0.8f;

        /// <summary>巡回地点を NavMesh 上に寄せるときに探す半径 (メートル)。</summary>
        public const float RouteSnapRadius = 2f;

        /// <summary>目的地がこれ以上動いたら経路を引き直す (メートル)。毎フレームの再計算を避ける。</summary>
        public const float RepathDistance = 0.25f;

        /// <summary>目標が見つからないときに探し直す間隔 (秒)。</summary>
        public const float TargetSearchInterval = 1f;

        /// <summary>進めないまま次の立て直しへ移るまでの秒数。</summary>
        public const float StuckSeconds = 2f;

        /// <summary>これだけ動けば (立て直し中は目的地へこれだけ近づけば) 進めたとみなす距離 (メートル)。</summary>
        public const float StuckProgressDistance = 0.5f;

        /// <summary>立て直しの途中で目的地がこれ以上変わったら、新しい目的地までの距離から測り直す距離 (メートル)。</summary>
        public const float StuckGoalChangeDistance = 2f;

        /// <summary>寄り道の行き先を探す半径 (メートル)。</summary>
        public const float SidestepRadius = 2f;

        /// <summary>寄り道の行き先として、今の位置からこれ以上離れた地点を選ぶ (メートル)。</summary>
        public const float SidestepMinDistance = 0.75f;

        /// <summary>寄り道の行き先を探し直す回数。</summary>
        public const int SidestepAttempts = 6;

        /// <summary>寄り道を切り上げて元の目的地へ向かい直すまでの秒数。</summary>
        public const float SidestepSeconds = 1.5f;

        /// <summary>瞬間移動の行き先にする経路の角は、今の位置からこれ以上先のもの (メートル)。</summary>
        public const float WarpLookAhead = 1.5f;

        /// <summary>NavMesh から外れたときに戻る先を探す半径 (メートル)。</summary>
        public const float OffMeshSnapRadius = 3f;

        // プレイヤーは反動で上がる照準を手で引き戻すので、敵もたまった反動の大半を打ち消す。打ち消さないと連射の後半が頭上へ抜ける。
        public const float RecoilControl = 0.75f;

        /// <summary>NavMesh に戻れなかったときに試し直す間隔 (秒)。</summary>
        public const float OffMeshRetryInterval = 1f;

        [SerializeField, Tooltip("ログに出す識別子。空なら Damageable の Id")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("巡回地点 (ワールド座標の足元)。空なら Idle")]
        private Vector3[] _patrolRoute = Array.Empty<Vector3>();

        [Header("Brain")]
        [SerializeField, Tooltip("敵のデータ (視界・反応・散らばり・遮蔽・種類ごとの値)。設定すると、この下の Brain と Cover の値と種類ごとの値の代わりにこれを使う。空なら入口 (GameContent) の同じ種類のデータ、それも無ければこの下の値を使う")]
        private EnemyDefinition _definition;

        [SerializeField, Tooltip("敵の種類")]
        private EnemyArchetype _archetype = EnemyArchetype.Rifleman;

        [SerializeField, Min(0.01f)]
        private float _sightRange = EnemySpec.DefaultSightRange;

        [SerializeField, Min(0.01f)]
        private float _attackRange = EnemySpec.DefaultAttackRange;

        [SerializeField, Min(0f)]
        private float _loseSightSeconds = EnemySpec.DefaultLoseSightSeconds;

        [SerializeField, Min(0f)]
        private float _searchSeconds = EnemySpec.DefaultSearchSeconds;

        [SerializeField, Min(0f)]
        private float _reactionSeconds = EnemySpec.DefaultReactionSeconds;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("Attack に入って最初の 1 発の散らばりの半角 (度)")]
        private float _openingSpreadDegrees = EnemySpec.DefaultOpeningSpreadDegrees;

        [SerializeField, Range(0f, EnemySpec.MaxSpreadDegrees), Tooltip("撃ち始めの数発が過ぎた後の散らばりの半角 (度)")]
        private float _settledSpreadDegrees = EnemySpec.DefaultSettledSpreadDegrees;

        [SerializeField, Min(0), Tooltip("散らばりを絞り切るまでの発数")]
        private int _openingShots = EnemySpec.DefaultOpeningShots;

        [SerializeField, Min(0f), Tooltip("怯みにくさ。0 で既定、1 で怯むのに 2 倍のダメージが要り、怯む長さは半分になる")]
        private float _staggerResistance = EnemySpec.DefaultStaggerResistance;

        // ApplySpec・ChangeSpec で渡された値は、波の強さやボスの段階のように敵のデータを元に作り直したものなので、データより優先する。
        private bool _specApplied;

        [Header("Body")]
        [SerializeField, Min(0f)]
        private float _eyeHeight = DefaultEyeHeight;

        [SerializeField, Min(0f)]
        private float _turnSpeed = DefaultTurnSpeed;

        [SerializeField, Min(0.01f), Tooltip("しゃがんだときの当たり判定の高さ (m)")]
        private float _crouchHeight = DefaultCrouchHeight;

        [SerializeField, Min(0f), Tooltip("しゃがんだときの足元からの目の高さ (m)")]
        private float _crouchEyeHeight = DefaultCrouchEyeHeight;

        [Header("Cover")]
        [SerializeField, Tooltip("隠れに行く遮蔽点をまとめたもの。シーン生成スクリプトが配線する。空なら遮蔽点を使わない")]
        private CoverPointRegistry _coverRegistry;

        [SerializeField, Min(0f), Tooltip("遮蔽点に着いてから、また隠れ直してから、覗くまで隠れている時間 (秒)")]
        private float _coverHideSeconds = EnemySpec.DefaultCoverHideSeconds;

        [SerializeField, Min(0f), Tooltip("遮蔽点から 1 回覗いている時間 (秒)")]
        private float _coverPeekSeconds = EnemySpec.DefaultCoverPeekSeconds;

        [SerializeField, Range(0f, 1f), Tooltip("体力の割合がこれを下回ったら、撃たれていなくても遮蔽点へ移る。0 なら体力では移らない")]
        private float _coverHealthFraction = EnemySpec.DefaultCoverHealthFraction;

        [Header("Weapon")]
        [SerializeField, RequiredReference, Tooltip("撃つ武器の定義 (弾数・連射間隔・ダメージ・射程・距離減衰)。シーン生成スクリプトが配置ごとに武器の一覧から引いて配線する")]
        private WeaponDefinition _weaponDefinition;

        private NavMeshAgent _agent;
        private Damageable _self;
        private EnemyBrain _brain;
        private CapsuleCollider _capsule;
        private float _standingCapsuleHeight;
        private Vector3 _standingCapsuleCenter;
        private bool _crouching;
        private int _loggedCoverIndex = -1;
        private EnemyCoverPhase _loggedCoverPhase = EnemyCoverPhase.None;
        private Vector3? _errand;
        private bool _errandOverridesCombat;
        private float _walkSpeed;
        private float _moveSpeedMultiplier = 1f;
        private bool _sprinting;
        private bool _aiming;

        public float WalkSpeed
        {
            get
            {
                if (_brain != null)
                {
                    return _walkSpeed;
                }

                var agent = _agent != null ? _agent : GetComponent<NavMeshAgent>();
                return agent != null ? agent.speed : 0f;
            }
        }

        public bool IsSprinting => _sprinting;
        public bool IsAiming => _aiming;

        /// <summary>ログに出す識別子。</summary>
        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(_id))
                {
                    return _id;
                }

                var self = _self != null ? _self : GetComponent<Damageable>();
                return self != null ? self.Id : name;
            }

            set => _id = value ?? string.Empty;
        }

        /// <summary>判断を行う状態機械。未初期化なら null。</summary>
        public EnemyBrain Brain => _brain;

        public bool IsInitialized => _brain != null;

        /// <summary>今の状態。未初期化なら Idle。</summary>
        public EnemyState State => _brain?.State ?? EnemyState.Idle;

        public EnemyArchetype Archetype => _archetype;

        public float StaggerResistance => _staggerResistance;

        /// <summary><see cref="SetErrand"/> で渡した行き先 (足元)。無ければ null。</summary>
        public Vector3? Errand => _errand;

        /// <summary><see cref="Errand"/> を戦いより優先するなら true。</summary>
        public bool ErrandOverridesCombat => _errandOverridesCombat;

        /// <summary>
        /// 戦いのほかの行き先を渡す。巡回・待機・捜索の間はそこへ動く。overridesCombat が true か武器を持っていなければ、追跡・攻撃・隠れる最中もそこへ動く。
        /// 着いても消さないので、要らなくなったら <see cref="ClearErrand"/> を呼ぶ。
        /// </summary>
        public void SetErrand(Vector3 destination, bool overridesCombat)
        {
            _errand = destination;
            _errandOverridesCombat = overridesCombat;
        }

        /// <summary><see cref="SetErrand"/> の行き先を消し、いつもの判断に戻す。</summary>
        public void ClearErrand()
        {
            _errand = null;
            _errandOverridesCombat = false;
        }

        /// <summary>巡回地点 (ワールド座標の足元)。</summary>
        public IReadOnlyList<Vector3> PatrolRoute => _patrolRoute;

        public float SightRange => _sightRange;
        public float AttackRange => _attackRange;
        public float EyeHeight => _eyeHeight;
        public float CrouchHeight => _crouchHeight;
        public float CrouchEyeHeight => _crouchEyeHeight;

        /// <summary>
        /// 隠れに行く遮蔽点をまとめたもの。null なら遮蔽点を使わない。変えたら次の Update から新しい点を使う (使っていた点は放す)。
        /// </summary>
        public CoverPointRegistry CoverRegistry
        {
            get => _coverRegistry;
            set => _coverRegistry = value;
        }

        /// <summary>低い壁の遮蔽点でしゃがんで隠れているか。</summary>
        public bool IsCrouching => _crouching;

        /// <summary>足元の位置。NavMeshAgent の baseOffset を引いた位置。</summary>
        public Vector3 GroundPosition
        {
            get
            {
                var offset = _agent != null ? _agent.baseOffset : 0f;
                return transform.position - (Vector3.up * offset);
            }
        }

        /// <summary>視線と射撃の始点。しゃがんでいれば <see cref="CrouchEyeHeight"/> の高さ。</summary>
        public Vector3 EyePosition => GroundPosition + (Vector3.up * (_crouching ? Mathf.Min(_crouchEyeHeight, _eyeHeight) : _eyeHeight));

        /// <summary>NavMesh の上にいて、Off-Mesh Link (飛び降りなど) を渡っている最中でなければ接地している。未初期化なら false。</summary>
        bool ICharacterMotionSource.IsGrounded => _agent != null && _agent.enabled && _agent.isOnNavMesh && !_agent.isOnOffMeshLink;

        bool ICharacterMotionSource.IsCrouched => _crouching;

        float ICharacterMotionSource.WalkSpeed => WalkSpeed;

        bool ICharacterMotionSource.TryGetSprinting(out bool sprinting)
        {
            sprinting = _sprinting;
            return true;
        }

        /// <summary>
        /// 巡回地点を設定する。初期化済みなら NavMesh 上に寄せてすぐ <see cref="EnemyBrain.SetPatrolRoute"/> に渡す。
        /// </summary>
        public void SetPatrolRoute(IReadOnlyList<Vector3> route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            var copy = new Vector3[route.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = route[i];
            }

            _patrolRoute = copy;
            if (_brain != null)
            {
                ApplyPatrolRoute();
            }
        }

        /// <summary>
        /// 部品を解決し、<see cref="CreateSpec"/> の値から <see cref="EnemyBrain"/> を、<see cref="WeaponDefinition"/> から <see cref="Weapon"/> を作る。
        /// 失敗したら (定義が未設定・不正なときも) 理由を Debug.LogError に出して false を返す。2 回目以降は何もしない。
        /// </summary>
        public bool TryInitialize()
        {
            if (_brain != null)
            {
                return true;
            }

            _agent = GetComponent<NavMeshAgent>();
            _self = GetComponent<Damageable>();
            if (_agent == null || _self == null)
            {
                Debug.LogError($"{LogTag} '{name}' には NavMeshAgent と Damageable が必要です。", this);
                return false;
            }

            if (_weaponDefinition == null)
            {
                Debug.LogError(
                    $"{LogTag} id={Id} に WeaponDefinition が設定されていません。シーン生成スクリプトで配線してください。", this);
                return false;
            }

            EnemySpec spec;
            try
            {
                spec = CreateSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} id={Id} の設定が不正です: {e.Message}", this);
                return false;
            }

            // 知覚 (視界の距離) や SightRange などはフィールドを読むので、敵のデータから作った値もフィールドに揃える。
            CopySpec(spec);

            WeaponSpec weaponSpec;
            try
            {
                weaponSpec = _weaponDefinition.ToSpec();
            }
            catch (WeaponDefinitionException e)
            {
                Debug.LogError($"{LogTag} id={Id} の武器定義が不正です: {e.Message}", _weaponDefinition);
                return false;
            }

            // しゃがみで縮めるのは Y 向きのカプセルだけ。ほかの形の当たり判定は形を推し量れないのでそのままにする。
            _capsule = GetComponent<CapsuleCollider>();
            if (_capsule != null && _capsule.direction == 1)
            {
                _standingCapsuleHeight = _capsule.height;
                _standingCapsuleCenter = _capsule.center;
            }
            else
            {
                _capsule = null;
            }

            // 歩きの速さはシーン生成スクリプトが NavMeshAgent に書いた値。以後 agent.speed は足の運びで毎フレーム書き換える。
            _walkSpeed = _agent.speed;
            _agent.autoTraverseOffMeshLink = false;
            _spreadSeed = _random.Next();
            _stuck = new StuckMonitor(StuckSeconds, StuckProgressDistance, StuckGoalChangeDistance);
            _stuck.Recovered += OnUnstuck;
            _brain = new EnemyBrain(spec);
            _brain.StateChanged += OnStateChanged;
            _weapon = new Weapon(weaponSpec, _weaponRules);
            _weapon.Reloaded += OnReloaded;
            _self.Died += OnDied;
            _self.Damaged += OnDamaged;
            NotifyInitialized();
            return true;
        }

        // 静的にするのは、波で後から出る敵にも、ルールで決まる部品 (EnemySuppression など) を出てくる前から付けられるようにするため。
        public static event Action<EnemyAgent> Initialized;

        // 受け取り側が投げても、敵の初期化は失敗にしない。
        private void NotifyInitialized()
        {
            var handlers = Initialized;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<EnemyAgent>)handler)(this);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        // ドメインの再読み込みを切った Play で、前の Play の購読が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInitialized()
        {
            Initialized = null;
        }

        /// <summary>
        /// 敵のデータ (<see cref="EffectiveDefinition"/>) があればそこから、無ければ Inspector の値から判断の設定を作る。
        /// <see cref="ApplySpec"/>・<see cref="ChangeSpec"/> を呼んだ後は、データがあってもその値から作る。
        /// 値が不正なら <see cref="ArgumentException"/> (データの値なら <see cref="EnemyDefinitionException"/>)。
        /// </summary>
        public EnemySpec CreateSpec()
        {
            var definition = EffectiveDefinition;
            if (definition != null && !_specApplied)
            {
                return definition.ToSpec();
            }

            return new EnemySpec(
                _sightRange,
                _attackRange,
                _loseSightSeconds,
                _searchSeconds,
                _reactionSeconds,
                _openingSpreadDegrees,
                _settledSpreadDegrees,
                _openingShots,
                _coverHideSeconds,
                _coverPeekSeconds,
                _coverHealthFraction,
                _archetype,
                CreateShieldSpec(),
                CreateSniperSpec(),
                CreateChargerSpec(),
                CreateBomberSpec(),
                _staggerResistance);
        }

        /// <summary>
        /// 判断の設定を spec の値にする。<see cref="TryInitialize"/> がこの値から <see cref="EnemyBrain"/> を作るので、それより前
        /// (GameObject を有効にする前) に呼ぶ。波で出す敵を強くするのに使う。初期化後なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public void ApplySpec(EnemySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_brain != null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is already initialized; apply the spec before activating it");
            }

            CopySpec(spec);
            _specApplied = true;
        }

        // 初期化後の差し替えを拒むのは、判断はもう作られていて、差し替えても何も変わらないことに気付けるようにするため。
        public EnemyDefinition Definition
        {
            get => _definition;
            set
            {
                if (_brain != null)
                {
                    throw new InvalidOperationException($"EnemyAgent '{name}' is already initialized; set the definition before activating it");
                }

                _definition = value;
                _specApplied = false;
            }
        }

        /// <summary>配線した <see cref="Definition"/>、空なら入口 (<see cref="GameContent.EnemyFor"/>) の、Inspector の種類と同じ敵のデータ。どちらも無ければ null。</summary>
        public EnemyDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.EnemyFor(_archetype));

        // ApplySpec は初期化後を拒むので、ボスの段階のように戦いの途中で変える口を別に設ける。
        // 波の強さ (ApplySpec) は出す前に決まるもので、途中で変わったら呼び出し側の誤りとして気付けるよう、そちらの決まりは残す。
        public void ChangeSpec(EnemySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            CopySpec(spec);
            _specApplied = true;
            _brain?.ChangeSpec(spec);
        }

        // 1 未満にもできる。ボスの段階で遅くする使い方を塞がないように。
        public float MoveSpeedMultiplier
        {
            get => _moveSpeedMultiplier;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MoveSpeedMultiplier must be positive and finite");
                }

                _moveSpeedMultiplier = value;
            }
        }

        private void CopySpec(EnemySpec spec)
        {
            _sightRange = spec.SightRange;
            _attackRange = spec.AttackRange;
            _loseSightSeconds = spec.LoseSightSeconds;
            _searchSeconds = spec.SearchSeconds;
            _reactionSeconds = spec.ReactionSeconds;
            _openingSpreadDegrees = spec.OpeningSpreadDegrees;
            _settledSpreadDegrees = spec.SettledSpreadDegrees;
            _openingShots = spec.OpeningShots;
            _coverHideSeconds = spec.CoverHideSeconds;
            _coverPeekSeconds = spec.CoverPeekSeconds;
            _coverHealthFraction = spec.CoverHealthFraction;
            _archetype = spec.Archetype;
            CopyArchetypeSpecs(spec);
            _staggerResistance = spec.StaggerResistance;
        }

        /// <summary>遮蔽点の段階が変わったときのログ。</summary>
        public static string FormatCoverLog(string id, EnemyCoverPhase phase, string point)
        {
            return $"{LogTag} id={id} cover phase={phase} point={(string.IsNullOrEmpty(point) ? "none" : point)}";
        }

        /// <summary>立て直しのログ。</summary>
        public static string FormatStuckLog(
            string id, StuckRecovery recovery, EnemyState state, float stalledSeconds, Vector3 position, Vector3? goal, string path)
        {
            var goalText = goal.HasValue ? goal.Value.ToString("F1") : "none";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} id={1} stuck recovery={2} state={3} stalled={4:F1}s pos={5} goal={6} path={7}",
                LogTag, id, recovery, state, stalledSeconds, position.ToString("F1"), goalText, path);
        }

        /// <summary>状態変化のログ。</summary>
        public static string FormatStateLog(string id, EnemyState previous, EnemyState next)
        {
            return $"{LogTag} id={id} state={next} prev={previous}";
        }

        private void Awake()
        {
            // 無効なまま置かれた敵は Awake が有効になるまで遅れるので、その間にコードが渡した値を JSON で消さない。
            if (Application.isPlaying)
            {
                ApplyKnockbackTuning(KitTuning.Knockback);
            }

            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        private void Start()
        {
            Debug.Log($"{LogTag} id={Id} state={_brain.State}", this);
            ApplyPatrolRoute();
            var sideMember = GetComponent<SideMember>();
            if (sideMember != null)
            {
                // Player タグの相手を先に目標にすると、間隔が過ぎるまで同陣営のプレイヤーを狙ってしまう。
                ResolveHostile(sideMember, force: true);
            }
            else
            {
                ResolveTarget(force: true);

                // 置かれたタレットの近くで有効になった敵が、選び直しの間隔が過ぎるまで遠いプレイヤーを狙わないように。
                if (Turret.Armed.Count > 0)
                {
                    ResolveTurretTarget(force: true);
                }
            }

            _lastFinitePosition = transform.position;
            _spawnPosition = NavMesh.SamplePosition(GroundPosition, out var spawnHit, OffMeshSnapRadius, NavMesh.AllAreas)
                ? spawnHit.position
                : GroundPosition;

            if (!_agent.isOnNavMesh)
            {
                Debug.LogError(
                    $"{LogTag} id={Id} が NavMesh の上にいません。シーンの NavMeshSurface を焼いたか確かめてください。",
                    this);
                _warnedOffMesh = true;
            }
        }

        private void OnDestroy()
        {
            if (_brain != null)
            {
                _brain.StateChanged -= OnStateChanged;
            }

            if (_stuck != null)
            {
                _stuck.Recovered -= OnUnstuck;
            }

            if (_weapon != null)
            {
                _weapon.Reloaded -= OnReloaded;
            }

            if (_self != null)
            {
                _self.Died -= OnDied;
                _self.Damaged -= OnDamaged;
            }
        }

        private void Update()
        {
            if (_brain.IsDead)
            {
                return;
            }

            if (!RecoverNonFinitePosition())
            {
                return;
            }

            var deltaSeconds = Time.deltaTime;
            _weapon.Tick(deltaSeconds);
            _recoil.Tick(deltaSeconds);

            var member = GetComponent<SideMember>();

            // 狙う指示の敵を目標にしている間は、陣営やタグで選び直すと指示の敵から外れてしまう。
            var pinned = TickOrder();
            if (!pinned && member != null)
            {
                // 陣営で狙う相手は動き回り倒れもするので、見つかっていても間隔ごとに選び直す。倒れたらすぐ次を探す。
                ResolveHostile(member, force: _target != null && _target.IsDead);
            }
            else if (!pinned && _target == null)
            {
                ResolveTarget(force: false);
            }
            else if (!pinned && (Turret.Armed.Count > 0 || _target.GetComponent<Turret>() != null))
            {
                // タレットが無くなった後も、狙っていたのがタレットならタグの相手へ戻す。
                ResolveTurretTarget(force: _target.IsDead);
            }

            // 乗っている間は NavMeshAgent を止めているので、止める処理 (MoveTo) を通る一時停止より先に見る。
            if (HoldWhileRiding())
            {
                return;
            }

            if (TickKnockback(deltaSeconds) || TraverseOffMeshLink(deltaSeconds))
            {
                return;
            }

            if (HoldWhileInterrupted(deltaSeconds))
            {
                return;
            }

            // 同じ selector なら何もしないので毎フレーム渡し、registry の差し替えや作り直しに追いつかせる。
            // 守る指示の間は遮蔽点へ移らせない。移る途中は撃たないので、守る場所で撃たれても反撃しなくなる。
            var holding = _order.Kind == BotErrandKind.Hold;
            _brain.UseCover(_coverRegistry != null && !holding ? _coverRegistry.Selector : null);

            var perception = Perceive();
            var decision = _brain.Tick(deltaSeconds, perception);
            if (_brain.IsDead)
            {
                return;
            }

            LogCoverChange();
            System.Numerics.Vector3? moveTo;
            bool steered;
            if (_order.IsOrder)
            {
                decision = BotDriver.FollowOrder(decision, _brain.State, _order, GroundPosition.ToNumerics(), _orderSpec, out steered);
                moveTo = steered ? decision.MoveTo : CoverMoveTo(decision);
                NoteOrderArrival();
            }
            else
            {
                var errand = ErrandMoveTo();
                steered = errand.HasValue;
                moveTo = errand ?? CoverMoveTo(decision);
            }

            SetCrouching(!steered && decision.Crouch && _brain.CurrentCoverPoint?.Height == CoverHeight.Low);
            var firing = decision.Fire && !_unarmed;
            UpdateGait(firing);
            MoveTo(moveTo);
            WatchProgress(deltaSeconds);
            Face(decision.LookAt, deltaSeconds);
            if (firing)
            {
                Shoot();
            }
            else
            {
                TopUpMagazine();
            }
        }

        private void UpdateGait(bool firing)
        {
            var phase = _brain.CoverPhase;
            _aiming = !_unarmed && EnemyGait.IsAiming(_brain.State, phase);
            _sprinting = _sprintAllowed && EnemyGait.IsSprinting(_brain.State, phase, _crouching, firing);
            _agent.speed = EnemyGait.Speed(GaitWalkSpeed, _sprinting, _crouching);
        }

        /// <summary>
        /// 今のフレームで判断の移動先の代わりに向かう <see cref="Errand"/>。巡回・待機・捜索の間か、戦いより優先する行き先か、武器を持っていなければ返す。
        /// 着いていれば (<see cref="EnemyBrain.ArrivalDistance"/> 以内) その場に留まるよう null にせず今の足元を返す。
        /// </summary>
        private System.Numerics.Vector3? ErrandMoveTo()
        {
            if (!_errand.HasValue)
            {
                return null;
            }

            var state = _brain.State;
            var engaged = state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.TakeCover;
            if (engaged && !_errandOverridesCombat && !_unarmed)
            {
                return null;
            }

            var destination = _errand.Value;
            return HorizontalDistance(GroundPosition, destination) > EnemyBrain.ArrivalDistance
                ? destination.ToNumerics()
                : GroundPosition.ToNumerics();
        }

        /// <summary>
        /// 遮蔽点に着いた後の TakeCover では <see cref="EnemyBrain"/> は移動先を出さないので、覗くときは覗く位置へ、
        /// 隠れるときは点へ戻る移動先をここで足す。立つ位置に着いていれば null (その場に留まる)。
        /// </summary>
        private System.Numerics.Vector3? CoverMoveTo(EnemyDecision decision)
        {
            if (decision.MoveTo.HasValue || _brain.State != EnemyState.TakeCover)
            {
                return decision.MoveTo;
            }

            var point = _brain.CurrentCoverPoint;
            if (!point.HasValue)
            {
                return null;
            }

            var stand = point.Value.Position.ToUnity();
            if (_brain.CoverPhase == EnemyCoverPhase.Peeking && _coverRegistry != null)
            {
                var peek = _coverRegistry.PeekPositionAt(_brain.CoverPointIndex);
                if (peek.HasValue)
                {
                    stand = peek.Value;
                }
            }

            return HorizontalDistance(GroundPosition, stand) > EnemyBrain.ArrivalDistance ? stand.ToNumerics() : (System.Numerics.Vector3?)null;
        }

        /// <summary>しゃがむ・立つ。Y 向きのカプセルの当たり判定があれば、足元の高さを変えずに縮める・戻す。</summary>
        private void SetCrouching(bool crouching)
        {
            if (_crouching == crouching)
            {
                return;
            }

            _crouching = crouching;
            if (_capsule == null)
            {
                return;
            }

            if (!crouching)
            {
                _capsule.height = _standingCapsuleHeight;
                _capsule.center = _standingCapsuleCenter;
                return;
            }

            var scaleY = Mathf.Abs(transform.lossyScale.y);
            var height = scaleY > 1e-4f ? _crouchHeight / scaleY : _standingCapsuleHeight;
            height = Mathf.Min(height, _standingCapsuleHeight);
            _capsule.height = height;
            _capsule.center = _standingCapsuleCenter - (Vector3.up * ((_standingCapsuleHeight - height) / 2f));
        }

        private void LogCoverChange()
        {
            var index = _brain.CoverPointIndex;
            var phase = _brain.CoverPhase;
            if (index == _loggedCoverIndex && phase == _loggedCoverPhase)
            {
                return;
            }

            _loggedCoverIndex = index;
            _loggedCoverPhase = phase;
            var marker = _coverRegistry != null ? _coverRegistry.MarkerAt(index) : null;
            Debug.Log(FormatCoverLog(Id, phase, marker != null ? marker.name : null), this);
        }

        /// <summary>
        /// 倒れた敵を groundPosition (足元) に戻して生き返らせる。体力を満たし、武器を弾の満ちた状態で作り直し、NavMeshAgent を有効に戻して
        /// 近くの NavMesh に置き、判断 (<see cref="EnemyBrain.Revive"/>)・経路・立て直し・撃たれた記録を初めからにして、目標を選び直す。
        /// 出現地点 (NavMesh から外れたときの戻り先) も置いた位置にする。
        /// まだ生きていれば何もせず false。未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public bool Respawn(Vector3 groundPosition, Quaternion rotation)
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            if (!_brain.IsDead)
            {
                return false;
            }

            PlaceAndStartOver(groundPosition, rotation, "respawned");
            return true;
        }

        /// <summary>
        /// 生死を問わず、<see cref="Respawn"/> と同じく groundPosition (足元) に置き直して体力・武器・判断・経路を初めからにする。
        /// ラウンド制のモードがラウンドの頭で、生き残っていた敵も含めて全員を出現点に戻すときに使う。未初期化なら <see cref="InvalidOperationException"/>。
        /// </summary>
        public void ResetTo(Vector3 groundPosition, Quaternion rotation)
        {
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            PlaceAndStartOver(groundPosition, rotation, "reset");
        }

        private void PlaceAndStartOver(Vector3 groundPosition, Quaternion rotation, string logVerb)
        {
            var ground = NavMesh.SamplePosition(groundPosition, out var hit, OffMeshSnapRadius, NavMesh.AllAreas)
                ? hit.position
                : groundPosition;

            LeaveVehicleForReset();
            ClearPendingPerception();
            _interruption.Clear();
            _flight.Clear();
            ClearOrder(logVerb);
            _loggedCoverIndex = -1;
            _loggedCoverPhase = EnemyCoverPhase.None;
            _destination = null;
            _detour = null;
            _goal = null;
            _stuck.Reset();
            ClearLinkTraversal();
            SetCrouching(false);

            _weapon.Reloaded -= OnReloaded;
            _weapon = new Weapon(_weapon.Spec, _weaponRules);
            _weapon.Reloaded += OnReloaded;
            _recoil.Reset();
            _sprinting = false;
            _aiming = false;
            _agent.speed = _walkSpeed * _moveSpeedMultiplier;

            // 無効のまま Transform を動かしてから有効にすると、NavMeshAgent は新しい位置の近くの NavMesh に載る。
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            _agent.enabled = false;
            transform.SetPositionAndRotation(ground + (Vector3.up * _agent.baseOffset), rotation);
            _agent.enabled = true;
            if (_agent.isOnNavMesh)
            {
                _agent.Warp(ground);
                _agent.isStopped = false;
                _warnedOffMesh = false;
            }
            else
            {
                Debug.LogWarning($"{LogTag} id={Id} respawn point {ground.ToString("F1")} is not on the NavMesh", this);
            }

            _spawnPosition = ground;
            _lastFinitePosition = transform.position;

            _self.ResetHealth();
            _brain.Restart();
            _target = null;
            _nextTargetSearch = 0f;
            Debug.Log($"{LogTag} id={Id} {logVerb} at={ground.ToString("F1")} state={_brain.State}", this);
        }

        private void OnUnstuck(float stalledSeconds)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0} id={1} unstuck after={2:F1}s", LogTag, Id, stalledSeconds), this);
        }

        private void OnStateChanged(EnemyState previous, EnemyState next)
        {
            Debug.Log(FormatStateLog(Id, previous, next), this);
        }

        private void OnDied(Damageable self)
        {
            ClearOrder("Died");
            ClearPendingPerception();
            _interruption.Clear();
            _flight.Clear();
            SetCrouching(false);
            _sprinting = false;
            _aiming = false;
            _brain.Kill();

            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }

            _destination = null;
            _detour = null;
            _goal = null;
            _stuck.Reset();
            ClearLinkTraversal();
            _agent.enabled = false;
            LeaveVehicleOnDeath();
        }
    }
}
