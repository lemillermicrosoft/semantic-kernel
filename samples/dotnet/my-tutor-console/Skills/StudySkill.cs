// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using RepoUtils;

namespace Skills;

public class StudySkill
{
    private readonly IKernel _studySkillKernel;
    private readonly IDictionary<string, ISKFunction> _semanticSkills;
    private readonly IDictionary<string, ISKFunction> _doWhileSkill;
    private readonly IDictionary<string, ISKFunction> _chatSkill;
    private readonly IDictionary<string, ISKFunction> _studySkill;

    public StudySkill()
    {
        // Create a kernel
        this._studySkillKernel = new KernelBuilder() /*.WithLogger(ConsoleLogger.Log)*/.Build();
        this._studySkillKernel.Config.AddAzureChatCompletionService(
            Env.Var("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
            Env.Var("AZURE_OPENAI_CHAT_ENDPOINT"),
            Env.Var("AZURE_OPENAI_CHAT_KEY"));

        this._studySkill = this._studySkillKernel.ImportSkill(this, "StudySkill");

        string folder = RepoFiles.SampleSkillsPath();
        this._semanticSkills = this._studySkillKernel.ImportSemanticSkillFromDirectory(folder,
            "StudySkill",
            "DoWhileSkill");

        this._chatSkill = this._studySkillKernel.ImportSkill(new ChatSkill((context) =>
        {
            var line = $"Bot: {context.Variables.Input.Trim()}";
            Console.WriteLine(line);
            context.Variables.Update(line);
            context.Variables.Get("chat_history", out var chatHistory);
            context.Variables.Set("chat_history", $"{chatHistory}\n{line}");
            Console.Write("User: ");
            return Task.FromResult(context);
        }, (context) =>
        {
            var line = Console.ReadLine();
            context.Variables.Update($"User: {line}");
            context.Variables.Get("chat_history", out var chatHistory);
            context.Variables.Set("chat_history", $"{chatHistory}\nUser: {line}");
            return Task.FromResult(context);
        }), "ChatSkill");

        this._doWhileSkill = this._studySkillKernel.ImportSkill(new DoWhileSkill(this._semanticSkills["IsTrue"]), "DoWhileSkill");
    }

    // StudySession
    [SKFunction(description: "Study session")]
    [SKFunctionName("StudySession")]
    [SKFunctionContextParameter(Name = "topic", Description = "Topic to study e.g. 'Equations and inequalities'")]
    [SKFunctionContextParameter(Name = "course", Description = "The course of study e.g. 'Algebra 1'")]
    public async Task<SKContext> StudySessionAsync(SKContext context)
    {
        //
        // Create a lesson to read
        //
        var lessonFunction = this._semanticSkills["CreateLesson"]; // input, course, TODO context
        var lessonContext = context.Variables.Clone();
        if (context.Variables.Get("topic", out var topic))
        {
            lessonContext.Update(topic);
        }
        else
        {
            topic = context.Variables.Input;
            context.Variables.Set("topic", topic);
            lessonContext.Update(topic);
        }

        var lctx = this._studySkillKernel.CreateNewContext();

        foreach (KeyValuePair<string, string> x in lessonContext)
        {
            lctx.Variables[x.Key] = x.Value;
        }

        var lessonStart = await lessonFunction.InvokeAsync(lctx);

        var doWhileContext = new ContextVariables(lessonStart.Result);
        doWhileContext.Set("message", lessonStart.Result);
        doWhileContext.Set("topic", topic);
        if (context.Variables.Get("course", out var course))
        {
            doWhileContext.Set("course", course);
        }

        // The action needs to be a Plan, really. Prepare Message and then Send Message
        var plan = new Plan("Prepare a message and send it.");
        plan.Outputs.Add("course");
        plan.Outputs.Add("topic");
        plan.Outputs.Add("context");
        // TODO What if I instead said plan.Outputs.Add("chat_history");
        var prepareStep = new Plan(this._studySkill["PrepareMessage"]);
        prepareStep.Outputs.Add("chat_history");
        var sendStep = new Plan(this._chatSkill["SendMessage"]);
        sendStep.Outputs.Add("chat_history");

        plan.AddSteps(prepareStep, sendStep);
        doWhileContext.Set("action", plan.ToJson()); // AND THEN WAIT FOR MESSAGE BEFORE EVALUATING CONDITION
        // Instead of passing in `skills["IsTrue"] -- we can pass in a plan object!
        doWhileContext.Set("condition", "User does not say 'goodbye'"); // todo advanced condition like amount of time, etc.

        return await this._studySkillKernel.RunAsync(doWhileContext, this._doWhileSkill["DoWhile"]);
    }

    // PrepareMessage
    [SKFunction(description: "Prepare a message")]
    [SKFunctionName("PrepareMessage")]
    [SKFunctionContextParameter(Name = "message", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "topic", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "context", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "course", Description = "Message to send")]
    public async Task<SKContext> PrepareMessageAsync(SKContext context)
    {
        // If there is a message, use it. Otherwise, get the chat history and generate completion for next message.
        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            // course, chat_history, topic, context
            var completion = await this._semanticSkills["ContinueLesson"].InvokeAsync(context);
            context.Variables.Update(completion.Result);
        }
        else if (context.Variables.Get("message", out var message))
        {
            context.Variables.Update(message);
        }
        else
        {
            // TODO: Get the chat history and generate completion for next message.
        }

        return context;
    }
}

// Chat With User until they say Goodbye (Say hello, wait for response, respond, repeat, exit on goodbye)
// |
// +-- ChatSkill.SendMessage("Hello")
// |   |
// |   +-- ChatSkill.WaitForResponse()
// |   |   |
// |   |   +-- ChatSkill.SendMessage("How are you?")
// |   |   |   |
// |   |   |   +-- ChatSkill.WaitForResponse()
// |   |   |   |   |
// |   |   |   |   +-- ChatSkill.SendMessage("Goodbye")

public class DoWhileSkill
{
    // Planner semantic function
    private readonly ISKFunction _isTrueFunction;

    // Do-While
    public DoWhileSkill(ISKFunction isTrueFunction)
    {
        this._isTrueFunction = isTrueFunction;
    }

#pragma warning disable CA1031
    [SKFunction(description: "Do-While")]
    [SKFunctionName("DoWhile")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    [SKFunctionContextParameter(Name = "action", Description = "Action to execute e.g. SomeSkill.CallFunction")]
    public async Task<SKContext> DoWhileAsync(SKContext context)
    {
        var doWhileContext = context.Variables.Clone();
        if (!context.Variables.Get("action", out var action))
        {
            context.Log.LogError("DoWhile: action not specified");
            return context;
        }

        ISKFunction? functionOrPlan = null;
        try
        {
            functionOrPlan = Plan.FromJson(action, context);
        }
        catch (Exception e)
        {
            context.Log.LogError("DoWhile: action {0} is not a valid plan: {1}", action, e.Message);
        }

        if (functionOrPlan == null)
        {
            if (action.Contains('.', StringComparison.Ordinal))
            {
                var parts = action.Split('.');
                functionOrPlan = context.Skills!.GetFunction(parts[0], parts[1]);
            }
            else
            {
                functionOrPlan = context.Skills!.GetFunction(action);
            }
        }

        if (functionOrPlan == null)
        {
            context.Log.LogError("DoWhile: action {0} not found", action);
            return context;
        }

        bool isTrue;
        do
        {
            var contextResponse = await functionOrPlan.InvokeAsync(context); // or the action? or maybe other actions after certain conditions?

            if (functionOrPlan is Plan)
            {
                // Reload the plan so it can be executed again
                functionOrPlan = Plan.FromJson(action, context);
            }

            doWhileContext.Set("context", context.Variables.Input); // TODO

            isTrue = await this.IsTrueAsync(context);
        } while (isTrue);

        return context;
    }
#pragma warning restore CA1031

    [SKFunction(description: "Is True")]
    [SKFunctionName("IsTrue")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    public async Task<bool> IsTrueAsync(SKContext context)
    {
        var state = JsonSerializer.Serialize(context.Variables);
        var result = await this._isTrueFunction.InvokeAsync(context);
        if (bool.TryParse(result.Result.ToString(), out var isTrue) || result.Result.ToString().Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return isTrue;
        }
        else
        {
            context.Log.LogError("IsTrue: condition '{0}' not found", result);
            return false;
        }
    }
}

// ChatSkill -- a skill that can be used to chat with the user particularly in conjunction with a plan.
public class ChatSkill
{
    public ChatSkill(
        Func<SKContext, Task<SKContext>>? sendMessage, // callback for SendMessage events
        Func<SKContext, Task<SKContext>>? waitForResponse // callback for WaitForResponse events
    )
    {
        this.sendMessage = sendMessage ?? (context => Task.FromResult(context));
        this.waitForResponse = waitForResponse ?? (context => Task.FromResult(context));
    }

    // Send Message
    [SKFunction(description: "Send message")]
    [SKFunctionName("SendMessage")]
    [SKFunctionContextParameter(Name = "input", Description = "Message to send")]
    public async Task<SKContext> SendMessageAsync(SKContext context)
    {
        context = await this.sendMessage(context); // this sets "chat_history" in the context

        context = await this.WaitForResponseAsync(context); // this also sets "chat_history" in the context

        return context;
    }

    // Wait for response
    [SKFunction(description: "Wait for response")]
    [SKFunctionName("WaitForResponse")]
    public async Task<SKContext> WaitForResponseAsync(SKContext context)
    {
        context = await this.waitForResponse(context);

        // DO SOMETHING WITH THE RESPONSE
        return context;
    }

    // Execute action
    [SKFunction(description: "Execute action")]
    public Task<SKContext> ExecuteAction(SKContext context)
    {
        return Task.FromResult(context);
    }

    // end conversation
    [SKFunction(description: "End conversation")]
    public Task<SKContext> EndConversation(SKContext context)
    {
        return Task.FromResult(context);
    }

    // get conversation history
    [SKFunction(description: "Get conversation history")]
    public Task<SKContext> GetConversationHistory(SKContext context)
    {
        return Task.FromResult(context);
    }

    private Func<SKContext, Task<SKContext>> sendMessage { get; }
    private Func<SKContext, Task<SKContext>> waitForResponse { get; }
}

// AssessmentSkill
// Identify current knowledge or skills level to determine where the individual needs to improve or learn more
// Conduct self-assessment, observation, or formal testing
public class AssessmentSkill
{
    // GetAssessment
    [SKFunction(description: "Get assessment")]
    public SKContext GetAssessment(SKContext context)
    {
        return context;
    }

    // SetAssessment
    [SKFunction(description: "Set assessment")]
    public SKContext SetAssessment(SKContext context)
    {
        return context;
    }

    // GetAssessmentResults
    [SKFunction(description: "Get assessment results")]
    public SKContext GetAssessmentResults(SKContext context)
    {
        return context;
    }

    // SetAssessmentResults
    [SKFunction(description: "Set assessment results")]
    public SKContext SetAssessmentResults(SKContext context)
    {
        return context;
    }
}
