using Godot;
using System.Collections.Generic;

/// <summary>
/// 场景E：牌组展示界面（MVP 结束画面）。
/// 显示所有卡牌 + 道痕 + 总张数。
/// </summary>
public partial class DeckDisplayController : Control
{
    private RichTextLabel _titleLabel;
    private FlowContainer _cardGrid;
    private Label _infoLabel;
    private Button _doneBtn;

    public override void _Ready()
    {
        _titleLabel = GetNode<RichTextLabel>("TitleLabel");
        _cardGrid = GetNode<FlowContainer>("CardGrid");
        _infoLabel = GetNode<Label>("InfoLabel");
        _doneBtn = GetNode<Button>("DoneBtn");

        _doneBtn.Pressed += OnDonePressed;

        BuildDeckDisplay();
    }

    private void BuildDeckDisplay()
    {
        _titleLabel.Text = "[center][font_size=28]牌 组 一 览[/font_size][/center]";

        var gm = GameManager.Instance;

        // 汇总所有卡牌
        var allCards = new List<CardRuntime>();
        allCards.AddRange(gm.GetPermanentDeckCards());

        // 统计
        var counts = new Dictionary<string, int>();
        foreach (var card in allCards)
        {
            if (!counts.ContainsKey(card.Info.Name))
                counts[card.Info.Name] = 0;
            counts[card.Info.Name]++;
        }

        int total = allCards.Count;
        _infoLabel.Text = $"总计：{total} 张卡牌";

        // 显示卡牌
        foreach (var kvp in counts)
        {
            var cardLabel = new Label();
            cardLabel.Text = $"{kvp.Key} ×{kvp.Value}";
            cardLabel.CustomMinimumSize = new Vector2(100, 40);
            _cardGrid.AddChild(cardLabel);
        }

        // 显示道痕
        if (gm.DaoMarks.Count > 0)
        {
            var dmLabel = new Label();
            dmLabel.Text = "\n已固化道痕：";
            _cardGrid.AddChild(dmLabel);

            foreach (var dm in gm.DaoMarks)
            {
                var label = new Label();
                label.Text = $"  • {dm.Name} [{dm.Grade}]";
                label.CustomMinimumSize = new Vector2(200, 30);
                _cardGrid.AddChild(label);
            }
        }

        // 验证标准
        string verifyMsg = "";
        if (total == 11)
            verifyMsg = "\n\n✅ 验证通过！牌组从 10 张增加到 11 张。";
        else if (total == 10)
            verifyMsg = "\n\n⚠ 牌组未增加（可能跳过了奖励）。";
        else
            verifyMsg = $"\n\n牌组大小：{total}（预期 11）";

        var verifyLabel = new Label();
        verifyLabel.Text = verifyMsg;
        _cardGrid.AddChild(verifyLabel);
    }

    private void OnDonePressed()
    {
        GameManager.Instance.GoToTitle();
    }
}
