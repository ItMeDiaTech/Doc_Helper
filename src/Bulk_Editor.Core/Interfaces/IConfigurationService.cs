using System;
using Doc_Helper.Shared.Models.Configuration;

namespace Doc_Helper.Core.Interfaces;

/// <summary>
/// Service interface for managing application configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets configuration options of the specified type
    /// </summary>
    /// <typeparam name="T">The type of configuration options to retrieve</typeparam>
    /// <returns>The configuration options instance</returns>
    T GetOptions<T>() where T : class, new();

    /// <summary>
    /// Gets the complete application options
    /// </summary>
    AppOptions AppOptions { get; }

    /// <summary>
    /// Gets the API configuration options
    /// </summary>
    ApiOptions ApiOptions { get; }

    /// <summary>
    /// Gets the processing configuration options
    /// </summary>
    ProcessingOptions ProcessingOptions { get; }

    /// <summary>
    /// Gets the UI configuration options
    /// </summary>
    UiOptions UiOptions { get; }

    /// <summary>
    /// Gets the data configuration options
    /// </summary>
    DataOptions DataOptions { get; }

    /// <summary>
    /// Event fired when configuration changes are detected
    /// </summary>
    event Action<AppOptions>? ConfigurationChanged;

    /// <summary>
    /// Gets a copy of the current application options
    /// </summary>
    /// <returns>A copy of the current AppOptions</returns>
    AppOptions GetAppOptions();

    /// <summary>
    /// Updates the application options
    /// </summary>
    /// <param name="options">The updated options</param>
    void UpdateAppOptions(AppOptions options);
}