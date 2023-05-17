// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Model;

/// <summary>
/// The source of a chat session
/// </summary>
public class ChatSource : IStorageEntity
{
    /// <summary>
    /// Source ID that is persistent and unique.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("chatSessionId")]
    public string ChatSessionId { get; set; }

    /// <summary>
    /// Name of the source.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// The Uri of the source.
    /// </summary>
    [JsonPropertyName("path")]
    public Uri Path { get; set; }

    /// <summary>
    /// The resource updated timestamp
    /// </summary>
    [JsonPropertyName("updatedOn")]
    public DateTimeOffset UpdatedOn { get; set; }

    /// <summary>
    /// The user ID of the user who shared the source.
    /// </summary>
    [JsonPropertyName("sharedBy")]
    public string SharedBy { get; set; }

    public ChatSource(string chatSessionId, string name, Uri path, DateTimeOffset updatedOn, string SharedBy)
    {
        this.Id = Guid.NewGuid().ToString();
        this.ChatSessionId = chatSessionId;
        this.Name = name;
        this.Path = path;
        this.UpdatedOn = updatedOn;
        this.SharedBy = SharedBy;
    }
}
