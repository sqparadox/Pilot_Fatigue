using System;
using System.Reflection;
using BattleTech;
using Harmony;
using BattleTech.UI;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using HBS.Collections;
using UnityEngine;

namespace Pilot_Fatigue
{
    public static class Pre_Control
    {
        public const string ModName = "Pilot_Fatigue";
        public const string ModId = "dZ.Zappo.Pilot_Fatigue";

        internal static ModSettings settings;
        internal static string ModDirectory;

        public static void Init(string directory, string modSettings)
        {
            ModDirectory = directory;
            try
            {
                settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
            }
            catch (Exception e)
            {
                Helper.Logger.LogError(e);
                settings = new ModSettings();
            }

            string logString = $@"Settings:
ArgoUpgradeReduction: {settings.ArgoUpgradeReduction}            
FatigueTimeStart: {settings.FatigueTimeStart}
FatigueMinimum: {settings.FatigueMinimum}
MoralePositiveTierOne: {settings.MoralePositiveTierOne}
MoralePositiveTierTwo: {settings.MoralePositiveTierTwo}
MoraleNegativeTierOne: {settings.MoraleNegativeTierOne}
MoraleNegativeTierTwo: {settings.MoraleNegativeTierTwo}
UseCumulativeDays: {settings.UseCumulativeDays}
FatigueReducesSkills: {settings.FatigueReducesSkills}
FatigueFactor: {settings.FatigueFactor}
FatigueFactorIsPercent {settings.FatigueFactorIsPercent}
InjuriesHurt: {settings.InjuriesHurt}
InjuryFactorIsPercent {settings.InjuryReductionIsPercent}
CanPilotInjured: {settings.CanPilotInjured}
pilot_athletic_FatigueDaysReduction: {settings.pilot_athletic_FatigueDaysReduction}
pilot_athletic_FatigueDaysReductionFactor: {settings.pilot_athletic_FatigueDaysReductionFactor}
QuirksEnabled: {settings.QuirksEnabled}
FatigueReducesResolve: {settings.FatigueReducesResolve}
FatigueResolveFactor: {settings.FatigueResolveFactor}
FatigueCausesLowSpirits: {settings.FatigueCausesLowSpirits}
LowMoraleTime: {settings.LowMoraleTime}
LightInjuriesOn: {settings.LightInjuriesOn}
MaximumFatigueTime: {settings.MaximumFatigueTime}
AllowNegativeResolve: {settings.AllowNegativeResolve}
pilot_wealthy_extra_fatigue: {settings.pilot_wealthy_extra_fatigue}
MechDamageMaxDays: {settings.MechDamageMaxDays}
BEXCE: {settings.BEXCE}";

            Helper.Logger.NewLog();
            Helper.Logger.LogLine(logString);

            var harmony = HarmonyInstance.Create(ModId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(SGBarracksRosterList), "SetSorting")]
        public static class SGBarracksRosterList_SetSorting_Postfix
        {
            private static readonly HashSet<RectTransform> AdjustedIcons = new HashSet<RectTransform>();
            private const float SizeDeltaFactor = 2;
            private static readonly Vector2 AnchoredPositionOffset = new Vector2(6f, 35f);
            
            public static void Postfix(SGBarracksRosterList __instance, Dictionary<string, SGBarracksRosterSlot> ___currentRoster)
            {
                foreach (var pilot in ___currentRoster.Values)
                {
                    var timeoutIcon = pilot.GetComponentsInChildren<RectTransform>(true)
                        .FirstOrDefault(x => x.name == "mw_TimeOutIcon");
                    if (timeoutIcon == null)
                        return;

                    if (!AdjustedIcons.Contains(timeoutIcon))
                    {
                        if (pilot.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        {
                            AdjustedIcons.Add(timeoutIcon);
                            timeoutIcon.sizeDelta /= SizeDeltaFactor;
                            timeoutIcon.anchoredPosition += AnchoredPositionOffset;
                        }
                    }
                    else
                    {
                        if (!pilot.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        {
                            AdjustedIcons.Remove(timeoutIcon);
                            timeoutIcon.sizeDelta *= SizeDeltaFactor;
                            timeoutIcon.anchoredPosition -= AnchoredPositionOffset;
                        }
                    }
                }

                __instance.ForceRefreshImmediate();
            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class Add_Fatigue_To_Pilots_Prefix
        {
            public static void Prefix(AAR_UnitStatusWidget __instance, SimGameState ___simState)
            {
                UnitResult unitResult = Traverse.Create(__instance).Field("UnitData").GetValue<UnitResult>();
                if (unitResult.pilot.pilotDef.TimeoutRemaining > 0 && unitResult.pilot.Injuries == 0)
                {
                }
                else if (unitResult.pilot.pilotDef.TimeoutRemaining > 0 && unitResult.pilot.Injuries > 0)
                {
                    unitResult.pilot.pilotDef.PilotTags.Remove("pilot_fatigued");
                    unitResult.pilot.pilotDef.SetTimeoutTime(0);
                    WorkOrderEntry_MedBayHeal workOrderEntry_MedBayHeal;
                    workOrderEntry_MedBayHeal = (WorkOrderEntry_MedBayHeal)___simState.MedBayQueue.GetSubEntry(unitResult.pilot.Description.Id);
                    ___simState.MedBayQueue.RemoveSubEntry(unitResult.pilot.Description.Id);
                }

            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class Add_Fatigue_To_Pilots_Postfix
        {
            public static void Postfix(AAR_UnitStatusWidget __instance, SimGameState ___simState)
            {
                UnitResult unitResult = Traverse.Create(__instance).Field("UnitData").GetValue<UnitResult>();

                int FatigueTimeStart = settings.FatigueTimeStart;
                int GutsValue = unitResult.pilot.Guts;
                int TacticsValue = unitResult.pilot.Tactics;
                SimGameState simstate = Traverse.Create(__instance).Field("simState").GetValue<SimGameState>();
                int MoraleDiff = simstate.Morale - simstate.Constants.Story.StartingMorale;
                int MoraleModifier = 0;

                if (MoraleDiff <= settings.MoraleNegativeTierTwo)
                    MoraleModifier = -2;
                else if (MoraleDiff <= settings.MoraleNegativeTierOne && MoraleDiff > settings.MoraleNegativeTierTwo)
                    MoraleModifier = -1;
                else if (MoraleDiff < settings.MoralePositiveTierTwo && MoraleDiff >= settings.MoralePositiveTierOne)
                    MoraleModifier = 1;
                else if (MoraleDiff >= settings.MoralePositiveTierTwo)
                    MoraleModifier = 2;

                Helper.Logger.LogLine($"current morale {simstate.Morale} starting morale {simstate.Constants.Story.StartingMorale} moarle diff is {MoraleDiff}\n");

                //Reduction in Fatigue Time for Guts tiers.
                int GutsReduction = 0;
                if (GutsValue >= 4)
                    GutsReduction = 1;
                if (GutsValue >= 7)
                    GutsReduction = 2;
                else if (GutsValue == 10)
                    GutsReduction = 3;

                //Additional Fatigue Time for 'Mech damage.
                double MechDamage = (unitResult.mech.MechDefCurrentStructure + unitResult.mech.MechDefCurrentArmor) /
                    (unitResult.mech.MechDefAssignedArmor + unitResult.mech.MechDefMaxStructure);

                int MechDamageTime = (int)Math.Ceiling((1 - MechDamage) * settings.MechDamageMaxDays);
                int argoReduction = 1;
                var simState = Traverse.Create(__instance).Field("simState").GetValue<SimGameState>();

                if (simState != null && settings.ArgoUpgradeReduction)
                {
                    var shipUpgrades = Traverse.Create(simState).Field("shipUpgrades").GetValue <List<ShipModuleUpgrade>>();
                    if (shipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_rec_hydroponics"))))
                        argoReduction++;
                    if (shipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_rec_pool"))))
                        argoReduction++;
                    if (shipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_rec_arcade"))))
                        argoReduction++;
                }
                var rand = new System.Random();
                argoReduction = rand.Next(0, argoReduction);

                //Calculate actual Fatigue Time for pilot.
                int FatigueTime = FatigueTimeStart + MechDamageTime - GutsReduction - MoraleModifier - argoReduction;

                Helper.Logger.LogLine($"Calculating Fatigue for {unitResult.pilot.Callsign}\n Fatigue Time({FatigueTime}) = Fatigue Time Start({FatigueTimeStart}) + Mech Damage Time({MechDamageTime}) - Pilot Guts Reduction({GutsReduction}) - Morale Modifier({MoraleModifier}) - Argo Upgrades Reduction({argoReduction})");

                if (unitResult.pilot.pilotDef.PilotTags.Contains("pilot_athletic") && settings.QuirksEnabled)
                    FatigueTime = (int)Math.Ceiling(FatigueTime / (settings.pilot_athletic_FatigueDaysReductionFactor / 100)) - settings.pilot_athletic_FatigueDaysReduction;

                if (unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green"))
                    FatigueTime -= settings.pilot_athletic_FatigueDaysReduction;

                if (FatigueTime < settings.FatigueMinimum)
                    FatigueTime = settings.FatigueMinimum;

                if (settings.QuirksEnabled && unitResult.pilot.pilotDef.PilotTags.Contains("pilot_wealthy"))
                    FatigueTime += settings.pilot_wealthy_extra_fatigue;

                if (unitResult.pilot.Injuries == 0 && unitResult.pilot.pilotDef.TimeoutRemaining == 0)
                {
                    unitResult.pilot.pilotDef.SetTimeoutTime(FatigueTime);
                    unitResult.pilot.pilotDef.PilotTags.Add("pilot_fatigued");
                }
                else if (unitResult.pilot.Injuries == 0 && unitResult.pilot.pilotDef.TimeoutRemaining > 0)
                {
                    float roll = UnityEngine.Random.Range(1, 100);
                    float GutCheck = 5 * GutsValue;
                    if (settings.QuirksEnabled && unitResult.pilot.pilotDef.PilotTags.Contains("pilot_gladiator"))
                        GutCheck = GutCheck + 25;
                    if (unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green"))
                        GutCheck = GutCheck + 25;

                    int currenttime = unitResult.pilot.pilotDef.TimeoutRemaining;
                    unitResult.pilot.pilotDef.SetTimeoutTime(0);
                    WorkOrderEntry_MedBayHeal workOrderEntry_MedBayHeal;
                    workOrderEntry_MedBayHeal = (WorkOrderEntry_MedBayHeal)___simState.MedBayQueue.GetSubEntry(unitResult.pilot.Description.Id);
                    ___simState.MedBayQueue.RemoveSubEntry(unitResult.pilot.Description.Id);
                    int TotalFatigueTime = currenttime + FatigueTime;
                    if (TotalFatigueTime > settings.MaximumFatigueTime && !(settings.QuirksEnabled && unitResult.pilot.pilotDef.PilotTags.Contains("pilot_wealthy")))
                        TotalFatigueTime = settings.MaximumFatigueTime;
                    unitResult.pilot.pilotDef.SetTimeoutTime(TotalFatigueTime);
                    unitResult.pilot.pilotDef.PilotTags.Add("pilot_fatigued");

                    if (roll > GutCheck && (settings.LightInjuriesOn))
                    {
                        if (settings.BEXCE && simstate.Constants.Story.MaximumDebt != 42)
                            return;

                        unitResult.pilot.pilotDef.PilotTags.Add("pilot_lightinjury");
                        unitResult.pilot.pilotDef.PilotTags.Remove("pilot_fatigued");
                    }
                }
                if (unitResult.pilot.pilotDef.PilotTags.Contains("PQ_pilot_green"))
                    unitResult.pilot.pilotDef.PilotTags.Remove("PQ_pilot_green");
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("CanPilot", MethodType.Getter)]
        public static class BattleTech_Pilot_CanPilot_Postfix
        {
            public static void Postfix(Pilot __instance, ref bool __result)
            {
                if (__instance.Injuries == 0 && __instance.pilotDef.TimeoutRemaining > 0 && __instance.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    __result = true;
                else if (settings.InjuriesHurt && settings.CanPilotInjured && (__instance.Injuries > 0 || __instance.pilotDef.PilotTags.Contains("pilot_lightinjury")))
                {
                    double InjuryCount = __instance.Injuries;
                    if (InjuryCount < 1 || __instance.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                        InjuryCount = 0.5;
                    //allow injured pilots if InjuriesHurt is true and they meet requirements
                    GameInstance battletechGame = UnityGameInstance.BattleTechGame;

                    if (battletechGame == null || battletechGame.Simulation == null)
                        return;

                    //formula is that for every 3 medtechs, we can ignore an injury
                    if (battletechGame.Simulation.MedTechSkill > 3)
                        if ((battletechGame.Simulation.MedTechSkill / 3) >= InjuryCount)
                            __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
        public static class CorrectTimeOut
        {
            public static void Postfix(SimGameState __instance, List<TemporarySimGameResult> ___TemporaryResultTracker)
            {
                List<Pilot> list = new List<Pilot>(__instance.PilotRoster);
                list.Add(__instance.Commander);
                for (int j = 0; j < list.Count; j++)
                {
                    Pilot pilot = list[j];
                    if (pilot.pilotDef.TimeoutRemaining == 0 && pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        pilot.pilotDef.PilotTags.Remove("pilot_fatigued");
                    }
                    if (pilot.pilotDef.TimeoutRemaining == 0 && pilot.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        pilot.pilotDef.PilotTags.Remove("pilot_lightinjury");
                        pilot.StatCollection.ModifyStat<int>("Light Injury Healed", 0, "Injuries", StatCollection.StatOperation.Set, 0, -1, true);
                    }
                    if (pilot.pilotDef.PilotTags.Contains("PF_pilot_morale_low"))
                    {
                        pilot.pilotDef.PilotTags.Remove("PF_pilot_morale_low");
                        pilot.pilotDef.PilotTags.Add("pilot_morale_low");

                        var eventTagSet = new TagSet();

                        Traverse.Create(eventTagSet).Field("items").SetValue(new string[] { "pilot_morale_low" });
                        Traverse.Create(eventTagSet).Field("tagSetSourceFile").SetValue("Tags/PilotTags");
                        Traverse.Create(eventTagSet).Method("UpdateHashCode").GetValue();

                        var EventTime = new TemporarySimGameResult();
                        EventTime.ResultDuration = settings.LowMoraleTime - 2;
                        EventTime.Scope = EventScope.MechWarrior;
                        EventTime.TemporaryResult = true;
                        EventTime.AddedTags = eventTagSet;
                        Traverse.Create(EventTime).Field("targetPilot").SetValue(pilot);

                        Traverse.Create(__instance).Method("AddOrRemoveTempTags", new[] { typeof(TemporarySimGameResult), typeof(bool) }).
                            GetValue(EventTime, true);
                        ___TemporaryResultTracker.Add(EventTime);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TaskManagementElement), "UpdateTaskInfo")]
        public static class Show_Fatigued_Info
        {
            public static void Postfix(TaskManagementElement __instance, TextMeshProUGUI ___subTitleText, UIColorRefTracker ___subTitleColor,
                WorkOrderEntry ___entry)
            {
                WorkOrderEntry_MedBayHeal healOrder = ___entry as WorkOrderEntry_MedBayHeal;
                try
                {
                    if (healOrder.Pilot.pilotDef.TimeoutRemaining > 0 && healOrder.Pilot.pilotDef.Injuries == 0
                        && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        ___subTitleText.text = "FATIGUED";
                        ___subTitleColor.SetUIColor(UIColor.Orange);
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    if (healOrder.Pilot.pilotDef.TimeoutRemaining > 0 && healOrder.Pilot.pilotDef.Injuries == 0
                        && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury") && !healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                    {
                        ___subTitleText.text = "UNAVAILABLE";
                        ___subTitleColor.SetUIColor(UIColor.Blue);
                    }
                }
                catch (Exception)
                {
                }
                try
                {
                    if (healOrder.Pilot.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        ___subTitleText.text = "LIGHT INJURY";
                        ___subTitleColor.SetUIColor(UIColor.Green);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        //Make Fatigue reduce resolve. 
        [HarmonyPatch(typeof(Team), "CollectUnitBaseline")]
        public static class Resolve_Reduction_Patch
        {
            public static void Postfix(Team __instance, ref int __result)
            {
                if (settings.FatigueReducesResolve)
                {
                    foreach (AbstractActor actor in __instance.units)
                    {
                        Pilot pilot = actor.GetPilot();
                        if (pilot.pilotDef.PilotTags.Contains("pilot_fatigued"))
                        {
                            int TimeOut = pilot.pilotDef.TimeoutRemaining;
                            int Penalty = 0;
                            if (settings.UseCumulativeDays)
                            {
                                GameInstance battletechGame = UnityGameInstance.BattleTechGame;
                                if (battletechGame != null)
                                    if (battletechGame.Simulation != null)
                                    {
                                        SimGameState sim = battletechGame.Simulation;
                                        if (sim.MedBayQueue.SubEntryContainsID(pilot.Description.Id))
                                            TimeOut = Mathf.Max(1, Mathf.CeilToInt((float)sim.MedBayQueue.GetSubEntry(pilot.Description.Id).GetRemainingCost() / sim.GetDailyHealValue()));
                                    }
                            }

                            if (pilot.pilotDef.PilotTags.Contains("pilot_gladiator") && settings.QuirksEnabled)
                                Penalty = (int)Math.Floor(TimeOut / settings.FatigueResolveFactor);
                            else
                                Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueResolveFactor);

                            __result -= Penalty;
                            if (!settings.AllowNegativeResolve && __result < 0)
                                __result = 0;
                        }
                    }
                }
            }
        }

        //Fatigue applies Low Spirits
        [HarmonyPatch(typeof(TurnEventNotification), "ShowTeamNotification")]
        public static class TurnEventNotification_Patch
        {
            public static void Prefix(TurnEventNotification __instance, Team team, bool ___hasBegunGame, CombatGameState ___Combat)
            {
                if (settings.FatigueCausesLowSpirits)
                {
                    if (!___hasBegunGame && ___Combat.TurnDirector.CurrentRound <= 1)
                    {
                        foreach (AbstractActor actor in team.units)
                        {
                            Pilot pilot = actor.GetPilot();
                            if (pilot.pilotDef.PilotTags.Contains("pilot_fatigued") && !(settings.QuirksEnabled && pilot.pilotDef.PilotTags.Contains("pilot_gladiator")))
                            {
                                pilot.pilotDef.PilotTags.Add("pilot_morale_low");
                                pilot.pilotDef.PilotTags.Add("PF_pilot_morale_low");
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Gunnery", MethodType.Getter)]
        public class GunneryTimeModifier
        {
            public static void Postfix(Pilot __instance, ref int __result)
            {
                int Penalty = 0;
                string LogString = "";
                if ((__instance.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills) || (__instance.pilotDef.PilotTags.Contains("pilot_lightinjury") && settings.InjuriesHurt))
                {
                    LogString += $"Pilot {__instance.Callsign}\n";
                    int TimeOut = __instance.pilotDef.TimeoutRemaining;
                    if (settings.UseCumulativeDays)
                    {
                        GameInstance battletechGame = UnityGameInstance.BattleTechGame;
                        if (battletechGame != null)
                            if (battletechGame.Simulation != null)
                            {
                                SimGameState sim = battletechGame.Simulation;
                                if (sim.MedBayQueue.SubEntryContainsID(__instance.Description.Id))
                                    TimeOut = Mathf.Max(1, Mathf.CeilToInt((float)sim.MedBayQueue.GetSubEntry(__instance.Description.Id).GetRemainingCost() / sim.GetDailyHealValue()));
                            }
                        LogString += $"Starting days = {__instance.pilotDef.TimeoutRemaining} Remaining Days = {TimeOut}\n";
                    }
                    if (settings.FatigueFactorIsPercent)
                    {
                        double reductionFactor = TimeOut * (settings.FatigueFactor / 100);
                        if (__instance.pilotDef.PilotTags.Contains("pilot_gladiator") && settings.QuirksEnabled)
                            Penalty = (int)Math.Floor(__result * reductionFactor);
                        else
                            Penalty = (int)Math.Ceiling(__result * reductionFactor);
                        LogString += $"TimeOut is {TimeOut} FatigueFactor is {settings.FatigueFactor} ReductionFactor is {reductionFactor} for Penalty of {Penalty}\n";
                    }
                    else
                    {
                        if (__instance.pilotDef.PilotTags.Contains("pilot_gladiator") && settings.QuirksEnabled)
                            Penalty = (int)Math.Floor(TimeOut / settings.FatigueFactor);
                        else
                            Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueFactor);
                    }
                    if (__instance.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                        Penalty = (int)Math.Ceiling(Penalty * 1.5);
                }

                if (settings.InjuriesHurt && __instance.Injuries > 0)
                {
                    if (settings.InjuryReductionIsPercent)
                    {
                        double Injuries = __instance.Injuries;
                        double Health = __instance.TotalHealth;
                        double ReductionFactor = Injuries / Health;
                        Penalty += (int)Math.Ceiling(__result * ReductionFactor);
                        LogString += $"Health is {__instance.Health} Bonus Health is {__instance.BonusHealth}\n";
                        LogString += $"Total Health is {__instance.TotalHealth} InjuryCount is {__instance.Injuries} ReductionFactor is {ReductionFactor} for Penalty of {Penalty}\n";
                    }
                    else
                        Penalty += __instance.Injuries;
                }
                int NewValue = __result - Penalty;
                if (NewValue < 1)
                    NewValue = 1;
                if (Penalty > 0)
                {
                    LogString += $"Gunnery of {__result} reduced by penalty of {Penalty} for reduced rating of {NewValue}";
                    //Helper.Logger.LogLine(LogString);
                }
                __result = NewValue;
            }
        }
        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Piloting", MethodType.Getter)]
        public class PilotingHealthModifier
        {
            public static void Postfix(Pilot __instance, ref int __result)
            {
                int Penalty = 0;
                string LogString = "";
                if ((__instance.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills) || (__instance.pilotDef.PilotTags.Contains("pilot_lightinjury") && settings.InjuriesHurt))
                {
                    LogString += $"Pilot {__instance.Callsign}\n";
                    int TimeOut = __instance.pilotDef.TimeoutRemaining;
                    if (settings.UseCumulativeDays)
                    {
                        GameInstance battletechGame = UnityGameInstance.BattleTechGame;
                        if (battletechGame != null)
                            if (battletechGame.Simulation != null)
                            {
                                SimGameState sim = battletechGame.Simulation;
                                if (sim.MedBayQueue.SubEntryContainsID(__instance.Description.Id))
                                    TimeOut = Mathf.Max(1, Mathf.CeilToInt((float)sim.MedBayQueue.GetSubEntry(__instance.Description.Id).GetRemainingCost() / sim.GetDailyHealValue()));
                            }
                        LogString += $"Starting days = {__instance.pilotDef.TimeoutRemaining} Remaining Days = {TimeOut}\n";
                    }
                    if (settings.FatigueFactorIsPercent)
                    {
                        double reductionFactor = TimeOut * (settings.FatigueFactor / 100);
                        Penalty = (int)Math.Ceiling(__result * reductionFactor);
                        LogString += $"TimeOut is {TimeOut} FatigueFactor is {settings.FatigueFactor} ReductionFactor is {reductionFactor} for Penalty of {Penalty}\n";
                    }
                    else
                        Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueFactor);
                    if (__instance.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                        Penalty = (int)Math.Ceiling(Penalty * 1.5);
                }

                if (settings.InjuriesHurt && __instance.Injuries > 0)
                {
                    if (settings.InjuryReductionIsPercent)
                    {
                        double Injuries = __instance.Injuries;
                        double Health = __instance.TotalHealth;
                        double ReductionFactor = Injuries / Health;
                        Penalty += (int)Math.Ceiling(__result * ReductionFactor);
                        LogString += $"Health is {__instance.Health} Bonus Health is {__instance.BonusHealth}\n";
                        LogString += $"Total Health is {__instance.TotalHealth} InjuryCount is {__instance.Injuries} ReductionFactor is {ReductionFactor} for Penalty of {Penalty}\n";
                    }
                    else
                        Penalty += __instance.Injuries;
                }
                int NewValue = __result - Penalty;
                if (NewValue < 1)
                    NewValue = 1;
                if (Penalty > 0)
                {
                    LogString += $"Piloting of {__result} reduced by penalty of {Penalty} for reduced rating of {NewValue}";
                    //Helper.Logger.LogLine(LogString);
                }
                __result = NewValue;
            }
        }

        [HarmonyPatch(typeof(Pilot))]
        [HarmonyPatch("Tactics", MethodType.Getter)]
        public class TacticsHealthModifier
        {
            public static void Postfix(Pilot __instance, ref int __result)
            {
                int Penalty = 0;
                string LogString = "";
                if ((__instance.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills) || (__instance.pilotDef.PilotTags.Contains("pilot_lightinjury") && settings.InjuriesHurt))
                {
                    LogString += $"Pilot {__instance.Callsign}\n";
                    int TimeOut = __instance.pilotDef.TimeoutRemaining;
                    if (settings.UseCumulativeDays)
                    {
                        GameInstance battletechGame = UnityGameInstance.BattleTechGame;
                        if (battletechGame != null)
                            if (battletechGame.Simulation != null)
                            {
                                SimGameState sim = battletechGame.Simulation;
                                if (sim.MedBayQueue.SubEntryContainsID(__instance.Description.Id))
                                    TimeOut = Mathf.Max(1, Mathf.CeilToInt((float)sim.MedBayQueue.GetSubEntry(__instance.Description.Id).GetRemainingCost() / sim.GetDailyHealValue()));
                            }
                        LogString += $"Starting days = {__instance.pilotDef.TimeoutRemaining} Remaining Days = {TimeOut}\n";
                    }
                    if (settings.FatigueFactorIsPercent)
                    {
                        double reductionFactor = TimeOut * (settings.FatigueFactor / 100);
                        Penalty = (int)Math.Ceiling(__result * reductionFactor);
                        LogString += $"TimeOut is {__instance.pilotDef.TimeoutRemaining} FatigueFactor is {settings.FatigueFactor} ReductionFactor is {reductionFactor} for Penalty of {Penalty}\n";
                    }
                    else
                        Penalty = (int)Math.Ceiling(TimeOut / settings.FatigueFactor);
                    if (__instance.pilotDef.PilotTags.Contains("pilot_lightinjury"))
                    {
                        Penalty = (int)Math.Ceiling(Penalty * 1.5);
                        LogString += $"Light Injury is Penalty {Penalty}\n";
                    }
                }

                if (settings.InjuriesHurt && __instance.Injuries > 0)
                {
                    if (settings.InjuryReductionIsPercent)
                    {
                        double Injuries = __instance.Injuries;
                        double Health = __instance.TotalHealth;
                        double ReductionFactor = Injuries / Health;
                        Penalty += (int)Math.Ceiling(__result * ReductionFactor);
                        LogString += $"Health is {__instance.Health} Bonus Health is {__instance.BonusHealth}\n";
                        LogString += $"Total Health is {__instance.TotalHealth} InjuryCount is {__instance.Injuries} ReductionFactor is {ReductionFactor} for Penalty of {Penalty}\n";
                    }
                    else
                        Penalty += __instance.Injuries;
                }
                int NewValue = __result - Penalty;
                if (NewValue < 1)
                    NewValue = 1;
                if (Penalty > 0)
                {
                    LogString += $"Tactics of {__result} reduced by penalty of {Penalty} for reduced rating of {NewValue}";
                    //Helper.Logger.LogLine(LogString);
                }
                __result = NewValue;
            }
        }

        //prevent skill up if skills are reduced by fatigue or injury
        [HarmonyPatch(typeof(SGBarracksAdvancementPanel), "SetPips")]
        public static class SGBarracksAdvancementPanel_SetPips_Prefix
        {
            //HarmonyPriority set to 100 to ensure prefix runs before Abilifier, because HarmonyBefore alone wasn't enough (likely due to Abilifier not having a HarmonyAfter annoation)
            [HarmonyPriority(100)]
            [HarmonyBefore(new string[] { "ca.gnivler.BattleTech.Abilifier" })]
            public static void Prefix(Pilot ___curPilot, ref bool needsXP, ref bool isLocked)
            {
                if ((___curPilot.pilotDef.PilotTags.Contains("pilot_fatigued") && settings.FatigueReducesSkills) || (___curPilot.Injuries > 0 && settings.InjuriesHurt))
                {
                    needsXP = true;
                    isLocked = true;
                }
            }
        }

        public static class Helper
        {
            public class Logger
            {
                public static void NewLog()
                {
                    string path = "mods/Pilot_Fatigue/Log.txt";
                    using (StreamWriter streamWriter = new StreamWriter(path, false))
                    {
                        streamWriter.WriteLine("");
                    }
                }
                public static void LogError(Exception ex)
                {
                    using (StreamWriter streamWriter = new StreamWriter("mods/Pilot_Fatigue/Log.txt", true))
                    {
                        streamWriter.WriteLine(string.Concat(new string[]
                        {
                        "Message :",
                        ex.Message,
                        "<br/>",
                        Environment.NewLine,
                        "StackTrace :",
                        ex.StackTrace,
                        Environment.NewLine,
                        "Date :",
                        DateTime.Now.ToString()
                        }));
                        streamWriter.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
                    }
                }

                public static void LogLine(string line)
                {
                    string path = "mods/Pilot_Fatigue/Log.txt";
                    using (StreamWriter streamWriter = new StreamWriter(path, true))
                    {
                        streamWriter.WriteLine(DateTime.Now.ToString() + Environment.NewLine + line + Environment.NewLine);
                        //streamWriter.WriteLine(line + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                        //streamWriter.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
                    }
                }
            }
        }
        internal class ModSettings
        {
            public bool ArgoUpgradeReduction = false;
            public int FatigueTimeStart = 7;
            public int FatigueMinimum = 0;
            public int MoralePositiveTierOne = 5;
            public int MoralePositiveTierTwo = 15;
            public int MoraleNegativeTierOne = -5;
            public int MoraleNegativeTierTwo = -15;
            public bool UseCumulativeDays = true;
            public bool FatigueReducesSkills = false;
            public double FatigueFactor = 7.5;
            public bool FatigueFactorIsPercent = true;
            public bool QuirksEnabled = false;
            public bool FatigueReducesResolve = true;
            public double FatigueResolveFactor = 2.5;
            public bool FatigueCausesLowSpirits = true;
            public int LowMoraleTime = 14;
            public bool LightInjuriesOn = true;
            public bool InjuriesHurt = true;
            public bool InjuryReductionIsPercent = true;
            public bool CanPilotInjured = true;
            public int MaximumFatigueTime = 14;
            public bool AllowNegativeResolve = false;
            public int MechDamageMaxDays = 5;
            public int pilot_wealthy_extra_fatigue = 1;
            public int pilot_athletic_FatigueDaysReduction = 1;
            public double pilot_athletic_FatigueDaysReductionFactor = 0.5;
            public bool BEXCE = false;
        }
    }
}