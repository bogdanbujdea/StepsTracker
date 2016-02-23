using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StepsTracker.Models;

namespace StepsTracker.StepsEngine
{
    /// <summary>
    /// Platform agnostic Steps Engine interface
    /// This interface is implementd by OSStepsEngine and LumiaStepsEngine.
    /// </summary>
    public interface IStepsEngine
    {
        /// <summary>
        /// Activates the step counter
        /// </summary>
        /// <returns>Asynchronous task</returns>
        Task ActivateAsync();

        /// <summary>
        /// Deactivates the step counter
        /// </summary>
        /// <returns>Asynchronous task</returns>
        Task DeactivateAsync();

        /// <summary>
        /// Returns steps for given day at given resolution
        /// </summary>
        /// <param name="day">Day to fetch data for</param>
        /// <param name="resolution">Resolution in minutes. Minimum resolution is five minutes.</param>
        /// <returns>List of steps counts for the given day at given resolution.</returns>
        Task<List<KeyValuePair<TimeSpan, uint>>> GetStepsCountsForDay(DateTime day, uint resolution);

        /// <summary>
        /// Returns step count for given day
        /// </summary>
        /// <returns>Step count for given day</returns>
        Task<StepCountData> GetTotalStepCountAsync(DateTime day);

        Task<List<ReadingByDate>> GetStepsForHour(DateTime date, int days);
    }
}