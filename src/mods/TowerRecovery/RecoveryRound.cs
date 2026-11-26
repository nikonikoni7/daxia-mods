using System.Collections.Generic;

namespace TowerRecoveryMod;

internal sealed class RecoveryRound
{
    private nint departedBattle;
    private nint departedSave;

    internal void MarkDeparture(nint battle, nint save)
    {
        departedBattle = battle;
        departedSave = save;
    }

    internal bool TryConsume(nint battle, nint save, bool normalOutcome)
    {
        if (!normalOutcome || departedBattle == 0 || departedSave == 0 ||
            battle != departedBattle || save != departedSave) return false;
        departedBattle = 0;
        departedSave = 0;
        return true;
    }

    internal static bool IsNewInjury(int uid, bool isNativeInjury, HashSet<int> beforeBattle, HashSet<int> beforeDeparture) =>
        isNativeInjury && !beforeBattle.Contains(uid) && !beforeDeparture.Contains(uid);
}
