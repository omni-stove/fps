using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// ヒットスキャン射撃。プレイヤーのカメラに付ける。Attack を武器の射撃モードに従って (Auto は押している間、Semi は押すたびに) <see cref="Weapon.TryFire"/> を呼び、
    /// 撃てたら画面中央が指す点へ <see cref="HitscanRange"/> まで Raycast して、当たったコライダーの <see cref="Hitbox"/> にダメージを渡す。
    /// 本体に <see cref="ViewRig"/> があれば始点は目で、三人称でもカメラではなくキャラの目から撃つ (<see cref="ViewRig.ResolveAim(float, out Vector3, out Vector3)"/>)。
    /// 無ければカメラの位置から正面へ撃つ。
    /// 武器は <see cref="Definitions"/> (既定のロードアウト) の並びで 1 本ずつ持ち (<see cref="Loadout"/>)、Next / Previous で持ち替える (端で折り返す)。
    /// 持ち替えに時間はかからず、しまった武器のリロードは取り消す。
    /// Reload で <see cref="Weapon.TryStartReload"/> (リロード中なら <see cref="Weapon.TryActiveReload"/>)、毎フレーム今の武器の <see cref="Weapon.Tick"/> を呼ぶ。
    /// 弾数・連射間隔・リロードの判断は <see cref="Weapon"/>、ダメージ量は <see cref="DamageCalculator"/> が行う。
    /// 弾の向きは武器のばらつき (<see cref="SpreadPattern"/>) で円錐の中へずらし、撃つたびに <see cref="Recoil"/> に反動を足す。
    /// どちらも種は <see cref="PlayerId"/> と発射番号 (<see cref="ShotCount"/>) で、値が 0 の武器では何も変わらない。
    /// 武器の値 (弾数・連射間隔・ダメージ・射程・距離減衰) は <see cref="WeaponDefinition"/> アセットから作り、このコンポーネントには持たない。
    /// 参照はコードで解決する: 照準は同じ GameObject の <see cref="Camera"/>、入力はプロジェクト全体のアクション、
    /// 定義はシーン生成スクリプト (または <see cref="GameBootstrap"/>) が <see cref="Definitions"/> に配線する。
    /// </summary>
    /// <remarks>
    /// <see cref="Disarm"/> で武器を持っていない状態 (<see cref="IsUnarmed"/>) にできる。その間は <see cref="Weapon"/> が null で、撃つ・リロード・持ち替えは何もしない。
    /// 武器を拾う (<see cref="AddWeapon"/>) と、その武器 1 本だけを持った状態になる。武器の並び (<see cref="Loadout"/>) は空にできないので、
    /// 武器を持っていない間も最後の並びを内に持ったままにする (外からは見せない)。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed partial class WeaponController : MonoBehaviour, IWeaponHolder, IPlayerCarryLoad
    {
        /// <summary>武器を手放したときのログ。</summary>
        public const string DisarmedLog = "[Combat] disarmed";

        /// <summary>Assets/InputSystem_Actions.inputactions の Attack アクション。</summary>
        public const string AttackActionPath = InputActionPaths.Attack;

        /// <summary>Assets/InputSystem_Actions.inputactions の Reload アクション (キーボードの R、ゲームパッドの X)。</summary>
        public const string ReloadActionPath = InputActionPaths.Reload;

        /// <summary>Assets/InputSystem_Actions.inputactions の Next アクション。次の番号の武器に持ち替える。</summary>
        public const string NextActionPath = "Player/Next";

        /// <summary>Assets/InputSystem_Actions.inputactions の Previous アクション。前の番号の武器に持ち替える。</summary>
        public const string PreviousActionPath = "Player/Previous";

        public const string ReloadedLog = "[Combat] reloaded";

        /// <summary>弾切れでトリガーを引いたときのログの頭。続けて <c>id=..</c> を出す。</summary>
        public const string DryFireLogPrefix = "[Combat] dry-fire";

        /// <summary>アクティブリロードの判定のログの頭。続けて <c>success</c> か <c>fail</c> を出す (<see cref="ActiveReloadLog"/>)。</summary>
        public const string ActiveReloadLogPrefix = "[Combat] active-reload";

        /// <summary>持ち替えたときのログの頭。続けて <c>index=.. id=.. name=..</c> を出す。</summary>
        public const string SwitchedLogPrefix = "[Combat] switched";

        /// <summary>1 発撃つごとに <see cref="SpreadFraction"/> に足す量。</summary>
        public const float SpreadPerShot = 0.25f;

        /// <summary>最後に撃ってから <see cref="SpreadFraction"/> が戻り始めるまでの秒数。</summary>
        public const float SpreadRecoveryDelaySeconds = 0.15f;

        /// <summary><see cref="SpreadFraction"/> が戻る速さ (1 秒あたりに減る量)。</summary>
        public const float SpreadRecoveryPerSecond = 2f;

        [Header("Weapon")]
        [SerializeField, Tooltip("持つ武器の定義の並び (既定のロードアウト)。0 番を持って始める。シーン生成スクリプトが配線する")]
        private WeaponDefinition[] _definitions = Array.Empty<WeaponDefinition>();

        [Header("Hitscan")]
        [SerializeField, Tooltip("Raycast が当たるレイヤー")]
        private LayerMask _hitMask = Physics.DefaultRaycastLayers;

        private readonly TriggerDriver _trigger = new TriggerDriver();
        private Transform _aim;
        private ViewRig _rig;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _attackAction;
        [NonSerialized]
        private InputAction _reloadAction;
        [NonSerialized]
        private InputAction _nextAction;
        [NonSerialized]
        private InputAction _previousAction;
        private bool _enabledAttackAction;
        private bool _enabledReloadAction;
        private bool _enabledNextAction;
        private bool _enabledPreviousAction;
        private Loadout<Weapon> _loadout;
        private WeaponSpec[] _specs;
        private WeaponDefinition[] _loadedDefinitions;
        private GameRules _rules = GameRules.Default;
        private GameRules _loadedRules;
        private readonly RecoilState _recoil = new RecoilState();
        private readonly PelletHits<Damageable> _pelletHits = new PelletHits<Damageable>();
        private float _spread;
        private float _secondsSinceShot;
        private int _shotCount;
        private bool _unarmed;

        /// <summary>撃つたびに反動を足し、<see cref="Tick"/> で戻す。視点 (<see cref="FirstPersonLook.Recoil"/>) に同じものを配線して揺らす。</summary>
        public RecoilState Recoil => _recoil;

        /// <summary>
        /// ばらつきと反動の種にするプレイヤー Id。既定は 0 (オフラインで撃つのは手元のプレイヤーだけ)。
        /// </summary>
        public int PlayerId { get; set; }

        /// <summary>このコンポーネントで撃った弾の数。次に撃つ弾の発射番号でもある。</summary>
        public int ShotCount => _shotCount;

        /// <summary>
        /// 覗き込み (ADS) の進み具合 (0 で腰撃ち、1 で覗き込み切り) を返す。撃つたびにばらつきを決めるのに読む。null なら腰撃ちとして扱う。
        /// </summary>
        public Func<float> AdsProgressSource { get; set; }

        /// <summary>
        /// 移動の速さの割合 (<see cref="SpreadPattern.MoveFraction"/>、0 で止まっている) を返す。撃つたびにばらつきを決めるのに読む。
        /// null なら止まっているとして扱う。
        /// </summary>
        public Func<float> MoveFractionSource { get; set; }

        /// <summary>
        /// 姿勢で散らばりに掛ける倍率 (伏せなら <see cref="PlayerController.SpreadScale"/>) を返す。撃つたびと照準の広がりを出すたびに読む。
        /// null なら 1 として扱う。
        /// </summary>
        public Func<float> SpreadScaleSource { get; set; }

        /// <summary>今の姿勢で散らばりに掛ける倍率。<see cref="SpreadScaleSource"/> の値を 0〜1 に収めたもの (NaN は 1)。</summary>
        public float SpreadScale
        {
            get
            {
                var scale = SpreadScaleSource != null ? SpreadScaleSource() : 1f;
                return float.IsNaN(scale) ? 1f : Mathf.Clamp01(scale);
            }
        }

        /// <summary>
        /// 今のばらつき (0〜1)。撃つたびに <see cref="SpreadPerShot"/> 増え、撃ち止めて <see cref="SpreadRecoveryDelaySeconds"/> 経つと
        /// <see cref="SpreadRecoveryPerSecond"/> で 0 へ戻る。時間は <see cref="Tick"/> (Play 中は Update) で進む。HUD が照準の広がりに使う。
        /// <see cref="Modifiers"/> があれば、それを通した値 (0〜1 に収める) を返す。
        /// </summary>
        public float SpreadFraction => ModifySpreadFraction(_spread);

        /// <summary>
        /// HUD の照準が実際の散らばり (<see cref="CrosshairConeDegrees"/>) の外へ足す、撃った後の開き (0〜1)。
        /// <see cref="SpreadFraction"/> に <see cref="SpreadScale"/> を掛ける。
        /// </summary>
        public float CrosshairSpreadFraction => Mathf.Clamp01(SpreadFraction * SpreadScale);

        /// <summary>
        /// HUD の照準の線を置く、今撃ったときに弾が散る半角 (度)。<see cref="CurrentConeDegrees"/> (覗き込み・移動・姿勢・modifier 込み) に、
        /// 今の弾の散弾の広がり (<see cref="RoundSpec.PelletSpreadDegrees"/>) を足す。武器を持っていなければ 0。
        /// </summary>
        public float CrosshairConeDegrees
        {
            get
            {
                var weapon = Weapon;
                if (weapon == null)
                {
                    return 0f;
                }

                var ads = AdsProgressSource != null ? AdsProgressSource() : 0f;
                var move = MoveFractionSource != null ? MoveFractionSource() : 0f;
                // HUD が毎フレーム読むので、源が NaN を返したフレームに CurrentConeDegrees の例外で HUD を止めない。
                if (float.IsNaN(ads) || float.IsNaN(move))
                {
                    return 0f;
                }

                var cone = CurrentConeDegrees(weapon.Spec) + weapon.AmmoTypes.Round.PelletSpreadDegrees;
                return Mathf.Clamp(cone, 0f, SpreadPattern.MaxDegrees);
            }
        }

        /// <summary>射撃の向きと起点。未初期化なら null。</summary>
        public Transform Aim => _aim;

        public int HitMask => _hitMask;

        // 覗き込みとロックオンは武器ではなくプレイヤーに付くので、撃つときにロックした相手を引く先を外から渡す。null ならロックしない。
        public LockOnAim LockOn { get; set; }

        /// <summary>
        /// 持つ武器の定義の並び。Awake (<see cref="TryInitializeFromHierarchy"/>) はこれから武器を作る。
        /// 設定しただけでは今の <see cref="Loadout"/> は変わらない。作り直すなら <see cref="Initialize(Transform, InputAction, InputAction)"/> などを呼ぶ。
        /// 実行時に AddComponent するときは、GameObject を無効にした状態で付けてここを設定してから有効にする (Awake より先に設定するため)。
        /// null を設定すると空になる。
        /// </summary>
        public IReadOnlyList<WeaponDefinition> Definitions
        {
            get => _definitions ?? Array.Empty<WeaponDefinition>();
            set => _definitions = value != null ? ToArray(value) : Array.Empty<WeaponDefinition>();
        }

        /// <summary>
        /// 武器を作るときの予備弾のルール (<see cref="GameRules.ReserveAmmoMultiplier"/> と <see cref="GameRules.InfiniteReserve"/>)。
        /// 既定は <see cref="GameRules.Default"/>。<see cref="Definitions"/> と同じく、設定しただけでは今の <see cref="Loadout"/> は変わらず、
        /// 次の初期化から使う。今の武器に効かせるなら <see cref="ApplyRules"/> を呼ぶ。null を設定すると既定に戻る。
        /// </summary>
        public GameRules Rules
        {
            get => _rules;
            set => _rules = value ?? GameRules.Default;
        }

        /// <summary>今の <see cref="Loadout"/> を作ったときのルール。未初期化なら null。</summary>
        public GameRules LoadedRules => _loadout != null ? _loadedRules : null;

        public float SpinFraction => _trigger.Model != null ? _trigger.Model.SpinFraction : 0f;

        public float ChargeFraction => _trigger.ChargeFraction;

        /// <summary>今持っている武器の定義。未初期化か武器を持っていなければ null。</summary>
        public WeaponDefinition Definition => _loadout != null && !_unarmed ? _loadedDefinitions[_loadout.CurrentIndex] : null;

        /// <summary><see cref="Disarm"/> してから、武器を拾うか初期化し直すまで true。</summary>
        public bool IsUnarmed => _unarmed;

        /// <summary>
        /// 今持っている武器の定義 (<see cref="Loadout"/> と同じ並び。拾って加えた武器を含む)。未初期化か武器を持っていなければ空。
        /// <see cref="Definitions"/> (既定のロードアウト) とは違い、倒れたときに落とす物を決めるのに使う。
        /// </summary>
        public IReadOnlyList<WeaponDefinition> HeldDefinitions =>
            _loadout != null && !_unarmed ? _loadedDefinitions : (IReadOnlyList<WeaponDefinition>)Array.Empty<WeaponDefinition>();

        public InputAction AttackAction => _attackAction;
        public InputAction ReloadAction => _reloadAction;

        /// <summary>次の武器に持ち替えるアクション。渡されていなければ null (入力では持ち替えない)。</summary>
        public InputAction NextAction => _nextAction;

        /// <summary>前の武器に持ち替えるアクション。渡されていなければ null (入力では持ち替えない)。</summary>
        public InputAction PreviousAction => _previousAction;

        /// <summary>持っている武器の並び。未初期化か武器を持っていなければ null。</summary>
        public Loadout<Weapon> Loadout => _unarmed ? null : _loadout;

        /// <summary>今持っている武器。未初期化か武器を持っていなければ null。</summary>
        public Weapon Weapon => _unarmed ? null : _loadout?.Current;

        /// <summary>今持っている武器 (<see cref="Weapon"/>) と並び (<see cref="Loadout"/>) から出した倍率。武器を持っていなければ何も持たない倍率。</summary>
        public float CarrySpeedMultiplier(CarryLoadSpec carry)
        {
            return CarrySpeedMultiplier(carry, 0f);
        }

        public float CarrySpeedMultiplier(CarryLoadSpec carry, float extraWeight)
        {
            if (carry == null)
            {
                throw new ArgumentNullException(nameof(carry));
            }

            var armed = _loadout != null && !_unarmed;
            return carry.SpeedMultiplier(armed ? _loadout.Current.Spec : null, armed ? _specs : null, extraWeight);
        }

        /// <summary>今持っている武器の番号。未初期化か武器を持っていなければ -1。</summary>
        public int CurrentIndex => _loadout != null && !_unarmed ? _loadout.CurrentIndex : -1;

        /// <summary>
        /// 今持っている <see cref="Weapon"/> が変わったとき (初期化・持ち替え・<see cref="RestoreLoadout"/>) に、新しい Weapon を渡して呼ばれる。
        /// Weapon のイベントを直接購読している側 (HUD など) はここで購読し直す。
        /// </summary>
        public event Action<Weapon> WeaponChanged;

        /// <summary><see cref="StartReload"/> でリロードが始まったときに、リロードする Weapon を渡して呼ばれる。</summary>
        public event Action<Weapon> ReloadStarted;

        /// <summary><see cref="TryActiveReload"/> で判定したときに、判定した Weapon と結果を渡して呼ばれる。</summary>
        public event Action<Weapon, ActiveReloadResult> ActiveReloadJudged;

        /// <summary><see cref="Fire"/> で弾が出たときに、撃った Weapon を渡して呼ばれる (当たり判定の前)。</summary>
        public event Action<Weapon> Fired;

        /// <summary>
        /// <see cref="PullTrigger"/> で Attack を押したフレームに、今の武器の弾倉が空でリロード中でもなく撃てなかったときに、その Weapon を渡して呼ばれる。
        /// 押し続けている間は呼ばない。弾数やリロードは変えない (空撃ちの音を鳴らすためだけのもの)。
        /// </summary>
        public event Action<Weapon> DryFired;

        /// <summary>
        /// <see cref="Fire"/> が <see cref="ResolveShot"/> で決めた始点と向きを、Raycast の前に書き換える差し込み口 (カバーなど)。
        /// null ならそのまま撃つ。
        /// </summary>
        public IShotFilter ShotFilter { get; set; }

        /// <summary>
        /// true を返す間は <see cref="Fire"/> が撃たない (回避中など)。弾は減らず、連射間隔も進めない。null なら止めない。
        /// オフラインは <see cref="GameBootstrap"/> が配線する。
        /// </summary>
        public Func<bool> FireBlocker { get; set; }

        /// <summary><see cref="FireBlocker"/> か、武器をしまわせているもの (<see cref="Holster"/>) か、登録された modifier (<see cref="IsFireBlockedByModifier"/>) が今の射撃を止めているか。</summary>
        public bool IsFireBlocked => IsHolstered || (FireBlocker != null && FireBlocker()) || IsFireBlockedByModifier;

        /// <summary>
        /// 武器をしまわせる口。はしごなどが <see cref="ControlHandoff.Claim"/> している間は撃たず (<see cref="IsFireBlocked"/>)、
        /// 一人称の武器 (<see cref="ViewmodelPresenter"/>) と自機モックの銃を隠す。すべてが <see cref="ControlHandoff.Release"/> したら戻す。
        /// </summary>
        public ControlHandoff Holster { get; } = new ControlHandoff();

        /// <summary>誰かが武器をしまわせているか (<see cref="Holster"/>)。</summary>
        public bool IsHolstered => Holster.IsHandedOff;

        /// <summary>
        /// 操作を手放す口。誰かが引き取っている間、Update は Attack・Reload・Next・Previous・SwitchAmmoType を読まない (武器の時間は進める)。
        /// null なら手放さない。オフラインは <see cref="GameBootstrap"/> が <see cref="PlayerController.Handoff"/> と同じものを配線する。
        /// </summary>
        public ControlHandoff Handoff { get; set; }

        public IHandedOffTrigger HandedOffTrigger { get; set; }

        /// <summary><see cref="Handoff"/> を誰かが引き取っていて、入力を読まないか。</summary>
        public bool IsInputHandedOff => Handoff != null && Handoff.IsHandedOff;

        /// <summary>今持っている武器の距離減衰。未初期化なら null。</summary>
        public DamageFalloff Falloff => Weapon?.Spec.Falloff;

        /// <summary>Raycast の射程 (m)。今持っている武器の <see cref="WeaponSpec.Range"/>。未初期化なら 0。</summary>
        public float HitscanRange => Weapon != null ? Weapon.Spec.Range : 0f;

        public bool IsInitialized => _aim != null && _attackAction != null && _reloadAction != null && _loadout != null;

        private void Awake()
        {
            if (!IsInitialized && !TryInitializeFromHierarchy())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                // Awake で解決に失敗したまま再び有効にされた場合。エラーは解決時に出している。
                enabled = false;
                return;
            }

            EnableActions();
        }

        private void OnDisable()
        {
            DisableIfEnabledHere(_attackAction, ref _enabledAttackAction);
            DisableIfEnabledHere(_reloadAction, ref _enabledReloadAction);
            DisableIfEnabledHere(_nextAction, ref _enabledNextAction);
            DisableIfEnabledHere(_previousAction, ref _enabledPreviousAction);
            DisableIfEnabledHere(_switchAmmoTypeAction, ref _enabledSwitchAmmoTypeAction);
        }

        private void OnDestroy()
        {
            SetLoadout(null, null, null, null);
        }

        private void Update()
        {
            if (_unarmed || IsInputHandedOff)
            {
                Tick(Time.deltaTime);
                if (!_unarmed && HandedOffTrigger != null
                    && HandedOffTrigger.TryReadTrigger(out var isPressed, out var wasPressed, out var reloadPressed))
                {
                    if (reloadPressed)
                    {
                        PressReload();
                    }

                    PullTrigger(isPressed, wasPressed);
                    return;
                }

                // 乗り物やテイクダウンは武器をしまわず入力だけ手放すので、押したままの溜めが戻った時に離した扱いで撃たれないよう捨てる。
                _trigger.Drop();
                return;
            }

            // 持ち替えを先に見るのは、同じフレームの射撃とリロードを持ち替え先の武器で行うため。
            if (_nextAction != null && _nextAction.WasPressedThisFrame())
            {
                SelectNext();
            }
            else if (_previousAction != null && _previousAction.WasPressedThisFrame())
            {
                SelectPrevious();
            }

            Tick(Time.deltaTime);

            // リロードより先に見るのは、同じフレームに両方押したとき、切り替えた種類を込めるため。
            ReadSwitchAmmoType();

            if (_reloadAction.WasPressedThisFrame())
            {
                PressReload();
            }

            PullTrigger(_attackAction.IsPressed(), _attackAction.WasPressedThisFrame());
        }

        /// <summary>
        /// Attack の押す・離すと時間を今の武器の撃ち方 (<see cref="Fps.Core.Weapon.Trigger"/>) に渡し、返った発数ぶん撃つ。
        /// Auto は押している間、Semi は押したフレームだけ撃つ。連射間隔は撃ち方と <see cref="Weapon"/> が制限する。撃てたら true。
        /// 押したフレームに弾倉が空で撃てなければ <see cref="DryFired"/> を呼ぶ (<see cref="IsDryFire"/>)。押し続けている間は鳴らさない。
        /// <see cref="Rules"/> の <see cref="GameRules.ReloadMode"/> が <see cref="ReloadMode.Auto"/> なら、撃って弾倉が空になったときと、空撃ちしたときに
        /// <see cref="StartReload"/> でリロードを始める (予備弾が無いか modifier が止めていれば始まらない)。
        /// </summary>
        /// <param name="isPressed">Attack が押されているか。</param>
        /// <param name="wasPressedThisFrame">Attack がこのフレームに押されたか。</param>
        public bool PullTrigger(bool isPressed, bool wasPressedThisFrame)
        {
            EnsureInitialized();
            var weapon = Weapon;
            _trigger.Bind(weapon?.Trigger);
            if (weapon == null)
            {
                return false;
            }

            if (IsFireBlocked || !weapon.Ammo.CanFire)
            {
                _trigger.Drop();
                if (wasPressedThisFrame && !IsFireBlocked && IsDryFire(weapon))
                {
                    Debug.Log($"{DryFireLogPrefix} id={weapon.Spec.Id}", this);
                    DryFired?.Invoke(weapon);
                    ReloadIfAuto(weapon);
                }

                return false;
            }

            var shots = _trigger.Pull(weapon.Spec.FireMode, isPressed, wasPressedThisFrame);
            var charge = _trigger.ShotChargeFraction;
            var fired = false;
            for (var i = 0; i < shots && FireShot(weapon, charge); i++)
            {
                fired = true;
            }

            if (fired)
            {
                ReloadIfAuto(weapon);
            }

            return fired;
        }

        private void ReloadIfAuto(Weapon weapon)
        {
            if (Rules.ReloadMode == ReloadMode.Auto && IsDryFire(weapon))
            {
                StartReload();
            }
        }

        /// <summary>weapon の弾倉が空で、リロード中でもないか (トリガーを引くと空撃ちになる)。null なら false。</summary>
        public static bool IsDryFire(Weapon weapon)
        {
            return weapon != null && !weapon.IsReloading && weapon.AmmoInMagazine == 0;
        }

        /// <summary>次の番号の武器に持ち替える (最後の次は 0 番)。1 本しかないか武器を持っていなければ false。</summary>
        public bool SelectNext()
        {
            EnsureInitialized();
            return !_unarmed && _loadout.SelectNext();
        }

        /// <summary>前の番号の武器に持ち替える (0 番の前は最後)。1 本しかないか武器を持っていなければ false。</summary>
        public bool SelectPrevious()
        {
            EnsureInitialized();
            return !_unarmed && _loadout.SelectPrevious();
        }

        /// <summary>index 番の武器に持ち替える。範囲外か今の番号か、武器を持っていなければ false。</summary>
        public bool Select(int index)
        {
            EnsureInitialized();
            return !_unarmed && _loadout.TrySelect(index);
        }

        /// <summary>Id が weaponId の武器に持ち替える。持っていないか今の武器なら false。</summary>
        public bool Select(string weaponId)
        {
            EnsureInitialized();
            return !_unarmed && _loadout.TrySelect(weaponId);
        }

        /// <summary>
        /// 武器を持っていない状態にする (<see cref="IsUnarmed"/>)。リロード中なら取り消し、<c>[Combat] disarmed</c> を出して
        /// <see cref="WeaponChanged"/> を null で呼ぶ (HUD は武器なしの表示にする)。もう持っていなければ何もせず false。
        /// 武器を拾う (<see cref="AddWeapon"/>) か初期化し直すと持った状態に戻る。
        /// </summary>
        public bool Disarm()
        {
            EnsureInitialized();
            if (_unarmed)
            {
                return false;
            }

            _loadout.Current.CancelReload();
            _unarmed = true;
            Debug.Log(DisarmedLog, this);
            WeaponChanged?.Invoke(null);
            return true;
        }

        /// <summary>持っている武器の今の予備弾 (並びの順)。武器を持っていなければ空。</summary>
        public IReadOnlyList<PickupWeaponState> DescribeWeapons(out string currentWeaponId)
        {
            EnsureInitialized();
            if (_unarmed)
            {
                currentWeaponId = null;
                return Array.Empty<PickupWeaponState>();
            }

            var items = _loadout.Items;
            var list = new PickupWeaponState[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                list[i] = PickupWeaponState.Of(items[i]);
            }

            currentWeaponId = _loadout.Current.Spec.Id;
            return list;
        }

        /// <summary>
        /// 同じ GameObject のカメラとプロジェクト全体の Attack / Reload / Next / Previous / SwitchAmmoType アクションを探し、<see cref="Definitions"/> の武器で初期化する。
        /// Next / Previous / SwitchAmmoType が無ければ警告を出し、持ち替え・弾薬の切り替えの入力だけを無効にする。
        /// それ以外が見つからない・定義が未設定か不正なら理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryInitializeFromHierarchy()
        {
            if (_definitions == null || _definitions.Length == 0)
            {
                Debug.LogError(
                    $"[WeaponController] '{name}' に WeaponDefinition が設定されていません。シーン生成スクリプトで Definitions を配線してください。",
                    this);
                return false;
            }

            var camera = GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError($"[WeaponController] '{name}' に Camera がありません。プレイヤーのカメラに付けてください。", this);
                return false;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    "[WeaponController] プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。",
                    this);
                return false;
            }

            var attack = actions.FindAction(AttackActionPath);
            if (attack == null)
            {
                Debug.LogError($"[WeaponController] Input Actions '{actions.name}' に '{AttackActionPath}' がありません。", this);
                return false;
            }

            var reload = actions.FindAction(ReloadActionPath);
            if (reload == null)
            {
                Debug.LogError($"[WeaponController] Input Actions '{actions.name}' に '{ReloadActionPath}' がありません。", this);
                return false;
            }

            var next = FindOptionalAction(actions, NextActionPath, "次の武器への持ち替え");
            var previous = FindOptionalAction(actions, PreviousActionPath, "前の武器への持ち替え");
            var switchAmmoType = FindOptionalAction(actions, SwitchAmmoTypeActionPath, "弾薬の種類の切り替え");

            try
            {
                Initialize(camera.transform, attack, reload, next, previous, _definitions);
            }
            catch (WeaponDefinitionException e)
            {
                // アセットの値が不正な場合 (falloff の終わりが始まりより手前など)。
                Debug.LogError($"[WeaponController] '{name}' の武器定義が不正です: {e.Message}", this);
                return false;
            }
            catch (ArgumentException e)
            {
                // 並びに null や同じ Id の武器が入っている場合。
                Debug.LogError($"[WeaponController] '{name}' のロードアウトが不正です: {e.Message}", this);
                return false;
            }

            SetSwitchAmmoTypeAction(switchAmmoType);
            return true;
        }

        /// <summary>
        /// 参照を直接渡し、<see cref="Definitions"/> の武器で初期化する。持ち替えの入力は無い (<see cref="SelectNext"/> などで持ち替える)。
        /// 定義が未設定なら <see cref="InvalidOperationException"/>、不正なら <see cref="WeaponDefinitionException"/> か
        /// <see cref="ArgumentException"/> を投げ、状態は変えない。
        /// </summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction)
        {
            if (_definitions == null || _definitions.Length == 0)
            {
                throw new InvalidOperationException(
                    $"WeaponController '{name}' has no WeaponDefinition; set Definitions or pass them to Initialize");
            }

            Initialize(aim, attackAction, reloadAction, null, null, _definitions);
        }

        /// <summary>武器 1 本 (definition) だけを持たせて初期化する。持ち替えの入力は無い。</summary>
        public void Initialize(Transform aim, InputAction attackAction, InputAction reloadAction, WeaponDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Initialize(aim, attackAction, reloadAction, null, null, new[] { definition });
        }

        /// <summary>
        /// 参照と武器の定義の並びを直接渡して初期化する。武器は各定義の <see cref="WeaponDefinition.ToSpec"/> から <see cref="Rules"/> の予備弾で
        /// 満タンに作り直し、0 番を持つ。
        /// 成功したら <see cref="Definitions"/> も definitions にし、<see cref="WeaponChanged"/> を呼ぶ。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/>、並びが空・null を含む・Id が重複していれば <see cref="ArgumentException"/> を投げ、
        /// 状態は変えない。
        /// </summary>
        /// <param name="nextAction">次の武器に持ち替えるアクション。null なら入力では持ち替えない。</param>
        /// <param name="previousAction">前の武器に持ち替えるアクション。null なら入力では持ち替えない。</param>
        public void Initialize(
            Transform aim,
            InputAction attackAction,
            InputAction reloadAction,
            InputAction nextAction,
            InputAction previousAction,
            IReadOnlyList<WeaponDefinition> definitions)
        {
            if (aim == null)
            {
                throw new ArgumentNullException(nameof(aim));
            }

            if (attackAction == null)
            {
                throw new ArgumentNullException(nameof(attackAction));
            }

            if (reloadAction == null)
            {
                throw new ArgumentNullException(nameof(reloadAction));
            }

            // 先に検査を通してから差し替える (失敗しても前の状態が残る)。
            var loaded = ToArray(definitions ?? throw new ArgumentNullException(nameof(definitions)));
            var specs = ToSpecs(loaded);
            var rules = _rules;
            var loadout = WeaponLoadout.Create(specs, rules);

            ReplaceAction(ref _attackAction, ref _enabledAttackAction, attackAction);
            ReplaceAction(ref _reloadAction, ref _enabledReloadAction, reloadAction);
            ReplaceAction(ref _nextAction, ref _enabledNextAction, nextAction);
            ReplaceAction(ref _previousAction, ref _enabledPreviousAction, previousAction);

            _aim = aim;
            _rig = aim.GetComponentInParent<ViewRig>(true);
            _definitions = loaded;
            _unarmed = false;
            ReleaseAttachments();
            SetLoadout(loadout, specs, loaded, rules, resetAmmo: true);

            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (Application.isPlaying && isActiveAndEnabled)
            {
                EnableActions();
            }

            WeaponChanged?.Invoke(Weapon);
        }

        /// <summary>
        /// rules の予備弾で全武器を満タンに作り直し、0 番を持つ。<see cref="Rules"/> も rules にする。
        /// キャンペーンが開始時に難易度のルールを効かせるのに使う (セーブから戻すのはこの後の <see cref="RestoreLoadout"/>)。
        /// <see cref="WeaponChanged"/> を呼ぶので HUD は購読し直す。武器を持っていなければ、持っていないまま (内の並びだけ作り直す)。
        /// </summary>
        public void ApplyRules(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            EnsureInitialized();
            var loadout = WeaponLoadout.Create(_specs, rules);
            _rules = rules;
            SetLoadout(loadout, _specs, _loadedDefinitions, rules, resetAmmo: true);
            WeaponChanged?.Invoke(Weapon);
        }

        /// <summary>
        /// 持っている全武器の弾数と持っている武器をセーブデータから戻す。リロード中なら取り消す。
        /// Id が ammo にある武器はその弾数 (弾倉の容量を超える分は容量に揃える)、無い武器は満タンにし、equippedWeaponId の武器を持つ
        /// (持っていない Id か空なら 0 番)。ammo にあって持っていない武器は警告を出して捨てる。
        /// 予備弾はセーブの値をそのまま使い、<see cref="GameRules.ReserveAmmoMultiplier"/> は掛け直さない。
        /// 無限予備弾かどうかは今の <see cref="LoadedRules"/> に従う。武器を持っていなかったら、持った状態に戻す。
        /// <see cref="WeaponChanged"/> を呼ぶので HUD は購読し直す。
        /// </summary>
        /// <param name="ammo">武器ごとの残弾 (<see cref="CampaignSave.Weapons"/>)。null を含まない。</param>
        /// <param name="equippedWeaponId">持っていた武器の Id (<see cref="CampaignSave.EquippedWeaponId"/>)。null 可。</param>
        public void RestoreLoadout(IEnumerable<WeaponAmmo> ammo, string equippedWeaponId)
        {
            EnsureInitialized();
            _unarmed = false;

            var loadout = WeaponLoadout.Restore(_specs, ammo, equippedWeaponId, _loadedRules, out var unknown);
            foreach (var id in unknown)
            {
                Debug.LogWarning($"[WeaponController] '{name}' は武器 '{id}' を持っていないため、セーブされたその残弾を捨てます。", this);
            }

            if (!string.IsNullOrEmpty(equippedWeaponId) && loadout.IndexOf(equippedWeaponId) < 0)
            {
                Debug.LogWarning(
                    $"[WeaponController] '{name}' はセーブで持っていた武器 '{equippedWeaponId}' を持っていないため、{loadout.Current.Spec.LogName} を持ちます。",
                    this);
            }

            SetLoadout(loadout, _specs, _loadedDefinitions, _loadedRules, resetAmmo: true);
            WeaponChanged?.Invoke(Weapon);
        }

        /// <summary>同時に持てる武器の数 (<see cref="Rules"/> の <see cref="GameRules.MaxWeapons"/>)。0 なら上限なし。</summary>
        public int MaxWeapons => _rules.MaxWeapons;

        /// <summary>
        /// definition の武器を持ち武器の最後に加える。ammo があればその弾数、null なら満タン (<see cref="LoadedRules"/> の予備弾)。今持っている武器と他の武器の弾数はそのまま。
        /// 武器を作り直すのでリロード中なら取り消し、<see cref="WeaponChanged"/> を呼ぶ (HUD は購読し直す)。持ち替えはしない。
        /// <see cref="Definitions"/> (既定のロードアウト) は変えないが、<see cref="ApplyRules"/> での満たし直しでは加えた武器も残る。
        /// 同じ Id の武器を既に持っていれば何もせず false。定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、状態は変えない。
        /// 武器を持っていなければ (<see cref="IsUnarmed"/>)、definition の武器 1 本だけを持った状態にする。
        /// 持てる数の上限 (<see cref="MaxWeapons"/>) はここでは見ない。上限で持ち替えるのは拾う側 (<see cref="Pickup"/>) が <see cref="SwapWeapon"/> で行う。
        /// attachments はその武器に付いていた物 (持ち物のアイテムの Id) で、<see cref="AttachmentCatalog"/> でその武器に付かない物は捨てる。
        /// </summary>
        /// <exception cref="ArgumentException">ammo の武器の Id が definition の Id と違う。</exception>
        public bool AddWeapon(WeaponDefinition definition, WeaponAmmo ammo = null, IReadOnlyList<string> attachments = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EnsureInitialized();
            var spec = definition.ToSpec();
            RequireAmmoFor(spec, ammo);
            var kept = WeaponAttachments.Keep(AttachmentCatalog, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, AttachmentCatalog);
            if (_unarmed)
            {
                var only = new[] { spec };
                var armed = WeaponLoadout.Restore(only, AmmoList(ammo), spec.Id, _loadedRules, out _);
                _unarmed = false;
                _attachments.Clear();
                RememberAttachments(spec.Id, kept);
                SetLoadout(armed, only, new[] { definition }, _loadedRules);
                Debug.Log($"[Combat] weapon added index=0 id={spec.Id} name={spec.LogName}", this);
                WeaponChanged?.Invoke(Weapon);
                return true;
            }

            if (_loadout.IndexOf(spec.Id) >= 0)
            {
                return false;
            }

            var specs = new WeaponSpec[_specs.Length + 1];
            Array.Copy(_specs, specs, _specs.Length);
            specs[_specs.Length] = spec;
            var definitions = new WeaponDefinition[_loadedDefinitions.Length + 1];
            Array.Copy(_loadedDefinitions, definitions, _loadedDefinitions.Length);
            definitions[_loadedDefinitions.Length] = definition;

            var carried = new List<WeaponAmmo>(_loadout.SnapshotAmmo());
            if (ammo != null)
            {
                carried.Add(ammo);
            }

            var loadout = WeaponLoadout.Restore(specs, carried, Weapon.Spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, -1);
            RememberAttachments(spec.Id, kept);
            SetLoadout(loadout, specs, definitions, _loadedRules);
            Debug.Log($"[Combat] weapon added index={specs.Length - 1} id={spec.Id} name={spec.LogName} attachments={WeaponAttachments.Describe(kept)}", this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        /// <summary>
        /// Id が droppedWeaponId の持ち武器を手放し、同じ番号に definition の武器を入れて構える。弾数は ammo があればその数、null なら満タン。
        /// 付いていた物 attachments は <see cref="AddWeapon"/> と同じく付かない物を捨てる。
        /// 手放した武器の定義と残弾と付けていた物を dropped に入れて true。他の武器の弾数はそのまま。リロード中なら取り消し、<see cref="WeaponChanged"/> を呼ぶ。
        /// 武器を持っていない・droppedWeaponId を持っていない・definition と同じ Id の武器を既に持っているなら何もせず false。
        /// 定義が不正なら <see cref="WeaponDefinitionException"/> を投げ、状態は変えない。
        /// </summary>
        /// <exception cref="ArgumentException">ammo の武器の Id が definition の Id と違う。</exception>
        public bool SwapWeapon(
            string droppedWeaponId, WeaponDefinition definition, WeaponAmmo ammo, IReadOnlyList<string> attachments, out HeldWeapon dropped)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            dropped = default;
            EnsureInitialized();
            var spec = definition.ToSpec();
            RequireAmmoFor(spec, ammo);
            var kept = WeaponAttachments.Keep(AttachmentCatalog, spec.Id, attachments);
            spec = WeaponAttachments.Apply(spec, kept, AttachmentCatalog);
            if (_unarmed || _loadout.IndexOf(spec.Id) >= 0)
            {
                return false;
            }

            var index = _loadout.IndexOf(droppedWeaponId);
            if (index < 0)
            {
                return false;
            }

            var leaving = _loadout.Items[index];
            leaving.CancelReload();
            var leavingAmmo = WeaponAmmo.Leaving(leaving);
            var leavingDefinition = _loadedDefinitions[index];
            var leavingAttachments = AttachmentsOf(leavingAmmo.WeaponId);

            var specs = (WeaponSpec[])_specs.Clone();
            specs[index] = spec;
            var definitions = (WeaponDefinition[])_loadedDefinitions.Clone();
            definitions[index] = definition;

            var carried = new List<WeaponAmmo>();
            foreach (var entry in _loadout.SnapshotAmmo())
            {
                if (!string.Equals(entry.WeaponId, leavingAmmo.WeaponId, StringComparison.Ordinal))
                {
                    carried.Add(entry);
                }
            }

            if (ammo != null)
            {
                carried.Add(ammo);
            }

            var loadout = WeaponLoadout.Restore(specs, carried, spec.Id, _loadedRules, out _);
            KeepAmmoTypes(loadout, index);
            _attachments.Remove(leavingAmmo.WeaponId);
            RememberAttachments(spec.Id, kept);
            SetLoadout(loadout, specs, definitions, _loadedRules);
            dropped = new HeldWeapon(leavingDefinition, leavingAmmo, leavingAttachments);
            Debug.Log(
                $"[Combat] weapon swapped index={index} id={spec.Id} name={spec.LogName} dropped={leavingAmmo.WeaponId} "
                + $"dropped-ammo={leavingAmmo.AmmoInMagazine}/{leavingAmmo.ReserveAmmo} dropped-attachments={WeaponAttachments.Describe(leavingAttachments)}",
                this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        /// <summary>
        /// Id が weaponId の持ち武器を手放し、その定義と残弾 (予備弾を持ち物から出し入れする武器は弾倉の分だけ) と付けていた物を dropped に入れて true。
        /// 今の武器を手放したら残りの 0 番を構え、最後の 1 本なら武器を持っていない状態 (<see cref="Disarm"/>) にする。
        /// 武器を持っていないか weaponId を持っていなければ何もせず false。<see cref="WeaponChanged"/> を呼ぶ。
        /// </summary>
        public bool DropWeapon(string weaponId, out HeldWeapon dropped)
        {
            dropped = default;
            EnsureInitialized();
            var index = _unarmed ? -1 : _loadout.IndexOf(weaponId);
            if (index < 0)
            {
                return false;
            }

            var leaving = _loadout.Items[index];
            leaving.CancelReload();
            dropped = new HeldWeapon(_loadedDefinitions[index], WeaponAmmo.Leaving(leaving), AttachmentsOf(leaving.Spec.Id));
            _attachments.Remove(leaving.Spec.Id);
            if (_loadout.Count == 1)
            {
                // 持ち物の山は持ち武器から作るので、武器を持っていない状態でも並びを残したまま Disarm で手放したことにする
                leaving.UseAmmoPool(null);
                Debug.Log($"[Combat] weapon dropped index=0 id={leaving.Spec.Id} name={leaving.Spec.LogName} ammo={dropped.Ammo.AmmoInMagazine}/{dropped.Ammo.ReserveAmmo}", this);
                Disarm();
                return true;
            }

            var specs = new WeaponSpec[_specs.Length - 1];
            var definitions = new WeaponDefinition[_loadedDefinitions.Length - 1];
            var carried = new List<WeaponAmmo>();
            var ammo = _loadout.SnapshotAmmo();
            for (int from = 0, to = 0; from < _specs.Length; from++)
            {
                if (from == index)
                {
                    continue;
                }

                specs[to] = _specs[from];
                definitions[to] = _loadedDefinitions[from];
                carried.Add(ammo[from]);
                to++;
            }

            var equipped = index == _loadout.CurrentIndex ? null : Weapon.Spec.Id;
            var loadout = WeaponLoadout.Restore(specs, carried, equipped, _loadedRules, out _);
            for (var i = 0; i < loadout.Count; i++)
            {
                loadout.Items[i].AmmoTypes.TrySelect(_loadout.Items[i < index ? i : i + 1].AmmoTypes.Index);
            }

            SetLoadout(loadout, specs, definitions, _loadedRules);
            Debug.Log($"[Combat] weapon dropped index={index} id={leaving.Spec.Id} name={leaving.Spec.LogName} ammo={dropped.Ammo.AmmoInMagazine}/{dropped.Ammo.ReserveAmmo}", this);
            WeaponChanged?.Invoke(Weapon);
            return true;
        }

        public Func<WeaponSpec, IAmmoPool> AmmoPools { get; private set; }

        // 持ち武器の並びを作り直したときに、予備弾の受け皿をつなぐ前に呼ぶ。持ち物が武器の場所を、受け皿に入る弾薬より先に取れるようにするため
        public event Action LoadoutReplaced;

        // 武器ごとの予備弾の受け皿 (持ち物の弾薬) を決め、今の武器と、この後に作り直す武器に使う。null で外す
        public void UseAmmoPools(Func<WeaponSpec, IAmmoPool> poolFor)
        {
            AmmoPools = poolFor;
            if (_loadout == null || _unarmed)
            {
                return;
            }

            foreach (var weapon in _loadout.Items)
            {
                weapon.UseAmmoPool(poolFor?.Invoke(weapon.Spec));
            }
        }

        // 残弾の数だけでは種類が戻るので、込めた弾が別の種類に変わらないよう作り直した武器へ選択を移す。replacedIndex の武器は入れ替えたので移さない。
        private void KeepAmmoTypes(Loadout<Weapon> rebuilt, int replacedIndex)
        {
            for (var i = 0; i < _loadout.Count; i++)
            {
                if (i != replacedIndex)
                {
                    rebuilt.Items[i].AmmoTypes.TrySelect(_loadout.Items[i].AmmoTypes.Index);
                }
            }
        }

        private static void RequireAmmoFor(WeaponSpec spec, WeaponAmmo ammo)
        {
            if (ammo != null && !string.Equals(ammo.WeaponId, spec.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException($"ammo is for weapon '{ammo.WeaponId}', not '{spec.Id}'", nameof(ammo));
            }
        }

        private static IEnumerable<WeaponAmmo> AmmoList(WeaponAmmo ammo)
        {
            return ammo != null ? new[] { ammo } : Array.Empty<WeaponAmmo>();
        }

        /// <summary>
        /// Id が weaponId の持ち武器に予備弾を amount 足し (<see cref="Fps.Core.Weapon.AddReserveAmmo"/>)、実際に足した数を返す。
        /// 持っていない武器 (武器を持っていないときはどの武器も) なら 0。
        /// </summary>
        public int AddReserveAmmo(string weaponId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            EnsureInitialized();
            if (_unarmed)
            {
                return 0;
            }

            var index = _loadout.IndexOf(weaponId);
            return index >= 0 ? _loadout.Items[index].AddReserveAmmo(amount) : 0;
        }

        /// <summary>持っている全武器の今の残弾。並びの順。セーブに使う。武器を持っていなければ空。</summary>
        public IReadOnlyList<WeaponAmmo> SnapshotAmmo()
        {
            EnsureInitialized();
            return _unarmed ? Array.Empty<WeaponAmmo>() : _loadout.SnapshotAmmo();
        }

        /// <summary>
        /// 今の武器で 1 発撃つ。<see cref="FireBlocker"/> が止めているか <see cref="Weapon.TryFire"/> が false なら何もせず false。
        /// 撃てたら照準の正面へ散らばりを掛けた弾 (<see cref="ShotPlanner"/>) を、<see cref="ShotFilter"/> があれば書き換えた始点と向きで、
        /// 弾の届け方 (<see cref="ShotDeliveries"/>) に渡す。Hitbox に当たれば着弾の効果 (<see cref="ImpactApplier"/>) でダメージを渡し、
        /// Hitbox の無い物に当たれば <see cref="StructureDamage.Apply"/> に武器の <see cref="WeaponSpec.ExplosionRadius"/> で渡す。
        /// 撃つと <see cref="CombatEvents.ShotFired"/>、何かに当たると <see cref="CombatEvents.Impact"/>、
        /// 生きている相手に当てると、1 回の射撃で相手ごとに 1 つ <see cref="CombatEvents.HitConfirmed"/> を出す。弾の線の近くを通られた相手には
        /// <see cref="Hitscan.ReportNearMisses"/> でニアミスを知らせる。倒した相手の <see cref="CombatEvents.Killed"/> は
        /// このプレイヤーと武器の帰属 (<see cref="CombatEvents.Attribute"/>) で <see cref="Damageable"/> が出す。
        /// </summary>
        public bool Fire()
        {
            EnsureInitialized();
            var weapon = Weapon;
            return FireShot(weapon, weapon != null && weapon.Trigger == _trigger.Model ? _trigger.ChargeFraction : 0f);
        }

        private bool FireShot(Weapon weapon, float chargeFraction)
        {
            if (weapon == null || IsFireBlocked || !weapon.TryFire())
            {
                return false;
            }

            Debug.Log($"[Combat] fire ammo={weapon.AmmoInMagazine}", this);
            _spread = Mathf.Min(1f, _spread + SpreadPerShot);
            _secondsSinceShot = 0f;
            var shotIndex = _shotCount;
            _shotCount = unchecked(_shotCount + 1);
            Fired?.Invoke(weapon);

            // この弾は今の視点の向きで撃ち、反動はカメラが次に向きを書くとき (FirstPersonLook.LateUpdate) から効く。
            ResolveShot(out var aimOrigin, out var aimDirection);
            var round = weapon.AmmoTypes.Round;
            var plan = ShotPlanner.Plan(
                round, aimOrigin.ToNumerics(), aimDirection.ToNumerics(), CurrentConeDegrees(weapon.Spec), PlayerId, shotIndex);
            _recoil.Kick(weapon.Spec, PlayerId, shotIndex);

            var rounds = new PlannedRound[plan.Rounds.Count];
            for (var i = 0; i < rounds.Length; i++)
            {
                var planned = plan.Rounds[i];
                var origin = planned.Origin.ToUnity();
                var direction = planned.Direction.ToUnity();
                ShotFilter?.Filter(ref origin, ref direction);
                rounds[i] = new PlannedRound(origin.ToNumerics(), direction.ToNumerics(), planned.Index);
            }

            var shooter = _aim.root.gameObject;
            StealthNoise.Emit(shooter, new SoundStimulus(SoundCueKind.Gunshot, rounds[0].Origin, weapon.Spec.ShotNoiseRadius, false));
            var impact = weapon.AmmoTypes.Impact;
            var charge = Mathf.Clamp01(chargeFraction);
            var delivery = ShotDeliveries.For(round.Delivery);
            var definition = Definition;
            var look = definition != null ? definition.Projectile : null;
            _pelletHits.Clear();
            var homingTarget = round.Homing != null && LockOn != null ? LockOn.FollowLocked() : null;
            foreach (var planned in rounds)
            {
                // 撃たれた側が撃った方向へ向かえるよう、弾ごとの始点を撃った者の位置として渡す。
                var request = new ShotRequest(
                    shooter,
                    _aim.root,
                    planned.Origin.ToUnity(),
                    round,
                    impact,
                    weapon.Spec.Id,
                    _hitMask,
                    weapon.Spec.Trigger.Charge,
                    charge,
                    look,
                    ConfirmHit,
                    homingTarget);
                CollectHit(delivery.Deliver(request, planned));
            }

            // ペレットごとに出すと 1 回の射撃でヒットマーカーが何度も出るので、相手ごとにまとめてから出す。
            for (var i = 0; i < _pelletHits.Count; i++)
            {
                CombatEvents.RaiseHitConfirmed(_pelletHits[i].ToHitConfirm());
            }

            _pelletHits.Clear();
            return true;
        }

        // オフラインで WeaponController を持つのは手元のプレイヤーだけなので、ここが撃った本人のクライアントになる。
        private void CollectHit(ShotOutcome outcome)
        {
            if (outcome.Hitbox == null || outcome.Owner == null)
            {
                return;
            }

            Debug.Log($"[Combat] hit zone={outcome.Hitbox.Zone} dmg={outcome.Damage}", this);
            if (outcome.WasAlive)
            {
                _pelletHits.Add(
                    outcome.Owner,
                    outcome.Damage,
                    outcome.Hitbox.Zone,
                    outcome.Killed ? DamageOutcome.Killed : DamageOutcome.Damaged,
                    outcome.RaycastHit.point.ToNumerics());
            }
        }

        // 飛ぶ弾は撃った後のフレームで着くので、射撃ごとにまとめる先が無く、着いた弾ごとに知らせる。
        private void ConfirmHit(ShotOutcome outcome)
        {
            if (outcome.Hitbox == null || outcome.Owner == null)
            {
                return;
            }

            Debug.Log($"[Combat] hit zone={outcome.Hitbox.Zone} dmg={outcome.Damage}", this);
            if (outcome.WasAlive)
            {
                CombatEvents.RaiseHitConfirmed(
                    new HitConfirm(outcome.Damage, outcome.Hitbox.Zone, outcome.Killed, downed: false, outcome.RaycastHit.point.ToNumerics()));
            }
        }

        /// <summary>
        /// 今の武器の <see cref="Weapon.TryStartReload"/> を呼ぶ。補充は <see cref="Tick"/> で時間が経ったときに行う。
        /// 始まったら <see cref="ReloadStarted"/> を呼んで true。modifier がリロードを止めている間は始めず false。
        /// </summary>
        public bool StartReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (weapon == null || IsReloadBlockedByModifier || !weapon.TryStartReload())
            {
                return false;
            }

            ReloadStarted?.Invoke(weapon);
            return true;
        }

        /// <summary>
        /// Reload を押したときの処理。今の武器がリロード中なら <see cref="TryActiveReload"/>、そうでなければ <see cref="StartReload"/> を呼ぶ。
        /// リロード中の 2 回目の押下で新しいリロードは始めない (アクティブリロードが Off なら何もしない)。どちらかが起きたら true。
        /// </summary>
        public bool PressReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            return weapon != null && (weapon.IsReloading ? TryActiveReload() : StartReload());
        }

        /// <summary>
        /// 今の武器の <see cref="Weapon.TryActiveReload"/> を呼び、判定したら <c>[Combat] active-reload success|fail</c> をログに出して
        /// <see cref="ActiveReloadJudged"/> を呼び、true を返す。ルールが Off・リロード中でない・判定済みなら何もせず false。
        /// 成功したときの補充のログ (<see cref="ReloadedLog"/>) は、判定のログより先に出る。
        /// </summary>
        public bool TryActiveReload()
        {
            EnsureInitialized();
            var weapon = Weapon;
            if (weapon == null || !weapon.TryActiveReload(out var result))
            {
                return false;
            }

            Debug.Log(ActiveReloadLog(result), this);
            ActiveReloadJudged?.Invoke(weapon, result);
            return true;
        }

        /// <summary>アクティブリロードの判定のログ。<c>[Combat] active-reload success</c> か <c>[Combat] active-reload fail</c>。</summary>
        public static string ActiveReloadLog(ActiveReloadResult result)
        {
            return $"{ActiveReloadLogPrefix} {(result == ActiveReloadResult.Success ? "success" : "fail")}";
        }

        /// <summary>
        /// direction を、今の覗き込みと移動の速さで決まる spec の散らばり (<see cref="SpreadPattern.ConeDegrees"/>) の円錐の中へ、
        /// 種 (<see cref="PlayerId"/>・shotIndex) で決めた向きにずらす。散らばりが 0 なら direction をそのまま返す。
        /// </summary>
        public Vector3 ApplySpread(WeaponSpec spec, Vector3 direction, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var cone = CurrentConeDegrees(spec);
            if (cone <= 0f)
            {
                return direction;
            }

            return SpreadPattern.Direction(direction.ToNumerics(), cone, PlayerId, shotIndex).ToUnity();
        }

        /// <summary>
        /// 今撃ったときの spec の散らばりの半角 (度)。<see cref="AdsProgressSource"/> と <see cref="MoveFractionSource"/> を読み、
        /// <see cref="SpreadScale"/> と登録された modifier の倍率を掛けてから <see cref="Modifiers"/> の足し引きを通し、0 以上 <see cref="SpreadPattern.MaxDegrees"/> 以下に収める。
        /// </summary>
        public float CurrentConeDegrees(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var ads = AdsProgressSource != null ? AdsProgressSource() : 0f;
            var move = MoveFractionSource != null ? MoveFractionSource() : 0f;
            var cone = SpreadPattern.ConeDegrees(spec, ads, move) * SpreadScale * ModifierSpreadMultiplier();
            if (_modifiers.Count == 0)
            {
                return cone;
            }

            // HUD が毎フレーム読むので、写しを作らずに回す (modifier の中で足し外しはしない約束)。
            for (var i = 0; i < _modifiers.Count; i++)
            {
                cone = _modifiers[i].ModifyConeDegrees(spec, cone);
            }

            // NaN を返す modifier があっても、SpreadPattern.Direction に渡して例外にしない。
            return float.IsNaN(cone) ? 0f : Mathf.Clamp(cone, 0f, SpreadPattern.MaxDegrees);
        }

        private float ModifySpreadFraction(float spread)
        {
            if (_modifiers.Count == 0)
            {
                return spread;
            }

            for (var i = 0; i < _modifiers.Count; i++)
            {
                spread = _modifiers[i].ModifySpreadFraction(spread);
            }

            return float.IsNaN(spread) ? 0f : Mathf.Clamp01(spread);
        }

        /// <summary>今の武器の時間 (武器を持っていなければ進めない) と、<see cref="SpreadFraction"/> と <see cref="Recoil"/> の戻りを deltaSeconds 進める。</summary>
        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            CancelReloadBlockedByModifier();
            var weapon = Weapon;
            weapon?.Tick(deltaSeconds);

            // 持ち替えた直後の時間をしまった武器の撃ち方に数えないよう、進める前に今の武器の撃ち方へ付け替える。
            _trigger.Bind(weapon?.Trigger);
            _trigger.Advance(deltaSeconds);
            _recoil.Tick(deltaSeconds);

            var waiting = Mathf.Max(0f, SpreadRecoveryDelaySeconds - _secondsSinceShot);
            var recovering = Mathf.Max(0f, deltaSeconds - waiting);
            _secondsSinceShot = Mathf.Min(SpreadRecoveryDelaySeconds, _secondsSinceShot + deltaSeconds);
            _spread = Mathf.Max(0f, _spread - (recovering * SpreadRecoveryPerSecond));
        }

        /// <summary>
        /// 当たったコライダーから <see cref="Hitbox"/> を探し、あれば持ち主にダメージを渡して
        /// <c>[Combat] hit zone=.. dmg=..</c> をログに出す。Hitbox が無ければ何もせず false。
        /// </summary>
        /// <param name="collider">命中したコライダー。</param>
        /// <param name="distance">射手から命中点までの距離。</param>
        /// <param name="baseDamage">武器の基本ダメージ。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        /// <param name="context">ログの文脈にするオブジェクト。null 可。</param>
        /// <param name="sourcePosition">撃った者の位置。撃たれた敵がこの方向へ向かう。分からなければ null。</param>
        /// <param name="direction">弾の進む向き。分からなければ null。</param>
        public static bool TryApplyHit(
            Collider collider,
            float distance,
            int baseDamage,
            DamageFalloff falloff,
            UnityEngine.Object context = null,
            Vector3? sourcePosition = null,
            Vector3? direction = null)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            var hitbox = Hitbox.FindFor(collider);
            if (hitbox == null)
            {
                return false;
            }

            var owner = hitbox.Owner;
            if (owner == null)
            {
                Debug.LogError($"[Combat] Hitbox '{hitbox.name}' has no Damageable owner; the hit is ignored", hitbox);
                return false;
            }

            var damage = hitbox.ApplyHit(baseDamage, distance, falloff, sourcePosition, direction);
            Debug.Log($"[Combat] hit zone={hitbox.Zone} dmg={damage}", context != null ? context : hitbox);
            return true;
        }

        /// <summary>
        /// 射撃の始点と向き。照準が <see cref="ViewRig"/> のカメラなら目から画面中央が指す点へ (一人称・三人称共通)、
        /// そうでなければ照準の位置から正面へ。
        /// </summary>
        public void ResolveShot(out Vector3 origin, out Vector3 direction)
        {
            EnsureInitialized();

            // ViewRig の Awake より先に撃たれても目が決まるよう、FindEye で探す (ログは出さない)。
            var eye = _rig != null ? ViewRig.FindEye(_rig.transform) : null;
            if (eye != null && _aim.parent == eye)
            {
                ViewRig.ResolveAim(_aim, eye, HitscanRange, _aim.root, _hitMask, out origin, out direction);
                return;
            }

            origin = _aim.position;
            direction = _aim.forward;
        }

        // resetAmmo: 予備弾を作った値にする作り直し (初期化・ルールの満たし直し・セーブから戻す)。持ち物の弾薬もその値に揃える
        private void SetLoadout(Loadout<Weapon> loadout, WeaponSpec[] specs, WeaponDefinition[] definitions, GameRules rules, bool resetAmmo = false)
        {
            if (_loadout != null)
            {
                _loadout.Switched -= OnSwitched;
                foreach (var weapon in _loadout.Items)
                {
                    weapon.Reloaded -= OnReloaded;
                    weapon.UseAmmoPool(null);
                }
            }

            _loadout = loadout;
            _specs = specs;
            _loadedDefinitions = definitions;
            _loadedRules = rules;
            LoadoutReplaced?.Invoke();
            if (_loadout == null)
            {
                return;
            }

            Dictionary<string, (IAmmoPool Pool, int Count)> reset = null;
            _loadout.Switched += OnSwitched;
            foreach (var weapon in _loadout.Items)
            {
                weapon.Reloaded += OnReloaded;

                // 武器を持っていない間の並びは手放した武器なので、その予備弾を持ち物に足さない
                if (AmmoPools == null || _unarmed)
                {
                    continue;
                }

                var own = weapon.ReserveAmmo;
                var pool = AmmoPools(weapon.Spec);
                if (weapon.UseAmmoPool(pool) && resetAmmo)
                {
                    reset ??= new Dictionary<string, (IAmmoPool Pool, int Count)>(StringComparer.Ordinal);
                    reset[pool.Id] = reset.TryGetValue(pool.Id, out var target) ? (pool, Math.Max(target.Count, own)) : (pool, own);
                }
            }

            // 受け皿につなぐと足りない分を足すだけなので、作り直しで予備弾が減る (難易度で減らす・セーブの値に戻す) ときは多い分を減らす
            if (reset != null)
            {
                foreach (var (pool, count) in reset.Values)
                {
                    var extra = pool.Count - count;
                    if (extra > 0)
                    {
                        pool.Take(extra);
                    }
                }
            }
        }

        private void OnSwitched(int index)
        {
            var spec = _loadout.Current.Spec;
            Debug.Log($"{SwitchedLogPrefix} index={index} id={spec.Id} name={spec.LogName}", this);
            WeaponChanged?.Invoke(Weapon);
        }

        private void OnReloaded()
        {
            Debug.Log(ReloadedLog, this);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("WeaponController is not initialized");
            }
        }

        private void EnableActions()
        {
            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            _enabledAttackAction |= EnableIfNeeded(_attackAction);
            _enabledReloadAction |= EnableIfNeeded(_reloadAction);
            _enabledNextAction |= EnableIfNeeded(_nextAction);
            _enabledPreviousAction |= EnableIfNeeded(_previousAction);
            _enabledSwitchAmmoTypeAction |= EnableIfNeeded(_switchAmmoTypeAction);
        }

        private InputAction FindOptionalAction(InputActionAsset actions, string path, string effect)
        {
            var action = actions.FindAction(path);
            if (action == null)
            {
                Debug.LogWarning($"[WeaponController] Input Actions '{actions.name}' に '{path}' がないため、{effect}は入力からはできません。", this);
            }

            return action;
        }

        private static void ReplaceAction(ref InputAction current, ref bool enabledHere, InputAction next)
        {
            if (current != next)
            {
                DisableIfEnabledHere(current, ref enabledHere);
            }

            current = next;
        }

        private static void DisableIfEnabledHere(InputAction action, ref bool enabledHere)
        {
            if (enabledHere && action != null)
            {
                action.Disable();
            }

            enabledHere = false;
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action == null || action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }

        private static WeaponDefinition[] ToArray(IReadOnlyList<WeaponDefinition> definitions)
        {
            var result = new WeaponDefinition[definitions.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = definitions[i];
            }

            return result;
        }

        private static WeaponSpec[] ToSpecs(WeaponDefinition[] definitions)
        {
            if (definitions.Length == 0)
            {
                throw new ArgumentException("definitions must not be empty", nameof(definitions));
            }

            var specs = new WeaponSpec[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null)
                {
                    throw new ArgumentException("definitions must not contain null (index " + i + ")", nameof(definitions));
                }

                specs[i] = definitions[i].ToSpec();
            }

            return specs;
        }
    }
}
