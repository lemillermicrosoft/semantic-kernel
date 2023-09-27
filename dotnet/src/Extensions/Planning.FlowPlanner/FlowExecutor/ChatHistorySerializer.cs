﻿// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0130 // Namespace does not match folder structure
// ReSharper disable once CheckNamespace
namespace Microsoft.SemanticKernel.Planning.Flow;
#pragma warning restore IDE0130 // Namespace does not match folder structure

using System;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel.AI.ChatCompletion;

internal static class ChatHistorySerializer
{
    internal static ChatHistory? Deserialize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var messages = JsonSerializer.Deserialize<SerializableChatMessage[]>(input) ?? Array.Empty<SerializableChatMessage>();
        ChatHistory history = new();
        foreach (var message in messages)
        {
            history.AddMessage(new AuthorRole(message.Role!), message.Content!);
        }

        return history;
    }

    internal static string Serialize(ChatHistory? history)
    {
        if (history == null)
        {
            return string.Empty;
        }

        var messages = history.Messages.Select(m => new SerializableChatMessage()
        {
            Role = m.Role.Label,
            Content = m.Content,
        });

        return JsonSerializer.Serialize(messages);
    }

    private class SerializableChatMessage
    {
        public string? Role { get; set; }

        public string? Content { get; set; }
    }
}
