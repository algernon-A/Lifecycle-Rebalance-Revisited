﻿// <copyright file="SpeedOptions.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace LifecycleRebalance
{
    using System;
    using AlgernonCommons;
    using AlgernonCommons.Translation;
    using AlgernonCommons.UI;
    using ColossalFramework.UI;

    /// <summary>
    /// Options panel for setting mod lifecycle speed options.
    /// </summary>
    internal sealed class SpeedOptions : OptionsPanelTab
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedOptions"/> class.
        /// Adds speed options tab to tabstrip.
        /// </summary>
        /// <param name="tabStrip">Tab strip to add to.</param>
        /// <param name="tabIndex">Index number of tab.</param>
        internal SpeedOptions(UITabstrip tabStrip, int tabIndex)
        {
            // Add tab.
            Panel = UITabstrips.AddTextTab(tabStrip, Translations.Translate("LBR_SPD"), tabIndex, out _);

            // Set tab object reference.
            tabStrip.tabs[tabIndex].objectUserData = this;
        }

        /// <summary>
        /// Performs initial setup; called via event when tab is first selected.
        /// </summary>
        internal override void Setup()
        {
            // Don't do anything if already set up.
            if (!IsSetup)
            {
                // Perform initial setup.
                IsSetup = true;
                Logging.Message("setting up ", this.GetType());

                // Lifespan multiplier.  Simple integer.
                UISlider lifeMult = UISliders.AddPlainSliderWithValue(
                    Panel,
                    0f,
                    0f,
                    Translations.Translate("LBR_SPD_FAC") + Environment.NewLine + Translations.Translate("LBR_SPD_FN1") + Environment.NewLine + Environment.NewLine + Translations.Translate("LBR_SPD_FN2") + Environment.NewLine + Translations.Translate("LBR_SPD_FN3"),
                    1f,
                    340f,
                    1f,
                    DataStore.LifeSpanMultiplier,
                    680f);

                // Reset to saved button.
                UIButton lifeMultReset = UIButtons.AddButton(Panel, UILayout.PositionUnder(lifeMult), Translations.Translate("LBR_RTS"));
                lifeMultReset.eventClicked += (c, p) =>
                {
                    // Retrieve saved value from datastore - inverted value (see above).
                    lifeMult.value = DataStore.LifeSpanMultiplier;
                };

                // Save settings button.
                UIButton lifeMultSave = UIButtons.AddButton(Panel, UILayout.PositionUnder(lifeMultReset), Translations.Translate("LBR_SAA"));
                lifeMultSave.eventClicked += (c, p) =>
                {
                    // Update mod settings - inverted value (see above).
                    DataStore.LifeSpanMultiplier = (int)lifeMult.value;
                    Logging.Message("lifespan multiplier set to: ", DataStore.LifeSpanMultiplier);

                    // Update WG configuration file.
                    DataStore.SaveXML();
                };
            }
        }
    }
}