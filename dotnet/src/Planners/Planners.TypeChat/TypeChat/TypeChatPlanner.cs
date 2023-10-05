// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planners.TypeChat;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TemplateEngine.Prompt;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planners;
#pragma warning restore IDE0130

/// <summary>
/// A planner that creates a Stepwise plan using Mrkl systems and TypeChat programs.
/// </summary>
/// <remarks>
/// An implementation of a Mrkl system as described in https://arxiv.org/pdf/2205.00445.pdf
/// </remarks>
public class TypeChatPlanner : StepwisePlanner
{
    /// <summary>
    /// Initialize a new instance of the <see cref="TypeChatPlanner"/> class.
    /// </summary>
    /// <param name="kernel">The semantic kernel instance.</param>
    /// <param name="config">Optional configuration object</param>
    public TypeChatPlanner(
        IKernel kernel,
        StepwisePlannerConfig? config = null) : base(kernel, config)
    {
    }

    protected override async Task<string> GetUserManualAsync(string question, SKContext context, CancellationToken cancellationToken)
    {
        // get validator
        var programTranslator = new PluginProgramTranslator(_kernel, "gpt-4-32k");

        var descriptions = await this._kernel.Functions.GetFunctionsTypeChatManualAsync(base.Config, question, base._logger, cancellationToken).ConfigureAwait(false);

        context.Variables.Set("functionDescriptions", descriptions);
        return await this._promptRenderer.RenderAsync(base._manualTemplate, context, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Parse LLM response into a SystemStep during execution
    /// </summary>
    /// <param name="input">The response from the LLM</param>
    /// <returns>A SystemStep</returns>
    protected override SystemStep ParseResult(string input)
    {
        var result = new SystemStep
        {
            OriginalResponse = input
        };

        // Extract final answer
        Match finalAnswerMatch = s_finalAnswerRegex.Match(input);

        if (finalAnswerMatch.Success)
        {
            result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
            return result;
        }

        // Extract thought
        Match thoughtMatch = s_thoughtRegex.Match(input);

        if (thoughtMatch.Success)
        {
            // if it contains Action, it was only an action
            if (!thoughtMatch.Value.Contains(Action))
            {
                result.Thought = thoughtMatch.Value.Trim();
            }
        }
        else if (!input.Contains(Action))
        {
            result.Thought = input;
        }
        else
        {
            return result;
        }

        result.Thought = result.Thought.Replace(Thought, string.Empty).Trim();

        // Extract action
        // Using regex is prone to issues with complex action json, so we use a simple string search instead
        // This can be less fault tolerant in some scenarios where the LLM tries to call multiple actions, for example.
        // TODO -- that could possibly be improved if we allow an action to be a list of actions.
        int actionIndex = input.IndexOf(Action, StringComparison.OrdinalIgnoreCase);

        if (actionIndex != -1)
        {
            // TODO check for ``` instead maybe
            int jsonStartIndex = input.IndexOf("{", actionIndex, StringComparison.OrdinalIgnoreCase);
            if (jsonStartIndex != -1)
            {
                int jsonEndIndex = input.Substring(jsonStartIndex).LastIndexOf("}", StringComparison.OrdinalIgnoreCase);
                if (jsonEndIndex != -1)
                {
                    string json = input.Substring(jsonStartIndex, jsonEndIndex + 1);

                    try
                    {
                        var systemStepResults = JsonSerializer.Deserialize<SystemStep>(json);

                        if (systemStepResults is not null)
                        {
                            result.Action = systemStepResults.Action;
                            result.ActionVariables = systemStepResults.ActionVariables;
                        }
                    }
                    catch (JsonException je)
                    {
                        result.Observation = $"Action parsing error: {je.Message}\nInvalid action: {json}";
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The Action tag
    /// </summary>
    private const string Action = "[ACTION]";

    /// <summary>
    /// The Thought tag
    /// </summary>
    private const string Thought = "[THOUGHT]";

    /// <summary>
    /// The regex for parsing the thought response
    /// </summary>
    private static readonly Regex s_thoughtRegex = new(@"(\[THOUGHT\])?(?<thought>.+?)(?=\[ACTION\]|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// The regex for parsing the final answer response
    /// </summary>
    private static readonly Regex s_finalAnswerRegex = new(@"\[FINAL[_\s\-]?ANSWER\](?<final_answer>.+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
}
