using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class InventoryView : HudElementView
    {
        public const string UxmlName = "inventory";

        public const string LogTag = "[Inventory]";

        public const string ToggleActionPath = InputActionPaths.Inventory;

        public const Key RotateKey = Key.R;

        public const float CellSize = 56f;

        public const string TitleKey = "hud.inventory.title";
        public const string WeightKey = "hud.inventory.weight";
        public const string WeightNoLimitKey = "hud.inventory.weightNoLimit";
        public const string StacksKey = "hud.inventory.stacks";
        public const string EmptyKey = "hud.inventory.empty";
        public const string CountKey = "hud.inventory.count";
        public const string UseKey = "hud.inventory.use";
        public const string EquipKey = "hud.inventory.equip";
        public const string DropKey = "hud.inventory.drop";
        public const string DropOneKey = "hud.inventory.dropOne";
        public const string DropAllKey = "hud.inventory.dropAll";
        public const string AttachKey = "hud.inventory.attach";
        public const string DetachKey = "hud.inventory.detach";
        public const string AttachedKey = "hud.inventory.attached";
        public const string HintKey = "hud.inventory.hint";

        public const string PanelClass = "inventory";
        public const string HeaderClass = "inventory__header";
        public const string TitleClass = "inventory__title";
        public const string StatusClass = "inventory__status";
        public const string GridClass = "inventory__grid";
        public const string CellClass = "inventory__cell";
        public const string ListClass = "inventory__list";
        public const string ItemClass = "inventory__item";
        public const string ListItemClass = "inventory__item--row";
        public const string SelectedItemClass = "inventory__item--selected";
        public const string GhostClass = "inventory__ghost";
        public const string GhostBlockedClass = "inventory__ghost--blocked";
        public const string ItemNameClass = "inventory__item-name";
        public const string ItemCountClass = "inventory__item-count";
        public const string DetailsClass = "inventory__details";
        public const string AttachedClass = "inventory__attached";
        public const string ButtonClass = "inventory__button";
        public const string HintClass = "inventory__hint";
        public const string EmptyClass = "inventory__empty";
        public const string LockedClass = "inventory__locked";

        private const string _pauseActionPath = "Player/Pause";

        // 開いている画面の数。GameUi.IsInventoryOpen と FirstPersonLook がカーソルをロックしないために見る
        private static int _openCount;

        private readonly List<InputAction> _suspended = new List<InputAction>();
        private IInventoryControl _source;
        private ProgressionLedger _ledger;
        private PlayerProfile _profile;
        private ItemIconCatalog _icons;
        private bool _iconsLoaded;
        private bool _open;
        private bool _dirty = true;
        private int _selectedHandle;
        [NonSerialized]
        private InputAction _toggleAction;
        private bool _resolvedToggleAction;
        private bool _enabledToggleAction;
        private bool _countedOpen;
        private bool _subscribed;

        // 出している持ち物が GameSignals から来たか。オフラインの HUD が直に渡した持ち物は、GameSignals が空のままでも手放さない
        private bool _showsSignal;

        private VisualElement _grid;
        private VisualElement _ghost;
        private int _dragHandle;
        private int _dragPointerId = -1;
        private bool _dragRotated;
        private Vector2 _dragGrabCells;
        private Vector2 _dragPointer;

        public static bool IsAnyOpen => _openCount > 0;

        public override string ElementName => UxmlName;

        public bool IsOpen => _open;

        public IInventoryControl Source
        {
            get => _source;
            set
            {
                if (_source == value)
                {
                    return;
                }

                if (_source != null)
                {
                    _source.Changed -= MarkDirty;
                }

                _source = value;
                _showsSignal = false;
                if (_source != null)
                {
                    _source.Changed += MarkDirty;
                }

                _selectedHandle = 0;
                MarkDirty();
                if (_source == null)
                {
                    SetOpen(false);
                }
            }
        }

        public int SelectedHandle => _selectedHandle;

        // IInventoryControl.Attach は断った理由を返さないので、Locked になるかは押す前にここで同じ表から求めて文を出す。null を渡すと全部使える
        public void ShowUnlocks(ProgressionLedger ledger, PlayerProfile profile)
        {
            if (ledger != null && profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _ledger = ledger;
            _profile = ledger != null ? profile : null;
            MarkDirty();
        }

        public void SetOpen(bool open)
        {
            if (open && (_source == null || _source.Inventory == null))
            {
                open = false;
            }

            if (_open == open)
            {
                return;
            }

            _open = open;
            CancelDrag();
            if (open)
            {
                _dirty = true;
                CountOpen(true);
                SuspendPlayerActions();
                Debug.Log($"{LogTag} opened stacks={_source.Inventory.Stacks.Count}", this);
            }
            else
            {
                ResumePlayerActions();
                CountOpen(false);
                Debug.Log($"{LogTag} closed", this);
            }

            Refresh();
        }

        public void Select(int handle)
        {
            var inventory = _source?.Inventory;
            _selectedHandle = inventory != null && inventory.IndexOfHandle(handle) >= 0 ? handle : 0;
            MarkDirty();
        }

        protected override void OnBound(VisualElement element)
        {
            element.AddToClassList(PanelClass);
            Subscribe();
            var inventory = GameSignals.Inventory.Value;
            if (inventory != null || _showsSignal)
            {
                ShowSignal(inventory);
            }

            _dirty = true;
            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            MarkDirty();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            CancelDrag();
            element.Clear();
            element.style.display = DisplayStyle.None;
            _grid = null;
            _ghost = null;
        }

        private void OnEnable()
        {
            AcquireToggleAction();
        }

        private void OnDisable()
        {
            ReleaseToggleAction();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            Source = null;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Inventory.Changed += HandleInventoryChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Inventory.Changed -= HandleInventoryChanged;
                _subscribed = false;
            }
        }

        private void HandleInventoryChanged(IInventoryControl inventory)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Inventory.Changed -= HandleInventoryChanged;
                return;
            }

            if (inventory == null && !_showsSignal)
            {
                return;
            }

            ShowSignal(inventory);
        }

        private void ShowSignal(IInventoryControl inventory)
        {
            Source = inventory;
            _showsSignal = inventory != null;
        }

        private void Update()
        {
            if (_open && GameUi.IsPauseOpen)
            {
                SetOpen(false);
            }

            // 閉じる側は譲らないのは、準備の時間に入る前から開いていた画面を同じキーで閉じられるようにするため
            if (_toggleAction != null && _toggleAction.WasPressedThisFrame() && !GameUi.IsPauseOpen && !GameUi.IsLoadoutSelectOpen
                && (_open || !BuyMenuView.TakesToggleKey))
            {
                SetOpen(!_open);
            }

            if (_dragHandle != 0 && _source?.Inventory != null && _source.Inventory.Rule.AllowRotation)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard[RotateKey].wasPressedThisFrame)
                {
                    _dragRotated = !_dragRotated;
                    UpdateGhost();
                }
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
            Refresh();
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (_open && _dirty && _dragHandle == 0)
            {
                Rebuild(element);
                _dirty = false;
            }

            element.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            element.pickingMode = _open ? PickingMode.Position : PickingMode.Ignore;
        }

        private void Rebuild(VisualElement element)
        {
            element.Clear();
            _grid = null;
            _ghost = null;
            var inventory = _source?.Inventory;
            if (inventory == null)
            {
                return;
            }

            if (inventory.IndexOfHandle(_selectedHandle) < 0)
            {
                _selectedHandle = 0;
            }

            var header = new VisualElement();
            header.AddToClassList(HeaderClass);
            header.Add(MakeLabel(Localizer.Resolve(new LocalizedText(TitleKey)), TitleClass));
            header.Add(MakeLabel(StatusText(inventory), StatusClass));
            element.Add(header);

            if (inventory.Rule.HasGrid)
            {
                element.Add(BuildGrid(inventory));
            }
            else
            {
                element.Add(BuildList(inventory));
            }

            if (inventory.Stacks.Count == 0)
            {
                element.Add(MakeLabel(Localizer.Resolve(new LocalizedText(EmptyKey)), EmptyClass));
            }

            element.Add(BuildDetails(inventory));
            element.Add(MakeLabel(Localizer.Resolve(new LocalizedText(HintKey, ToggleKeyLabel())), HintClass));
        }

        private static string StatusText(Inventory inventory)
        {
            var rule = inventory.Rule;
            var weight = Number(inventory.TotalWeight);
            var text = rule.MaxWeight > 0f
                ? Localizer.Resolve(new LocalizedText(WeightKey, weight, Number(rule.MaxWeight)))
                : Localizer.Resolve(new LocalizedText(WeightNoLimitKey, weight));
            if (rule.MaxStacks > 0)
            {
                text += "    " + Localizer.Resolve(new LocalizedText(
                    StacksKey, inventory.Stacks.Count.ToString(CultureInfo.InvariantCulture), rule.MaxStacks.ToString(CultureInfo.InvariantCulture)));
            }

            return text;
        }

        private VisualElement BuildGrid(Inventory inventory)
        {
            var rule = inventory.Rule;

            // 下に広げていけるなら、一番下の次の行まで出して、そこへも置けるようにする
            var rows = rule.HasFixedGridHeight ? rule.GridHeight : inventory.GridRows + 1;
            var grid = new VisualElement();
            grid.AddToClassList(GridClass);
            grid.style.width = rule.GridWidth * CellSize;
            grid.style.height = rows * CellSize;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < rule.GridWidth; x++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList(CellClass);
                    Place(cell, x, y, 1, 1);
                    grid.Add(cell);
                }
            }

            foreach (var stack in inventory.Stacks)
            {
                var box = ItemBox(inventory, stack);
                Place(box, stack.X, stack.Y, inventory.WidthOf(stack), inventory.HeightOf(stack));
                var handle = stack.Handle;
                box.RegisterCallback<PointerDownEvent>(e => BeginDrag(handle, box, e));
                grid.Add(box);
            }

            grid.RegisterCallback<PointerMoveEvent>(OnDragMove);
            grid.RegisterCallback<PointerUpEvent>(OnDragEnd);
            grid.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());
            _grid = grid;
            return grid;
        }

        private VisualElement BuildList(Inventory inventory)
        {
            var list = new VisualElement();
            list.AddToClassList(ListClass);
            foreach (var stack in inventory.Stacks)
            {
                var row = ItemBox(inventory, stack);
                row.AddToClassList(ListItemClass);
                var handle = stack.Handle;
                row.RegisterCallback<PointerDownEvent>(_ => Select(handle));
                list.Add(row);
            }

            return list;
        }

        private VisualElement ItemBox(Inventory inventory, InventoryEntry stack)
        {
            var box = new VisualElement();
            box.AddToClassList(ItemClass);
            if (stack.Handle == _selectedHandle)
            {
                box.AddToClassList(SelectedItemClass);
            }

            inventory.Catalog.TryFind(stack.ItemId, out var item);
            var icon = IconOf(item);
            if (icon != null)
            {
                box.style.backgroundImage = new StyleBackground(icon);
            }
            else
            {
                box.Add(MakeLabel(NameOf(item, stack.ItemId), ItemNameClass));
            }

            if (stack.Count > 1)
            {
                box.Add(MakeLabel(Localizer.Resolve(new LocalizedText(CountKey, stack.Count.ToString(CultureInfo.InvariantCulture))), ItemCountClass));
            }

            return box;
        }

        private VisualElement BuildDetails(Inventory inventory)
        {
            var details = new VisualElement();
            details.AddToClassList(DetailsClass);
            if (!inventory.TryGetStack(_selectedHandle, out var stack))
            {
                return details;
            }

            inventory.Catalog.TryFind(stack.ItemId, out var item);
            var weight = item != null ? inventory.WeightOf(item) * stack.Count : 0f;
            details.Add(MakeLabel(NameOf(item, stack.ItemId) + "  " + Localizer.Resolve(new LocalizedText(CountKey, stack.Count.ToString(CultureInfo.InvariantCulture)))
                + "  " + Localizer.Resolve(new LocalizedText(WeightNoLimitKey, Number(weight))), ItemNameClass));
            var handle = stack.Handle;
            if (item != null && item.IsWeapon)
            {
                details.Add(MakeButton(EquipKey, () => _source?.Use(handle)));
                details.Add(MakeButton(DropKey, () => _source?.Drop(handle, 1)));
                AddAttached(details, inventory, item, handle);
                return details;
            }

            if (item != null && item.IsUsable)
            {
                details.Add(MakeButton(UseKey, () => _source?.Use(handle)));
            }

            if (item != null && item.IsAttachment)
            {
                var attach = MakeButton(AttachKey, () => _source?.Attach(handle));
                details.Add(attach);
                if (_ledger != null && !_ledger.IsUnlocked(item.Id, _profile))
                {
                    attach.SetEnabled(false);
                    details.Add(MakeLabel(ProgressionFormat.AttachmentLocked(_ledger.UnlockLevelOf(item.Id)), LockedClass));
                }
            }

            details.Add(MakeButton(DropOneKey, () => _source?.Drop(handle, 1)));
            if (stack.Count > 1)
            {
                details.Add(MakeButton(DropAllKey, () => _source?.Drop(handle, stack.Count)));
            }

            return details;
        }

        private void AddAttached(VisualElement details, Inventory inventory, ItemSpec weapon, int weaponHandle)
        {
            foreach (var itemId in inventory.AttachmentsOf(weapon.WeaponId))
            {
                inventory.Catalog.TryFind(itemId, out var attached);
                var row = new VisualElement();
                row.AddToClassList(AttachedClass);
                row.Add(MakeLabel(Localizer.Resolve(new LocalizedText(AttachedKey, NameOf(attached, itemId))), ItemNameClass));
                var id = itemId;
                row.Add(MakeButton(DetachKey, () => _source?.Detach(weaponHandle, id)));
                details.Add(row);
            }
        }

        private void BeginDrag(int handle, VisualElement box, PointerDownEvent e)
        {
            Select(handle);
            var inventory = _source?.Inventory;
            if (_grid == null || inventory == null || e.button != 0 || !inventory.TryGetStack(handle, out var stack))
            {
                return;
            }

            _dragHandle = handle;
            _dragPointerId = e.pointerId;
            _dragRotated = stack.Rotated;
            var local = _grid.WorldToLocal(e.position);
            _dragGrabCells = new Vector2(local.x / CellSize - stack.X, local.y / CellSize - stack.Y);
            _dragPointer = local;
            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.AddToClassList(GhostClass);
            _grid.Add(_ghost);
            _grid.CapturePointer(e.pointerId);
            UpdateGhost();
            e.StopPropagation();
        }

        private void OnDragMove(PointerMoveEvent e)
        {
            if (_dragHandle == 0 || e.pointerId != _dragPointerId || _grid == null)
            {
                return;
            }

            _dragPointer = _grid.WorldToLocal(e.position);
            UpdateGhost();
        }

        private void OnDragEnd(PointerUpEvent e)
        {
            if (_dragHandle == 0 || e.pointerId != _dragPointerId)
            {
                return;
            }

            var handle = _dragHandle;
            var target = DropCell();
            var rotated = _dragRotated;
            CancelDrag();
            _source?.Move(handle, target.x, target.y, rotated);
            MarkDirty();
        }

        private void CancelDrag()
        {
            if (_grid != null && _dragPointerId >= 0 && _grid.HasPointerCapture(_dragPointerId))
            {
                _grid.ReleasePointer(_dragPointerId);
            }

            _ghost?.RemoveFromHierarchy();
            _ghost = null;
            _dragHandle = 0;
            _dragPointerId = -1;
        }

        private void UpdateGhost()
        {
            var inventory = _source?.Inventory;
            if (_ghost == null || inventory == null || !inventory.TryGetStack(_dragHandle, out var stack))
            {
                return;
            }

            var cell = DropCell();
            var moved = new InventoryEntry(stack.Handle, stack.ItemId, stack.Count, Math.Max(0, cell.x), Math.Max(0, cell.y), _dragRotated);
            Place(_ghost, cell.x, cell.y, inventory.WidthOf(moved), inventory.HeightOf(moved));
            var blocked = inventory.Check(_dragHandle, cell.x, cell.y, _dragRotated) != InventoryMoveResult.Moved;
            _ghost.EnableInClassList(GhostBlockedClass, blocked);
        }

        private Vector2Int DropCell()
        {
            var x = Mathf.FloorToInt(_dragPointer.x / CellSize - _dragGrabCells.x + 0.5f);
            var y = Mathf.FloorToInt(_dragPointer.y / CellSize - _dragGrabCells.y + 0.5f);
            return new Vector2Int(x, y);
        }

        private static void Place(VisualElement element, int x, int y, int width, int height)
        {
            element.style.position = Position.Absolute;
            element.style.left = x * CellSize;
            element.style.top = y * CellSize;
            element.style.width = width * CellSize;
            element.style.height = height * CellSize;
        }

        private Texture2D IconOf(ItemSpec item)
        {
            if (item == null || string.IsNullOrEmpty(item.IconId))
            {
                return null;
            }

            if (!_iconsLoaded)
            {
                _icons = ItemIconCatalog.Load();
                _iconsLoaded = true;
            }

            return _icons != null ? _icons.Find(item.IconId) : null;
        }

        private static string NameOf(ItemSpec item, string itemId)
        {
            return item != null && !string.IsNullOrEmpty(item.NameKey) ? Localizer.Resolve(new LocalizedText(item.NameKey)) : itemId;
        }

        private static string Number(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static Label MakeLabel(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private static Button MakeButton(string key, Action clicked)
        {
            var button = new Button(clicked) { text = Localizer.Resolve(new LocalizedText(key)) };
            button.AddToClassList(ButtonClass);
            return button;
        }

        private string ToggleKeyLabel()
        {
            return _toggleAction != null ? _toggleAction.GetBindingDisplayString(InputBinding.MaskByGroup(PlayerBindings.KeyboardMouseGroup)) : string.Empty;
        }

        private void CountOpen(bool open)
        {
            if (!Application.isPlaying || open == _countedOpen)
            {
                return;
            }

            _countedOpen = open;
            _openCount = Math.Max(0, _openCount + (open ? 1 : -1));
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else if (!PauseMenu.IsAnyOpen)
            {
                FirstPersonLook.RefreshCursorLock();
            }
        }

        // 開いている間にクリックで撃ったり視点が回ったりしないよう、開け閉めとポーズ以外の Player のアクションを止める
        private void SuspendPlayerActions()
        {
            var map = InputSystem.actions != null ? InputSystem.actions.FindActionMap(PlayerBindings.MapName) : null;
            if (!Application.isPlaying || map == null)
            {
                return;
            }

            foreach (var action in map.actions)
            {
                if (!action.enabled || action == _toggleAction || action == InputSystem.actions.FindAction(_pauseActionPath))
                {
                    continue;
                }

                action.Disable();
                _suspended.Add(action);
            }
        }

        private void ResumePlayerActions()
        {
            foreach (var action in _suspended)
            {
                if (!action.enabled)
                {
                    action.Enable();
                }
            }

            _suspended.Clear();
        }

        private void AcquireToggleAction()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_resolvedToggleAction)
            {
                _toggleAction = InputSystem.actions != null ? InputSystem.actions.FindAction(ToggleActionPath) : null;
                _resolvedToggleAction = true;
                if (_toggleAction == null)
                {
                    Debug.LogError($"{LogTag} the project actions have no '{ToggleActionPath}'; the inventory cannot be opened", this);
                }
            }

            if (_toggleAction == null || _toggleAction.enabled)
            {
                return;
            }

            _toggleAction.Enable();
            _enabledToggleAction = true;
        }

        private void ReleaseToggleAction()
        {
            if (_enabledToggleAction && _toggleAction != null)
            {
                _toggleAction.Disable();
            }

            _enabledToggleAction = false;
        }
    }
}
