// <copyright file="ResidentAIPatches.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard) and Whitefang Greytail. All rights reserved.
// Licensed under the Apache license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace LifecycleRebalance
{
    using System;
    using System.Runtime.CompilerServices;
    using AlgernonCommons;
    using ColossalFramework;
    using HarmonyLib;
    using UnityEngine;

    /// <summary>
    /// Harmony patches for ResidentAI to implement mod functionality.
    /// </summary>
    [HarmonyPatch(typeof(ResidentAI))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony")]
    public static class ResidentAIPatches
    {
        private static Type _cimCareAI = null;

        /// <summary>
        /// Reflects the CimCare mod's NursingHomeAI type, if present, for use in CanMakeBabies patch.
        /// </summary>
        public static void ReflectCimCare()
        {
            _cimCareAI = AssemblyUtils.GetEnabledAssembly("CimCareMod")?.GetType("CimCareMod.AI.NursingHomeAI");

            if (_cimCareAI != null)
            {
                Logging.KeyMessage("Found CimCare NursingHomeAI type ", _cimCareAI.ToString());
            }
            else
            {
                Logging.KeyMessage("CimCare NursingHomeAI not found");
            }
        }

        /// <summary>
        /// Harmony pre-emptive Prefix patch for ResidentAI.CanMakeBabies - implements mod's minor fix so that only adult females (of less than age 180) give birth.
        /// </summary>
        /// <param name="__result">Original method result.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        /// <returns>Always false (never execute original method).</returns>
        [HarmonyPatch(nameof(ResidentAI.CanMakeBabies))]
        [HarmonyPrefix]
        public static bool CanMakeBabies(ref bool __result, uint citizenID, ref Citizen data)
        {
            // Dead people, people moving in, and homeless people can't have babies.
            if (data.Dead || ((data.m_flags & Citizen.Flags.MovingIn) != Citizen.Flags.None) || data.m_homeBuilding == 0)
            {
                __result = false;
            }
            else
            {
                // data.m_family  access group data?
                // Only check child 1 and 2. Don't care about 3, won't fit if there's someone there :)

                // Unlock males within all groups. Find partner except for initial seeding is the exact same age group, so shortcut is allowed
                __result =

                  // Females are now limited to the time that they have aged.  Males are excluded from this calculation if they are under 60, so females can still have children with older males.
                  (Citizen.GetGender(citizenID) == Citizen.Gender.Male && data.Age <= 210) ||

                  // Exclude females over default maximum adult age (51.4 years).
                  (data.Age <= 180 &&
                  ((citizenID % DataStore.LifeSpanMultiplier) == Threading.Counter) && (Citizen.GetAgeGroup(data.Age) == Citizen.AgeGroup.Adult));

                // Check for CimCare (nursing home) mod (it has a bug where it puts unrelated adults into the same houshold/family unit, so we need to check for that and exclude them from being able to have children).
                if (__result)
                {
                    // Check for CimCare mod nursing home AI.
                    if (Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_homeBuilding].Info.m_buildingAI.GetType() == _cimCareAI)
                    {
                        Logging.Message("Found CimCare nursing home AI for building ", data.m_homeBuilding, "; excluding citizen ", citizenID, " from being able to have children");
                        __result = false;
                    }
                }
            }

            // Don't execute base method after this.
            return false;
        }

        /// <summary>
        /// Harmony pre-emptive Prefix patch for ResidentAI.UpdateAge - implements mod's ageing and deathcare rate functions.
        /// CRITICAL for mod functionality.
        /// </summary>
        /// <param name="__result">Original method result.</param>
        /// <param name="__instance">ResidentAI instance.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        /// <returns>Always false (never execute original method).</returns>
        [HarmonyPatch("UpdateAge")]
        [HarmonyPrefix]
        public static bool UpdateAge(ref bool __result, ResidentAI __instance, uint citizenID, ref Citizen data)
        {
            // Method result.
            bool removed = false;

            // Allow for lifespan multipler.
            if ((citizenID % DataStore.LifeSpanMultiplier) == Threading.Counter)
            {
                // Local reference.
                CitizenManager citizenManager = Singleton<CitizenManager>.instance;

                // Increment citizen age.
                int newAge = data.Age + 1;

                if (newAge <= ModSettings.YoungStartAge)
                {
                    // Children and teenagers finish school.
                    if (newAge == ModSettings.TeenStartAge || newAge == ModSettings.YoungStartAge)
                    {
                        FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                    }
                }
                else if (newAge == ModSettings.VanillaAdultAge || newAge >= ModSettings.RetirementAge)
                {
                    // Young adults finish university/college, adults retire.
                    FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                }
                else if ((data.m_flags & Citizen.Flags.Student) != Citizen.Flags.None)
                {
                    // Education management.
                    if (newAge > ModSettings.YoungStartAge)
                    {
                        // Students older than teenagers graduate every 15 age units.
                        if ((newAge - ModSettings.YoungStartAge) % 15 == 0)
                        {
                            FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                        }
                        else
                        {
                            // Evict high school students who've overstayed.
                            if (Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_workBuilding].Info.m_buildingAI.GetEducationLevel2())
                            {
                                Logging.Message("evicting high school student ", citizenID, " at age ", newAge);
                                FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                            }
                        }
                    }
                    else if (newAge > ModSettings.TeenStartAge && Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_workBuilding].Info.m_buildingAI.GetEducationLevel1())
                    {
                        // Evict elementary school students who've overstayed.
                        Logging.Message("evicting elementary school student ", citizenID, " at age ", newAge);
                        FinishSchoolOrWorkRev(__instance, citizenID, ref data);
                    }
                }

                // Original citizen?
                if ((data.m_flags & Citizen.Flags.Original) != Citizen.Flags.None)
                {
                    // Yes - if necessary, update oldest original resident flags.
                    if (citizenManager.m_tempOldestOriginalResident < newAge)
                    {
                        citizenManager.m_tempOldestOriginalResident = newAge;
                    }

                    // Update full lifespan counter.
                    if (newAge == 240)
                    {
                        Singleton<StatisticsManager>.instance.Acquire<StatisticInt32>(StatisticType.FullLifespans).Add(1);
                    }
                }

                // Update age.
                data.Age = newAge;

                // Checking for death and sickness chances.
                // Citizens who are currently moving or currently in a vehicle aren't affected.
                if (data.CurrentLocation != Citizen.Location.Moving && data.m_vehicle == 0)
                {
                    // Local reference.
                    SimulationManager simulationManager = Singleton<SimulationManager>.instance;

                    bool died = false;

                    if (ModSettings.Settings.VanillaCalcs)
                    {
                        // Using vanilla lifecycle calculations.
                        int num2 = 240;
                        int num3 = 255;
                        int num4 = Mathf.Max(0, 145 - ((100 - data.m_health) * 3));
                        if (num4 != 0)
                        {
                            num2 += num4 / 3;
                            num3 += num4;
                        }

                        if (newAge >= num2)
                        {
                            bool flag = simulationManager.m_randomizer.Int32(2000u) < 3;
                            died = simulationManager.m_randomizer.Int32(num2 * 100, num3 * 100) / 100 <= newAge || flag;
                        }
                    }
                    else
                    {
                        // Using custom lifecycle calculations.
                        // Game defines years as being age divided by 3.5.  Hence, 35 age increments per decade.
                        // Legacy mod behaviour worked on 25 increments per decade.
                        // If older than the maximum index - lucky them, but keep going using that final index.
                        int index = Math.Min((int)(newAge * ModSettings.Settings.DecadeFactor), 10);

                        // Calculate 90% - 110%; using 100,000 as 100% (for precision).
                        int modifier = 100000 + ((150 * data.m_health) + (50 * data.m_wellbeing) - 10000);

                        // Death chance is simply if a random number between 0 and the modifier calculated above is less than the survival probability calculation for that decade of life.
                        // Also set maximum age of 400 (~114 years) to be consistent with the base game.
                        died = (simulationManager.m_randomizer.Int32(0, modifier) < DataStore.SurvivalProbCalc[index]) || newAge > 400;

                        // Check for sickness chance if they haven't died.
                        if (!died && simulationManager.m_randomizer.Int32(0, modifier) < DataStore.SicknessProbCalc[index])
                        {
                            // Make people sick, if they're unlucky.
                            data.Sick = true;

                            if (LifecycleLogging.UseSicknessLog)
                            {
                                LifecycleLogging.WriteToLog(LifecycleLogging.SicknessLogName, "Citizen became sick with chance factor ", DataStore.SicknessProbCalc[index]);
                            }
                        }
                    }

                    // Handle citizen death.
                    if (died)
                    {
                        // Check if citizen is only remaining parent and there are children.
                        uint unitID = data.GetContainingUnit(citizenID, Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_homeBuilding].m_citizenUnits, CitizenUnit.Flags.Home);
                        CitizenUnit containingUnit = citizenManager.m_units.m_buffer[unitID];

                        // Log if we're doing that.
                        if (LifecycleLogging.UseDeathLog)
                        {
                            LifecycleLogging.WriteToLog(LifecycleLogging.DeathLogName, "Killed citzen ", citizenID, " at age ", data.Age, " (", (int)(data.Age / ModSettings.AgePerYear), " years old) with family ", containingUnit.m_citizen0, ", " + containingUnit.m_citizen1, ", ", containingUnit.m_citizen2, ", ", containingUnit.m_citizen3, ", ", containingUnit.m_citizen4);
                        }

                        // Reverse redirect to access private method Die().
                        DieRev(__instance, citizenID, ref data);

                        // If there are no adults remaining in this CitizenUnit, remove the others, as orphan households end up in simulation purgatory.
                        bool hasParent = containingUnit.m_citizen0 == citizenID | containingUnit.m_citizen1 == citizenID;
                        bool singleParent = hasParent && (containingUnit.m_citizen0 == 0 | containingUnit.m_citizen1 == 0);
                        bool hasChild = containingUnit.m_citizen2 != 0 | containingUnit.m_citizen3 != 0 | containingUnit.m_citizen4 != 0;

                        if (singleParent && hasChild)
                        {
                            for (int i = 0; i <= 2; ++i)
                            {
                                uint currentChild;
                                switch (i)
                                {
                                    case 0:
                                        currentChild = containingUnit.m_citizen2;
                                        break;
                                    case 1:
                                        currentChild = containingUnit.m_citizen3;
                                        break;
                                    default:
                                        currentChild = containingUnit.m_citizen4;
                                        break;
                                }

                                if (currentChild != 0)
                                {
                                    if (LifecycleLogging.UseDeathLog)
                                    {
                                        LifecycleLogging.WriteToLog(LifecycleLogging.DeathLogName, "Removed orphan ", currentChild);
                                        citizenManager.ReleaseCitizen(currentChild);
                                    }
                                }
                            }
                        }

                        // Chance for 'vanishing corpse' (no need for deathcare).
                        if (!KeepCorpse())
                        {
                            citizenManager.ReleaseCitizen(citizenID);
                            removed = true;
                        }
                    }
                }
            }

            // Original method return value.
            __result = removed;

            // Don't execute base method after this.
            return false;
        }

        /// <summary>
        /// Harmony pre-emptive Prefix patch to ResidentAI.UpdateWorkplace to stop children below school age going to school, and to align young adult and adult behaviour with custom childhood factors.
        /// </summary>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        /// <returns>Always false (never execute original method).</returns>
        [HarmonyPatch("UpdateWorkplace")]
        [HarmonyPrefix]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool UpdateWorkplace(uint citizenID, ref Citizen data)
        {
            // Don't do anything if the citizen is employed or is homeless.
            if (data.m_workBuilding != 0 || data.m_homeBuilding == 0)
            {
                // Don't execute original method (which would just abort anyway).
                return false;
            }

            // Local references.
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;
            Vector3 position = buildingManager.m_buildings.m_buffer[data.m_homeBuilding].m_position;
            DistrictManager districtManager = Singleton<DistrictManager>.instance;
            byte district = districtManager.GetDistrict(position);
            DistrictPolicies.Services servicePolicies = districtManager.m_districts.m_buffer[district].m_servicePolicies;
            int age = data.Age;

            // Default transfer reason - will be replaced with any valid workplace offers.
            TransferManager.TransferReason educationReason = TransferManager.TransferReason.None;

            // Treatment depends on citizen age.
            switch (Citizen.GetAgeGroup(age))
            {
                case Citizen.AgeGroup.Child:
                    // Is this a young child?
                    if (data.m_age < ModSettings.SchoolStartAge)
                    {
                        // Young children should never be educated.
                        // Sometimes the UpdateWellbeing method (called immediately before UpdateWorkplace in SimulationStep) will give these kids education, so we just clear it here.
                        // Easier than messing with UpdateWellbeing.
                        data.Education1 = false;

                        // Young children should also not go shopping (this is checked in following UpdateLocation call in SimulationStep).
                        // This prevents children from going shopping normally (vanilla code), but an additional patch is needed for the Real Time mod - see RealTime.cs.
                        data.m_flags &= ~Citizen.Flags.NeedGoods;

                        // Don't execute original method (thus avoiding assigning to a school).
                        return false;
                    }

                    // If of school age, and not already educated, go to elementary school.
                    if (!data.Education1)
                    {
                        educationReason = TransferManager.TransferReason.Student1;
                    }

                    break;

                case Citizen.AgeGroup.Teen:
                    if (data.Education1 && !data.Education2)
                    {
                        // Teens go to high school, if they've finished elementary school.
                        educationReason = TransferManager.TransferReason.Student2;
                    }

                    break;

                case Citizen.AgeGroup.Young:
                case Citizen.AgeGroup.Adult:
                    // Try for university, if they've finished elementary and high school - delaying two age units to look for work instead if the 'School's out' policy is set.
                    if (data.Education1 & data.Education2 & !data.Education3)
                    {
                        educationReason = TransferManager.TransferReason.Student3;
                    }

                    break;
            }

            // If citizen is unemployed (young adults and adults only), and either:
            // The citizen isn't eligible for university;
            // 'Education boost' is not on, or if it's on, the citizen's age above young adulthood modulo five is 3 or 4 (meaning they've already been looking for work for at least three age units);
            // Or the citizen's age above young adulthood modulo five is 3 or 4 (meaning they've already been looking for work for at least three age units)
            // ...then they look for work (this can be parallel to still seeking education).
            if (data.Unemployed != 0 &&
                (educationReason != TransferManager.TransferReason.Student3 ||
                (servicePolicies & DistrictPolicies.Services.EducationBoost) == 0 ||
                (age - ModSettings.YoungStartAge) % 5 > 2))
            {
                TransferManager.TransferOffer jobSeeking = default;
                jobSeeking.Priority = Singleton<SimulationManager>.instance.m_randomizer.Int32(8u);
                jobSeeking.Citizen = citizenID;
                jobSeeking.Position = position;
                jobSeeking.Amount = 1;
                jobSeeking.Active = true;
                switch (data.EducationLevel)
                {
                    case Citizen.Education.Uneducated:
                        Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Worker0, jobSeeking);
                        break;
                    case Citizen.Education.OneSchool:
                        Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Worker1, jobSeeking);
                        break;
                    case Citizen.Education.TwoSchools:
                        Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Worker2, jobSeeking);
                        break;
                    case Citizen.Education.ThreeSchools:
                        Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Worker3, jobSeeking);
                        break;
                }
            }

            // Handle education reason.
            switch (educationReason)
            {
                case TransferManager.TransferReason.Student3:
                    // If school's out policy is active, citizens won't look for education for the first two age units after becoming young adults.
                    if ((servicePolicies & DistrictPolicies.Services.SchoolsOut) != 0 && (age - ModSettings.YoungStartAge) % 5 <= 1)
                    {
                        break;
                    }

                    goto default;

                default:
                    // Look for education (this can be parallel with looking for work, above).
                    TransferManager.TransferOffer educationSeeking = default;
                    educationSeeking.Priority = Singleton<SimulationManager>.instance.m_randomizer.Int32(8u);
                    educationSeeking.Citizen = citizenID;
                    educationSeeking.Position = position;
                    educationSeeking.Amount = 1;
                    educationSeeking.Active = true;
                    Singleton<TransferManager>.instance.AddOutgoingOffer(educationReason, educationSeeking);
                    break;

                case TransferManager.TransferReason.None:
                    break;
            }

            // If we got here, we need to continue on to the original method (this is not a young child).
            return false;
        }

        /// <summary>
        /// Reverse patch for ResidentAI.FinishSchoolOrWork to access private method of original instance.
        /// </summary>
        /// <param name="instance">ResidentAI instance.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ResidentAI), "FinishSchoolOrWork")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FinishSchoolOrWorkRev(object instance, uint citizenID, ref Citizen data)
        {
            string message = "FinishSchoolOrWork reverse Harmony patch wasn't applied";
            Logging.Error(message, instance.ToString(), citizenID.ToString(), data.ToString());
            throw new NotImplementedException(message);
        }

        /// <summary>
        /// Reverse patch for ResidentAI.Die to access private method of original instance.
        /// </summary>
        /// <param name="instance">ResidentAI instance.</param>
        /// <param name="citizenID">Citizen ID.</param>
        /// <param name="data">Citizen data.</param>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ResidentAI), "Die")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DieRev(object instance, uint citizenID, ref Citizen data)
        {
            string message = "Die reverse Harmony patch wasn't applied";
            Logging.Error(message, instance, citizenID, data);
            throw new NotImplementedException(message);
        }

        /// <summary>
        /// Calculates whether or not a corpse should remain (to be picked up deathcare services), or 'vanish into thin air'.
        /// </summary>
        /// <returns>True if the corpse should remain, False if the corpse should vanish.</returns>
        public static bool KeepCorpse()
        {
            return Singleton<SimulationManager>.instance.m_randomizer.Int32(0, 99) > DataStore.AutoDeadRemovalChance;
        }
    }
}
