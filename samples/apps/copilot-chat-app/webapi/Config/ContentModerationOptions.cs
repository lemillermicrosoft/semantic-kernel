// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;

namespace SemanticKernel.Service.Config;

/// <summary>
/// Configuration options for content moderation.
/// </summary>
public class ContentModerationOptions
{
    public const string PropertyName = "ContentModeration";

    /// <summary>
    /// Whether to enable content moderation.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Azure Content Moderator endpoints
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Key to access the content moderation service.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Set the violation threshold. See https://github.com/Azure/Project-Carnegie-Private-Preview for details.
    /// </summary>
    public short ViolationThreshold { get; set; } = 4;
}
