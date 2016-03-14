/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Microsoft
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using StepsTracker.Models;
using StepsTracker.StepsEngine;

namespace StepsTracker.Pedometer
{
    /// <summary>
    /// Steps engine that wraps the Windows.Devices.Sensors.Pedometer APIs
    /// </summary>
    public class OSStepsEngine : IStepsEngine
    {
        /// <summary>
        /// Activates the step counter when app goes to foreground
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public async Task ActivateAsync()
        {
            // This is where you can subscribe to Pedometer ReadingChanged events if needed.
            // Do nothing here because we are not using events.
            var pedometer = await Windows.Devices.Sensors.Pedometer.GetDefaultAsync();
            pedometer.ReadingChanged += ReadingChanged;
        }

        private void ReadingChanged(Windows.Devices.Sensors.Pedometer sender, PedometerReadingChangedEventArgs args)
        {            
            OnMoving(StepCountData.FromPedometerReadings(new List<PedometerReading> {args.Reading}));
        }

        /// <summary>
        /// Deactivates the step counter when app goes to background
        /// </summary>
        /// <returns>Asynchronous task</returns>
        public Task DeactivateAsync()
        {
            // This is where you can unsubscribe from Pedometer ReadingChanged events if needed.
            // Do nothing here because we are not using events.
            return Task.FromResult(false);
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
            uint numIntervals = (((24 * 60) / resolution) + 1);
            if (day.Date.Equals(DateTime.Today))
            {
                numIntervals = (uint)((DateTime.Now - DateTime.Today).TotalMinutes / resolution) + 1;
            }
 
            uint totalSteps = 0;
            for (uint i = 0; i < numIntervals; i++)
            {
                TimeSpan ts = TimeSpan.FromMinutes(i * resolution);
                DateTime startTime = day.Date + ts;
                if (startTime < DateTime.Now)
                {
                    // Get history from startTime to the resolution duration
                    
                    var readings = await Windows.Devices.Sensors.Pedometer.GetSystemHistoryAsync(startTime, TimeSpan.FromMinutes(resolution));

                    // Compute the deltas
                    var stepsDelta = StepCountData.FromPedometerReadings(readings);

                    // Add to the total count
                    totalSteps += stepsDelta.TotalCount;
                    steps.Add(new KeyValuePair<TimeSpan, uint>(ts, totalSteps));
                }
                else
                {
                    break;
                }
            }
            return steps;
        }

        public event EventHandler<StepCountData> Moving;

        /// <summary>
        /// Returns step count for given day
        /// </summary>
        /// <returns>Step count for given day</returns>
        public async Task<StepCountData> GetTotalStepCountAsync(DateTime day)
        {
            // Get history from 1 day
            var readings = await Windows.Devices.Sensors.Pedometer.GetSystemHistoryAsync(day.Date, TimeSpan.FromDays(1));

            return StepCountData.FromPedometerReadings(readings);
        }

        public async Task<List<ReadingByDate>> GetStepsForHourAsync(DateTime date, int days)
        {
            List<ReadingByDate> stepsByDay = new List<ReadingByDate>();
            for (int i = 0; i < days; i++)
            {
                var dateTime = DateTime.Now.Subtract(TimeSpan.FromDays(i));
                var stepsForDay = await Windows.Devices.Sensors.Pedometer.GetSystemHistoryAsync(dateTime, new TimeSpan(date.Hour, date.Minute, date.Second));
                var data = StepCountData.FromPedometerReadings(stepsForDay);
                stepsByDay.Add(new ReadingByDate
                {
                    DateTime = dateTime.AddHours(date.Hour).AddSeconds(date.Second),
                    RunningStepsCount = data.RunningCount,
                    WalkingStepsCount = data.WalkingCount,
                    TotalStepsCount = data.TotalCount
                });
            }
            return stepsByDay;
        }

        protected virtual void OnMoving(StepCountData e)
        {
            Moving?.Invoke(this, e);
        }
    }
}