using Godot;
using System.Collections.Generic;

/// <summary>ACT1 商店最小页面：稳定三卡库存、购买和离开结算。</summary>
public partial class ShopController : Control
{
    [Export] public PackedScene SettingsDialogScene { get; set; }

    private Label _titleLabel;
    private Label _descriptionLabel;
    private Label _resultLabel;
    private VBoxContainer _itemContainer;
    private readonly List<CardInfo> _inventory = new();
    private IReadOnlyList<int> _prices = System.Array.Empty<int>();
    private readonly List<Button> _purchaseButtons = new();
    private TopBar _topBar;
    private NodePageNavigationCoordinator _nodeNavigation;
    private ShopPurchaseCommand _purchaseCommand;
    private bool _invalidEntry;
    private ShopDefinition _shopDefinition;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("ContentPanel/TitleLabel");
        _descriptionLabel = GetNode<Label>("ContentPanel/DescriptionLabel");
        _resultLabel = GetNode<Label>("ContentPanel/ResultLabel");
        _itemContainer = GetNode<VBoxContainer>("ContentPanel/ItemScroll/ItemContainer");
        _topBar = GetNode<TopBar>("TopBar");
        _topBar.OnDeckPressed = () => DeckViewer.Show(this);
        _topBar.OnMapPressed = ToggleMapOverlay;
        _topBar.OnSettingsPressed = () => SettingsHelper.Show(this, SettingsDialogScene);
        _nodeNavigation = new NodePageNavigationCoordinator(this, GameManager.Instance,
            error => _resultLabel.Text = $"地图导航失败：{error}");

        var gm = GameManager.Instance;
        var node = gm.ActiveNode == null ? null : gm.MapGraph?.GetNode(gm.ActiveNode.NodeId);
        if (gm.ActiveNode == null || gm.ActiveNode.NodeType != MapGraphNodeType.Shop || node == null)
        {
            GD.PrintErr("[商店] 场景进入失败：缺少有效 Shop NodeContext。");
            _invalidEntry = true;
            ShowInvalidState();
            return;
        }

        if (!ShopDefinitionCatalog.TryGet(node.ContentId, out _shopDefinition, out var definitionError))
        {
            GD.PrintErr($"[商店] 商店定义绑定失败：{definitionError}");
            _invalidEntry = true;
            ShowInvalidState();
            return;
        }

        if (!BuildInventory(node.NodeId, out var inventoryError))
        {
            GD.PrintErr($"[商店] 库存构建失败：{inventoryError}");
            _invalidEntry = true;
            ShowInvalidState();
            return;
        }
        NodeMapEntry.Add(this, ToggleMapOverlay);
        RefreshItems();
    }

    private bool BuildInventory(string nodeId, out string error)
    {
        error = "";
        if (!ShopInventoryService.TryBuild(_shopDefinition,
                GameManager.Instance.RandomStreams.CreateShopStream(nodeId, 0),
                out var inventory, out error))
            return false;

        _inventory.Clear();
        _inventory.AddRange(inventory.Cards);
        _prices = inventory.Prices;
        try
        {
            _purchaseCommand = new ShopPurchaseCommand(GameManager.Instance.RunState, _inventory, _prices);
        }
        catch (System.Exception exception)
        {
            error = $"商店购买命令初始化失败：{exception.Message}";
            GD.PrintErr($"[商店] {error}");
            return false;
        }
        return true;
    }

    private void RefreshItems()
    {
        foreach (Node child in _itemContainer.GetChildren())
            child.QueueFree();
        _purchaseButtons.Clear();

        _titleLabel.Text = _shopDefinition.Title;
        _descriptionLabel.Text = _shopDefinition.Description;
        _resultLabel.Text = $"当前灵韵：{GameManager.Instance.LingYun}";
        for (int i = 0; i < _inventory.Count; i++)
        {
            int index = i;
            int price = _prices[i];
            var button = new Button
            {
                Text = _purchaseCommand.IsSold(i)
                    ? $"已售出  { _inventory[i].Name }"
                    : $"{_inventory[i].Name}  | {price} 灵韵\n{_inventory[i].Description.Replace("{0}", _inventory[i].Value.ToString())}",
                CustomMinimumSize = new Vector2(560, 82),
                Disabled = _purchaseCommand.IsSold(i) || GameManager.Instance.LingYun < price,
            };
            button.AddThemeFontSizeOverride("font_size", 16);
            if (!_purchaseCommand.IsSold(i) && GameManager.Instance.LingYun < price)
                button.TooltipText = "灵韵不足";
            button.Pressed += () => BuyCard(index);
            _itemContainer.AddChild(button);
            _purchaseButtons.Add(button);
        }

    }

    private void BuyCard(int index)
    {
        if (_purchaseCommand == null)
        {
            _resultLabel.Text = "商店购买服务未初始化，请查看日志。";
            GD.PrintErr("[商店] 购买被拒绝：购买命令为空。");
            return;
        }

        if (!_purchaseCommand.TryPurchase(index, out var result))
        {
            _resultLabel.Text = result.Message;
            GD.PrintErr($"[商店] 购买被拒绝：{result.Message}");
            return;
        }

        GD.Print($"[商店] 已购买：{result.CardDefinitionId}，价格={result.Price}");
        RefreshItems();
    }

    private void ShowInvalidState()
    {
        _titleLabel.Text = "商店节点不可用";
        _descriptionLabel.Text = "当前入口无有效商店定义，不能伪造商店库存。";
        _resultLabel.Text = "请返回地图重新选择可达节点。";
        foreach (Node child in _itemContainer.GetChildren())
            child.QueueFree();
        var button = new Button { Text = "返回地图", CustomMinimumSize = new Vector2(560, 72) };
        button.Pressed += RecoverInvalidEntry;
        _itemContainer.AddChild(button);
    }

    private void RecoverInvalidEntry()
    {
        if (!_invalidEntry) return;
        if (!GameManager.Instance.RecoverFromInvalidNodeEntry(out var error))
        {
            GD.PrintErr($"[商店] 无效入口恢复失败：{error}");
            return;
        }
        ToggleMapOverlay();
    }

    private void ToggleMapOverlay()
    {
        if (_nodeNavigation == null)
        {
            _resultLabel.Text = "地图导航服务未初始化，请查看日志。";
            return;
        }
        if (!_nodeNavigation.TryToggleMap(out var error) && !string.IsNullOrWhiteSpace(error))
            _resultLabel.Text = $"地图 overlay 创建失败：{error}";
    }

    public override void _ExitTree()
    {
        _nodeNavigation?.Dispose();
    }
}
