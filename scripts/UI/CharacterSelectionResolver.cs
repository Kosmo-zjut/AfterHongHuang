using System;
using System.Collections.Generic;

/// <summary>Resolves selectable character definitions without controller-specific catalog positions.</summary>
public static class CharacterSelectionResolver
{
    /// <summary>Maps a supplied random value onto every unlocked definition in stable catalog order.</summary>
    public static bool TryChooseUnlocked(IReadOnlyList<CharacterInfo> definitions, uint randomValue,
        out int selectedIndex, out string error)
    {
        selectedIndex = -1;
        error = "";
        if (definitions == null || definitions.Count == 0)
        {
            error = "角色定义目录为空。";
            return false;
        }

        var available = new List<int>();
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && definitions[i].Unlocked)
                available.Add(i);
        }
        if (available.Count == 0)
        {
            error = "角色定义目录没有可用的已解锁角色。";
            return false;
        }

        selectedIndex = available[(int)(randomValue % (uint)available.Count)];
        return true;
    }
}
