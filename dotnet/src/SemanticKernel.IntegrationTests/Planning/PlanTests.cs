// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using SemanticKernel.IntegrationTests.Fakes;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Planning;

public sealed class PlanTests : IDisposable
{
    public PlanTests(ITestOutputHelper output)
    {
        this._logger = NullLogger.Instance; //new XunitLogger<object>(output);
        this._testOutputHelper = new RedirectOutput(output);

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<PlanTests>()
            .Build();
    }

    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.")]
    public void CreatePlan(string prompt)
    {
        // Arrange

        // Act
        var plan = new Plan(prompt);

        // Assert
        Assert.Equal(prompt, plan.Description);
        Assert.Equal(prompt, plan.Name);
        Assert.Equal("Microsoft.SemanticKernel.Orchestration.Plan", plan.SkillName);
        Assert.Empty(plan.Steps);
    }

    [Theory]
    [InlineData("Write a poem and send it in an e-mail to Kai.", "SendEmailAsync", "_GLOBAL_FUNCTIONS_")]
    public async Task CreatePlanFunctionFlowAsync(string prompt, string expectedFunction, string expectedSkill)
    {
        // Arrange
        IKernel target = this.InitializeKernel();

        var planner = new Planner(Planner.Mode.FunctionFlow, target);

        // Act
        if (await planner.CreatePlanAsync(prompt) is SequentialPlan plan)
        {
            // Assert
            Assert.Contains(
                plan.Steps,
                step =>
                    step.Name.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    step.SkillName.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }
    }

    [Theory]
    [InlineData("Write a poem and send it in an e-mail to Kai.", "SendEmailAsync", "_GLOBAL_FUNCTIONS_")]
    public async Task CreatePlanGoalRelevantAsync(string prompt, string expectedFunction, string expectedSkill)
    {
        // Arrange
        IKernel target = this.InitializeKernel(true);

        var planner = new Planner(Planner.Mode.GoalRelevant, target);

        // Act
        if (await planner.CreatePlanAsync(prompt) is SequentialPlan plan)
        {
            // Assert
            Assert.Contains(
                plan.Steps,
                step =>
                    step.Name.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
                    step.SkillName.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }
    }

    [Theory]
    [InlineData("This is a story about a dog.", "kai@email.com")]
    public async Task CanExecuteRunSimpleAsync(string inputToEmail, string expectedEmail)
    {
        // Arrange
        IKernel target = this.InitializeKernel();
        var emailSkill = target.ImportSkill(new EmailSkill());
        var expectedBody = $"Sent email to: {expectedEmail}. Body: {inputToEmail}".Trim();

        var plan = new Plan(emailSkill["SendEmailAsync"]);

        // Act
        var cv = new ContextVariables();
        cv.Update(inputToEmail);
        cv.Set("email_address", expectedEmail);
        var result = await target.RunAsync(cv, plan);

        // Assert
        Assert.Equal(expectedBody, result.Result);
    }

    [Theory]
    [InlineData("Send a story to kai.", "This is a story about a dog.", "French", "kai@email.com")]
    public async Task CanExecuteRunSimpleStepsAsync(string goal, string inputToTranslate, string language, string expectedEmail)
    {
        // Arrange
        IKernel target = this.InitializeKernel();
        var emailSkill = target.ImportSkill(new EmailSkill());
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);
        var expectedBody = $"Sent email to: {expectedEmail}. Body:".Trim();

        var plan = new Plan(goal);
        plan.AddStep(writerSkill["Translate"], emailSkill["SendEmailAsync"]);

        // Act
        var cv = new ContextVariables();
        cv.Update(inputToTranslate);
        cv.Set("email_address", expectedEmail);
        cv.Set("language", language);
        var result = await target.RunAsync(cv, plan);

        // Assert
        Assert.Contains(expectedBody, result.Result, StringComparison.OrdinalIgnoreCase);
        Assert.True(expectedBody.Length < result.Result.Length);
    }

    [Fact]
    public async Task CanExecutePanWithTreeStepsAsync()
    {
        // Arrange
        IKernel target = this.InitializeKernel();
        var goal = "Write a poem or joke and send it in an e-mail to Kai.";
        var plan = new Plan(goal);
        var subPlan = new Plan("Write a poem or joke");

        var emailSkill = target.ImportSkill(new EmailSkill());

        // Arrange
        var returnContext = target.CreateNewContext();

        subPlan.AddStep(emailSkill["WritePoemAsync"], emailSkill["WritePoemAsync"], emailSkill["WritePoemAsync"]);
        plan.AddStep(subPlan, emailSkill["SendEmailAsync"]);
        plan.State.Set("email_address", "something@email.com");

        // Act
        var result = await target.RunAsync("PlanInput", plan);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(
            $"Sent email to: something@email.com. Body: Roses are red, violets are blue, Roses are red, violets are blue, Roses are red, violets are blue, PlanInput is hard, so is this test. is hard, so is this test. is hard, so is this test.",
            result.Result);
    }

    [Theory]
    [InlineData(null, "Write a poem or joke and send it in an e-mail to Kai.", null)]
    [InlineData("", "Write a poem or joke and send it in an e-mail to Kai.", "")]
    [InlineData("Hello World!", "Write a poem or joke and send it in an e-mail to Kai.", "some_email@email.com")]
    public async Task CanExecuteRunPlanSimpleManualStateAsync(string input, string goal, string email)
    {
        // Arrange
        IKernel target = this.InitializeKernel();

        // Create the input mapping from parent (plan) plan state to child plan (sendEmailPlan) state.
        var cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        var sendEmailPlan = new SequentialPlan("SendEmailAsync")
        {
            SkillName = "_GLOBAL_FUNCTIONS_",
            NamedParameters = cv
        }; // TODO A separate test where this is just a Plan() object using the function //todo I think I did this

        var plan = new SequentialPlan(goal);
        plan.Steps.Add(sendEmailPlan);
        plan.State.Set("TheEmailFromState", email); // manually prepare the state

        // Act
        var result = await target.StepAsync(input, plan);

        // Assert
        var expectedBody = input;
        Assert.Empty(plan.Steps);
        Assert.Equal(goal, plan.Description);
        Assert.Equal($"Sent email to: {email}. Body: {expectedBody}".Trim(), plan.State.ToString());
    }

    [Theory]
    [InlineData(null, "Write a poem or joke and send it in an e-mail to Kai.", null)]
    [InlineData("", "Write a poem or joke and send it in an e-mail to Kai.", "")]
    [InlineData("Hello World!", "Write a poem or joke and send it in an e-mail to Kai.", "some_email@email.com")]
    public async Task CanExecuteRunPlanManualStateAsync(string input, string goal, string email)
    {
        // Arrange
        IKernel target = this.InitializeKernel();

        var emailSkill = target.ImportSkill(new EmailSkill());

        // Create the input mapping from parent (plan) plan state to child plan (sendEmailPlan) state.
        var cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        var sendEmailPlan = new Plan(emailSkill["SendEmailAsync"])
        {
            NamedParameters = cv
        };

        var plan = new SequentialPlan(goal);
        plan.Steps.Add(sendEmailPlan);
        plan.State.Set("TheEmailFromState", email); // manually prepare the state

        // Act
        var result = await target.StepAsync(input, plan);

        // Assert
        var expectedBody = input;
        Assert.Empty(plan.Steps);
        Assert.Equal(goal, plan.Description);
        Assert.Equal($"Sent email to: {email}. Body: {expectedBody}".Trim(), plan.State.ToString());
    }

    [Theory]
    [InlineData("Summarize an input, translate to french, and e-mail to Kai", "This is a story about a dog.", "French", "Kai", "Kai@example.com")]
    public async Task CanExecuteRunPlanSimpleAsync(string goal, string inputToSummarize, string inputLanguage, string inputName, string expectedEmail)
    {
        // Arrange
        IKernel target = this.InitializeKernel();

        var expectedBody = $"Sent email to: {expectedEmail}. Body:".Trim();

        var summarizePlan = new SequentialPlan("Summarize")
        {
            SkillName = "SummarizeSkill"
        };

        var cv = new ContextVariables();
        cv.Set("language", inputLanguage);
        var translatePlan = new SequentialPlan("Translate")
        {
            SkillName = "WriterSkill",
            OutputKey = "TRANSLATED_SUMMARY",
            NamedParameters = cv
        };

        cv = new ContextVariables();
        cv.Update(inputName);
        var getEmailPlan = new SequentialPlan("GetEmailAddressAsync")
        {
            SkillName = "_GLOBAL_FUNCTIONS_",
            OutputKey = "TheEmailFromState",
            NamedParameters = cv,
        };

        cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        cv.Set("input", "$TRANSLATED_SUMMARY");
        var sendEmailPlan = new SequentialPlan("SendEmailAsync")
        {
            SkillName = "_GLOBAL_FUNCTIONS_",
            NamedParameters = cv
        };

        var plan = new SequentialPlan(goal);
        plan.Steps.Add(summarizePlan);
        plan.Steps.Add(translatePlan);
        plan.Steps.Add(getEmailPlan);
        plan.Steps.Add(sendEmailPlan);

        // Act
        var result = await target.StepAsync(inputToSummarize, plan);
        Assert.Equal(3, result.Steps.Count);
        result = await target.StepAsync(result);
        Assert.Equal(2, result.Steps.Count);
        result = await target.StepAsync(result);
        Assert.Single(result.Steps);
        result = await target.StepAsync(result);

        // Assert
        Assert.Empty(plan.Steps);
        Assert.Equal(goal, plan.Description);
        Assert.Contains(expectedBody, plan.State.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(expectedBody.Length < plan.State.ToString().Length);
    }

    [Theory]
    [InlineData("Summarize an input, translate to french, and e-mail to Kai", "This is a story about a dog.", "French", "Kai", "Kai@example.com")]
    public async Task CanExecuteRunSequentialAsync(string goal, string inputToSummarize, string inputLanguage, string inputName, string expectedEmail)
    {
        // Arrange
        IKernel target = this.InitializeKernel();

        var expectedBody = $"Sent email to: {expectedEmail}. Body:".Trim();

        var summarizePlan = new SequentialPlan("Summarize")
        {
            SkillName = "SummarizeSkill"
        };

        var cv = new ContextVariables();
        cv.Set("language", inputLanguage);
        var translatePlan = new SequentialPlan("Translate")
        {
            SkillName = "WriterSkill",
            OutputKey = "TRANSLATED_SUMMARY",
            NamedParameters = cv
        };

        cv = new ContextVariables();
        cv.Update(inputName);
        var getEmailPlan = new SequentialPlan("GetEmailAddressAsync")
        {
            SkillName = "_GLOBAL_FUNCTIONS_",
            OutputKey = "TheEmailFromState",
            NamedParameters = cv,
        };

        cv = new ContextVariables();
        cv.Set("email_address", "$TheEmailFromState");
        cv.Set("input", "$TRANSLATED_SUMMARY");
        var sendEmailPlan = new SequentialPlan("SendEmailAsync")
        {
            SkillName = "_GLOBAL_FUNCTIONS_",
            NamedParameters = cv
        };

        var plan = new SequentialPlan(goal);
        plan.Steps.Add(summarizePlan);
        plan.Steps.Add(translatePlan);
        plan.Steps.Add(getEmailPlan);
        plan.Steps.Add(sendEmailPlan);

        // Act
        var result = await target.RunAsync(inputToSummarize, plan);

        // Assert
        Assert.Contains(expectedBody, result.Result, StringComparison.OrdinalIgnoreCase);
        Assert.True(expectedBody.Length < result.Result.Length);
    }

    [Theory]
    [InlineData("Summarize an input, translate to french, and e-mail to Kai", "This is a story about a dog.", "French", "kai@email.com")]
    public async Task CanExecuteRunSequentialFunctionsAsync(string goal, string inputToSummarize, string inputLanguage, string expectedEmail)
    {
        // Arrange
        IKernel target = this.InitializeKernel();

        var summarizeSkill = TestHelpers.GetSkill("SummarizeSkill", target);
        var writerSkill = TestHelpers.GetSkill("WriterSkill", target);
        var emailSkill = target.ImportSkill(new EmailSkill());

        var expectedBody = $"Sent email to: {expectedEmail}. Body:".Trim();

        var summarizePlan = new Plan(summarizeSkill["Summarize"]);
        var translatePlan = new Plan(writerSkill["Translate"]);
        var sendEmailPlan = new Plan(emailSkill["SendEmailAsync"]);

        var plan = new SequentialPlan(goal);
        plan.Steps.Add(summarizePlan);
        plan.Steps.Add(translatePlan);
        plan.Steps.Add(sendEmailPlan);

        // Act
        var cv = new ContextVariables();
        cv.Update(inputToSummarize);
        cv.Set("email_address", expectedEmail);
        cv.Set("language", inputLanguage);
        var result = await target.RunAsync(cv, plan);

        // Assert
        Assert.Contains(expectedBody, result.Result, StringComparison.OrdinalIgnoreCase);
    }

    private IKernel InitializeKernel(bool useEmbeddings = false)
    {
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        AzureOpenAIConfiguration? azureOpenAIEmbeddingsConfiguration = this._configuration.GetSection("AzureOpenAIEmbeddings").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIEmbeddingsConfiguration);

        var builder = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                if (useEmbeddings)
                {
                    config.AddAzureOpenAIEmbeddingGenerationService(
                        serviceId: azureOpenAIEmbeddingsConfiguration.ServiceId,
                        deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
                        endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
                        apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey);
                }

                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            });

        if (useEmbeddings)
        {
            builder = builder.WithMemoryStorage(new VolatileMemoryStore());
        }

        var kernel = builder.Build();

        // Import all sample skills available for demonstration purposes.
        TestHelpers.ImportSampleSkills(kernel);

        _ = kernel.ImportSkill(new EmailSkill());
        return kernel;
    }

    private readonly ILogger _logger;
    private readonly RedirectOutput _testOutputHelper;
    private readonly IConfigurationRoot _configuration;

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PlanTests()
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
