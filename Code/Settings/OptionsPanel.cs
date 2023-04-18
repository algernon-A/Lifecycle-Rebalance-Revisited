// <copyright file="OptionsPanel.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace LifecycleRebalance
{
    using AlgernonCommons.UI;
    using ColossalFramework.UI;

    /// <summary>
    /// Lifecycle Rebalance Revisited options panel.
    /// </summary>
    public sealed class OptionsPanel : OptionsPanelBase
    {
        /// <summary>
        /// Performs on-demand panel setup.
        /// </summary>
        protected override void Setup()
        {
            // Add tabstrip.
            AutoTabstrip tabstrip = AutoTabstrip.AddTabstrip(this, 0f, 0f, OptionsPanelManager<OptionsPanel>.PanelWidth, OptionsPanelManager<OptionsPanel>.PanelHeight, out _, tabHeight: 50f);

            // Add tabs and panels.
            new ModOptions(tabstrip, 0);
            new CalculationOptions(tabstrip, 1);
            new SpeedOptions(tabstrip, 2);
            new DeathOptions(tabstrip, 3);
            new HealthOptions(tabstrip, 4);
            new TransportOptions(tabstrip, 5);
            new ImmigrationOptions(tabstrip, 6);

            // Event handler for tab index change; setup the selected tab.
            tabstrip.eventSelectedIndexChanged += (c, index) =>
            {
                if (index >= 0)
                {
                    if (tabstrip.tabs[index].objectUserData is OptionsPanelTab childTab)
                    {
                        childTab.Setup();
                    }
                }
            };

            // Ensure initial selected tab (doing a 'quickstep' to ensure proper events are triggered).
            tabstrip.selectedIndex = -1;
            tabstrip.selectedIndex = 0;
        }
    }
}