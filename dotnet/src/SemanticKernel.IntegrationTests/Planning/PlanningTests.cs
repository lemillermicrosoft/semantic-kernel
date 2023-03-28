// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Planning.Models;
using Microsoft.SemanticKernel.Planning.Planners;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Planning;

public sealed class PlanningTests : IDisposable
{
    public PlanningTests(ITestOutputHelper output)
    {
        this._logger = new XunitLogger<object>(output);
        this._testOutputHelper = new RedirectOutput(output);

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<PlanningTests>()
            .Build();
    }

    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.", "SendEmailAsync", "_GLOBAL_FUNCTIONS_")]
    public async Task CreatePlanDefaultAsync(string prompt, string expectedFunction, string expectedSkill)
    {
        // Arrange
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAICompletionBackend(
                    label: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        // TestHelpers.ImportSampleSkills(target);
        // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

        var emailSkill = target.ImportSkill(new EmailSkill());

        var planner = new Planner(target);

        // Act
        if (await planner.CreatePlanAsync(prompt) is SimplePlan plan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            Assert.Contains(
                plan.Steps,
                step =>
                    step.SelectedFunction.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    step.SelectedSkill.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }
    }

    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.", "SendEmailAsync", "_GLOBAL_FUNCTIONS_")]
    public async Task CreatePlanFunctionFlowAsync(string prompt, string expectedFunction, string expectedSkill)
    {
        // Arrange
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        AzureOpenAIConfiguration? azureOpenAIEmbeddingsConfiguration = this._configuration.GetSection("AzureOpenAIEmbeddings").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIEmbeddingsConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAICompletionBackend(
                    label: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                config.AddAzureOpenAIEmbeddingsBackend(
                    label: azureOpenAIEmbeddingsConfiguration.Label,
                    deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
                    endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
                    apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey);

                config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
            })
            .WithMemoryStorage(new VolatileMemoryStore())
            .Build();

        // Import all sample skills available for demonstration purposes.
        // TestHelpers.ImportSampleSkills(target);
        var chatSkill = TestHelpers.GetSkill("ChatSkill", target);
        var summarizeSkill = TestHelpers.GetSkill("SummarizeSkill", target);
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);
        var calendarSkill = TestHelpers.GetSkill("CalendarSkill", target);
        var childrensBookSkill = TestHelpers.GetSkill("ChildrensBookSkill", target);
        var classificationSkill = TestHelpers.GetSkill("ClassificationSkill", target);

        // TODO This is still unreliable in creating a valid plan xml -- what's going on?


        var emailSkill = target.ImportSkill(new EmailSkill());

        var planner = new Planner(target, Planner.Mode.GoalRelevant);

        // Act
        if (await planner.CreatePlanAsync(prompt) is SimplePlan plan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            Assert.Contains(
                plan.Steps,
                step =>
                    step.SelectedFunction.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    step.SelectedSkill.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }
    }

    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.")]
    public async Task CreatePlanSimpleAsync(string prompt)
    {
        // Arrange
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAICompletionBackend(
                    label: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        // TestHelpers.ImportSampleSkills(target);
        // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

        var emailSkill = target.ImportSkill(new EmailSkill());

        var planner = new Planner(target, Planner.Mode.Simple);

        // Act
        if (await planner.CreatePlanAsync(prompt) is BasePlan plan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            Assert.Equal(prompt, plan.Goal);
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }
    }

    [Theory]
    [InlineData(null, "Write a poem or joke and send it in an e-mail to Kai.", null)]
    [InlineData("", "Write a poem or joke and send it in an e-mail to Kai.", "")]
    [InlineData("Hello World!", "Write a poem or joke and send it in an e-mail to Kai.", "some_email@email.com")]
    public async Task CanExecuteRunPlanSimpleAsync(string input, string goal, string email)
    {
        // Arrange
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAICompletionBackend(
                    label: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        TestHelpers.ImportSampleSkills(target);
        var emailSkill = target.ImportSkill(new EmailSkill());

        var cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        var plan = new SimplePlan() { Goal = goal };
        plan.Steps.Add(new PlanStep()
        {
            SelectedFunction = "SendEmailAsync",
            SelectedSkill = "_GLOBAL_FUNCTIONS_",
            NamedParameters = cv
        });
        plan.State.Set("TheEmailFromState", email);

        // Act
        await target.RunAsync(input, plan);

        // BUG FOUND -- The parameters of the Step are not populated properly

        // Assert
        // Assert.Empty(plan.LastErrorDescription);
        // Assert.False(plan.ErrorOccurred);
        var expectedBody = string.IsNullOrEmpty(input) ? goal : input;
        Assert.Equal(0, plan.Steps.Count);
        Assert.Equal(goal, plan.Goal);
        Assert.Equal($"Sent email to: {email}. Body: {expectedBody}", plan.State.ToString()); // TODO Make a Result and other properties
    }

    private readonly XunitLogger<object> _logger;
    private readonly RedirectOutput _testOutputHelper;
    private readonly IConfigurationRoot _configuration;

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PlanningTests()
    {
        this.Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._logger.Dispose();
            this._testOutputHelper.Dispose();
        }
    }

    internal class EmailSkill
    {
        [SKFunction("Given an e-mail and message body, send an email")]
        [SKFunctionInput(Description = "The body of the email message to send.")]
        [SKFunctionContextParameter(Name = "email_address", Description = "The email address to send email to.", DefaultValue = "default@email.com")]
        public Task<SKContext> SendEmailAsync(string input, SKContext context)
        {
            context.Variables.Get("email_address", out string emailAddress);
            context.Variables.Update($"Sent email to: {emailAddress}. Body: {input}");
            return Task.FromResult(context);
        }

        [SKFunction("Given a name, find email address")]
        [SKFunctionInput(Description = "The name of the person to email.")]
        public Task<SKContext> GetEmailAddressAsync(string input, SKContext context)
        {
            context.Log.LogDebug("Returning hard coded email for {0}", input);
            context.Variables.Update("johndoe1234@example.com");
            return Task.FromResult(context);
        }

        [SKFunction("Write a short poem for an e-mail")]
        [SKFunctionInput(Description = "The topic of the poem.")]
        public Task<SKContext> WritePoemAsync(string input, SKContext context)
        {
            context.Variables.Update($"Roses are red, violets are blue, {input} is hard, so is this test.");
            return Task.FromResult(context);
        }
    }
}
