// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Configuration;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Planning.Planners;
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
                config.AddAzureOpenAITextCompletion(
                    serviceId: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.Label);
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
                plan.Steps.Children,
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
                config.AddAzureOpenAITextCompletion(
                    serviceId: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                config.AddAzureOpenAIEmbeddingGeneration(
                    serviceId: azureOpenAIEmbeddingsConfiguration.Label,
                    deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
                    endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
                    apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey);

                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.Label);
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
                plan.Steps.Children,
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
                config.AddAzureOpenAITextCompletion(
                    serviceId: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.Label);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        // TestHelpers.ImportSampleSkills(target);
        // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

        var emailSkill = target.ImportSkill(new EmailSkill());

        var planner = new Planner(target, Planner.Mode.Simple);

        // Act
        if (await planner.CreatePlanAsync(prompt) is SimplePlan plan)
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
                config.AddAzureOpenAITextCompletion(
                    serviceId: azureOpenAIConfiguration.Label,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.Label);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        TestHelpers.ImportSampleSkills(target);
        var emailSkill = target.ImportSkill(new EmailSkill());

        var cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        var plan = new SimplePlan() { Goal = goal };
        plan.Steps.Children.Add(new PlanStep()
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
        Assert.Equal(0, plan.Steps.Children.Count);
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
}
