using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using GameData;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using WuLin;

namespace TowerRecoveryMod;

[BepInPlugin(Id, "Tower Recovery", "1.0.0")]
public sealed class TowerRecovery : BasePlugin
{
    private const string Id = "local.daxia.towerrecovery";
    private const string GameHash = "F8393E44423442526DFFBF3AF52F8CCD56FB33769D97E2C7161EBC49D68B3564";
    private static TowerRecovery? plugin;
    private Harmony? harmony;
    private ConfigEntry<bool> restoreVitals = null!;
    private ConfigEntry<bool> clearInjuries = null!;
    private PagodaManager? launching;
    private Challenge? challenge;

    private sealed class Challenge(string battleName, nint pagoda, nint save, nint team, nint saveManager)
    {
        internal readonly string BattleName = battleName;
        internal readonly nint Pagoda = pagoda;
        internal readonly nint Save = save;
        internal readonly nint Team = team;
        internal readonly nint SaveManager = saveManager;
        internal readonly RecoveryRound Round = new();
        internal readonly List<Participant> Participants = new();
        internal nint PreparedBattle;
        internal nint StartedBattle;
        internal bool Leaving;
        internal bool ReachedPredefinedDeparture;
        internal BattleFinishResult Result;
    }

    private sealed class Participant(GameCharacterInstance character, HashSet<int>? beforeBattle)
    {
        internal readonly GameCharacterInstance Character = character;
        internal readonly HashSet<int>? BeforeBattle = beforeBattle;
        internal HashSet<int>? BeforeDeparture;
        internal List<BuffInstance>? NativeInjuries;
        internal PriorWound? HeavyWound;
    }

    private sealed class PriorWound(BuffInstance buff)
    {
        internal readonly BuffData Template = buff.Templete;
        internal readonly Il2CppSystem.Decimal Stack = buff.stack;
        internal readonly Il2CppSystem.Decimal InitTime = buff.initTime;
        internal readonly float Remaining = buff.lifeTime;
        internal readonly bool IsNew = buff.IsNew;
        internal readonly Il2CppStructArray<Il2CppSystem.Decimal>? OverrideValues = buff.overrideValues;
    }

    public override void Load()
    {
        restoreVitals = Config.Bind("General", "RestoreVitals", true, "Recover health and inner energy after a completed Pagoda battle.");
        clearInjuries = Config.Bind("General", "ClearNewInjuries", true, "Remove eligible persistent injuries newly applied at Pagoda battle settlement.");
        try
        {
            using var native = File.OpenRead(Path.Combine(Paths.GameRootPath, "GameAssembly.dll"));
            using var sha = SHA256.Create();
            if (Convert.ToHexString(sha.ComputeHash(native)) != GameHash)
            {
                Log.LogError("Tower Recovery requires Steam Build 24794582. Patches disabled.");
                return;
            }

            var patches = new (MethodInfo Target, string? Prefix, string? Postfix, string? Finalizer)[]
            {
                (Target(typeof(PagodaManager), nameof(PagodaManager.StartBattle), typeof(int)), nameof(PagodaLaunch), null, nameof(PagodaLaunchEnd)),
                (Target(typeof(GameManager), nameof(GameManager.StartPredefinedBattle), typeof(string)), nameof(PredefinedLaunch), null, null),
                (Target(typeof(BattleManager), nameof(BattleManager.ApplyPredefinedBattleFieldData), typeof(BattleFieldData)), null, nameof(Prepared), null),
                (Target(typeof(BattleManager), nameof(BattleManager.BattleStart)), nameof(Starting), null, null),
                (Target(typeof(BattleManager), nameof(BattleManager.LeaveBattle)), nameof(Leaving), nameof(Left), nameof(LeaveError)),
                (Target(typeof(GameManager), nameof(GameManager.LeavePredefinedBattle)), nameof(PredefinedDeparture), null, null),
                (Target(typeof(PagodaManager), "_RegisterBattleEvent_b__59_0", typeof(Il2CppReferenceArray<Il2CppSystem.Object>)), null, nameof(PagodaSettled), null),
                (Target(typeof(GameManager), nameof(GameManager.LoadGame), typeof(SaveManager.SaveDataInfo)), nameof(Reset), null, null),
                (Target(typeof(GameManager), nameof(GameManager.StartNewGame)), nameof(Reset), null, null),
                (Target(typeof(GameManager), nameof(GameManager.QuitToMenu)), nameof(Reset), null, null)
            };
            harmony = new Harmony(Id);
            plugin = this;
            foreach (var patch in patches)
                harmony.Patch(patch.Target, Hook(patch.Prefix), Hook(patch.Postfix), finalizer: Hook(patch.Finalizer));
            Log.LogInfo("Tower Recovery loaded for Steam Build 24794582.");
        }
        catch (Exception error)
        {
            Unload();
            Log.LogError($"Tower Recovery disabled: {error}");
        }
    }

    public override bool Unload()
    {
        Reset();
        plugin = null;
        harmony?.UnpatchSelf();
        harmony = null;
        return true;
    }

    private static MethodInfo Target(Type type, string name, params Type[] args) =>
        AccessTools.DeclaredMethod(type, name, args) ?? throw new MissingMethodException(type.FullName, name);

    private static HarmonyMethod? Hook(string? name) =>
        name == null ? null : new HarmonyMethod(TargetHook(name));

    private static MethodInfo TargetHook(string name) =>
        typeof(TowerRecovery).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(TowerRecovery).FullName, name);

    private static void Reset()
    {
        if (plugin == null) return;
        plugin.launching = null;
        plugin.challenge = null;
    }

    private void Observe(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            Reset();
            Log.LogWarning($"Tower Recovery skipped this battle: {error}");
        }
    }

    private static void PagodaLaunch(PagodaManager __instance)
    {
        Reset();
        if (plugin != null) plugin.launching = __instance;
    }

    private static void PagodaLaunchEnd(Exception? __exception)
    {
        if (plugin == null) return;
        plugin.launching = null;
        if (__exception != null) plugin.challenge = null;
    }

    private static void PredefinedLaunch(string __0)
    {
        if (plugin is not { } mod) return;
        mod.challenge = null;
        if (mod.launching is not { } pagoda || (!mod.restoreVitals.Value && !mod.clearInjuries.Value)) return;
        mod.Observe(() =>
        {
            var save = pagoda.dataContainer?.initedObject;
            var team = PlayerTeamManager.Instance;
            var saves = SaveManager.Instance;
            if (string.IsNullOrEmpty(__0) || !save || !team || !saves) return;
            mod.challenge = new Challenge(__0, pagoda.Pointer, save!.Pointer, team.Pointer, saves.Pointer);
        });
    }

    private bool ContextMatches(Challenge current, PagodaManager? pagoda)
    {
        var save = pagoda?.dataContainer?.initedObject;
        return pagoda && pagoda!.Pointer == current.Pagoda && save && save!.Pointer == current.Save &&
               PlayerTeamManager.Instance?.Pointer == current.Team && SaveManager.Instance?.Pointer == current.SaveManager;
    }

    private static void Prepared(BattleManager __instance, BattleFieldData __0)
    {
        if (plugin is not { challenge: { } current } mod) return;
        mod.Observe(() =>
        {
            if (current.PreparedBattle != 0 || !MatchesField(__0, current.BattleName) ||
                !mod.ContextMatches(current, PagodaManager.Instance))
            {
                mod.challenge = null;
                return;
            }
            current.PreparedBattle = __instance.Pointer;
        });
    }

    // MapManager.IsPagodaMapID identifies lobby 600. The tower's configured
    // predefined battles load separate maps 50051 and 50052.
    private static bool MatchesField(BattleFieldData? field, string launchedName) =>
        field != null && field.UName == launchedName && field.Map is 50051 or 50052;

    private static void Starting(BattleManager __instance)
    {
        if (plugin is not { challenge: { } current } mod) return;
        mod.Observe(() =>
        {
            if (current.StartedBattle == __instance.Pointer) return;
            if (current.PreparedBattle != __instance.Pointer || BattleManager.curBattleMode != BattleMode.Predefined ||
                !mod.ContextMatches(current, PagodaManager.Instance))
            {
                mod.challenge = null;
                return;
            }

            var team = PlayerTeamManager.Instance;
            var actors = __instance.GetAllBattleActors(true);
            if (actors == null || actors.Count == 0)
            {
                mod.challenge = null;
                return;
            }

            var seen = new HashSet<nint>();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || actor.info.battleTeamEnum != BattleTeamEnum.Player) continue;
                var character = actor.info.characterInstance;
                if (character == null || !character.IsValid || !IsCurrentHumanMember(team, character) ||
                    !seen.Add(character.Pointer)) continue;
                current.Participants.Add(new Participant(character, mod.clearInjuries.Value ? Snapshot(character) : null));
            }
            if (current.Participants.Count == 0)
            {
                mod.challenge = null;
                return;
            }
            var leader = team.GetTeamMemberByIndex(0);
            if (leader != null && leader.IsValid && seen.Add(leader.Pointer))
                current.Participants.Add(new Participant(leader, mod.clearInjuries.Value ? Snapshot(leader) : null));

            current.StartedBattle = __instance.Pointer;
            mod.Log.LogInfo($"Pagoda battle recognized: {current.BattleName}, participants={current.Participants.Count}.");
        });
    }

    private static bool Normal(BattleFinishResult result) => result is BattleFinishResult.Win or BattleFinishResult.Lose;

    private static bool IsCurrentHumanMember(PlayerTeamManager team, GameCharacterInstance character)
    {
        for (int i = 0; i < team.TeamSize; i++)
            if (team.GetTeamMemberByIndex(i)?.Pointer == character.Pointer) return true;
        return false;
    }

    private static void Leaving(BattleManager __instance, out Challenge? __state)
    {
        __state = null;
        if (plugin is not { challenge: { } current } mod || current.StartedBattle != __instance.Pointer ||
            !__instance.isBattleFinished || BattleManager.curBattleMode != BattleMode.Predefined) return;
        mod.Observe(() =>
        {
            if (!Normal(BattleManager.battleFinishResult) || !mod.ContextMatches(current, PagodaManager.Instance))
            {
                mod.challenge = null;
                return;
            }
            current.Result = BattleManager.battleFinishResult;
            current.ReachedPredefinedDeparture = false;
            foreach (var participant in current.Participants)
            {
                if (participant.BeforeBattle == null) continue;
                participant.BeforeDeparture = Snapshot(participant.Character);
                if (participant.BeforeBattle.Contains(5) &&
                    participant.Character.buffLib?.TryGetBuff("重伤", false) is { IsValid: true } heavy &&
                    heavy.Templete is { Uid: 5 } && heavy.lifeTime > 0)
                    participant.HeavyWound = new PriorWound(heavy);
            }
            current.Leaving = true;
        });
        if (mod.challenge == current) __state = current;
    }

    private static void PredefinedDeparture()
    {
        if (plugin is not { challenge: { Leaving: true } current } mod) return;
        mod.Observe(() =>
        {
            // LeaveBattle has written back the combat effects. Capture their
            // identities before the return-to-exploration event runs stories.
            foreach (var participant in current.Participants)
                participant.NativeInjuries = FindNativeInjuries(participant);
            current.ReachedPredefinedDeparture = true;
        });
    }

    private static void Left(BattleManager __instance, Challenge? __state)
    {
        if (__state == null || plugin?.challenge != __state) return;
        __state.Leaving = false;
        if (__state.ReachedPredefinedDeparture) __state.Round.MarkDeparture(__instance.Pointer, __state.Save);
    }

    private static void LeaveError(Challenge? __state, Exception? __exception)
    {
        if (__exception != null && __state != null && plugin?.challenge == __state) Reset();
    }

    private static void PagodaSettled(PagodaManager __instance, Il2CppReferenceArray<Il2CppSystem.Object> __0)
    {
        if (plugin is not { challenge: { } current } mod) return;
        mod.Observe(() =>
        {
            if (!mod.ContextMatches(current, __instance) || __0 == null || __0.Length == 0 || __0[0] == null) return;
            var result = __0[0].Unbox<BattleFinishResult>();
            if (!current.Round.TryConsume(current.StartedBattle, current.Save, Normal(result) && current.Result == result)) return;
            mod.challenge = null;
            mod.Recover(current);
        });
    }

    private static HashSet<int>? Snapshot(GameCharacterInstance character)
    {
        var buffs = character.buffLib?.GetAllBuffs(false);
        if (buffs == null) return null;
        var ids = new HashSet<int>();
        foreach (var buff in buffs)
            if (buff != null && buff.IsValid && buff.Templete is { } data) ids.Add(data.Uid);
        return ids;
    }

    private static bool IsNativeInjury(BuffData data)
    {
        // These are the local, persistent wounds applied by native LeaveBattle.
        if (data.IsPositive || data.TickType != BuffTickType.Time) return false;
        return data.UName switch
        {
            "重伤" => data.Uid == 5,
            "濒死" => data.Uid == 6,
            _ => false
        };
    }

    private static List<BuffInstance>? FindNativeInjuries(Participant participant)
    {
        if (participant.BeforeBattle is not { } before || participant.BeforeDeparture is not { } departure) return null;
        var buffs = participant.Character.buffLib?.GetAllBuffs(false);
        if (buffs == null) return null;

        var removable = new List<BuffInstance>();
        foreach (var buff in buffs)
            if (buff != null && buff.IsValid && buff.Templete is { } data &&
                RecoveryRound.IsNewInjury(data.Uid, IsNativeInjury(data), before, departure)) removable.Add(buff);
        return removable;
    }

    private int ClearInjuries(Participant participant)
    {
        var lib = participant.Character.buffLib;
        if (participant.NativeInjuries is not { } removable || lib == null) return 0;
        int removed = 0;
        foreach (var buff in removable)
        {
            if (!buff.IsValid || buff.Templete is not { } data ||
                lib.TryGetBuff(data.UName, false)?.Pointer != buff.Pointer ||
                !lib.TryRemoveBuff(buff, true)) continue;
            removed++;
            // Native near-death replaces a previous heavy wound. Preserve that
            // wound with its original duration when removing the new upgrade.
            if (data.Uid == 6 && participant.HeavyWound is { } old &&
                lib.TryGetBuff("重伤", false) == null)
            {
                var restored = participant.Character.ApplyBuff(old.Template, CharacterBuffSource.Default, 1, old.OverrideValues);
                if (restored == null || !restored.IsValid)
                    throw new InvalidOperationException("Unable to restore the pre-existing heavy wound.");
                restored.stack = old.Stack;
                restored.initTime = old.InitTime;
                restored.lifeTime = old.Remaining;
                restored.IsNew = old.IsNew;
            }
        }
        return removed;
    }

    private void Recover(Challenge current)
    {
        int restored = 0;
        int removed = 0;
        var team = PlayerTeamManager.Instance;
        foreach (var participant in current.Participants)
        {
            try
            {
                var character = participant.Character;
                if (!character.IsValid || !character.IsAlive || !IsCurrentHumanMember(team, character)) continue;
                if (clearInjuries.Value) removed += ClearInjuries(participant);
                if (!restoreVitals.Value) continue;
                // Buff removal can defer its normal stat invalidation. Recompute
                // now so the native full refill uses the post-treatment limits.
                character.SetTemporyaryPropDirty();
                character.ComputeTemporyaryProps();
                character.FullyRecover(true);
                restored++;
            }
            catch (Exception error)
            {
                Log.LogWarning($"Tower Recovery participant skipped: {error}");
            }
        }
        Log.LogInfo($"Pagoda recovery: {current.BattleName}, {current.Result}, restored={restored}, injuries removed={removed}.");
    }
}
