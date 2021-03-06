﻿using ColossalFramework;
using HarmonyLib;


namespace LifecycleRebalance
{
    /// <summary>
    /// Harmony patch to slowly cycle through buildings and remove citizens with invalid flags.
    /// </summary>
    [HarmonyPatch(typeof(CitizenManager), "SimulationStepImpl")]
    public static class CitizenManagerPatch
    {
        /// <summary>
        /// Sequntial step count; start with a random value to ensure spread of checking over multiple saves.
        /// </summary>
        private static uint stepCount = (uint)new System.Random().Next(16);

        // Local reference.


        /// <summary>
        /// Harmony Prefix patch to CitizenManager.SimulationStepImpl, to detect and remove any citizens in the building that don't have the 'created' flag set.
        /// </summary>
        /// <param name="__instance">Instance reference</param>
        /// <param name="subStep">Simulation cycle substep.</param>
        public static void Prefix(CitizenManager __instance, int subStep)
        {
            // Don't do anything when subStep is zero.
            if (subStep != 0)
            {
                // Local references.
                Citizen[] citizenBuffer = __instance.m_citizens.m_buffer;
                CitizenUnit[] citizenUnits = __instance.m_units.m_buffer;

                // Get current framecount to align with parent method's frame, so we avoid unnecessary cache misses.
                uint frameCount = Singleton<SimulationManager>.instance.m_currentFrameIndex & 0xFFF;
                uint currentFrame = frameCount * 128;
                uint baseUnit = currentFrame + (stepCount * 8);
                uint endUnit = baseUnit + 7;

                // Increment our step counter when frameCount wraps back to zero (evey 4096 increments).
                if (frameCount == 0)
                {
                    ++stepCount;

                    // If our step counter is greater than 15, wrap back to zero.
                    if (stepCount > 15)
                    {
                        stepCount = 0;
                    }
                }

                // Iterate through next 8 units in this frame.
                for (uint currentUnit = baseUnit; currentUnit <= endUnit; ++currentUnit)
                {
                    // Only interested in home units.
                    if ((citizenUnits[currentUnit].m_flags & CitizenUnit.Flags.Home) != 0 && citizenUnits[currentUnit].m_building != 0)
                    {
                        // Check for citizens with invalid flags in this household.
                        uint thisCitizen = citizenUnits[currentUnit].m_citizen0;
                        if (thisCitizen != 0)
                        {
                            Citizen.Flags citizenFlags = citizenBuffer[thisCitizen].m_flags;
                            if ((citizenFlags & Citizen.Flags.Created) == 0)
                            {
                                RemoveCitizen(__instance, ref citizenUnits[currentUnit].m_citizen0, currentUnit, citizenUnits[currentUnit].m_building, citizenFlags);
                            }
                        }
                        thisCitizen = citizenUnits[currentUnit].m_citizen1;
                        if (thisCitizen != 0)
                        {
                            Citizen.Flags citizenFlags = citizenBuffer[thisCitizen].m_flags;
                            if ((citizenFlags & Citizen.Flags.Created) == 0)
                            {
                                RemoveCitizen(__instance, ref citizenUnits[currentUnit].m_citizen1, currentUnit, citizenUnits[currentUnit].m_building, citizenFlags);
                            }
                        }
                        thisCitizen = citizenUnits[currentUnit].m_citizen2;
                        if (thisCitizen != 0)
                        {
                            Citizen.Flags citizenFlags = citizenBuffer[thisCitizen].m_flags;
                            if ((citizenFlags & Citizen.Flags.Created) == 0)
                            {
                                RemoveCitizen(__instance, ref citizenUnits[currentUnit].m_citizen2, currentUnit, citizenUnits[currentUnit].m_building, citizenFlags);
                            }
                        }
                        thisCitizen = citizenUnits[currentUnit].m_citizen3;
                        if (thisCitizen != 0)
                        {
                            Citizen.Flags citizenFlags = citizenBuffer[thisCitizen].m_flags;
                            if ((citizenFlags & Citizen.Flags.Created) == 0)
                            {
                                RemoveCitizen(__instance, ref citizenUnits[currentUnit].m_citizen3, currentUnit, citizenUnits[currentUnit].m_building, citizenFlags);
                            }
                        }
                        thisCitizen = citizenUnits[currentUnit].m_citizen4;
                        if (thisCitizen != 0)
                        {
                            Citizen.Flags citizenFlags = citizenBuffer[thisCitizen].m_flags;
                            if ((citizenFlags & Citizen.Flags.Created) == 0)
                            {
                                RemoveCitizen(__instance, ref citizenUnits[currentUnit].m_citizen4, currentUnit, citizenUnits[currentUnit].m_building, citizenFlags);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Removes a citizen with invalid flags.
        /// </summary>
        /// <param name="citizenManager">CitizenManager instance reference</param>
        /// <param name="citizenID">Citizen ID to remove</param>
        /// <param name="citizenUnit">Owning CitizenUnit ID</param>
        /// <param name="buildingID">Home building ID</param>
        /// <param name="flags">Citizen flags</param>
        private static void RemoveCitizen(CitizenManager citizenManager, ref uint citizenID, uint citizenUnit, ushort buildingID, Citizen.Flags flags)
        {
            // Log messaged.
            Logging.Message("found citizen ", citizenID.ToString(), " in unit ", citizenUnit.ToString(), " of building ", buildingID.ToString(), " with invalid flags ", flags.ToString());

            // Remove citizen and reset reference in CitizenUnit.
            citizenManager.ReleaseCitizen(citizenID);
            citizenID = 0;
        }
    }
}
