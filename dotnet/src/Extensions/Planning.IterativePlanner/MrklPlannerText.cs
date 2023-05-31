// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Planning.IterativePlanner;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planning;
#pragma warning restore IDE0130

/// <summary>
/// A planner that uses semantic function to create a sequential plan.
/// </summary>
/// <remark>
/// Attempt to implement MRKL systems as described in https://arxiv.org/pdf/2205.00445.pdf
/// strongly inspired by https://github.com/hwchase17/langchain/tree/master/langchain/agents/mrkl
/// </remark>
public class MrklPlannerText
{
    protected readonly int MaxIterations;

    /// <summary>
    /// Initialize a new instance of the <see cref="MrklPlannerText"/> class.
    /// This planer is optimized and tested for text completion ITextCompletion.
    /// For Chat completion use the ones which Has Chat in the name
    /// </summary>
    /// <param name="kernel">The semantic kernel instance.</param>
    /// <param name="maxIterations"></param>
    /// <param name="prompt">Optional prompt override</param>
    /// <param name="embeddedResourceName"></param>
    /// <param name="logger"></param>
    public MrklPlannerText(
        IKernel kernel,
        int maxIterations = 5,
        string? prompt = null,
        string? embeddedResourceName = "iterative-planer-text.txt",
        ILogger? logger = null)
    {
        Verify.NotNull(kernel);

        // TODO Config object
        this.MaxIterations = maxIterations;

        if (!string.IsNullOrEmpty(embeddedResourceName))
        {
            this._promptTemplate = EmbeddedResource.Read(embeddedResourceName);
        }

        if (!string.IsNullOrEmpty(prompt))
        {
            this._promptTemplate = prompt;
        }

        this._context = kernel.CreateNewContext();
        this.Kernel = kernel;
        this.StepsTaken = new List<AgentStep>();
        this._logger = logger;

        this._semanticFunction = this.InitiateSemanticFunction(this.Kernel, this._promptTemplate, "Observation:");

        this._nativeFunctions = this.Kernel.ImportSkill(this, "MrklPlannerText");
    }

    private ISKFunction InitiateSemanticFunction(IKernel kernel, string promptTemplate, string stopSequence)
    {
        return kernel.CreateSemanticFunction(
            promptTemplate: promptTemplate,
            skillName: RestrictedSkillName,
            description: "Given a request or command or goal generate multi-step plan to reach the goal, " +
                         "after each step LLM is called to perform the reasoning for the nxt step",
            maxTokens: this.MaxTokens,
            temperature: 0.0,
            stopSequences: new[] { stopSequence }
        );
    }

    public int MaxTokens { get; set; } = 256;

    public Plan CreatePlan(string goal)
    {
        if (string.IsNullOrEmpty(goal))
        {
            throw new PlanningException(PlanningException.ErrorCodes.InvalidGoal, "The goal specified is empty");
        }

        (string toolNames, string toolDescriptions) = this.GetToolNamesAndDescriptions();
        var context = this.Kernel.CreateNewContext();

        Plan plan = new(goal);
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
            for (int i = 0; i < this.MaxIterations; i++)
            {
                var scratchPad = this.CreateScratchPad(goal);
                this.Trace("Scratchpad: " + scratchPad);
                context.Variables.Set("agentScratchPad", scratchPad);
                var llmResponse = await this._semanticFunction.InvokeAsync(context).ConfigureAwait(false);
                string actionText = llmResponse.Result.Trim();
                this.Trace("Response : " + actionText);

                var nextStep = this.ParseResult(actionText);
                this.StepsTaken.Add(nextStep);

                if (!string.IsNullOrEmpty(nextStep.FinalAnswer))
                {
                    context.Variables.Update(nextStep.FinalAnswer);
                    return context;
                }

                nextStep.Observation = await this.InvokeActionAsync(nextStep!.Action!, nextStep!.ActionInput!).ConfigureAwait(false);
                this.Trace("Observation : " + nextStep.Observation);
            }
            context.Variables.Update($"Result Not found, check out the StepsTaken to see what happen\n{JsonSerializer.Serialize(this.StepsTaken)}");
        }
        else
        {
            context.Variables.Update("Question Not found.");
        }

        return context;
    }

    protected virtual string CreateScratchPad(string goal)
    {
        if (this.StepsTaken.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        result.AppendLine("This was your previous work (but I haven't seen any of it! I only see what you return as final answer):");

        //in the longer conversations without this it forgets the question on gpt-3.5
        result.AppendLine("Question: " + goal);
        foreach (var step in this.StepsTaken)
        {
            result.AppendLine("Thought: " + step.OriginalResponse);
            //result.AppendLine("Action: " + step.Action);
            //result.AppendLine("Input: " + step.ActionInput);
            result.AppendLine("Observation: " + step.Observation);
        }

        return result.ToString();
    }

    protected virtual async Task<string> InvokeActionAsync(string actionName, string actionActionInput)
    {
        //var availableFunctions = await this.Context.GetAvailableFunctionsAsync(this.Config).ConfigureAwait(false);
        List<FunctionView> availableFunctions = this.GetAvailableFunctions().ToList();

        var theFunction = availableFunctions.FirstOrDefault(f => f.Name == actionName);

        if (theFunction == null)
        {
            throw new PlanningException(PlanningException.ErrorCodes.UnknownError, "The function " + actionName + " was not found");
        }

        var func = this.Kernel.Func(theFunction.SkillName, theFunction.Name);
        var result = await func.InvokeAsync(actionActionInput).ConfigureAwait(false);
        this.Trace("invoking " + theFunction.Name, result.Result);

        // TODO Should other variables from result be included?
        return result.Result;
    }

    private IEnumerable<FunctionView> GetAvailableFunctions()
    {
        FunctionsView functionsView = this._context.Skills.GetFunctionsView();

        var availableFunctions =
            functionsView.NativeFunctions
                .Concat(functionsView.SemanticFunctions)
                .SelectMany(x => x.Value)
                .Where(s => s.SkillName != RestrictedSkillName);
        return availableFunctions;
    }

    protected void Trace(string message)
    {
        //var color = Console.ForegroundColor;
        //Console.ForegroundColor = ConsoleColor.Yellow;
        //Console.WriteLine(planResultString);
        this._logger?.LogTrace("############" + Environment.NewLine + message + Environment.NewLine + "############");

        //this._logger?.Log(
        //    LogLevel.Trace,
        //    new EventId(0, $"Printing"),
        //    message);
    }

    protected void Trace(string title, string message)
    {
        //var color = Console.ForegroundColor;
        //Console.ForegroundColor = ConsoleColor.Yellow;
        //Console.WriteLine(planResultString);
        this._logger?.LogTrace("############  " + title + "  ########" + Environment.NewLine + message + Environment.NewLine + "############");

        //this._logger?.Log(
        //    LogLevel.Trace,
        //    new EventId(0, $"Printing"),
        //    message);
    }

    protected virtual AgentStep ParseResult(string input)
    {
        var result = new AgentStep
        {
            OriginalResponse = input
        };
        //until Action:
        Regex untilAction = new("(.*)(?=Action:)", RegexOptions.Singleline);
        Match untilActionMatch = untilAction.Match(input);

        if (input.StartsWith("Final Answer:"))
        {
            result.FinalAnswer = input;
            return result;
        }

        if (untilActionMatch.Success)
        {
            result.Thought = untilActionMatch.Value.Trim();
        }

        Regex actionRegex = new("```(.*?)```", RegexOptions.Singleline);

        Match actionMatch = actionRegex.Match(input);
        if (actionMatch.Success)
        {
            var json = actionMatch.Value.Trim('`').Trim();
            ActionDetails? actionDetails = JsonSerializer.Deserialize<ActionDetails>(json);
            Debug.Assert(actionDetails != null, nameof(actionDetails) + " != null");
            result.Action = actionDetails.Action;
            result.ActionInput = actionDetails.ActionInput;
        }
        else
        {
            throw new ApplicationException("no action found");
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

        string toolNames = string.Join(", ", availableFunctions.Select(x => x.Name));
        string toolDescriptions = ">" + string.Join("\n>", availableFunctions.Select(x => x.Name + ": " + x.Description));
        return (toolNames, toolDescriptions);
    }

    private readonly SKContext _context;

    private ISKFunction _semanticFunction;

    protected readonly IKernel Kernel;
    private readonly string _promptTemplate;
    private readonly ILogger? _logger;

    /// <summary>
    /// The name to use when creating semantic functions that are restricted from plan creation
    /// </summary>
    private const string RestrictedSkillName = "MrklPlanner_Excluded";

    private IDictionary<string, ISKFunction> _nativeFunctions = new Dictionary<string, ISKFunction>();

    public List<AgentStep> StepsTaken { get; set; }
}

public class ActionDetails
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("action_input")]
    public string? ActionInput { get; set; }
}
