using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// HUD (左下に体力、右下に弾倉/予備、上中央に残り時間とその下に一時的な案内、中央に照準)。同じ GameObject の <see cref="UIDocument"/> に出す。
    /// 値の源 (プレイヤーの <see cref="Damageable"/>・<see cref="WeaponController"/>・<see cref="Fps.Core.Match"/>) は
    /// <see cref="Initialize"/> でコードから渡す。値はそれぞれのイベントで <see cref="HudModel"/> が更新し、
    /// <see cref="HudView"/> が Label に写す。毎フレーム読みに行くのは、時間で進みイベントの無いリロードの進み
    /// (<see cref="HudModel.RefreshReloadProgress"/>)・溜めと熱と弾薬の種類 (<see cref="HudModel.RefreshWeaponGauges"/>)・スコープの倍率 (<see cref="AimDownSights.Magnification"/>) と
    /// 照準の広がり・表示・目からの射線の遮り (<see cref="RefreshCrosshair"/>) だけ。
    /// Health や Weapon が作り直されたとき (<see cref="Damageable.HealthReset"/> / <see cref="WeaponController.WeaponChanged"/>) は購読し直す。
    /// オンライン (Fps.Net の NetHud) は源を渡さずに作り、<see cref="Model"/> の Set 系で値を書く。チームの点数 (左上) と
    /// 試合結果 (中央上) はそのときだけ出る。
    /// ヒットマーカーなど HUD の部分ごとのビュー (<see cref="HudElementView"/>) は <see cref="Create"/> が同じ GameObject に付け
    /// (<see cref="AddViews"/>)、View を作り直すたびに Hud.uxml の要素を渡し直す。
    /// Hud.uxml にキーで書いた文字 (見出し・ポーズのボタン) は <see cref="LocalizedTexts"/> で今の言語にし、言語が変わったら書き直す。
    /// ゲーム進行からは <see cref="IHud"/> として使われ、オフラインでは既定の生成役 (<see cref="DefaultHudFactory"/>) が作る。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour, IHud
    {
        public const string LogTag = "[HUD]";

        /// <summary>Resources からの HUD のレイアウト (Assets/game/UI/Resources/Hud/Hud.uxml)。</summary>
        public const string LayoutResourcePath = "Hud/Hud";

        /// <summary>Resources からのランタイムテーマ (Assets/game/UI/Resources/Hud/HudTheme.tss)。</summary>
        public const string ThemeResourcePath = "Hud/HudTheme";

        /// <summary><see cref="Create"/> が作る PanelSettings の名前。</summary>
        public const string PanelSettingsName = "HudPanelSettings";

        /// <summary>HUD の基準解像度。Hud.uss の寸法はこの解像度で決めてあり、画面の大きさに合わせて拡縮する。</summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        private readonly HudModel _model = new HudModel();
        private HudView _view;
        private Damageable _player;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private PlayerController _mover;
        private ViewRig _rig;
        private PanelSettings _ownedPanelSettings;
        private Color _alphaColor = Color.white;
        private Color _bravoColor = Color.white;
        private bool _followsPalette;
        private Team? _paletteViewer;
        private ColorPalette _paintedPalette;
        private HudVisibility _visibility = HudVisibility.Full;
        private LocalizedTexts _texts;
        private AimBlockCatalog _aimBlockCatalog;
        private bool _aimBlockCatalogResolved;
        private AimBlockSettings _aimBlockSettings;

        /// <summary>表示中の文字列。</summary>
        public HudModel Model => _model;

        /// <summary>
        /// 目からの射線が遮られているときの照準の知らせ方 (<see cref="RefreshAimBlock"/>)。初めて読むときに <see cref="AimBlockCatalog.Load"/> で探し、
        /// 見つからなければ null (<see cref="AimBlockSettings.Default"/> と、コードで作る印で出す)。
        /// </summary>
        public AimBlockCatalog AimBlockCatalog
        {
            get
            {
                if (!_aimBlockCatalogResolved)
                {
                    _aimBlockCatalog = AimBlockCatalog.Load();
                    _aimBlockCatalogResolved = true;
                }

                return _aimBlockCatalog;
            }
            set
            {
                _aimBlockCatalog = value;
                _aimBlockCatalogResolved = true;
                _aimBlockSettings = null;
                _view?.SetAimBlockMarker(value != null ? value.Marker : null);
            }
        }

        /// <summary>Label に写している View。UIDocument の木がまだ無ければ null。</summary>
        public HudView View => _view;

        /// <summary>この HUD 用に作り、破棄時に一緒に消す PanelSettings。無ければ null。</summary>
        public PanelSettings OwnedPanelSettings => _ownedPanelSettings;

        /// <summary>
        /// HUD 用の PanelSettings と UIDocument を持つ GameObject を objectName で作り、Resources の Hud.uxml とテーマを読み込んで
        /// <see cref="Initialize"/> で源を渡す。源はどれも null 可 (Set 系で <see cref="Model"/> に直接書く場合)。
        /// UIDocument が有効になる前に参照をすべて設定するため、GameObject は無効で作ってから有効にする。
        /// 失敗したら作りかけのものは消して例外を投げる。
        /// </summary>
        /// <exception cref="InvalidOperationException">Resources にレイアウトかテーマが無い。</exception>
        public static Hud Create(string objectName, Damageable player, WeaponController weapon, Match match)
        {
            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"HUD layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under Assets/game/UI/Resources)");
            }

            var theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"HUD theme 'Resources/{ThemeResourcePath}.tss' was not found (expected under Assets/game/UI/Resources)");
            }

            PanelSettings panelSettings = null;
            GameObject hudObject = null;
            try
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = PanelSettingsName;
                panelSettings.themeStyleSheet = theme;
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.referenceResolution = ReferenceResolution;
                panelSettings.match = 0.5f;

                hudObject = new GameObject(objectName);
                hudObject.SetActive(false);
                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;

                var hud = hudObject.AddComponent<Hud>();
                AddViews(hudObject);
                hud.Initialize(player, weapon, match, panelSettings);

                // 有効にすると UIDocument が木を作り、Play 中は Hud の OnEnable が Label を探す。
                // Edit Mode では Hud の OnEnable が走らないので、木ができていればここで探す。
                hudObject.SetActive(true);
                if (!Application.isPlaying)
                {
                    hud.RebuildView();
                }

                return hud;
            }
            catch
            {
                DestroySafely(hudObject);
                DestroySafely(panelSettings);
                throw;
            }
        }

        /// <summary>
        /// HUD の部分ごとのビュー (<see cref="HitMarkerView"/>・<see cref="DamageNumberView"/>・<see cref="DamageDirectionView"/>・<see cref="GrenadeWarningView"/>・<see cref="DamageVignetteView"/>・<see cref="FlashOverlay"/>・<see cref="KillFeedView"/>・
        /// <see cref="SubtitleView"/>・<see cref="ScoreboardView"/>・<see cref="LoadoutSelectView"/>・<see cref="TutorialPromptView"/>・<see cref="ModeHudView"/>・<see cref="KillCamView"/>・
        /// <see cref="GrenadeCountView"/>・<see cref="AbilitySlotsView"/>・<see cref="DominationView"/>) と <see cref="PauseMenu"/>・その設定画面 (<see cref="PauseSettingsView"/>)・フォトモード (<see cref="PhotoModeView"/>) を
        /// hudObject に付ける。グレネード・能力の枠・拠点制圧は、出すものを渡されるまで隠れている。
        /// オフライン (<see cref="Create"/>) とオンライン (Fps.Net の NetHud は <see cref="Create"/> を通る) で同じものを付ける。
        /// 既に付いているものは付け直さない。
        /// </summary>
        public static void AddViews(GameObject hudObject)
        {
            if (hudObject == null)
            {
                throw new ArgumentNullException(nameof(hudObject));
            }

            AddIfMissing<HitMarkerView>(hudObject);
            AddIfMissing<DamageNumberView>(hudObject);
            AddIfMissing<DamageDirectionView>(hudObject);
            AddIfMissing<GrenadeWarningView>(hudObject);
            AddIfMissing<DamageVignetteView>(hudObject);
            AddIfMissing<FlashOverlay>(hudObject);
            AddIfMissing<KillFeedView>(hudObject);
            AddIfMissing<SubtitleView>(hudObject);
            AddIfMissing<ScoreboardView>(hudObject);
            AddIfMissing<LoadoutSelectView>(hudObject);
            AddIfMissing<InventoryView>(hudObject);
            AddIfMissing<TutorialPromptView>(hudObject);
            AddIfMissing<InteractPromptView>(hudObject);
            AddIfMissing<ModeHudView>(hudObject);
            AddIfMissing<KillCamView>(hudObject);
            AddIfMissing<GrenadeCountView>(hudObject);
            AddIfMissing<AbilitySlotsView>(hudObject);
            AddIfMissing<DominationView>(hudObject);
            AddIfMissing<PauseMenu>(hudObject);
            // PauseMenu の後に付けると、木を作り直したときも PauseMenu が「設定」のボタンを持ってから開く処理を渡せる。
            AddIfMissing<PauseSettingsView>(hudObject);
            AddIfMissing<PhotoModeView>(hudObject);
        }

        /// <summary>同じ GameObject のヒットマーカー。<see cref="AddViews"/> で付けていなければ null。</summary>
        public HitMarkerView HitMarker => GetComponent<HitMarkerView>();

        /// <summary>同じ GameObject の被弾方向。<see cref="AddViews"/> で付けていなければ null。</summary>
        public DamageDirectionView DamageDirection => GetComponent<DamageDirectionView>();

        /// <summary>同じ GameObject の被弾の画面効果と低体力の演出。<see cref="AddViews"/> で付けていなければ null。</summary>
        public DamageVignetteView DamageVignette => GetComponent<DamageVignetteView>();

        public FlashOverlay FlashOverlay => GetComponent<FlashOverlay>();

        /// <summary>同じ GameObject のキルフィード。<see cref="AddViews"/> で付けていなければ null。</summary>
        public KillFeedView KillFeed => GetComponent<KillFeedView>();

        /// <summary>同じ GameObject の音の合図の字幕。<see cref="AddViews"/> で付けていなければ null。</summary>
        public SubtitleView Subtitles => GetComponent<SubtitleView>();

        /// <summary>同じ GameObject のスコアボード。<see cref="AddViews"/> で付けていなければ null。</summary>
        public ScoreboardView Scoreboard => GetComponent<ScoreboardView>();

        /// <summary>同じ GameObject のロードアウト選択。<see cref="AddViews"/> で付けていなければ null。</summary>
        public LoadoutSelectView LoadoutSelect => GetComponent<LoadoutSelectView>();

        public InventoryView InventoryScreen => GetComponent<InventoryView>();

        /// <summary>同じ GameObject のチュートリアルの案内。<see cref="AddViews"/> で付けていなければ null。</summary>
        public TutorialPromptView TutorialPrompt => GetComponent<TutorialPromptView>();

        /// <summary>同じ GameObject の Interact の案内。<see cref="AddViews"/> で付けていなければ null。</summary>
        public InteractPromptView InteractPrompt => GetComponent<InteractPromptView>();

        /// <summary>同じ GameObject のオフラインのモードの表示。<see cref="AddViews"/> で付けていなければ null。</summary>
        public ModeHudView ModeHud => GetComponent<ModeHudView>();

        /// <summary>同じ GameObject のキルカムの枠と見出し。<see cref="AddViews"/> で付けていなければ null。</summary>
        public KillCamView KillCam => GetComponent<KillCamView>();

        /// <summary>同じ GameObject の残りのグレネードの数。<see cref="AddViews"/> で付けていなければ null。</summary>
        public GrenadeCountView GrenadeCount => GetComponent<GrenadeCountView>();

        /// <summary>同じ GameObject の能力の枠。<see cref="AddViews"/> で付けていなければ null。</summary>
        public AbilitySlotsView AbilitySlots => GetComponent<AbilitySlotsView>();

        /// <summary>同じ GameObject の拠点制圧の表示。<see cref="AddViews"/> で付けていなければ null。</summary>
        public DominationView Domination => GetComponent<DominationView>();

        /// <summary>同じ GameObject のポーズメニュー。<see cref="AddViews"/> で付けていなければ null。</summary>
        public PauseMenu PauseMenu => GetComponent<PauseMenu>();

        /// <summary>同じ GameObject のポーズ画面の設定画面。<see cref="AddViews"/> で付けていなければ null。</summary>
        public PauseSettingsView PauseSettings => GetComponent<PauseSettingsView>();

        public PhotoModeView PhotoMode => GetComponent<PhotoModeView>();

        IScoreboard IHud.Scoreboard => AsInterface<IScoreboard>(Scoreboard);

        IHitMarker IHud.HitMarker => AsInterface<IHitMarker>(HitMarker);

        IModeHud IHud.ModeHud => AsInterface<IModeHud>(ModeHud);

        ITutorialPrompt IHud.TutorialPrompt => AsInterface<ITutorialPrompt>(TutorialPrompt);

        IInteractPrompt IHud.InteractPrompt => AsInterface<IInteractPrompt>(InteractPrompt);

        /// <summary>ポーズを開いている間の時間の扱い (<see cref="Fps.Runtime.PauseMenu.TimeMode"/>) を決める。ポーズメニューが無ければ何もしない。</summary>
        public void SetPauseTimeMode(PauseTimeMode mode)
        {
            var pause = PauseMenu;
            if (pause != null)
            {
                pause.TimeMode = mode;
            }
        }

        // AddViews で付いているものを返す。AddViews を通さずに組んだ HUD だけ、ここで付ける (AttachTo は付いていればそれを返す)。
        IGrenadeCount IHud.AttachGrenadeCount()
        {
            return GrenadeCountView.AttachTo(this);
        }

        IAbilitySlots IHud.AttachAbilitySlots()
        {
            return AbilitySlotsView.AttachTo(this);
        }

        // エディターの GetComponent は見つからなくても「null と等しい」オブジェクトを返すので、インターフェースに変える前に本当の null にする
        private static T AsInterface<T>(Component component)
            where T : class
        {
            return component != null ? component as T : null;
        }

        /// <summary>
        /// チームの点数・スコアボードの見出し・キルログの名前・拠点制圧の欄を塗る色を決める。View を作り直しても引き継ぐ。
        /// 色の組に従わせていた (<see cref="UseTeamPalette"/>) ときはやめ、この色のままにする。
        /// </summary>
        public void SetTeamColors(Color alpha, Color bravo)
        {
            StopFollowingPalette();
            ApplyTeamColors(alpha, bravo);
        }

        /// <summary>
        /// viewer のチームから見た敵味方の色 (<see cref="TeamColors"/>) で塗り、設定の色の組が変わったら塗り直す。
        /// viewer が null (自分のチームがまだ無い) なら Alpha を味方の色にする。チームが変わったら呼び直す。
        /// </summary>
        public void UseTeamPalette(Team? viewer)
        {
            _paletteViewer = viewer;
            if (!_followsPalette)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsPalette = true;
            }

            ApplyPaletteColors();
        }

        /// <summary>色の組に従っている (<see cref="UseTeamPalette"/>) か。</summary>
        public bool FollowsTeamPalette => _followsPalette;

        /// <summary>team の点数を塗る色。<see cref="SetTeamColors"/> の前は白。</summary>
        public Color TeamColor(Team team)
        {
            return team == Team.Bravo ? _bravoColor : _alphaColor;
        }

        private void ApplyTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            _view?.SetTeamColors(alpha, bravo);

            var scoreboard = Scoreboard;
            if (scoreboard != null)
            {
                scoreboard.RefreshTeamColors();
            }

            var killFeed = KillFeed;
            if (killFeed != null)
            {
                killFeed.RefreshTeamColors();
            }

            var domination = GetComponent<DominationView>();
            if (domination != null)
            {
                domination.RefreshTeamColors();
            }
        }

        private void ApplyPaletteColors()
        {
            var palette = TeamColors.Current;
            _paintedPalette = palette.Kind;
            ApplyTeamColors(
                TeamColors.Of(palette, Team.Alpha, _paletteViewer),
                TeamColors.Of(palette, Team.Bravo, _paletteViewer));
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // 破棄の後に届いた通知では消えた要素を塗らない。感度などほかの設定だけが変わったときも、キルログを作り直さない
            if (this == null || settings.Palette == _paintedPalette)
            {
                return;
            }

            ApplyPaletteColors();
        }

        private void StopFollowingPalette()
        {
            if (_followsPalette)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsPalette = false;
            }
        }

        /// <summary>今出している項目。<see cref="ApplyDensity"/> の前は <see cref="HudVisibility.Full"/>。</summary>
        public HudVisibility Visibility => _visibility;

        /// <summary>
        /// HUD の量 (<see cref="GameRules.HudDensity"/>) に合わせて出す項目を切り替える (<see cref="HudVisibility.For"/>)。
        /// オフラインは GameBootstrap とキャンペーンがルールを決めたときに、オンラインは NetHud がサーバーのルールが届いたときに呼ぶ。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">density が定義済みの値でない。</exception>
        public void ApplyDensity(HudDensity density)
        {
            ApplyVisibility(HudVisibility.For(density));
        }

        /// <summary>
        /// 出す項目を visibility に替える。弾数の書式 (<see cref="HudModel.SetReserveAmmoShown"/>)、Label の欄 (<see cref="HudView"/>)、
        /// 部分ごとのビュー (<see cref="HitMarkerView"/>・<see cref="DamageDirectionView"/>・<see cref="KillFeedView"/>) に効かせる。
        /// View を作り直しても引き継ぐ。
        /// </summary>
        public void ApplyVisibility(HudVisibility visibility)
        {
            if (visibility == null)
            {
                throw new ArgumentNullException(nameof(visibility));
            }

            var changed = visibility != _visibility;
            _visibility = visibility;
            _model.SetReserveAmmoShown(visibility.ReserveAmmo);
            _view?.ApplyVisibility(visibility);
            ApplyVisibilityToElementViews();

            if (changed)
            {
                Debug.Log($"{LogTag} density={visibility.Density}", this);
            }
        }

        /// <summary>
        /// 値の源を渡して購読する。どれも null 可 (その項目は仮の表示になる)。前に渡した源の購読は外す。
        /// UIDocument の木が既にあればすぐ Label に反映する。
        /// </summary>
        /// <param name="player">プレイヤーの体力。</param>
        /// <param name="weapon">プレイヤーの武器。未初期化 (Edit Mode など) なら初期化されたときに購読する。</param>
        /// <param name="match">残り時間を出す試合。null なら後で <see cref="BindMatch"/> で渡す。</param>
        /// <param name="ownedPanelSettings">この HUD 用に作った PanelSettings。渡すと破棄時に一緒に消す。null 可。</param>
        public void Initialize(Damageable player, WeaponController weapon, Match match, PanelSettings ownedPanelSettings = null)
        {
            UnsubscribeSources();

            _player = player;
            _weapon = weapon;
            _ads = weapon != null ? weapon.GetComponentInParent<AimDownSights>(true) : null;
            _mover = weapon != null ? weapon.GetComponentInParent<PlayerController>(true) : null;
            _rig = weapon != null ? weapon.GetComponentInParent<ViewRig>(true) : null;
            var damageNumbers = GetComponent<DamageNumberView>();
            if (damageNumbers != null)
            {
                damageNumbers.Viewer = _ads != null ? _ads.Camera : null;
            }

            _ownedPanelSettings = ownedPanelSettings;

            if (_player != null)
            {
                _player.HealthReset += _model.BindHealth;
            }

            if (_weapon != null)
            {
                _weapon.WeaponChanged += _model.BindWeapon;
            }

            _model.BindHealth(_player != null ? _player.Health : null);
            _model.BindWeapon(_weapon != null ? _weapon.Weapon : null);

            // オフラインのプレイヤーは持ち物を自分で持つ。ネット対戦は持ち主の HUD が届いた持ち物を後から渡す
            var inventoryView = InventoryScreen;
            var ownInventory = _player != null ? _player.GetComponent<PlayerInventory>() : null;
            if (inventoryView != null && ownInventory != null)
            {
                inventoryView.Source = ownInventory;
            }
            _model.BindMatch(match);
            RefreshCrosshair();

            RebuildView();
        }

        /// <summary>残り時間を出す試合を差し替える。null なら仮の表示に戻す。<see cref="MatchRunner"/> が試合を始めたときに呼ぶ。</summary>
        public void BindMatch(Match match)
        {
            _model.BindMatch(match);
        }

        /// <summary>
        /// 一時的な案内の源を差し替える。null なら案内を消す。<see cref="CampaignRunner"/> がキャンペーンを始めたときに呼び、
        /// 止めるときに null で外す。
        /// </summary>
        public void BindNotice(CampaignNotice notice)
        {
            _model.BindNotice(notice);
        }

        /// <summary>
        /// UIDocument の今の木から Label を探し直して View を作る。UIDocument は有効にし直すたびに木を作り直すので、
        /// そのたびに呼ぶ (Play 中は OnEnable で呼ぶ)。木がまだ無ければ何もせず false。
        /// UXML に必要な要素が無ければ <see cref="InvalidOperationException"/> を投げる。
        /// </summary>
        public bool RebuildView()
        {
            DisposeView();

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root == null || root.childCount == 0)
            {
                return false;
            }

            _view = new HudView(root, _model, _visibility);
            _view.SetTeamColors(_alphaColor, _bravoColor);
            var aimBlockCatalog = AimBlockCatalog;
            _view.SetAimBlockMarker(aimBlockCatalog != null ? aimBlockCatalog.Marker : null);
            _texts = LocalizedTexts.Bind(root);
            Localizer.Changed += HandleLanguageChanged;
            ApplyVisibilityToElementViews();
            foreach (var view in GetComponents<HudElementView>())
            {
                view.Bind(root);
            }

            return true;
        }

        private void OnEnable()
        {
            TryRebuildViewAndLog();
        }

        private void Start()
        {
            // OnEnable の時点で UIDocument の木がまだ無かった場合の取りこぼしを拾う。
            if (_view == null)
            {
                TryRebuildViewAndLog();
            }

            if (_view == null)
            {
                Debug.LogError($"{LogTag} the UIDocument has no visual tree; check its PanelSettings and source asset", this);
            }
        }

        private void LateUpdate()
        {
            // WeaponController を渡さない HUD (NetHud) は Set 系で進みや照準を書くので、ここで読み直して消さない。
            if (_weapon != null)
            {
                _model.RefreshReloadProgress();
                _model.RefreshWeaponGauges();
                RefreshMagnification();
                RefreshLockOn();
                RefreshCrosshair();
            }
        }

        // ロックオンは覗き込みと相手で決まり、HudModel が持つ Weapon からは読めないので、武器に渡したロックオンから読んで書く。
        private void RefreshLockOn()
        {
            var lockOn = _weapon.LockOn;
            if (lockOn != null)
            {
                lockOn.ShowOn(_model);
            }
            else
            {
                _model.ClearLockOn();
            }
        }

        // 倍率は覗き込みの状態で決まり、HudModel が持つ Weapon からは読めないので、ADS から読んで書く。
        private void RefreshMagnification()
        {
            var magnification = _ads != null ? _ads.Magnification : null;
            if (magnification.HasValue)
            {
                _model.SetMagnification(magnification.Value);
            }
            else
            {
                _model.ClearMagnification();
            }
        }

        /// <summary>
        /// 照準の線を、武器の今の散らばり (<see cref="WeaponController.CrosshairConeDegrees"/>) の縁をカメラ (<see cref="AimDownSights.Camera"/>) の
        /// 視野角で画面に写した位置 (<see cref="SpreadPattern.ScreenRadius"/>) に置き、撃った後の開き (<see cref="WeaponController.CrosshairSpreadFraction"/>) を足す。
        /// 照準を出すかは <see cref="ShowsCrosshair"/> で決める (一人称の ADS 中とダッシュ中は隠す)。
        /// カメラが無ければ縁は 0 で出し、武器を渡していなければ広がり 0 で出す。値の源は <see cref="Initialize"/> の weapon と、その親の階層の ADS・プレイヤー・<see cref="ViewRig"/>。
        /// </summary>
        public void RefreshCrosshair()
        {
            if (_weapon == null)
            {
                _model.ResetCrosshair();
                ApplyAimBlock(false, null);
                return;
            }

            var aiming = _ads != null && _ads.IsAiming;
            var sprinting = _mover != null && _mover.IsSprinting;
            var view = _rig != null ? _rig.Mode : ViewMode.FirstPerson;
            var shown = ShowsCrosshair(view, aiming, sprinting);
            _model.SetCrosshair(_weapon.CrosshairSpreadFraction, CrosshairConeGap(), shown);
            RefreshAimBlock(shown);
        }

        /// <summary>
        /// 照準を出している間、目から画面中央が指す点への線がその点より手前で遮られているかを武器の射撃と同じ条件
        /// (<see cref="ViewRig.TryFindAimBlock(Transform, Transform, float, Transform, LayerMask, float, out Vector3)"/>・武器の射程と当たるレイヤー) で調べ、
        /// 遮られていれば遮られた点の画面上の位置を <see cref="HudModel.SetAimBlock"/> で渡す。視点に関係なく同じ判定を使う。
        /// 出すかと余裕は <see cref="AimBlockCatalog"/> から読み、変わったときに <c>[HUD] aim blocked=True</c> のようにログに出す。
        /// </summary>
        private void RefreshAimBlock(bool crosshairShown)
        {
            var settings = AimBlockSettingsInUse();
            var aim = _weapon.Aim;
            var eye = _rig != null ? ViewRig.FindEye(_rig.transform) : null;
            var range = _weapon.HitscanRange;
            if (!crosshairShown || !settings.Enabled || aim == null || eye == null || aim.parent != eye
                || !(range > 0f) || float.IsInfinity(range))
            {
                ApplyAimBlock(false, null);
                return;
            }

            if (!ViewRig.TryFindAimBlock(aim, eye, range, aim.root, _weapon.HitMask, settings.Margin, out var point))
            {
                ApplyAimBlock(false, null);
                return;
            }

            var camera = _ads != null && _ads.Camera != null ? _ads.Camera : aim.GetComponent<Camera>();
            ApplyAimBlock(true, AimBlockViewport(camera, point));
        }

        /// <summary>world の点をカメラのビューポート座標にする。カメラが無いか点がカメラの後ろなら null。</summary>
        public static System.Numerics.Vector2? AimBlockViewport(Camera camera, Vector3 world)
        {
            if (camera == null)
            {
                return null;
            }

            var viewport = camera.WorldToViewportPoint(world);
            if (!(viewport.z > 0f)
                || float.IsNaN(viewport.x) || float.IsInfinity(viewport.x)
                || float.IsNaN(viewport.y) || float.IsInfinity(viewport.y))
            {
                return null;
            }

            return new System.Numerics.Vector2(viewport.x, viewport.y);
        }

        private void ApplyAimBlock(bool blocked, System.Numerics.Vector2? viewport)
        {
            var changed = blocked != _model.IsAimBlocked;
            if (blocked)
            {
                _model.SetAimBlock(viewport);
            }
            else
            {
                _model.ClearAimBlock();
            }

            if (changed)
            {
                Debug.Log($"{LogTag} aim blocked={blocked}", this);
            }
        }

        private AimBlockSettings AimBlockSettingsInUse()
        {
            if (_aimBlockSettings != null)
            {
                return _aimBlockSettings;
            }

            var catalog = AimBlockCatalog;
            if (catalog == null)
            {
                _aimBlockSettings = AimBlockSettings.Default;
                return _aimBlockSettings;
            }

            try
            {
                _aimBlockSettings = catalog.ToSettings();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogWarning($"{LogTag} aim block catalog '{catalog.name}' is invalid; using the defaults: {e.Message}", this);
                _aimBlockSettings = AimBlockSettings.Default;
            }

            return _aimBlockSettings;
        }

        public static bool ShowsCrosshair(ViewMode view, bool aiming, bool sprinting)
        {
            // 三人称の ADS は肩越しに寄るだけでサイトを覗かないので、照準を消すと狙う点が分からなくなる。
            return !sprinting && (!aiming || view == ViewMode.ThirdPerson);
        }

        /// <summary>
        /// 今の武器の散らばりの縁が画面の中央から離れる距離 (HUD の基準解像度の px)。カメラか武器が無ければ 0。
        /// 画面の縦半分は UIDocument の木の高さ (パネルの拡縮後の px) から取り、まだ並べていなければ <see cref="ReferenceResolution"/> の縦半分を使う。
        /// </summary>
        public float CrosshairConeGap()
        {
            var camera = _ads != null ? _ads.Camera : null;
            if (_weapon == null || camera == null || camera.orthographic)
            {
                return 0f;
            }

            var cone = _weapon.CrosshairConeDegrees;
            var fov = camera.fieldOfView;
            if (cone <= 0f || !(fov > 0f && fov < 180f))
            {
                return 0f;
            }

            return SpreadPattern.ScreenRadius(cone, fov, PanelHalfHeight());
        }

        private float PanelHalfHeight()
        {
            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            var height = root != null ? root.layout.height : float.NaN;
            return float.IsNaN(height) || height <= 0f ? ReferenceResolution.y * 0.5f : height * 0.5f;
        }

        private void TryRebuildViewAndLog()
        {
            try
            {
                if (RebuildView())
                {
                    Debug.Log($"{LogTag} ready hp={HudNumberFormat.Health(_model)} weapon={_model.WeaponSpec?.Id} ammo={HudNumberFormat.Ammo(_model)} time={HudNumberFormat.Time(_model)}", this);
                }
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{LogTag} could not bind the HUD layout: {e.Message}", this);
            }
        }

        private void OnDisable()
        {
            DisposeView();
        }

        private void OnDestroy()
        {
            StopFollowingPalette();
            DisposeView();
            UnsubscribeSources();
            _model.UnbindAll();

            if (_ownedPanelSettings != null)
            {
                Destroy(_ownedPanelSettings);
                _ownedPanelSettings = null;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void DisposeView()
        {
            foreach (var view in GetComponents<HudElementView>())
            {
                // 破棄の途中では先に消えたビューが null として返ることがある。
                if (view != null)
                {
                    view.Unbind();
                }
            }

            Localizer.Changed -= HandleLanguageChanged;
            _texts = null;
            if (_view == null)
            {
                return;
            }

            _view.Dispose();
            _view = null;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            // 破棄の後に届いた通知では消えた要素に書かない。
            if (this == null)
            {
                return;
            }

            _texts?.Refresh();
            _view?.RefreshText();
        }

        // スコアボード・ロードアウト選択・ポーズは自分で開くものなので、HUD の量では隠さない
        private void ApplyVisibilityToElementViews()
        {
            var hitMarker = HitMarker;
            if (hitMarker != null)
            {
                hitMarker.SetAllowed(_visibility.HitMarker);
            }

            // ダメージの数値も当てた手応えなので、ハードコアで隠すヒットマーカーと一緒に出し入れする。
            var damageNumbers = GetComponent<DamageNumberView>();
            if (damageNumbers != null)
            {
                damageNumbers.SetAllowed(_visibility.HitMarker);
            }

            var damageDirection = DamageDirection;
            if (damageDirection != null)
            {
                damageDirection.SetAllowed(_visibility.DamageDirection);
            }

            // 近くのグレネードも被弾方向と同じく身の危険の向きを知らせるものなので、一緒に出し入れする。
            var grenadeWarning = GetComponent<GrenadeWarningView>();
            if (grenadeWarning != null)
            {
                grenadeWarning.SetAllowed(_visibility.DamageDirection);
            }

            var killFeed = KillFeed;
            if (killFeed != null)
            {
                killFeed.SetAllowed(_visibility.KillFeed);
            }

            // グレネードの数は持っている弾の一種なので、弾数と一緒に出し入れする。
            var grenades = GetComponent<GrenadeCountView>();
            if (grenades != null)
            {
                grenades.SetAllowed(_visibility.Ammo);
            }

            // 能力の回数も持っているものの残りなので、弾数と一緒に出し入れする。
            var abilities = GetComponent<AbilitySlotsView>();
            if (abilities != null)
            {
                abilities.SetAllowed(_visibility.Ammo);
            }
        }

        private static void AddIfMissing<T>(GameObject target)
            where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }

        private void UnsubscribeSources()
        {
            if (_player != null)
            {
                _player.HealthReset -= _model.BindHealth;
            }

            if (_weapon != null)
            {
                _weapon.WeaponChanged -= _model.BindWeapon;
            }

            _player = null;
            _weapon = null;
            _ads = null;
            _mover = null;
        }
    }
}
