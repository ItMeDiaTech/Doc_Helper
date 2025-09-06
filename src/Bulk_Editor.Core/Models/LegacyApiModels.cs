using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Doc_Helper.Core.Models
{
    /// <summary>
    /// Legacy API response structure for backward compatibility with the Legacy version
    /// Based on WordDocumentProcessor.ApiResponse from Legacy version
    /// </summary>
    public class LegacyApiResponse
    {
        public string StatusCode { get; set; } = string.Empty;
        public LegacyApiHeaders Headers { get; set; } = new();
        public LegacyApiBody Body { get; set; } = new();

        // Legacy properties for backward compatibility
        public string Version => Body?.Version ?? string.Empty;
        public string Changes => Body?.Changes ?? string.Empty;
        public List<LegacyApiResult> Results => Body?.Results ?? new();
    }

    /// <summary>
    /// Legacy API response headers
    /// </summary>
    public class LegacyApiHeaders
    {
        public string ContentType { get; set; } = "application/json";

        // Map property name for JSON deserialization
        [JsonPropertyName("Content-Type")]
        public string ContentTypeJson
        {
            get => ContentType;
            set => ContentType = value;
        }
    }

    /// <summary>
    /// Legacy API response body containing the actual data
    /// </summary>
    public class LegacyApiBody
    {
        public List<LegacyApiResult> Results { get; set; } = new();
        public string Version { get; set; } = string.Empty;
        public string Changes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual Legacy API result item - matches the Legacy version structure
    /// </summary>
    public class LegacyApiResult
    {
        public string Document_ID { get; set; } = string.Empty;
        public string Content_ID { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Legacy API request payload
    /// </summary>
    public class LegacyApiRequest
    {
        [JsonPropertyName("Lookup_ID")]
        public List<string> Lookup_ID { get; set; } = new();
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}