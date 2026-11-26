using System;
using System.Collections.Generic;
using TowerRecoveryMod;

internal static class TowerRecoveryTests
{
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Console.WriteLine("PASS " + description);
    }

    public static void Main()
    {
        var round = new RecoveryRound();
        Check(!round.TryConsume(10, 20, true), "no recovery before native departure");
        round.MarkDeparture(10, 20);
        Check(!round.TryConsume(11, 20, true), "unrelated battle manager rejected");
        Check(!round.TryConsume(10, 21, true), "replaced save rejected");
        Check(!round.TryConsume(10, 20, false), "force-over or cancellation rejected");
        Check(round.TryConsume(10, 20, true), "normal outcome consumes the matching round");
        Check(!round.TryConsume(10, 20, true), "duplicate callback rejected");

        var next = new RecoveryRound();
        next.MarkDeparture(10, 20);
        Check(next.TryConsume(10, 20, true), "consecutive floor gets a fresh round");

        var old = new HashSet<int> { 5 };
        var beforeDeparture = new HashSet<int> { 17 };
        Check(!RecoveryRound.IsNewInjury(5, true, old, beforeDeparture), "pre-existing injury retained");
        Check(!RecoveryRound.IsNewInjury(17, true, old, beforeDeparture), "unrelated pre-departure grant retained");
        Check(!RecoveryRound.IsNewInjury(18, false, old, beforeDeparture), "unlisted story buff retained");
        Check(RecoveryRound.IsNewInjury(6, true, old, beforeDeparture), "new native injury selected");
    }
}
