﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using Azure.AI.OpenAI;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

/// <summary>
/// Object containing function information and parameter values for a function call generated by the AI model.
/// </summary>
public class FunctionCallResponse
{
    /// <summary>
    /// Name of the function chosen
    /// </summary>
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the function chosen
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// Parameter values
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Parses the function call and parameter information generated by the model.
    /// </summary>
    /// <param name="functionCall">The OpenAI function call object generated by the model.</param>
    /// <returns>Instance of <see cref="FunctionCallResponse"/>.</returns>
    public static FunctionCallResponse FromFunctionCall(FunctionCall functionCall)
    {
        FunctionCallResponse response = new();
        if (functionCall.Name.Contains("-"))
        {
            var parts = functionCall.Name.Split('-');
            response.SkillName = parts[0];
            response.FunctionName = parts[1];
        }
        else
        {
            response.FunctionName = functionCall.Name;
        }

        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(functionCall.Arguments);
        if (parameters is not null)
        {
            response.Parameters = parameters;
        }

        return response;
    }
}
