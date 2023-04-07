// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning.Planners;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Planning;

public sealed class PlanningTests
{
    // Method to create Mock<ISKFunction> objects
    private static Mock<ISKFunction> CreateMockFunction(FunctionView functionView)
    {
        var mockFunction = new Mock<ISKFunction>();
        mockFunction.Setup(x => x.Describe()).Returns(functionView);
        mockFunction.Setup(x => x.Name).Returns(functionView.Name);
        mockFunction.Setup(x => x.SkillName).Returns(functionView.SkillName);
        return mockFunction;
    }

    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.")]
    public async Task ItCanCreatePlanAsync(string goal)
    {
        // Arrange
        var kernel = new Mock<IKernel>();
        kernel.Setup(x => x.Log).Returns(new Mock<ILogger>().Object);

        var memory = new Mock<ISemanticTextMemory>();

        var input = new List<(string name, string skillName, string description, bool isSemantic)>()
        {
            ("SendEmail", "email", "Send an e-mail", false),
            ("GetEmailAddress", "email", "Get an e-mail address", false),
            ("Translate", "WriterSkill", "Translate something", true),
            ("Summarize", "SummarizeSkill", "Summarize something", true)
        };

        var functionsView = new FunctionsView();
        var skills = new Mock<ISkillCollection>();
        foreach (var (name, skillName, description, isSemantic) in input)
        {
            var functionView = new FunctionView(name, skillName, description, new List<ParameterView>(), isSemantic, true);
            var mockFunction = CreateMockFunction(functionView);
            functionsView.AddFunction(functionView);

            // Task<SKContext> InvokeAsync(
            //     SKContext? context = null,
            //     CompleteRequestSettings? settings = null,
            //     ILogger? log = null,
            //     CancellationToken? cancel = null);
            mockFunction.Setup(x => x.InvokeAsync(It.IsAny<SKContext>(), It.IsAny<CompleteRequestSettings>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .Returns<SKContext, CompleteRequestSettings, ILogger, CancellationToken>((context, settings, log, cancel) =>
                {
                    context.Variables.Update("MOCK FUNCTION CALLED");
                    return Task.FromResult(context);
                });

            if (isSemantic)
            {
                skills.Setup(x => x.GetSemanticFunction(It.Is<string>(s => s == skillName), It.Is<string>(s => s == name)))
                    .Returns(mockFunction.Object);
                skills.Setup(x => x.HasSemanticFunction(It.Is<string>(s => s == skillName), It.Is<string>(s => s == name))).Returns(true);
            }
            else
            {
                skills.Setup(x => x.GetNativeFunction(It.Is<string>(s => s == skillName), It.Is<string>(s => s == name)))
                    .Returns(mockFunction.Object);
                skills.Setup(x => x.HasNativeFunction(It.Is<string>(s => s == skillName), It.Is<string>(s => s == name))).Returns(true);
            }
        }

        skills.Setup(x => x.GetFunctionsView(It.IsAny<bool>(), It.IsAny<bool>())).Returns(functionsView);

        var expectedFunctions = input.Select(x => x.name).ToList();
        var expectedSkills = input.Select(x => x.skillName).ToList();

        var context = new SKContext(
            new ContextVariables(),
            memory.Object,
            skills.Object,
            new Mock<ILogger>().Object
        );

        var returnContext = new SKContext(
            new ContextVariables(),
            memory.Object,
            skills.Object,
            new Mock<ILogger>().Object
        );
        var planString =
            @"
<plan>
    <function.SummarizeSkill.Summarize/>
    <function.WriterSkill.Translate language=""French"" setContextVariable=""TRANSLATED_SUMMARY""/>
    <function.email.GetEmailAddress input=""John Doe"" setContextVariable=""EMAIL_ADDRESS""/>
    <function.email.SendEmail input=""$TRANSLATED_SUMMARY"" email_address=""$EMAIL_ADDRESS""/>
</plan>";

        returnContext.Variables.Update(planString);

        var mockFunctionFlowFunction = new Mock<ISKFunction>();
        mockFunctionFlowFunction.Setup(x => x.InvokeAsync(
            It.IsAny<SKContext>(),
            null,
            null,
            null
        )).Callback<SKContext, CompleteRequestSettings, ILogger, CancellationToken?>(
            (c, s, l, ct) => c.Variables.Update("Hello world!")
        ).Returns(() => Task.FromResult(returnContext));

        // Mock Skills
        kernel.Setup(x => x.Skills).Returns(skills.Object);
        kernel.Setup(x => x.CreateNewContext()).Returns(context);

        kernel.Setup(x => x.RegisterSemanticFunction(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<SemanticFunctionConfig>()
        )).Returns(mockFunctionFlowFunction.Object);

        // var planner = new Planner(Planner.Mode.FunctionFlow, kernel.Object);
        var planner = new FunctionFlowPlanner(kernel.Object);

        // Act
        var plan = await planner.CreatePlanAsync(goal);
        // Assert
        Assert.Equal(goal, plan.Description);

        Assert.Contains(
            plan.Steps,
            step =>
                expectedFunctions.Contains(step.Name) &&
                expectedSkills.Contains(step.SkillName));

        foreach (var expectedFunction in expectedFunctions)
        {
            Assert.Contains(
                plan.Steps,
                step => step.Name == expectedFunction);
        }

        foreach (var expectedSkill in expectedSkills)
        {
            Assert.Contains(
                plan.Steps,
                step => step.SkillName == expectedSkill);
        }
    }

    // [Theory]
    // [InlineData("Write a poem or joke and send it in an e-mail to Kai.", "SendEmailAsync", "_GLOBAL_FUNCTIONS_")]
    // public async Task CreatePlanDefaultAsync(string prompt, string expectedFunction, string expectedSkill)
    // {
    //     // Arrange
    //     AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
    //     Assert.NotNull(azureOpenAIConfiguration);

    //     IKernel target = Kernel.Builder
    //         .WithLogger(this._logger)
    //         .Configure(config =>
    //         {
    //             config.AddAzureOpenAICompletionBackend(
    //                 label: azureOpenAIConfiguration.Label,
    //                 deploymentName: azureOpenAIConfiguration.DeploymentName,
    //                 endpoint: azureOpenAIConfiguration.Endpoint,
    //                 apiKey: azureOpenAIConfiguration.ApiKey);
    //             config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
    //         })
    //         .Build();

    //     // Import all sample skills available for demonstration purposes.
    //     // TestHelpers.ImportSampleSkills(target);
    //     // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
    //     var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

    //     var emailSkill = target.ImportSkill(new EmailSkill());

    //     var planner = new Planner(target);

    //     // Act
    //     if (await planner.CreatePlanAsync(prompt) is SequentialPlan plan)
    //     {
    //         // Assert
    //         // Assert.Empty(actual.LastErrorDescription);
    //         // Assert.False(actual.ErrorOccurred);
    //         Assert.Contains(
    //             plan.Root.Steps,
    //             step =>
    //                 step.SelectedFunction.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
    //                 step.SelectedSkill.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
    //     }
    //     else
    //     {
    //         Assert.Fail("Plan was not created successfully.");
    //     }
    // }

    // [Theory]
    // [InlineData("Write a poem or joke and send it in an e-mail to Kai.", "SendEmailAsync", "_GLOBAL_FUNCTIONS_")]
    // public async Task CreatePlanFunctionFlowAsync(string prompt, string expectedFunction, string expectedSkill)
    // {
    //     // Arrange
    //     AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
    //     Assert.NotNull(azureOpenAIConfiguration);

    //     AzureOpenAIConfiguration? azureOpenAIEmbeddingsConfiguration = this._configuration.GetSection("AzureOpenAIEmbeddings").Get<AzureOpenAIConfiguration>();
    //     Assert.NotNull(azureOpenAIEmbeddingsConfiguration);

    //     IKernel target = Kernel.Builder
    //         .WithLogger(this._logger)
    //         .Configure(config =>
    //         {
    //             config.AddAzureOpenAICompletionBackend(
    //                 label: azureOpenAIConfiguration.Label,
    //                 deploymentName: azureOpenAIConfiguration.DeploymentName,
    //                 endpoint: azureOpenAIConfiguration.Endpoint,
    //                 apiKey: azureOpenAIConfiguration.ApiKey);

    //             config.AddAzureOpenAIEmbeddingsBackend(
    //                 label: azureOpenAIEmbeddingsConfiguration.Label,
    //                 deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
    //                 endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
    //                 apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey);

    //             config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
    //         })
    //         .WithMemoryStorage(new VolatileMemoryStore())
    //         .Build();

    //     // Import all sample skills available for demonstration purposes.
    //     // TestHelpers.ImportSampleSkills(target);
    //     var chatSkill = TestHelpers.GetSkill("ChatSkill", target);
    //     var summarizeSkill = TestHelpers.GetSkill("SummarizeSkill", target);
    //     var writerSkill = TestHelpers.GetSkill("WriterSkill", target);
    //     var calendarSkill = TestHelpers.GetSkill("CalendarSkill", target);
    //     var childrensBookSkill = TestHelpers.GetSkill("ChildrensBookSkill", target);
    //     var classificationSkill = TestHelpers.GetSkill("ClassificationSkill", target);

    //     // TODO This is still unreliable in creating a valid plan xml -- what's going on?

    //     var emailSkill = target.ImportSkill(new EmailSkill());

    //     var planner = new Planner(target, Planner.Mode.Root.DescriptionRelevant);

    //     // Act
    //     if (await planner.CreatePlanAsync(prompt) is SequentialPlan plan)
    //     {
    //         // Assert
    //         // Assert.Empty(actual.LastErrorDescription);
    //         // Assert.False(actual.ErrorOccurred);
    //         Assert.Contains(
    //             plan.Root.Steps,
    //             step =>
    //                 step.SelectedFunction.Equals(expectedFunction, StringComparison.OrdinalIgnoreCase) &&
    //                 step.SelectedSkill.Equals(expectedSkill, StringComparison.OrdinalIgnoreCase));
    //     }
    //     else
    //     {
    //         Assert.Fail("Plan was not created successfully.");
    //     }
    // }

    // [Theory]
    // [InlineData("Write a poem or joke and send it in an e-mail to Kai.")]
    // public async Task CreatePlanSimpleAsync(string prompt)
    // {
    //     // Arrange
    //     AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
    //     Assert.NotNull(azureOpenAIConfiguration);

    //     IKernel target = Kernel.Builder
    //         .WithLogger(this._logger)
    //         .Configure(config =>
    //         {
    //             config.AddAzureOpenAICompletionBackend(
    //                 label: azureOpenAIConfiguration.Label,
    //                 deploymentName: azureOpenAIConfiguration.DeploymentName,
    //                 endpoint: azureOpenAIConfiguration.Endpoint,
    //                 apiKey: azureOpenAIConfiguration.ApiKey);
    //             config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
    //         })
    //         .Build();

    //     // Import all sample skills available for demonstration purposes.
    //     // TestHelpers.ImportSampleSkills(target);
    //     // If I import everything with no relevance, the changes of an invalid xml being returned is very high.
    //     var writerSkill = TestHelpers.GetSkill("WriterSkill", target);

    //     var emailSkill = target.ImportSkill(new EmailSkill());

    //     var planner = new Planner(target, Planner.Mode.Simple);

    //     // Act
    //     if (await planner.CreatePlanAsync(prompt) is BasePlan plan)
    //     {
    //         // Assert
    //         // Assert.Empty(actual.LastErrorDescription);
    //         // Assert.False(actual.ErrorOccurred);
    //         Assert.Equal(prompt, plan.Root.Description);
    //     }
    //     else
    //     {
    //         Assert.Fail("Plan was not created successfully.");
    //     }
    // }

    // [Theory]
    // [InlineData(null, "Write a poem or joke and send it in an e-mail to Kai.", null)]
    // [InlineData("", "Write a poem or joke and send it in an e-mail to Kai.", "")]
    // [InlineData("Hello World!", "Write a poem or joke and send it in an e-mail to Kai.", "some_email@email.com")]
    // public async Task CanExecuteRunPlanSimpleAsync(string input, string goal, string email)
    // {
    //     // Arrange
    //     AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
    //     Assert.NotNull(azureOpenAIConfiguration);

    //     IKernel target = Kernel.Builder
    //         .WithLogger(this._logger)
    //         .Configure(config =>
    //         {
    //             config.AddAzureOpenAICompletionBackend(
    //                 label: azureOpenAIConfiguration.Label,
    //                 deploymentName: azureOpenAIConfiguration.DeploymentName,
    //                 endpoint: azureOpenAIConfiguration.Endpoint,
    //                 apiKey: azureOpenAIConfiguration.ApiKey);
    //             config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
    //         })
    //         .Build();

    //     // Import all sample skills available for demonstration purposes.
    //     TestHelpers.ImportSampleSkills(target);
    //     var emailSkill = target.ImportSkill(new EmailSkill());

    //     var cv = new ContextVariables();
    //     cv.Set("email_address", "$TheEmailFromState");
    //     var plan = new SequentialPlan() { Goal = goal };
    //     plan.Root.Steps.Add(new PlanStep()
    //     {
    //         SelectedFunction = "SendEmailAsync",
    //         SelectedSkill = "_GLOBAL_FUNCTIONS_",
    //         NamedParameters = cv
    //     });
    //     plan.State.Set("TheEmailFromState", email);

    //     // Act
    //     await target.RunAsync(input, plan);

    //     // BUG FOUND -- The parameters of the Step are not populated properly

    //     // Assert
    //     // Assert.Empty(plan.LastErrorDescription);
    //     // Assert.False(plan.ErrorOccurred);
    //     var expectedBody = string.IsNullOrEmpty(input) ? goal : input;
    //     Assert.Equal(0, plan.Root.Steps.Count);
    //     Assert.Equal(goal, plan.Root.Description);
    //     Assert.Equal($"Sent email to: {email}. Body: {expectedBody}", plan.State.ToString()); // TODO Make a Result and other properties
    // }

    // private readonly XunitLogger<object> _logger;
    // private readonly RedirectOutput _testOutputHelper;
    // private readonly IConfigurationRoot _configuration;

    // public void Dispose()
    // {
    //     this.Dispose(true);
    //     GC.SuppressFinalize(this);
    // }

    // ~PlanningTests()
    // {
    //     this.Dispose(false);
    // }

    // private void Dispose(bool disposing)
    // {
    //     if (disposing)
    //     {
    //         this._logger.Dispose();
    //         this._testOutputHelper.Dispose();
    //     }
    // }
}
