// <copyright file="OutsideConnectionAIPatches.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace LifecycleRebalance
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using AlgernonCommons;
    using ColossalFramework;
    using ColossalFramework.Math;
    using HarmonyLib;

    /// <summary>
    /// Transpiler to patch OutsideConnectionAI.StartConnectionTransferImpl.
    /// This implements the mods custom immigrant (age, education level) settings.
    /// </summary>
    [HarmonyPatch(typeof(OutsideConnectionAI))]
    [HarmonyPatch("StartConnectionTransferImpl")]
    public static class OutsideConnectionAIPatches
    {
        /// <summary>
        /// StartConnectionTransferImpl transpiler.
        /// </summary>
        /// <param name="original">Method being patched.</param>
        /// <param name="instructions">Original ILCode.</param>
        /// <param name="generator">ILCode generator.</param>
        /// <returns>Modified ILCode.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            // Local variable ILCode indexes (original method).
            const int EducationVarIndex = 2;
            const int NumVarIndex = 3;
            const int ILoopVarIndex = 19;
            const int AgeVarIndex = 23;
            const int Education2VarIndex = 24;

            Logging.Message("starting StartConnectionTransferImpl transpiler");

            // Local variables used by the patch.
            // These need to be set up prior to the i loop that the patch goes into.
            var ageArray = generator.DeclareLocal(typeof(int[]));
            var childrenAgeMax = generator.DeclareLocal(typeof(int));
            var childrenAgeMin = generator.DeclareLocal(typeof(int));
            var minAdultAge = generator.DeclareLocal(typeof(int));

            // Transpiler meta.
            var instructionsEnumerator = instructions.GetEnumerator();
            var instruction = (CodeInstruction)null;
            var startFound = false;
            var instructionsRemaining = 1;

            // Find start pont of patch - keep going while we have instructions left and we've got instructions remaining to patch.
            while (instructionsEnumerator.MoveNext() && instructionsRemaining > 0)
            {
                // Get next instruction and add it to output.
                instruction = instructionsEnumerator.Current;
                yield return instruction;

                // Decrement remaining instructions counter if we're patching.
                if (startFound)
                {
                    instructionsRemaining -= 1;
                }

                // Otherwise, check to see if we've found the start.
                // We're looking for stloc.s 16 (initialising the for i = 0 at the start of the key loop); this only occurs twice in the original method, and we want the first.
                else if (instruction.opcode == OpCodes.Stloc_S && instruction.operand is LocalBuilder builder && builder.LocalIndex == ILoopVarIndex)
                {
                    // Set the flag.
                    startFound = true;

                    // Set additional local variable values before we start the loop.
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Stloc_S, childrenAgeMax.LocalIndex);

                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Stloc_S, childrenAgeMin.LocalIndex);

                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    yield return new CodeInstruction(OpCodes.Stloc_S, minAdultAge.LocalIndex);

                    yield return new CodeInstruction(OpCodes.Ldloc_S, NumVarIndex);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OutsideConnectionAIPatches), nameof(OutsideConnectionAIPatches.GetAgeArray)));
                    yield return new CodeInstruction(OpCodes.Stloc_S, ageArray.LocalIndex);
                }
            }

            // Save the labels from the current (original) instruction.
            var startForLabels = instructionsEnumerator.Current.labels;

            // Now we've got the location and added the local variable setup, skip forward until we find the location for the end of the patch.
            // Looking for Stloc_S 23 (initialising bool flag4 = false).
            do
            {
                instruction = instructionsEnumerator.Current;
            }
            while ((instruction.opcode != OpCodes.Stloc_S || !(instruction.operand is LocalBuilder builder && builder.LocalIndex == Education2VarIndex)) && instructionsEnumerator.MoveNext());

            // Load required variables for patch method onto stack.
            yield return new CodeInstruction(OpCodes.Ldloc_S, ILoopVarIndex) { labels = startForLabels };
            yield return new CodeInstruction(OpCodes.Ldloc_S, EducationVarIndex);
            yield return new CodeInstruction(OpCodes.Ldloc_S, ageArray.LocalIndex);
            yield return new CodeInstruction(OpCodes.Ldloca_S, childrenAgeMax.LocalIndex);
            yield return new CodeInstruction(OpCodes.Ldloca_S, childrenAgeMin.LocalIndex);
            yield return new CodeInstruction(OpCodes.Ldloca_S, minAdultAge.LocalIndex);
            yield return new CodeInstruction(OpCodes.Ldloca_S, Education2VarIndex);
            yield return new CodeInstruction(OpCodes.Ldloca_S, AgeVarIndex);

            // Add call to patch method.
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OutsideConnectionAIPatches), nameof(OutsideConnectionAIPatches.RandomizeImmigrants)));

            // Patch done; add remaining instructions.
            while (instructionsEnumerator.MoveNext())
            {
                yield return instructionsEnumerator.Current;
            }

            Logging.Message("StartConnectionTransferImpl transpiler completed");
        }

        /// <summary>
        /// Method called by OutsideConnectionAI.StartConnectionTransferImpl Transpiler insertion.
        /// Randomises (within parameters) age and education levels of immigrants.
        /// All variables are local variables within OutsideConnectionAI.StartConnectionTransferImpl.
        /// </summary>
        /// <param name="i">Loop counter (for loop containing method call).</param>
        /// <param name="education">Education level input (StartConnectionTransferImpl local variable).</param>
        /// <param name="ageArray">Array of acceptible ages (from mod DataStore); placed on stack in advance via Transpiler insertion.</param>
        /// <param name="childrenAgeMax">Maximum child immigrant age; placed on stack in advance via Transpiler insertion.</param>
        /// <param name="childrenAgeMin">Minimum child immigrant age; placed on stack in advance via Transpiler insertion.</param>
        /// <param name="minAdultAge">Minimum adult immigrant age; placed on stack in advance via Transpiler insertion.</param>
        /// <param name="resultEducation">Resultant education level for immigrant after mod calculations (StartConnectionTransferImpl local variable 'education2').</param>
        /// <param name="resultAge">Resultant age level for immigrant after mod calculations (StartConnectionTransferImpl local variable 'age').</param>
        public static void RandomizeImmigrants(int i, Citizen.Education education, int[] ageArray, ref int childrenAgeMax, ref int childrenAgeMin, ref int minAdultAge, out Citizen.Education resultEducation, out int resultAge)
        {
            // Minimum and maximum ages.
            int min = ageArray[0];
            int max = ageArray[1];

            // We start inside an i loop.
            // i is is the family member number for this incoming family.  0 is primary adult, 1 is secondary adults, and after that are children.
            if (i == 1)
            {
                // Age of second adult - shouldn't be too far from the first. Just because.
                min = Math.Max(minAdultAge - 20, DataStore.IncomingAdultAge[0]);
                max = Math.Min(minAdultAge + 20, DataStore.IncomingAdultAge[1]);
            }
            else if (i >= 2)
            {
                // Children.
                min = childrenAgeMin;
                max = childrenAgeMax;
            }

            // Calculate actual age randomly between minumum and maxiumum.
            resultAge = Singleton<SimulationManager>.instance.m_randomizer.Int32(min, max);

            // Adust age brackets for subsequent family members.
            if (i == 0)
            {
                minAdultAge = resultAge;
            }
            else if (i == 1)
            {
                // Restrict to adult age. Young adult is 18 according to National Institutes of Health... even if the young adult section in a library isn't that range.
                minAdultAge = Math.Min(resultAge, minAdultAge);

                // Children should be between 80 and 180 younger than the youngest adult.
                childrenAgeMax = Math.Max(minAdultAge - 80, 0);  // Allow people 10 ticks from 'adulthood' to have kids
                childrenAgeMin = Math.Max(minAdultAge - 178, 0); // Accounting gestation, which isn't simulated yet (2 ticks)
            }

            // Set default eductation output to what the game has already determined.
            resultEducation = education;

            if (education < Citizen.Education.Uneducated || resultEducation > Citizen.Education.ThreeSchools)
            {
                Logging.Error("invalid starting education level ", education);
            }

            // Calculate education levels.
            if (i < 2)
            {
                // Adults (set education levels).
                // Local references.
                ref Randomizer simulationRandomizer = ref Singleton<SimulationManager>.instance.m_randomizer;
                ModSettings settings = ModSettings.Settings;

                // 24% different education levels.
                int eduModifier = settings.RandomImmigrantEd ? simulationRandomizer.Int32(-12, 12) / 10 : 0;

                if (settings.ImmiEduBoost)
                {
                    // Education boost - 50% chance.
                    eduModifier += simulationRandomizer.Int32(0, 1);
                }
                else if (settings.ImmiEduDrag)
                {
                    // Education drag - 50% chance.
                    eduModifier -= simulationRandomizer.Int32(0, 1);
                }

                // Apply education modifier and clamp bounds.
                resultEducation += eduModifier;
                if (resultEducation < Citizen.Education.Uneducated)
                {
                    resultEducation = Citizen.Education.Uneducated;
                }
                else if (resultEducation > Citizen.Education.ThreeSchools)
                {
                    resultEducation = Citizen.Education.ThreeSchools;
                }
            }
            else
            {
                // Children (set education levels).
                switch (Citizen.GetAgeGroup(resultAge))
                {
                    case Citizen.AgeGroup.Child:
                        resultEducation = Citizen.Education.Uneducated;
                        break;
                    case Citizen.AgeGroup.Teen:
                        resultEducation = Citizen.Education.OneSchool;
                        break;
                    default:
                        // Make it that 80% graduate from high school
                        resultEducation = (Singleton<SimulationManager>.instance.m_randomizer.Int32(0, 100) < 80) ? Citizen.Education.TwoSchools : Citizen.Education.OneSchool;
                        break;
                }
            }

            // Write to immigration log if that option is selected.
            if (LifecycleLogging.UseImmigrationLog)
            {
                LifecycleLogging.WriteToLog(LifecycleLogging.ImmigrationLogName, "Family member ", i, " immigrating with age ", resultAge, " (" + (int)(resultAge / ModSettings.AgePerYear), " years old) and education level ", resultEducation, " from original education level ", education);
            }
        }

        /// <summary>
        /// Invoked by Transpiler insertion to set up age array data from DataStore prior to immigrant calculation method call.
        /// </summary>
        /// <param name="num">Number of immigrants; '1' will select single immigrant age range, other values select ages for initial adult family member.</param>
        /// <returns>Immigration age aarray reference.</returns>
        public static int[] GetAgeArray(int num) => num == 1 ? DataStore.IncomingSingleAge : DataStore.IncomingAdultAge;
    }
}