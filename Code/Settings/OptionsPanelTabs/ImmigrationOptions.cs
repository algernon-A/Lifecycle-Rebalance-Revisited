﻿using UnityEngine;
using ColossalFramework.UI;


namespace LifecycleRebalance
{
    /// <summary>
    /// Options panel for setting health options.
    /// </summary>
    public class ImmigrationOptions
    {
        /// <summary>
        /// Adds immigration options tab to tabstrip.
        /// </summary>
        /// <param name="tabStrip">Tab strip to add to</param>
        /// <param name="tabIndex">Index number of tab</param>
        public ImmigrationOptions(UITabstrip tabStrip, int tabIndex)
        {
            // Add tab.
            UIPanel immigrationTab = PanelUtils.AddTab(tabStrip, Translations.Translate("LBR_IMM"), tabIndex);

            // Use vanilla.
            UICheckBox immigrationCheckBox = PanelUtils.AddPlainCheckBox(immigrationTab, Translations.Translate("LBR_IMM_VAR"));
            immigrationCheckBox.relativePosition = new Vector3(5f, 5f);
            immigrationCheckBox.isChecked = OptionsPanel.settings.RandomImmigrantEd;
            immigrationCheckBox.eventCheckChanged += (control, isChecked) =>
            {
                // Update mod settings.
                ModSettings.UseTransportModes = isChecked;

                // Update configuration file.
                OptionsPanel.settings.RandomImmigrantEd = isChecked;
                Configuration<SettingsFile>.Save();
            };

            // Boost immigrant education.
            UICheckBox immiEduBoostCheck = PanelUtils.AddPlainCheckBox(immigrationTab, Translations.Translate("LBR_IMM_INC"));
            immiEduBoostCheck.relativePosition = new Vector3(5f, 50f);
            immiEduBoostCheck.isChecked = ModSettings.immiEduBoost;

            // Suppress immigrant education.
            UICheckBox immiEduDragCheck = PanelUtils.AddPlainCheckBox(immigrationTab, Translations.Translate("LBR_IMM_DEC"));
            immiEduDragCheck.relativePosition = new Vector3(5f, 75f);
            immiEduDragCheck.isChecked = ModSettings.immiEduDrag;


            immiEduBoostCheck.eventCheckChanged += (control, isChecked) =>
            {
                // Update mod settings.
                ModSettings.immiEduBoost = isChecked;

                // Toggle immigrant boost check if needed.
                if (isChecked && immiEduDragCheck.isChecked)
                {
                    immiEduDragCheck.isChecked = false;
                }

                // Update configuration file.
                Configuration<SettingsFile>.Save();
            };
            immiEduDragCheck.eventCheckChanged += (control, isChecked) =>
            {
                // Update mod settings.
                ModSettings.immiEduDrag = isChecked;

                // Toggle immigrant boost check if needed.
                if (isChecked && immiEduBoostCheck.isChecked)
                {
                    immiEduBoostCheck.isChecked = false;
                }

                // Update configuration file.
                Configuration<SettingsFile>.Save();
            };
        }
    }
}