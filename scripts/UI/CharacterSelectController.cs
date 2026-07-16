using Godot;
using System;

/// <summary>
/// 场景A：角色选择界面（v2.0 重制）。
/// 全屏背景插画 + 右侧信息面板 + 底部6头像栏 + 返回按钮。
/// </summary>
public partial class CharacterSelectController : Control
{
    private ColorRect _background;
    private RichTextLabel _infoLabel;
    private Label _nameLabel;
    private Label _deckPreviewLabel;
    private Label _hpLabel;
    private Button _startBtn;
    private Button _backBtn;

    // 底部头像按钮
    private Button[] _avatarBtns;
    private Label[] _avatarLabels;
    private Label[] _avatarStatusLabels;

    private CharacterInfo[] _characterDefinitions = Array.Empty<CharacterInfo>();
    private CharacterInfo _selectedCharacter;
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        _background = GetNode<ColorRect>("Background");
        _nameLabel = GetNode<Label>("InfoPanel/NameLabel");
        _infoLabel = GetNode<RichTextLabel>("InfoPanel/InfoLabel");
        _deckPreviewLabel = GetNode<Label>("InfoPanel/DeckPreviewLabel");
        _hpLabel = GetNode<Label>("InfoPanel/HpLabel");
        _startBtn = GetNode<Button>("StartBtn");
        _backBtn = GetNode<Button>("BackBtn");

        // 底部 6 个头像按钮
        _avatarBtns = new Button[6];
        _avatarLabels = new Label[6];
        _avatarStatusLabels = new Label[6];

        var avatarContainer = GetNode<HBoxContainer>("AvatarContainer");
        for (int i = 0; i < 6; i++)
        {
            _avatarBtns[i] = avatarContainer.GetNode<Button>($"Avatar{i + 1}");
            _avatarLabels[i] = _avatarBtns[i].GetNode<Label>("NameLabel");
            _avatarStatusLabels[i] = _avatarBtns[i].GetNode<Label>("StatusLabel");
            var idx = i; // closure capture
            _avatarBtns[i].Pressed += () => OnAvatarPressed(idx);
            _avatarBtns[i].MouseEntered += () => OnAvatarHover(idx);
        }

        _startBtn.Pressed += OnStartPressed;
        _backBtn.Pressed += OnBackPressed;
        _deckPreviewLabel.Visible = false;

        _characterDefinitions = DataDefs.Characters ?? Array.Empty<CharacterInfo>();

        // 初始化头像状态
        UpdateAvatars();
        SelectFirstUnlockedCharacter();
    }

    private void UpdateAvatars()
    {
        int definitionSlotCount = Mathf.Min(_characterDefinitions.Length, _avatarBtns.Length - 1);
        for (int i = 0; i < definitionSlotCount; i++)
        {
            var info = _characterDefinitions[i];
            _avatarLabels[i].Text = info.Name;

            if (info.Unlocked)
            {
                _avatarBtns[i].Modulate = Colors.White;
                _avatarStatusLabels[i].Text = "";
            }
            else
            {
                _avatarBtns[i].Modulate = new Color(0.4f, 0.4f, 0.4f, 1.0f);
                _avatarStatusLabels[i].Text = "🔒";
            }
        }

        for (int i = definitionSlotCount; i < _avatarBtns.Length - 1; i++)
        {
            _avatarLabels[i].Text = "未配置角色";
            _avatarStatusLabels[i].Text = "";
            _avatarBtns[i].Disabled = true;
            _avatarBtns[i].Modulate = new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        // 最后一个头像槽是随机入口；它只从当前定义目录中选择，不伪造角色定义。
        int randomSlot = _avatarBtns.Length - 1;
        _avatarLabels[randomSlot].Text = "随机";
        _avatarStatusLabels[randomSlot].Text = "?";
        _avatarBtns[randomSlot].Modulate = Colors.White;
    }

    private void SelectFirstUnlockedCharacter()
    {
        for (int i = 0; i < _characterDefinitions.Length && i < _avatarBtns.Length - 1; i++)
        {
            if (_characterDefinitions[i] != null && _characterDefinitions[i].Unlocked)
            {
                SelectCharacter(i);
                return;
            }
        }

        _selectedCharacter = null;
        _startBtn.Disabled = true;
        _nameLabel.Text = "角色定义不可用";
        _infoLabel.Text = "当前没有可用的已解锁角色定义，无法开始新局。";
        _hpLabel.Text = "心：--/--";
        GD.PrintErr("[角色选择] 角色定义目录没有可用的已解锁角色。");
    }

    private void SelectCharacter(int index)
    {
        if (index < 0 || index >= _characterDefinitions.Length || index >= _avatarBtns.Length - 1 ||
            _characterDefinitions[index] == null)
        {
            GD.PrintErr($"[角色选择] 角色索引无效，拒绝选择：{index}");
            return;
        }

        _selectedIndex = index;
        _selectedCharacter = _characterDefinitions[index];

        // 清除旧选中标记
        for (int i = 0; i < _characterDefinitions.Length && i < _avatarBtns.Length - 1; i++)
            _avatarStatusLabels[i].Text = _characterDefinitions[i].Unlocked ? "" : "🔒";

        // 设置新选中标记
        _avatarStatusLabels[index].Text = _selectedCharacter.Unlocked ? "✓" : "🔒";

        var info = _selectedCharacter;
        if (!info.Unlocked)
        {
            _startBtn.Disabled = true;
            GD.PrintErr($"[角色选择] 角色未解锁，拒绝选择：{info.Id}");
            return;
        }

        _startBtn.Disabled = false;
        _nameLabel.Text = info.Name;
        _infoLabel.Text = info.Introduction;
        _deckPreviewLabel.Text = "";
        _hpLabel.Text = $"心：{info.MaxHp}/{info.MaxHp}";

        // 背景色切换（占位——后续改为插画）
        _background.Color = new Color(0.16f, 0.09f, 0.12f, 1f);
    }

    private void OnAvatarPressed(int index)
    {
        if (index == _avatarBtns.Length - 1)
        {
            SelectRandomUnlockedCharacter();
            return;
        }

        if (index >= 0 && index < _characterDefinitions.Length && _characterDefinitions[index] != null &&
            _characterDefinitions[index].Unlocked)
        {
            SelectCharacter(index);
        }
        else
        {
            // 锁定角色：轻微抖动提示
            ShakeNode(_avatarBtns[index]);
        }
    }

    /// <summary>Chooses from every unlocked CharacterDefinition instead of treating the first catalog entry as random.</summary>
    private void SelectRandomUnlockedCharacter()
    {
        if (!CharacterSelectionResolver.TryChooseUnlocked(_characterDefinitions, GD.Randi(),
                out int index, out var error))
        {
            GD.PrintErr($"[角色选择] 随机角色选择失败：{error}");
            return;
        }

        SelectCharacter(index);
    }

    private void OnAvatarHover(int index)
    {
        if (index >= 0 && index < _characterDefinitions.Length && !_characterDefinitions[index].Unlocked)
        {
            _avatarBtns[index].TooltipText = "此角色将在后续版本中开放";
        }
        else
        {
            _avatarBtns[index].TooltipText = "";
        }
    }

    private void OnStartPressed()
    {
        var charInfo = _selectedCharacter;
        if (charInfo == null || !charInfo.Unlocked)
        {
            GD.PrintErr("[角色选择] 当前没有有效的已解锁角色定义，拒绝开始新局。");
            return;
        }

        if (!DataDefs.TryResolveStarterDeck(charInfo, out _, out var deckError))
        {
            GD.PrintErr($"[角色选择] 当前角色缺少初始牌组，拒绝开始：{deckError}");
            return;
        }

        if (!GameManager.Instance.StartNewRun(charInfo.Id))
        {
            GD.PrintErr($"[角色选择] 开始新局失败：{charInfo.Id}");
            return;
        }
        if (!GameManager.Instance.ChangeSceneToFile("res://scenes/Map/Map.tscn", out var routeError))
            GD.PrintErr($"[角色选择] 地图场景路由失败：{routeError}");
    }

    private void OnBackPressed()
    {
        GameManager.Instance.GoToTitle();
    }

    /// <summary>简单抖动动画</summary>
    private async void ShakeNode(Control node)
    {
        var origPos = node.Position;
        for (int i = 0; i < 3; i++)
        {
            node.Position = origPos + new Vector2(5, 0);
            await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
            node.Position = origPos + new Vector2(-5, 0);
            await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
        }
        node.Position = origPos;
    }
}
