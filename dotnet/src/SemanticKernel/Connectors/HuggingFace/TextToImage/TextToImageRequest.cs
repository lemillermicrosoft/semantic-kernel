// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.HuggingFace.TextToImage;

/// <summary>
/// HTTP schema to perform completion request.
/// </summary>
[Serializable]
internal class TextToImageRequest
{
    /// <summary>
    /// Prompt to complete.
    /// </summary>
    [JsonPropertyName("inputs")]
    public string Input { get; set; } = string.Empty;
}
