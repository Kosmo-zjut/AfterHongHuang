using System;
using Godot;

/// <summary>Instantiates the centered victory reward column defined by the reusable scene.</summary>
public static class VictoryRewardList
{
    private const string ScenePath = "res://scenes/UI/VictoryRewardList.tscn";

    /// <summary>Adds the shared centered reward column to the supplied victory panel.</summary>
    public static VBoxContainer AddTo(Control parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var packedScene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (packedScene == null)
            throw new InvalidOperationException($"无法加载胜利奖励列场景：{ScenePath}");

        var rewardList = packedScene.Instantiate<VBoxContainer>();
        parent.AddChild(rewardList);
        return rewardList;
    }
}
