// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning.MrklSystem;
using Microsoft.SemanticKernel.SkillDefinition;

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

        string promptTemplate = prompt ?? EmbeddedResource.Read("skprompt.txt");
        this._systemStepFunction = this.ImportSemanticFunction(this._kernel, promptTemplate, "Observation:");
        this._nativeFunctions = this._kernel.ImportSkill(this, RestrictedSkillName);

        this._stepsTaken = new List<SystemStep>();

        this._context = this._kernel.CreateNewContext();
        this._logger = this._kernel.Log;
    }

    private ISKFunction ImportSemanticFunction(IKernel kernel, string promptTemplate, string stopSequence)
    {
        return kernel.CreateSemanticFunction(
            promptTemplate: promptTemplate,
            skillName: RestrictedSkillName,
            functionName: "MrklSystemStep",
            description: "Given a request or command or goal generate multi-step plan to reach the goal, " +
                         "after each step LLM is called to perform the reasoning for the next step",
            maxTokens: this.MaxTokens,
            temperature: 0.0,
            stopSequences: new[] { stopSequence }
        );
    }

    public int MaxTokens { get; set; } = 256; // todo configuration object

    public Plan CreatePlan(string goal)
    {
        if (string.IsNullOrEmpty(goal))
        {
            throw new PlanningException(PlanningException.ErrorCodes.InvalidGoal, "The goal specified is empty");
        }

        (string toolNames, string toolDescriptions) = this.GetToolNamesAndDescriptions();
        var context = this._kernel.CreateNewContext();

        Plan plan = new(goal);

        // TODO -- or do we add the number of steps from the config so it's easy to step still? Yes, I like that.
        plan.AddSteps(this._nativeFunctions["ExecutePlan"]);
        plan.State.Set("toolNames", toolNames);
        plan.State.Set("toolDescriptions", toolDescriptions);
        plan.State.Set("question", goal);

        return plan;
    }

    [SKFunctionName("ExecutePlan")]
    [SKFunction("Execute a plan")]
    [SKFunctionContextParameter(Name = "toolNames", Description = "List of tool names.")]
    [SKFunctionContextParameter(Name = "toolDescriptions", Description = "List of tool descriptions.")]
    [SKFunctionContextParameter(Name = "question", Description = "The question to answer.")]
    public async Task<SKContext> ExecutePlanAsync(SKContext context)
    {
        if (context.Variables.Get("question", out var goal))
        {
            for (int i = 0; i < this.Config.MaxIterations; i++)
            {
                var scratchPad = this.CreateScratchPad(goal);
                this._logger?.LogTrace("Scratchpad: {ScratchPad}", scratchPad);
                context.Variables.Set("agentScratchPad", scratchPad);
                var llmResponse = await this._systemStepFunction.InvokeAsync(context).ConfigureAwait(false);
                string actionText = llmResponse.Result.Trim();
                this._logger?.LogTrace("Response : {ActionText}", actionText);

                var nextStep = this.ParseResult(actionText);
                this._stepsTaken.Add(nextStep);

                if (!string.IsNullOrEmpty(nextStep.FinalAnswer))
                {
                    context.Variables.Update(nextStep.FinalAnswer);
                    return context;
                }

                if (!string.IsNullOrEmpty(nextStep!.Action!))
                {
                    nextStep.Observation = await this.InvokeActionAsync(nextStep.Action!, nextStep!.ActionInput!).ConfigureAwait(false);
                    this._logger?.LogTrace("Observation : {Observation}", nextStep.Observation);
                }
            }

            context.Variables.Update($"Result Not found, check out the _stepsTaken to see what happen\n{JsonSerializer.Serialize(this._stepsTaken)}");
        }
        else
        {
            context.Variables.Update("Question Not found.");
        }

        return context;
    }

    protected virtual string CreateScratchPad(string goal)
    {
        if (this._stepsTaken.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        result.AppendLine("This was your previous work (but I haven't seen any of it! I only see what you return as final answer):");

        //in the longer conversations without this it forgets the question on gpt-3.5
        result.AppendLine("Question: " + goal);

        // TODO: What happens steps are large. How many tokens should we limit this to?
        foreach (var step in this._stepsTaken)
        {
            result.AppendLine("Thought: " + step.OriginalResponse);
            //result.AppendLine("Action: " + step.Action);
            //result.AppendLine("Input: " + step.ActionInput);
            result.AppendLine("Observation: " + step.Observation);
        }

        return result.ToString();
    }

    protected virtual async Task<string> InvokeActionAsync(string actionName, string actionInput)
    {
        //var availableFunctions = await this.Context.GetAvailableFunctionsAsync(this.Config).ConfigureAwait(false);
        var availableFunctions = this.GetAvailableFunctions();

        // TODO - Include SkillName in the FunctionView
        var theFunction = availableFunctions.FirstOrDefault(f => f.Name == actionName);

        if (theFunction == null)
        {
            throw new PlanningException(PlanningException.ErrorCodes.UnknownError, $"The function '{actionName}' was not found. actionInput: {actionInput}");
        }

        var func = this._kernel.Func(theFunction.SkillName, theFunction.Name);
        var result = await func.InvokeAsync(actionInput).ConfigureAwait(false);

        if (result.ErrorOccurred)
        {
            this._logger?.LogError("Error occurred: {ErrorMessage}.", result.LastErrorDescription);
            return $"Error occurred: {result.LastErrorDescription}. Result: {result.Result}";
        }

        this._logger?.LogTrace("Invoked {FunctionName}. Result: {Result}", theFunction.Name, result.Result);

        // TODO Should other variables from result be included?
        return result.Result;
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
                .Where(s => !excludedSkills.Contains(s.SkillName) && !excludedFunctions.Contains(s.Name));
        return availableFunctions;
    }

    // TODO This could be simplified.
    protected virtual SystemStep ParseResult(string input)
    {
        var result = new SystemStep
        {
            OriginalResponse = input
        };

        Regex untilAction = new("(.*)(?=Action:)", RegexOptions.Singleline);
        Match untilActionMatch = untilAction.Match(input);

        if (input.StartsWith("Final Answer:", StringComparison.OrdinalIgnoreCase))
        {
            result.FinalAnswer = input;
            return result;
        }

        if (untilActionMatch.Success)
        {
            result.Thought = untilActionMatch.Value.Trim();
        }

        // input: "To answer the first part of the question, I need to search for Leo DiCaprio's\nAction: {\"Action\":\"GetAnswer\",\"ActionInput\":\"What is the answer to life, the universe and everything?\"}"
        // capture: "{\"Action\":\"GetAnswer\",\"ActionInput\":\"What is the answer to life, the universe and everything?\"}"
        // input: "To answer the first part of the question, I need to search for Leo DiCaprio's Action: ```\n{\"Action\":\"GetAnswer\",\"ActionInput\":\"What is the answer to life, the universe and everything?\"}\n```"
        // capture: "{\"Action\":\"GetAnswer\",\"ActionInput\":\"What is the answer to life, the universe and everything?\"}"
        // input: "To answer the first part of the question, I need to search for Leo DiCaprio's girlfriend on the web. To answer the second part, I need to find her current age and use a calculator to raise it to the 0.43 power.\nAction:\n{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}"
        // capture: "{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}"
        // input: "To answer the first part of the question, I need to search the web for Leo DiCaprio's girlfriend. To answer the second part, I need to find her current age and use the calculator tool to raise it to the 0.43 power.\nAction:\n```\n{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}\n```"
        // capture: "{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}"
        // Capture everything After first 'Action:' and in between optional ``` and ``` that come after that 'Action:', with whitespace/newlines allowed.
        // There also may be text before the first 'Action:' that we want to capture.
        Regex actionRegex = new("Action:(.*?)(?:```)(.*?)?(.*?)(?:```)?$", RegexOptions.Singleline);

        Match actionMatch = actionRegex.Match(input);
        if (actionMatch.Success)
        {
            var json = actionMatch.Groups[3].Value.Trim();
            try
            {
                var systemStepResults = JsonSerializer.Deserialize<SystemStep>(json);

                if (systemStepResults == null)
                {
                    // TODO New error code maybe?
                    throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, "The system step deserialized to a null object");
                }

                result.Action = systemStepResults.Action;
                result.ActionInput = systemStepResults.ActionInput;
            }
            catch (Exception e)
            {
                // TODO New error code maybe?
                throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, $"System step parsing error, invalid JSON: {json}", e);
            }
        }
        else
        {
            // TODO New error code maybe?
            // throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, $"no action found in response: {input}");

            // Actually, this just means there was only a thought, so we should just carry on.
        }

        if (result.Action == "Final Answer")
        {
            result.FinalAnswer = result.ActionInput;
        }

        return result;
    }

    protected (string, string) GetToolNamesAndDescriptions()
    {
        var availableFunctions = this.GetAvailableFunctions();

        // Mrkl doc describes these are 'expert modules' or 'experts'
        string toolNames = string.Join(", ", availableFunctions.Select(x => x.Name));
        string toolDescriptions = ">" + string.Join("\n>", availableFunctions.Select(x => x.Name + ": " + x.Description));
        return (toolNames, toolDescriptions);
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
