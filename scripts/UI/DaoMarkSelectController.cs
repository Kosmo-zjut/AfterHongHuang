using Godot;
using System.Collections.Generic;

/// <summary>
/// 场景B：道痕选择界面（纯道痕3选1，无地图面板）。
/// 新版：地图功能已迁移至 MapController，此场景作为独立道痕选择备用。
/// </summary>
public partial class DaoMarkSelectController : Control
{
    private RichTextLabel _daoTitleLabel;
    private HBoxContainer _cardContainer;
    private Button[] _selectBtns;
    private RichTextLabel[] _cardDescs;

    private DaoMarkInfo[] _choices;

    public override void _Ready()
    {
        _daoTitleLabel = GetNode<RichTextLabel>("DaoMarkPanel/TitleLabel");
        _cardContainer = GetNode<HBoxContainer>("DaoMarkPanel/CardContainer");

        _selectBtns = new Button[3];
        _cardDescs = new RichTextLabel[3];
        for (int i = 0; i < 3; i++)
        {
            _selectBtns[i] = _cardContainer.GetNode<Button>($"Card{i + 1}/SelectBtn");
            _cardDescs[i] = _cardContainer.GetNode<RichTextLabel>($"Card{i + 1}/DescLabel");
            var idx = i;
            _selectBtns[i].Pressed += () => OnDaoMarkSelected(idx);
        }

        PickRandomDaoMarks();
        UpdateDaoMarkUI();
    }

    private void PickRandomDaoMarks()
    {
        var pool = new List<DaoMarkInfo>(DataDefs.DaoMarkPool);
        var rng = new System.Random();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        _choices = pool.GetRange(0, 3).ToArray();
        GameManager.Instance.CurrentChoices = new List<DaoMarkInfo>(_choices);
    }

    private void UpdateDaoMarkUI()
    {
        _daoTitleLabel.Text = "[center][font_size=28]道韵浮现，择一而固[/font_size][/center]";

        for (int i = 0; i < 3; i++)
        {
            var dm = _choices[i];
            _cardDescs[i].Text = $"[center][font_size=20][b]{dm.Name}[/b][/font_size][/center]\n" +
                                  $"[center][{dm.Grade}][/center]\n\n" +
                                  $"{dm.Description}";
        }
    }

    private void OnDaoMarkSelected(int index)
    {
        if (GameManager.Instance.DaoMarkSelected) return;

        var selected = _choices[index];
        GameManager.Instance.DaoMarks.Add(selected);
        GameManager.Instance.DaoMarkSelected = true;

        foreach (var btn in _selectBtns)
            btn.Disabled = true;

        if (selected.EffectType == DaoMarkEffect.加血上限)
        {
            var gm = GameManager.Instance;
            gm.PlayerMaxHp += selected.EffectValue;
            gm.PlayerHp += selected.EffectValue;
        }

        GD.Print($"[道痕] 已固化：{selected.Name}");
    }
}
