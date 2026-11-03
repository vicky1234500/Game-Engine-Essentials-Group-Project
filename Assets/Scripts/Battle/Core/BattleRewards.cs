using System.Collections.Generic;
using UnityEngine;

// What the player gets after winning. Doc: "Get Money from Battle, Chests, or other events".
public static class BattleRewards
{
    public static int RollCredits(List<EnemyCombatant> enemies, int bonusPercent = 0)
    {
        int total = 0;
        foreach (var e in enemies)
            if (e && e.Data) total += Random.Range(e.Data.minCredits, e.Data.maxCredits + 1);

        // Stolen Credit Chip passive: +50% money from all sources
        return total + total * bonusPercent / 100;
    }

    // TODO: item reward after battle (pick 1 of 3?), none for Underground Fight Ring
}
