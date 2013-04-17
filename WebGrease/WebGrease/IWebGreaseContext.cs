// ----------------------------------------------------------------------------------------------------
// <copyright file="IWebGreaseContext.cs" company="Microsoft Corporation">
//   Copyright Microsoft Corporation, all rights reserved.
// </copyright>
// ----------------------------------------------------------------------------------------------------
namespace WebGrease
{
    using WebGrease.Configuration;
    using WebGrease.Preprocessing;

    /// <summary>
    /// The interface for the web grease context, can be used separately to mock and/or replace the context in tests.
    /// See WebGreaseContext for more info on the functionality provides by implementations.
    /// </summary>
    public interface IWebGreaseContext
    {
        #region Public Properties

        /// <summary>Gets the configuration.</summary>
        WebGreaseConfiguration Configuration { get; }

        /// <summary>Gets the log.</summary>
        LogManager Log { get; }

        /// <summary>Gets the time measure object.</summary>
        ITimeMeasure Measure { get; }

        /// <summary>Gets the preprocessing.</summary>
        PreprocessingManager Preprocessing { get; }

        #endregion
    }
}