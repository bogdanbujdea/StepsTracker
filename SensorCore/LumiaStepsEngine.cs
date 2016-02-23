using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Lumia.Sense;
using StepsTracker.Models;
using StepsTracker.StepsEngine;

namespace StepsTracker.SensorCore
{
    /// <summary>
    /// Steps engine that wraps the Lumia SensorCore StepCounter APIs
    /// </summary>
    public class LumiaStepsEngine : IStepsEngine
    {
        #region Private members
        /// <summary>
        /// Step counter instance
        /// </summary>
        private IStepCounter _stepCounter;

        /// <summary>
        /// Is step counter currently active?
        /// </summary>
        private bool _sensorActive = false;

        /// <summary>
        /// Constructs a new ResourceLoader object.
        /// </summary>
        protected static readonly ResourceLoader _resourceLoader = ResourceLoader.GetForCurrentView("Resources");
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public LumiaStepsEngine()
        {
        }

        /// <summary>
        /// Makes sure necessary settings are enabled in order to use SensorCore
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public static async Task ValidateSettingsAsync()
        {
            if (await StepCounter.IsSupportedAsync())
            {
                // Starting from version 2 of Motion data settings Step counter and Acitivity monitor are always available. In earlier versions system
                // location setting and Motion data had to be enabled.
                MotionDataSettings settings = await SenseHelper.GetSettingsAsync();
                if (settings.Version < 2)
                {
                    if (!settings.LocationEnabled)
                    {
                        MessageDialog dlg = new MessageDialog("In order to count steps you need to enable location in system settings. Do you want to open settings now? If not, application will exit.", "Information");
                        dlg.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (cmd) => await SenseHelper.LaunchLocationSettingsAsync())));
                        dlg.Commands.Add(new UICommand("No", new UICommandInvokedHandler((cmd) => { Application.Current.Exit(); })));
                        await dlg.ShowAsync();
                    }
                    if (!settings.PlacesVisited)
                    {
                        MessageDialog dlg = new MessageDialog("In order to count steps you need to enable Motion data in Motion data settings. Do you want to open settings now? If not, application will exit.", "Information");
                        dlg.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (cmd) => await SenseHelper.LaunchSenseSettingsAsync())));
                        dlg.Commands.Add(new UICommand("No", new UICommandInvokedHandler((cmd) => { Application.Current.Exit(); })));
                        await dlg.ShowAsync();
                    }
                }
            }
        }

        /// <summary>
        /// SensorCore needs to be deactivated when app goes to background
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public async Task DeactivateAsync()
        {
            _sensorActive = false;
            if (_stepCounter != null) await _stepCounter.DeactivateAsync();
        }

        /// <summary>
        /// SensorCore needs to be activated when app comes back to foreground
        /// </summary>
        public async Task ActivateAsync()
        {
            if (_sensorActive) return;
            if (_stepCounter != null)
            {
                await _stepCounter.ActivateAsync();
            }
            else
            {
                await InitializeAsync();
            }
            _sensorActive = true;
        }

        /// <summary>
        /// Returns steps for given day at given resolution
        /// </summary>
        /// <param name="day">Day to fetch data for</param>
        /// <param name="resolution">Resolution in minutes. Minimum resolution is five minutes.</param>
        /// <returns>List of steps counts for the given day at given resolution.</returns>
        public async Task<List<KeyValuePair<TimeSpan, uint>>> GetStepsCountsForDay(DateTime day, uint resolution)
        {
            List<KeyValuePair<TimeSpan, uint>> steps = new List<KeyValuePair<TimeSpan, uint>>();
            uint totalSteps = 0;
            uint numIntervals = (((24 * 60) / resolution) + 1);
            if (day.Date.Equals(DateTime.Today))
            {
                numIntervals = (uint)((DateTime.Now - DateTime.Today).TotalMinutes / resolution) + 1;
            }
            for (int i = 0; i < numIntervals; i++)
            {
                TimeSpan ts = TimeSpan.FromMinutes(i * resolution);
                DateTime startTime = day.Date + ts;
                if (startTime < DateTime.Now)
                {
                    try
                    {
                        var stepCount = await _stepCounter.GetStepCountForRangeAsync(startTime, TimeSpan.FromMinutes(resolution));
                        if (stepCount != null)
                        {
                            totalSteps += (stepCount.WalkingStepCount + stepCount.RunningStepCount);
                            steps.Add(new KeyValuePair<TimeSpan, uint>(ts, totalSteps));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    break;
                }
            }
            return steps;
        }

        /// <summary>
        /// Returns step count for given day
        /// </summary>
        /// <returns>Step count for given day</returns>
        public async Task<StepCountData> GetTotalStepCountAsync(DateTime day)
        {
            if (_stepCounter != null && _sensorActive)
            {
                StepCount steps = await _stepCounter.GetStepCountForRangeAsync(day.Date, TimeSpan.FromDays(1));
                return StepCountData.FromLumiaStepCount(steps);
            }
            return null;
        }

        public async Task<List<ReadingByDate>> GetStepsForHour(int hour, int days)
        {
            List<ReadingByDate> stepsByDay = new List<ReadingByDate>();
            for (int i = 0; i < days; i++)
            {
                try
                {
                    var dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).Subtract(TimeSpan.FromDays(i));
                    Debug.WriteLine("hour: " + i);
                    Debug.WriteLine("date: " + dateTime);
                    
                    if (hour == 0) hour = 1;
                    var stepsForDay = await _stepCounter.GetStepCountForRangeAsync(dateTime, TimeSpan.FromHours(hour));
                    var data = StepCountData.FromLumiaStepCount(stepsForDay);
                    stepsByDay.Add(new ReadingByDate
                    {
                        DateTime = dateTime.AddHours(hour),
                        RunningStepsCount = data.RunningCount,
                        WalkingStepsCount = data.WalkingCount,
                        TotalStepsCount = data.TotalCount
                    });
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception.Message);
                }
            }
            return stepsByDay;
        }

        /// <summary>
        /// Initializes simulator if example runs on emulator otherwise initializes StepCounter
        /// </summary>
        private async Task InitializeAsync()
        {
            // Using this method to detect if the application runs in the emulator or on a real device. Later the *Simulator API is used to read fake sense data on emulator. 
            // In production code you do not need this and in fact you should ensure that you do not include the Lumia.Sense.Testing reference in your project.
            EasClientDeviceInformation x = new EasClientDeviceInformation();
            if (x.SystemProductName.StartsWith("Virtual"))
            {
                //await InitializeSimulatorAsync();
            }
            else
            {
                await InitializeSensorAsync();
            }
        }

        /// <summary>
        /// Initializes the step counter
        /// </summary>
        private async Task InitializeSensorAsync()
        {
            if (_stepCounter == null)
            {
                await CallSensorCoreApiAsync(async () => { _stepCounter = await StepCounter.GetDefaultAsync(); });
            }
            else
            {
                await _stepCounter.ActivateAsync();
            }
            _sensorActive = true;
        }

        /// <summary>
        /// Initializes StepCounterSimulator (requires Lumia.Sense.Testing)
        /// </summary>
        //public async Task InitializeSimulatorAsync()
        //{
        //    var obj = await SenseRecording.LoadFromFileAsync("Simulations\\short recording.txt");
        //    if (!await CallSensorCoreApiAsync(async () => { _stepCounter = await StepCounterSimulator.GetDefaultAsync(obj, DateTime.Now - TimeSpan.FromHours(12)); }))
        //    {
        //        Application.Current.Exit();
        //    }
        //    _sensorActive = true;
        //}

        /// <summary>
        /// Performs asynchronous Sensorcore SDK operation and handles any exceptions
        /// </summary>
        /// <param name="action">Action for which the SensorCore will be activated.</param>
        /// <returns><c>true</c> if call was successful, <c>false</c> otherwise</returns>
        private async Task<bool> CallSensorCoreApiAsync(Func<Task> action)
        {
            Exception failure = null;
            try
            {
                await action();
            }
            catch (Exception e)
            {
                failure = e;
            }
            if (failure != null)
            {
                MessageDialog dlg = null;
                switch (SenseHelper.GetSenseError(failure.HResult))
                {
                    case SenseError.LocationDisabled:
                    {
                        dlg = new MessageDialog(_resourceLoader.GetString("FeatureDisabled/Location"), _resourceLoader.GetString("FeatureDisabled/Title"));
                        dlg.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (cmd) => await SenseHelper.LaunchLocationSettingsAsync())));
                        dlg.Commands.Add(new UICommand("No", new UICommandInvokedHandler((cmd) => { /* do nothing */ })));
                        await dlg.ShowAsync();
                        new System.Threading.ManualResetEvent(false).WaitOne(500);
                        return false;
                    }
                    case SenseError.SenseDisabled:
                    {
                        dlg = new MessageDialog(_resourceLoader.GetString("FeatureDisabled/MotionData"), _resourceLoader.GetString("FeatureDisabled/Title"));
                        dlg.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (cmd) => await SenseHelper.LaunchSenseSettingsAsync())));
                        dlg.Commands.Add(new UICommand("No", new UICommandInvokedHandler((cmd) => { /* do nothing */ })));
                        await dlg.ShowAsync();
                        return false;
                    }
                    case SenseError.SenseNotAvailable:
                    {
                        dlg = new MessageDialog(_resourceLoader.GetString("FeatureNotSupported/Message"), _resourceLoader.GetString("FeatureNotSupported/Title"));
                        await dlg.ShowAsync();
                        return false;
                    }
                    default:
                    {
                        dlg = new MessageDialog("Failure: " + SenseHelper.GetSenseError(failure.HResult), "");
                        await dlg.ShowAsync();
                        return false;
                    }
                }
            }
            else
            {
                return true;
            }
        }
    }
}