using System;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using Windows.UI.Popups;
using StepsTracker.SensorCore;

namespace StepsTracker.StepsEngine
{
    /// <summary>
    /// Factory class for instantiating Step Engines.
    /// If a pedometer is surfaced through Windows.Devices.Sensors, the factory creates an instance of OSStepsEngine.
    /// Otherwise, the factory creates an instance of LumiaStepsEngine.
    /// </summary>
    public static class StepsEngineFactory
    {
        /// <summary>
        /// Static method to get the default steps engine present in the system.
        /// </summary>
        public static async Task<IStepsEngine> GetDefaultAsync()
        {
            IStepsEngine stepsEngine = null;

            try
            {
                // Check if there is a pedometer in the system.
                // This also checks if the user has disabled motion data from Privacy settings
                Pedometer pedometer = await Pedometer.GetDefaultAsync();

                // If there is one then create OSStepsEngine.
                if (pedometer != null)
                {
                    stepsEngine = new OSStepsEngine();
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                // If there is a pedometer but the user has disabled motion data
                // then check if the user wants to open settngs and enable motion data.
                MessageDialog dialog = new MessageDialog("Motion access has been disabled in system settings. Do you want to open settings now?", "Information");
                dialog.Commands.Add(new UICommand("Yes", async cmd => await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-motion"))));
                dialog.Commands.Add(new UICommand("No"));
                await dialog.ShowAsync();
                new System.Threading.ManualResetEvent(false).WaitOne(500);
                return null;
            }

            // No Windows.Devices.Sensors.Pedometer exists, fall back to using Lumia Sensor Core.
            if (stepsEngine == null)
            {
                // Check if all the required settings have been configured correctly
                await LumiaStepsEngine.ValidateSettingsAsync();

                stepsEngine = new LumiaStepsEngine();
            }
            return stepsEngine;
        }
    }
}