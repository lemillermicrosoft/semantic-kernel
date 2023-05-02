
using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Planning;
using RepoUtils;

namespace mytutorconsole;

public class StudySkill
{
    private IKernel studySkillKernel;
    private IDictionary<string, ISKFunction> skills;
    private IDictionary<string, ISKFunction> dws;
    private IDictionary<string, ISKFunction> chatSkill;
    private IDictionary<string, ISKFunction> nativeSkills;

    public StudySkill()
    {
        // Create a kernel
        this.studySkillKernel = new KernelBuilder()/*.WithLogger(ConsoleLogger.Log)*/.Build();
        this.studySkillKernel.Config.AddAzureTextCompletionService(
            Env.Var("AZURE_OPENAI_DEPLOYMENT_NAME"),
            Env.Var("AZURE_OPENAI_ENDPOINT"),
            Env.Var("AZURE_OPENAI_KEY"));

        this.nativeSkills = this.studySkillKernel.ImportSkill(this, "StudySkill");

        string folder = RepoFiles.SampleSkillsPath();
        this.skills = this.studySkillKernel.ImportSemanticSkillFromDirectory(folder,
            "StudySkill",
            "DoWhileSkill");

        this.chatSkill = this.studySkillKernel.ImportSkill(new ChatSkill((context) =>
    {
        var line = $"Bot: {context.Variables.Input.Trim()}";
        Console.WriteLine(line);
        context.Variables.Update(line);
        context.Variables.Get("chat_history", out var chatHistory);
        context.Variables.Set("chat_history", $"{chatHistory}\n{line}");
        Console.Write("User: ");
        return Task.FromResult<SKContext>(context);
    }, (context) =>
        {
            var line = Console.ReadLine();
            context.Variables.Update($"User: {line}");
            context.Variables.Get("chat_history", out var chatHistory);
            context.Variables.Set("chat_history", $"{chatHistory}\nUser: {line}");
            return Task.FromResult<SKContext>(context);
        }), "ChatSkill");

        this.dws = this.studySkillKernel.ImportSkill(new DoWhileSkill(skills["IsTrue"]), "DoWhileSkill");
    }

    // StudySession
    [SKFunction(description: "Study session")]
    [SKFunctionContextParameter(Name = "topic", Description = "Topic to study e.g. 'Equations and inequalities'")]
    [SKFunctionContextParameter(Name = "course", Description = "The course of study e.g. 'Algebra 1'")]
    public async Task<SKContext> StudySession(SKContext context)
    {
        //
        // Create a lesson to read
        //
        var lessonFunction = this.skills["CreateLesson"]; // input, course, TODO context
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

        var lctx = this.studySkillKernel.CreateNewContext();

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
        plan.NamedOutputs.Set("course", "");
        plan.NamedOutputs.Set("topic", "");
        plan.NamedOutputs.Set("context", "");
        // TODO What if I instead said plan.NamedOutputs.Set("chat_history", "");
        var prepareStep = new Plan(this.nativeSkills["PrepareMessage"]);
        prepareStep.NamedOutputs.Set("chat_history", "");
        var sendStep = new Plan(this.chatSkill["SendMessage"]);
        sendStep.NamedOutputs.Set("chat_history", "");

        plan.AddSteps(prepareStep, sendStep);
        doWhileContext.Set("action", plan.ToJson()); // AND THEN WAIT FOR MESSAGE BEFORE EVALUATING CONDITION
        // Instead of passing in `skills["IsTrue"] -- we can pass in a plan object!
        doWhileContext.Set("condition", "User does not say 'goodbye'"); // todo advanced condition like amount of time, etc.

        return await this.studySkillKernel.RunAsync(doWhileContext, dws["DoWhile"]);
    }

    // PrepareMessage
    [SKFunction(description: "Prepare a message")]
    [SKFunctionContextParameter(Name = "message", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "chat_history", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "topic", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "context", Description = "Message to send")]
    [SKFunctionContextParameter(Name = "course", Description = "Message to send")]
    public async Task<SKContext> PrepareMessage(SKContext context)
    {
        // If there is a message, use it. Otherwise, get the chat history and generate completion for next message.
        if (context.Variables.Get("chat_history", out var chatHistory))
        {
            // course, chat_history, topic, context
            var completion = await this.skills["ContinueLesson"].InvokeAsync(context);
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
    private readonly ISKFunction isTrueFunction;

    // Do-While
    public DoWhileSkill(ISKFunction isTrueFunction)
    {
        this.isTrueFunction = isTrueFunction;
    }

    [SKFunction(description: "Do-While")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    [SKFunctionContextParameter(Name = "action", Description = "Action to execute e.g. SomeSkill.CallFunction")]
    public async Task<SKContext> DoWhile(SKContext context)
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
            context.Log.LogError($"DoWhile: action {action} is not a valid plan: {e.Message}");
        }

        if (functionOrPlan == null)
        {
            if (action.Contains("."))
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
            context.Log.LogError($"DoWhile: action {action} not found");
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


            isTrue = await this.IsTrue(context);
        }
        while (isTrue);
        return context;
    }

    [SKFunction(description: "Is True")]
    [SKFunctionContextParameter(Name = "condition", Description = "Condition to evaluate")]
    public async Task<bool> IsTrue(SKContext context)
    {
        var state = JsonSerializer.Serialize(context.Variables);
        var result = await this.isTrueFunction.InvokeAsync(context);
        if (bool.TryParse(result.Result.ToString(), out var isTrue) || result.Result.ToString().Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return isTrue;
        }
        else
        {
            context.Log.LogError($"IsTrue: condition '{result}' not found");
            return false;
        }
    }
}


// ChatSkill -- a skill that can be used to chat with the user particularly in conjunction with a plan.
public class ChatSkill
{
    public ChatSkill(
        // callback for SendMessage events
        Func<SKContext, Task<SKContext>>? sendMessage,
        // callback for WaitForResponse events
        Func<SKContext, Task<SKContext>>? waitForResponse
    )
    {
        this.sendMessage = sendMessage;
        this.waitForResponse = waitForResponse;
    }

    // Send Message
    [SKFunction(description: "Send message")]
    [SKFunctionContextParameter(Name = "input", Description = "Message to send")]
    public async Task<SKContext> SendMessage(SKContext context)
    {
        context = await this.sendMessage(context); // this sets "chat_history" in the context

        context = await this.WaitForResponse(context); // this also sets "chat_history" in the context

        return context;
    }

    // Wait for response
    [SKFunction(description: "Wait for response")]
    public async Task<SKContext> WaitForResponse(SKContext context)
    {
        context = await this.waitForResponse(context);

        // DO SOMETHING WITH THE RESPONSE
        return context;
    }

    // Execute action
    [SKFunction(description: "Execute action")]
    public Task<SKContext> ExecuteAction(SKContext context)
    {
        return Task.FromResult<SKContext>(context);
    }

    // end conversation
    [SKFunction(description: "End conversation")]
    public Task<SKContext> EndConversation(SKContext context)
    {
        return Task.FromResult<SKContext>(context);
    }

    // get conversation history
    [SKFunction(description: "Get conversation history")]
    public Task<SKContext> GetConversationHistory(SKContext context)
    {
        return Task.FromResult<SKContext>(context);
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
