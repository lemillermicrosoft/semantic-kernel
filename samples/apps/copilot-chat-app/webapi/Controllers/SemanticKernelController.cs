﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Reliability;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.MsGraph;
using Microsoft.SemanticKernel.Skills.MsGraph.Connectors;
using Microsoft.SemanticKernel.Skills.MsGraph.Connectors.Client;
using Microsoft.SemanticKernel.Skills.OpenAPI.Authentication;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Model;
using SemanticKernel.Service.Skills;
using SemanticKernel.Service.Storage;
using Directory = System.IO.Directory;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class SemanticKernelController : ControllerBase, IDisposable
{
    private readonly ILogger<SemanticKernelController> _logger;
    private readonly PromptSettings _promptSettings;
    private readonly ServiceOptions _options;
    private readonly List<IDisposable> _disposables;

    public SemanticKernelController(
        IOptions<ServiceOptions> options,
        PromptSettings promptSettings,
        ILogger<SemanticKernelController> logger)
    {
        this._logger = logger;
        this._options = options.Value;
        this._promptSettings = promptSettings;
        this._disposables = new List<IDisposable>();
    }

    /// <summary>
    /// Invoke a Semantic Kernel function on the server.
    /// </summary>
    /// <remarks>
    /// We create and use a new kernel for each request.
    /// We feed the kernel the ask received via POST from the client
    /// and attempt to invoke the function with the given name.
    /// </remarks>
    /// <param name="kernel">Semantic kernel obtained through dependency injection</param>
    /// <param name="chatRepository">Storage repository to store chat sessions</param>
    /// <param name="chatMessageRepository">Storage repository to store chat messages</param>
    /// <param name="documentMemoryOptions">Options for document memory handling.</param>
    /// <param name="planner">Planner to use to create function sequences.</param>
    /// <param name="plannerOptions">Options for the planner.</param>
    /// <param name="ask">Prompt along with its parameters</param>
    /// <param name="openApiSkillsAuthHeaders">Authentication headers to connect to OpenAPI Skills</param>
    /// <param name="skillName">Skill in which function to invoke resides</param>
    /// <param name="functionName">Name of function to invoke</param>
    /// <returns>Results consisting of text generated by invoked function along with the variable in the SK that generated it</returns>
    [Authorize]
    [Route("skills/{skillName}/functions/{functionName}/invoke")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AskResult>> InvokeFunctionAsync(
        [FromServices] IKernel kernel,
        [FromServices] ChatSessionRepository chatRepository,
        [FromServices] ChatMessageRepository chatMessageRepository,
        [FromServices] IOptions<DocumentMemoryOptions> documentMemoryOptions,
        [FromServices] CopilotChatPlanner planner,
        [FromServices] IOptions<PlannerOptions> plannerOptions,
        [FromBody] Ask ask,
        [FromHeader] OpenApiSkillsAuthHeaders openApiSkillsAuthHeaders,
        string skillName, string functionName)
    {
        this._logger.LogDebug("Received call to invoke {SkillName}/{FunctionName}", skillName, functionName);

        if (string.IsNullOrWhiteSpace(ask.Input))
        {
            return this.BadRequest("Input is required.");
        }

        // Put ask's variables in the context we will use.
        var context = kernel.CreateNewContext();
        context.Variables.Update(ask.Input);
        foreach (var input in ask.Variables)
        {
            context.Variables.Set(input.Key, input.Value);
        }

        // Not required for Copilot Chat, but this is how to register additional skills for the service to provide.
        if (!string.IsNullOrWhiteSpace(this._options.SemanticSkillsDirectory))
        {
            kernel.RegisterSemanticSkills(this._options.SemanticSkillsDirectory, this._logger);
        }

        // Register skills with the planner if enabled.
        if (plannerOptions.Value.Enabled)
        {
            await this.RegisterPlannerSkillsAsync(planner, plannerOptions.Value, openApiSkillsAuthHeaders, context.Variables);
        }

        // Register native skills with the chat's kernel
        kernel.RegisterNativeSkills(
            chatSessionRepository: chatRepository,
            chatMessageRepository: chatMessageRepository,
            promptSettings: this._promptSettings,
            planner: planner,
            plannerOptions: plannerOptions.Value,
            documentMemoryOptions: documentMemoryOptions.Value,
            logger: this._logger);

        // Get the function to invoke
        ISKFunction? function = null;
        try
        {
            function = kernel.Skills.GetFunction(skillName, functionName);
        }
        catch (KernelException)
        {
            return this.NotFound($"Failed to find {skillName}/{functionName} on server");
        }

        // Run the function.
        SKContext result = await function.InvokeAsync(context);

        return result.ErrorOccurred
            ? result.LastException is AIException aiException && aiException.Detail is not null
                ? (ActionResult<AskResult>)this.BadRequest(string.Concat(aiException.Message, " - Detail: " + aiException.Detail))
                : (ActionResult<AskResult>)this.BadRequest(result.LastErrorDescription)
            : (global::Microsoft.AspNetCore.Mvc.ActionResult<global::SemanticKernel.Service.Model.AskResult>)this.Ok(new AskResult { Value = result.Result, Variables = result.Variables.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)) });
    }

    /// <summary>
    /// Register skills with the planner's kernel.
    /// </summary>
    private async Task RegisterPlannerSkillsAsync(CopilotChatPlanner planner, PlannerOptions options, OpenApiSkillsAuthHeaders openApiSkillsAuthHeaders, ContextVariables variables)
    {
        // Register authenticated skills with the planner's kernel only if the request includes an auth header for the skill.

        // Klarna Shopping
        if (openApiSkillsAuthHeaders.KlarnaAuthentication != null)
        {
            // Register the Klarna shopping ChatGPT plugin with the planner's kernel.
            using DefaultHttpRetryHandler retryHandler = new(new HttpRetryConfig(), this._logger)
            {
                InnerHandler = new HttpClientHandler() { CheckCertificateRevocationList = true }
            };
            using HttpClient importHttpClient = new(retryHandler, false);
            importHttpClient.DefaultRequestHeaders.Add("User-Agent", "Microsoft.CopilotChat");
            await planner.Kernel.ImportChatGptPluginSkillFromUrlAsync("KlarnaShoppingSkill", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"),
                importHttpClient);
        }

        // GitHub
        if (!string.IsNullOrWhiteSpace(openApiSkillsAuthHeaders.GithubAuthentication))
        {
            this._logger.LogInformation("Enabling GitHub skill.");
            BearerAuthenticationProvider authenticationProvider = new(() => Task.FromResult(openApiSkillsAuthHeaders.GithubAuthentication));
            await planner.Kernel.ImportOpenApiSkillFromFileAsync(
                skillName: "GitHubSkill",
                filePath: Path.Combine(Directory.GetCurrentDirectory(), @"Skills/OpenApiSkills/GitHubSkill/openapi.json"),
                authCallback: authenticationProvider.AuthenticateRequestAsync);
        }

        // Jira
        if (openApiSkillsAuthHeaders.JiraAuthentication != null)
        {
            this._logger.LogInformation("Registering Jira Skill");
            var authenticationProvider = new BasicAuthenticationProvider(() => { return Task.FromResult(openApiSkillsAuthHeaders.JiraAuthentication); });
            var hasServerUrlOverride = variables.Get("jira-server-url", out string serverUrlOverride);

            // TODO: Import Jira OpenAPI Skill
        }

        // Microsoft Graph
        if (!string.IsNullOrWhiteSpace(openApiSkillsAuthHeaders.GraphAuthentication))
        {
            this._logger.LogInformation("Enabling Microsoft Graph skill(s).");
            BearerAuthenticationProvider authenticationProvider = new(() => Task.FromResult(openApiSkillsAuthHeaders.GraphAuthentication));
            GraphServiceClient graphServiceClient = this.CreateGraphServiceClient(authenticationProvider.AuthenticateRequestAsync);

            planner.Kernel.ImportSkill(new TaskListSkill(new MicrosoftToDoConnector(graphServiceClient)), "todo");
            planner.Kernel.ImportSkill(new CalendarSkill(new OutlookCalendarConnector(graphServiceClient)), "calendar");
            planner.Kernel.ImportSkill(new EmailSkill(new OutlookMailConnector(graphServiceClient)), "email");
        }
    }

    /// <summary>
    /// Create a Microsoft Graph service client.
    /// </summary>
    /// <param name="authenticateRequestAsyncDelegate">The delegate to authenticate the request.</param>
    private GraphServiceClient CreateGraphServiceClient(AuthenticateRequestAsyncDelegate authenticateRequestAsyncDelegate)
    {
        MsGraphClientLoggingHandler graphLoggingHandler = new(this._logger);
        this._disposables.Add(graphLoggingHandler);

        IList<DelegatingHandler> graphMiddlewareHandlers =
            GraphClientFactory.CreateDefaultHandlers(new DelegateAuthenticationProvider(authenticateRequestAsyncDelegate));
        graphMiddlewareHandlers.Add(graphLoggingHandler);

        HttpClient graphHttpClient = GraphClientFactory.Create(graphMiddlewareHandlers);
        this._disposables.Add(graphHttpClient);

        GraphServiceClient graphServiceClient = new(graphHttpClient);
        return graphServiceClient;
    }

    /// <summary>
    /// Dispose of the object.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (IDisposable disposable in this._disposables)
            {
                disposable.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
