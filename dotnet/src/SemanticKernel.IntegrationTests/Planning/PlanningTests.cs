// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
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
        this._logger = NullLogger.Instance; //new XunitLogger<object>(output);
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
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        // TestHelpers.ImportSampleSkills(target);
        // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

        var emailSkill = target.ImportSkill(new EmailSkill());

        var planner = new Planner(target);

        // Act
        if (await planner.CreatePlanAsync(prompt) is SequentialPlan plan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            Assert.Contains(
                // plan.Root.Steps,
                plan.Steps,
                step =>
                    // step.SelectedFunction.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    // step.SelectedSkill.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
                    step.Name.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    step.SkillName.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
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
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                config.AddAzureOpenAIEmbeddingGenerationService(
                    serviceId: azureOpenAIEmbeddingsConfiguration.ServiceId,
                    deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
                    endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
                    apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey);

                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
                // config.SetDefaultTextEmbeddingGenerationService(azureOpenAIEmbeddingsConfiguration.ServiceId);
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
        if (await planner.CreatePlanAsync(prompt) is SequentialPlan plan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            Assert.Contains(
                // plan.Root.Steps,
                plan.Steps,
                step =>
                    // step.SelectedFunction.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    // step.SelectedSkill.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
                    step.Name.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    step.SkillName.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
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
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        // TestHelpers.ImportSampleSkills(target);
        // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

        var emailSkill = target.ImportSkill(new EmailSkill());

        var planner = new Planner(target, Planner.Mode.Simple);

        // Act
        var act = await planner.CreatePlanAsync(prompt);
        if (act is Plan plan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            // Assert.Equal(prompt, plan.Root.Description);
            Assert.Equal(prompt, plan.Description);
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }
    }

    [Theory]
    // [InlineData(null, "Write a poem or joke and send it in an e-mail to Kai.", null)]
    // [InlineData("", "Write a poem or joke and send it in an e-mail to Kai.", "")]
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
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);
                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            })
            .Build();

        // Import all sample skills available for demonstration purposes.
        TestHelpers.ImportSampleSkills(target);
        var emailSkill = target.ImportSkill(new EmailSkill());

        var cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        // var plan = new SequentialPlan() { Root = new() { Description = goal } };
        var plan = new SequentialPlan() { Description = goal };
        // plan.Root.Steps.Add(new PlanStep()
        // {
        //     SelectedFunction = "SendEmailAsync",
        //     SelectedSkill = "_GLOBAL_FUNCTIONS_",
        //     NamedParameters = cv
        // });
        plan.Steps.Add(new SequentialPlan()
        {
            Name = "SendEmailAsync",
            SkillName = "_GLOBAL_FUNCTIONS_",
            NamedParameters = cv
        });
        plan.State.Set("TheEmailFromState", email);

        // Act
        // When Plan was IPlan, this hit extension method. Now that Plan is ISKFunction, it will hit default kernel methods
        // await target.RunAsync(input, plan);
        var result = await target.StepAsync(input, plan); // renaming Extensions to 'stepasync' to avoid confusion with 'runasync'

        // BUG FOUND -- The parameters of the Step are not populated properly

        // Assert
        // Assert.Empty(plan.LastErrorDescription);
        // Assert.False(plan.ErrorOccurred);
        var expectedBody = string.IsNullOrEmpty(input) ? goal : input;
        // Assert.Equal(0, plan.Root.Steps.Count);
        Assert.Empty(plan.Steps);
        // Assert.Equal(goal, plan.Root.Description);
        Assert.Equal(goal, plan.Description);
        Assert.Equal($"Sent email to: {email}. Body: {expectedBody}", plan.State.ToString()); // TODO Make a Result and other properties
    }

    private readonly ILogger _logger;
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
            if (this._logger is IDisposable ld)
            {
                ld.Dispose();
            }

            this._testOutputHelper.Dispose();
        }
    }
}
