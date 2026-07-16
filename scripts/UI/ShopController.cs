using Godot;
using System.Collections.Generic;

/// <summary>ACT1 商店最小页面：稳定三卡库存、购买和离开结算。</summary>
public partial class ShopController : Control
{
    private Label _titleLabel;
    private Label _descriptionLabel;
    private Label _resultLabel;
    private VBoxContainer _itemContainer;
    private readonly List<CardInfo> _inventory = new();
    private IReadOnlyList<int> _prices = System.Array.Empty<int>();
    private readonly List<Button> _purchaseButtons = new();
    private TopBar _topBar;
    private MapOverlayController _mapOverlay;
    private bool[] _sold = System.Array.Empty<bool>();
    private bool _invalidEntry;
    private bool _settled;
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
        _topBar.OnSettingsPressed = () => SettingsHelper.Show(this);

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
        _sold = new bool[_inventory.Count];
        return true;
    }

    private void RefreshItems()
    {
        foreach (Node child in _itemContainer.GetChildren())
            child.QueueFree();
        _purchaseButtons.Clear();

        _titleLabel.Text = _shopDefinition.Title;
        _descriptionLabel.Text = _shopDefinition.Description;
        if (!_settled)
            _resultLabel.Text = $"当前灵韵：{GameManager.Instance.LingYun}";
        for (int i = 0; i < _inventory.Count; i++)
        {
            int index = i;
            int price = _prices[i];
            var button = new Button
            {
                Text = _sold[i]
                    ? $"已售出  { _inventory[i].Name }"
                    : $"{_inventory[i].Name}  | {price} 灵韵\n{_inventory[i].Description.Replace("{0}", _inventory[i].Value.ToString())}",
                CustomMinimumSize = new Vector2(560, 82),
                Disabled = _settled || _sold[i] || GameManager.Instance.LingYun < price,
            };
            button.AddThemeFontSizeOverride("font_size", 16);
            if (!_sold[i] && GameManager.Instance.LingYun < price)
                button.TooltipText = "灵韵不足";
            button.Pressed += () => BuyCard(index, price);
            _itemContainer.AddChild(button);
            _purchaseButtons.Add(button);
        }

        var leaveButton = new Button
        {
            Text = _settled ? "当前商店已结算\n请通过顶部地图选择下一节点" : "完成商店结算",
            CustomMinimumSize = new Vector2(560, 72),
            Disabled = _settled,
        };
        leaveButton.Pressed += LeaveShop;
        _itemContainer.AddChild(leaveButton);
    }

    private void BuyCard(int index, int price)
    {
        if (_settled || index < 0 || index >= _inventory.Count || _sold[index])
            return;
        var gm = GameManager.Instance;
        if (gm.LingYun < price)
        {
            _resultLabel.Text = "灵韵不足，未购买卡牌。";
            GD.PrintErr("[商店] 购买被拒绝：灵韵不足。");
            return;
        }

        gm.LingYun -= price;
        gm.AddCardToDeck(_inventory[index]);
        _sold[index] = true;
        GD.Print($"[商店] 已购买：{_inventory[index].Id}，价格={price}");
        RefreshItems();
    }

    private void LeaveShop()
    {
        if (_settled) return;
        var resultSummary = $"完成{_shopDefinition.Title}";
        if (GameManager.Instance.CreateNodeResult(NodeResultType.Completed, resultSummary, out var createError) is not NodeResult result)
        {
            GD.PrintErr($"[商店] 创建结算失败：{createError}");
            return;
        }
        if (!GameManager.Instance.SubmitNodeResult(result, out var submitError))
        {
            GD.PrintErr($"[商店] 结算失败：{submitError}");
            _resultLabel.Text = "商店结算失败，未离开节点。";
            return;
        }
        _settled = true;
        _resultLabel.Text = $"{resultSummary}。请通过顶部地图选择下一节点。";
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
        if (_mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay))
        {
            _mapOverlay.Close();
            return;
        }

        var gm = GameManager.Instance;
        bool interactive = _settled && gm.ActiveNodeResultSubmitted;
        _mapOverlay = MapOverlayController.Open(this, interactive,
            interactive ? OnMapNodePressed : null, () => _mapOverlay = null);
        if (_mapOverlay == null)
            _resultLabel.Text = "地图 overlay 创建失败，请查看日志。";
    }

    private void OnMapNodePressed(MapNodeDefinition info)
    {
        if (!_settled || info == null)
            return;
        var gm = GameManager.Instance;
        if (!gm.TryTransitionAndRouteFromCompletedNode(info, out var transitionError))
        {
            GD.PrintErr($"[商店] 目标节点事务迁移失败：{transitionError}");
            _resultLabel.Text = "进入下一节点失败，当前商店结果页已保留。";
            return;
        }
        _mapOverlay?.CloseImmediately();
        _mapOverlay = null;
    }
}
