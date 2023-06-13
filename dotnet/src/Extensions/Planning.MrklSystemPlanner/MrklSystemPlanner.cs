﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning.MrklSystem;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.TemplateEngine;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planning;
#pragma warning restore IDE0130

/// <summary>
/// A planner that creates a plan using Mrkl systems.
/// </summary>
/// <remark>
/// An implementation of a Mrkl system as described in https://arxiv.org/pdf/2205.00445.pdf
/// </remark>
public class MrklSystemPlanner
{
    /// <summary>
    /// Initialize a new instance of the <see cref="MrklSystemPlanner"/> class.
    /// </summary>
    /// <param name="kernel">The semantic kernel instance.</param>
    /// <param name="config">Optional configuration object</param>
    /// <param name="prompt">Optional prompt override</param>
    public MrklSystemPlanner(
        IKernel kernel,
        MrklSystemPlannerConfig? config = null,
        string? prompt = null)
    {
        Verify.NotNull(kernel);
        this._kernel = kernel;

        this.Config = config ?? new();
        this.Config.ExcludedSkills.Add(RestrictedSkillName);

        var promptConfig = new PromptTemplateConfig();
        var promptTemplate = prompt ?? EmbeddedResource.Read("Skills.MrklSystemStep.skprompt.txt");
        string promptConfigString = EmbeddedResource.Read("Skills.MrklSystemStep.config.json");
        if (!string.IsNullOrEmpty(promptConfigString))
        {
            promptConfig = PromptTemplateConfig.FromJson(promptConfigString);
        }
        promptConfig.Completion.MaxTokens = this.Config.MaxTokens;

        this._systemStepFunction = this.ImportSemanticFunction(this._kernel, "MrklSystemStep", promptTemplate, promptConfig);
        this._nativeFunctions = this._kernel.ImportSkill(this, RestrictedSkillName);

        this._stepsTaken = new List<SystemStep>();

        this._context = this._kernel.CreateNewContext();
        this._logger = this._kernel.Log;
    }

    public Plan CreatePlan(string goal)
    {
        if (string.IsNullOrEmpty(goal))
        {
            throw new PlanningException(PlanningException.ErrorCodes.InvalidGoal, "The goal specified is empty");
        }

        (string functionNames, string functionDescriptions) = this.GetFunctionNamesAndDescriptions();

        Plan plan = new(goal);

        // TODO -- or do we add the number of steps from the config so it's easy to step still? Yes, I like that.
        plan.AddSteps(this._nativeFunctions["ExecutePlan"]);
        plan.State.Set("functionNames", functionNames);
        plan.State.Set("functionDescriptions", functionDescriptions);
        plan.State.Set("question", goal);
        plan.Steps[0].Outputs.Add("agentScratchPad");
        plan.Steps[0].Outputs.Add("stepCount");
        plan.Steps[0].Outputs.Add("skillCount");

        return plan;
    }

    [SKFunctionName("ExecutePlan")]
    [SKFunction("Execute a plan")]
    [SKFunctionContextParameter(Name = "functionNames", Description = "List of tool names.")]
    [SKFunctionContextParameter(Name = "functionDescriptions", Description = "List of tool descriptions.")]
    [SKFunctionContextParameter(Name = "question", Description = "The question to answer.")]
    [Obsolete]
    public async Task<SKContext> ExecutePlanAsync(SKContext context)
    {
        this._logger?.BeginScope("MrklSystemPlanner");
        if (context.Variables.Get("question", out string goal))
        {
            this._logger?.LogInformation("Goal: {Goal}", goal);
            for (int i = 0; i < this.Config.MaxIterations; i++)
            {
                var scratchPad = this.CreateScratchPad(goal);
                this._logger?.LogDebug("Scratchpad: {ScratchPad}", scratchPad);
                context.Variables.Set("agentScratchPad", scratchPad);

                var llmResponse = (await this._systemStepFunction.InvokeAsync(context).ConfigureAwait(false));

                string actionText = llmResponse.Result.Trim();
                this._logger?.LogDebug("Response : {ActionText}", actionText);

                var nextStep = this.ParseResult(actionText);
                this._stepsTaken.Add(nextStep);

                if (!string.IsNullOrEmpty(nextStep.FinalAnswer))
                {
                    this._logger?.LogInformation("Final Answer: {FinalAnswer}", nextStep.FinalAnswer);
                    context.Variables.Update(nextStep.FinalAnswer);
                    var updatedScratchPlan = this.CreateScratchPad(goal);
                    context.Variables.Set("agentScratchPad", updatedScratchPlan);
                    context.Variables.Set("stepCount", this._stepsTaken.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

                    var skillCallCount = this._stepsTaken.Where(s => !string.IsNullOrEmpty(s.Action)).Count().ToString();
                    var skillsCalled = this._stepsTaken.Where(s => !string.IsNullOrEmpty(s.Action)).Select(s => s.Action).Distinct().ToList();
                    var skillCallList = string.Join(", ", skillsCalled);
                    var skillCallListWithCounts = string.Join(", ", skillsCalled.Select(s => $"{s}({this._stepsTaken.Where(s2 => s2.Action == s).Count()})").ToList());
                    context.Variables.Set("skillCount", $"{skillCallCount} ({skillCallListWithCounts})");
                    return context;
                }

                this._logger?.LogWarning("Thought: {Thought}", nextStep.Thought);

                if (!string.IsNullOrEmpty(nextStep!.Action!))
                {
                    this._logger?.LogInformation("Action: {Action}({ActionVariables})", nextStep.Action, JsonSerializer.Serialize(nextStep.ActionVariables));
                    try
                    {
                        var result = await this.InvokeActionAsync(nextStep.Action!, nextStep!.ActionVariables!).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(result))
                        {
                            nextStep.Observation = "Got no result from action";
                        }
                        else
                        {
                            nextStep.Observation = result;
                        }

                    }
                    catch (Exception ex) when (!ex.IsCriticalException())
                    {
                        nextStep.Observation = $"Error invoking action {nextStep.Action} : {ex.Message}";
                        this._logger?.LogDebug(ex, "Error invoking action {Action}", nextStep.Action);
                    }

                    this._logger?.LogInformation("Observation: {Observation}", nextStep.Observation);
                }
                else
                {
                    this._logger?.LogInformation("Action: No action to take");
                }
            }

            context.Variables.Update($"Result not found, review _stepsTaken to see what happened.\n{JsonSerializer.Serialize(this._stepsTaken)}");
        }
        else
        {
            context.Variables.Update("Question not found.");
        }

        return context;
    }

    internal string CreateScratchPad(string goal)
    {
        if (this._stepsTaken.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        // add the original first thought
        result.Append($"[THOUGHT] {this._stepsTaken[0].Thought}\n");

        var insertPoint = result.Length;

        // Instead of most recent, we could use semantic relevance to keep important pieces and deduplicate
        for (var i = this._stepsTaken.Count - 1; i >= 0; i--)
        {
            if (result.Length / 4.0 > (this.Config.MaxTokens * 0.8))
            {
                this._logger.LogDebug("Scratchpad is too long, truncating. Skipping {CountSkipped} steps.", i + 1);
                break;
            }

            var s = this._stepsTaken[i];

            if (!string.IsNullOrEmpty(s.Observation))
            {
                result.Insert(insertPoint, $"[OBSERVATION] {s.Observation}\n");
            }

            if (!string.IsNullOrEmpty(s.Action))
            {
                result.Insert(insertPoint, $"[ACTION] {{\"action\": \"{s.Action}\",\"action_variables\": {JsonSerializer.Serialize(s.ActionVariables)}}}\n");
            }

            if (i != 0)
            {
                result.Insert(insertPoint, $"[THOUGHT] {s.Thought}\n");
            }
        }

        return result.ToString().Trim();
    }

    protected virtual async Task<string> InvokeActionAsync(string actionName, Dictionary<string, string> actionVariables)
    {
        var availableFunctions = this.GetAvailableFunctions();
        var theFunction = availableFunctions.FirstOrDefault(f => ToFullyQualifiedName(f) == actionName);
        if (theFunction == null)
        {
            throw new PlanningException(PlanningException.ErrorCodes.UnknownError, $"The function '{actionName}' was not found.");
        }

        try
        {
            var func = this._kernel.Func(theFunction.SkillName, theFunction.Name);
            var actionContext = this._kernel.CreateNewContext();
            if (actionVariables != null)
            {
                foreach (var kvp in actionVariables)
                {
                    actionContext.Variables.Set(kvp.Key, kvp.Value);
                }
            }

            var result = await func.InvokeAsync(actionContext).ConfigureAwait(false);

            if (result.ErrorOccurred)
            {
                this._logger?.LogError("Error occurred: {ErrorMessage}.", result.LastErrorDescription);
                return $"Error occurred: {result.LastErrorDescription}";
            }

            this._logger?.LogDebug("Invoked {FunctionName}. Result: {Result}", theFunction.Name, result.Result);

            // TODO Should other variables from result be included?
            return result.Result;
        }
        catch (Exception e) when (!e.IsCriticalException())
        {
            this._logger?.LogError(e, "Something went wrong in system step: {0}.{1}. Error: {2}", theFunction.SkillName, theFunction.Name, e.Message);
            return $"Something went wrong in system step: {theFunction.SkillName}.{theFunction.Name}. Error: {e.Message} {e.InnerException.Message}";
        }
    }

    private IEnumerable<FunctionView> GetAvailableFunctions()
    {
        FunctionsView functionsView = this._context.Skills!.GetFunctionsView();

        var excludedSkills = this.Config.ExcludedSkills ?? new();
        var excludedFunctions = this.Config.ExcludedFunctions ?? new();

        var availableFunctions =
            functionsView.NativeFunctions
                .Concat(functionsView.SemanticFunctions)
                .SelectMany(x => x.Value)
                .Where(s => !excludedSkills.Contains(s.SkillName) && !excludedFunctions.Contains(s.Name))
                .OrderBy(x => x.SkillName)
                .ThenBy(x => x.Name);
        return availableFunctions;
    }

    // TODO This could be simplified.
    internal virtual SystemStep ParseResult(string input)
    {
        var result = new SystemStep
        {
            OriginalResponse = input
        };

        Regex untilAction = new("(.*)(?=\\[ACTION\\])", RegexOptions.Singleline);
        Match untilActionMatch = untilAction.Match(input);

        if (input.StartsWith("[FINAL ANSWER]", StringComparison.OrdinalIgnoreCase))
        {
            result.FinalAnswer = input.Replace("[FINAL ANSWER]", string.Empty).Trim();
            return result;
        }

        // Otherwise look for "[FINAL ANSWER]" with the text after captured
        Regex finalAnswer = new("\\[FINAL ANSWER\\](.*)", RegexOptions.Singleline);
        Match finalAnswerMatch = finalAnswer.Match(input);

        if (finalAnswerMatch.Success)
        {
            result.FinalAnswer = $"{finalAnswerMatch.Groups[1].Value.Trim()}";
            return result;
        }

        if (untilActionMatch.Success)
        {
            result.Thought = untilActionMatch.Value.Trim();
        }
        else if (!input.Contains("[ACTION]"))
        {
            result.Thought = input;
        }
        else
        {
            throw new InvalidOperationException("This should never happen");
        }

        result.Thought = result.Thought.Replace("[THOUGHT]", string.Empty).Trim();

        Regex actionRegex = new("\\[ACTION\\][^{}]*({(?:[^{}]*{[^{}]*})*[^{}]*})", RegexOptions.Singleline);
        Match actionMatch = actionRegex.Match(input);

        if (actionMatch.Success)
        {
            var json = actionMatch.Groups[1].Value.Trim();

            try
            {
                var systemStepResults = JsonSerializer.Deserialize<SystemStep>(json);

                if (systemStepResults == null)
                {
                    // TODO New error code maybe?
                    throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, "The system step deserialized to a null object");
                }

                result.Action = systemStepResults.Action;
                result.ActionVariables = systemStepResults.ActionVariables;
            }
            catch (Exception)
            {
                // TODO New error code maybe?
                // throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, $"System step parsing error, invalid JSON: {json}", e);
                result.Observation = $"System step parsing error, invalid JSON: {json}";
            }
        }
        else
        {
            // TODO New error code maybe?
            // throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, $"no action found in response: {input}");

            // Actually, this just means there was only a thought, so we should just carry on.
            // result.Action = "DEBUG: No action found in response: " + input;
        }

        if (result.Action == "Final Answer")
        {
            result.FinalAnswer = JsonSerializer.Serialize(result.ActionVariables);
            this._logger?.LogError("[FINAL ANSWER] {FinalAnswer}", result.FinalAnswer); // Does this ever happen?
        }

        return result;
    }

    private (string, string) GetFunctionNamesAndDescriptions()
    {
        var availableFunctions = this.GetAvailableFunctions();

        // Mrkl doc describes these are 'expert modules' or 'experts'
        string functionNames = string.Join(", ", availableFunctions.Select(x => ToFullyQualifiedName(x)));
        string functionDescriptions = string.Join("\n", availableFunctions.Select(x => ToManualString(x)));
        return (functionNames, functionDescriptions);
    }

    private ISKFunction ImportSemanticFunction(IKernel kernel, string functionName, string promptTemplate, PromptTemplateConfig config)
    {
        var template = new PromptTemplate(promptTemplate, config, kernel.PromptTemplateEngine);
        var functionConfig = new SemanticFunctionConfig(config, template);

        return kernel.RegisterSemanticFunction(RestrictedSkillName, functionName, functionConfig);
    }

    private static string ToManualString(FunctionView function)
    {
        var inputs = string.Join("\n", function.Parameters.Select(parameter =>
        {
            var defaultValueString = string.IsNullOrEmpty(parameter.DefaultValue) ? string.Empty : $"(default='{parameter.DefaultValue}')";
            return $"  - {parameter.Name}: {parameter.Description} {defaultValueString}";
        }));

        var functionDescription = function.Description.Trim();

        if (string.IsNullOrEmpty(inputs))
        {
            return $"{ToFullyQualifiedName(function)}: {functionDescription}\n";
        }

        return $"{ToFullyQualifiedName(function)}: {functionDescription}\n{inputs}\n";
    }

    private static string ToFullyQualifiedName(FunctionView function)
    {
        return $"{function.SkillName}.{function.Name}";
    }

    private MrklSystemPlannerConfig Config { get; }

    // Context used to access the list of functions in the kernel
    private readonly SKContext _context;
    private readonly IKernel _kernel;
    private readonly ILogger _logger;

    /// <summary>
    /// Planner native functions
    /// </summary>
    private IDictionary<string, ISKFunction> _nativeFunctions = new Dictionary<string, ISKFunction>();

    /// <summary>
    /// System step function for Plan execution
    /// </summary>
    private ISKFunction _systemStepFunction;

    /// <summary>
    /// The steps taken so far
    /// </summary>
    private List<SystemStep> _stepsTaken { get; set; }

    /// <summary>
    /// The name to use when creating semantic functions that are restricted from plan creation
    /// </summary>
    private const string RestrictedSkillName = "MrklSystemPlanner_Excluded";
}
