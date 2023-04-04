// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Planning.Planners;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Planning;

public sealed class PlanningTests
{
    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.")]
    public async Task ItCanCreatePlanAsync(string goal)
    {
        // Arrange
        // var kernel = KernelBuilder.Create();
        // _ = kernel.Config.AddOpenAICompletionBackend("test", "test", "test");

        var kernel = new Mock<IKernel>();
        kernel.Setup(x => x.Log).Returns(new Mock<ILogger>().Object);

        var memory = new Mock<ISemanticTextMemory>();

        // var skills = new SkillCollection();
        var skills = new Mock<ISkillCollection>();
        var functionsView = new FunctionsView();
        // var semanticFunctions = new ConcurrentDictionary<string, List<FunctionView>>();
        // semanticFunctions["SummarizeSkill"] = new List<FunctionView>() {
        //     new FunctionView("Summarize", "SummarizeSkill", "Summarize something", new List<ParameterView>(), true, true)
        // };
        // semanticFunctions["WriterSkill"] = new List<FunctionView>() {
        //     new FunctionView("Translate", "WriterSkill", "Translate something", new List<ParameterView>(), true, true)
        // };
        // semanticFunctions["EmailSkill"] = new List<FunctionView>() {
        //     new FunctionView("GetEmailAddress", "EmailSkill", "Get an e-mail address", new List<ParameterView>(), true, true),
        //     new FunctionView("SendEmail", "EmailSkill", "Send an e-mail", new List<ParameterView>(), true, true)
        // };
        var summarizeFunctionView = new FunctionView("Summarize", "SummarizeSkill", "Summarize something", new List<ParameterView>(), true, true);
        functionsView.AddFunction(summarizeFunctionView);
        var translateFunctionView = new FunctionView("Translate", "WriterSkill", "Translate something", new List<ParameterView>(), true, true);
        functionsView.AddFunction(translateFunctionView);
        var getEmailAddressFunctionView = new FunctionView("GetEmailAddress", "email", "Get an e-mail address", new List<ParameterView>(), true, true);
        functionsView.AddFunction(getEmailAddressFunctionView);
        var sendEmailFunctionView = new FunctionView("SendEmail", "email", "Send an e-mail", new List<ParameterView>(), true, true);
        functionsView.AddFunction(sendEmailFunctionView);


        // ****************************************************
        // TODO Add my functions or skills to the skill collection
        // or mock so it returns that function is registered no matter what.
        // ****************************************************

        // bool HasNativeFunction(string skillName, string functionName);
        // ISKFunction GetNativeFunction(string skillName, string functionName);
        skills.Setup(x => x.HasNativeFunction(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        skills.Setup(x => x.GetFunctionsView(It.IsAny<bool>(), It.IsAny<bool>())).Returns(functionsView);


        var summarizeMockFunction = new Mock<ISKFunction>();
        summarizeMockFunction.Setup(x => x.Describe()).Returns(summarizeFunctionView);
        summarizeMockFunction.Setup(x => x.Name).Returns("Summarize");
        summarizeMockFunction.Setup(x => x.SkillName).Returns("SummarizeSkill");

        var translateMockFunction = new Mock<ISKFunction>();
        translateMockFunction.Setup(x => x.Describe()).Returns(translateFunctionView);
        translateMockFunction.Setup(x => x.Name).Returns("Translate");
        translateMockFunction.Setup(x => x.SkillName).Returns("WriterSkill");

        var getEmailAddressMockFunction = new Mock<ISKFunction>();
        getEmailAddressMockFunction.Setup(x => x.Describe()).Returns(getEmailAddressFunctionView);
        getEmailAddressMockFunction.Setup(x => x.Name).Returns("GetEmailAddress");
        getEmailAddressMockFunction.Setup(x => x.SkillName).Returns("email");

        var sendEmailMockFunction = new Mock<ISKFunction>();
        sendEmailMockFunction.Setup(x => x.Describe()).Returns(sendEmailFunctionView);
        sendEmailMockFunction.Setup(x => x.Name).Returns("SendEmail");
        sendEmailMockFunction.Setup(x => x.SkillName).Returns("email");

        skills.Setup(x => x.GetNativeFunction(It.Is<string>(s => s == "SummarizeSkill"), It.Is<string>(s => s == "Summarize"))).Returns(summarizeMockFunction.Object);
        skills.Setup(x => x.GetNativeFunction(It.Is<string>(s => s == "WriterSkill"), It.Is<string>(s => s == "Translate"))).Returns(translateMockFunction.Object);
        skills.Setup(x => x.GetNativeFunction(It.Is<string>(s => s == "email"), It.Is<string>(s => s == "GetEmailAddress"))).Returns(getEmailAddressMockFunction.Object);
        skills.Setup(x => x.GetNativeFunction(It.Is<string>(s => s == "email"), It.Is<string>(s => s == "SendEmail"))).Returns(sendEmailMockFunction.Object);

        var mockFunctionFlowFunction = new Mock<ISKFunction>();
        var summarizeFunction = new Mock<ISKFunction>();
        var translateFunction = new Mock<ISKFunction>();
        var getEmailAddressFunction = new Mock<ISKFunction>();
        var sendEmailFunction = new Mock<ISKFunction>();

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

        var expectedFunctions = new List<string>() { "SendEmail", "GetEmailAddress", "Translate", "Summarize" };
        var expectedSkills = new List<string>() { "email", "WriterSkill", "WriterSkill", "SummarizeSkill" };

        returnContext.Variables.Update(planString);

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

        // var plannerSkill = new PlannerSkill(kernel);
        // var planner = kernel.ImportSkill(plannerSkill, "planner");
        var planner = new Planner(kernel.Object);

        // Act
        // var context = await kernel.RunAsync(GoalText, planner["CreatePlan"]);
        var plan = await planner.CreatePlanAsync(goal);
        if (plan is SequentialPlan sequentialPlan)
        {
            // Assert
            // Assert.Empty(actual.LastErrorDescription);
            // Assert.False(actual.ErrorOccurred);
            // Assert.Contains(
            //     plan.Root.Steps,
            //     step =>
            //         expectedFunctions.Contains(step.SelectedFunction) &&
            //         expectedSkills.Contains(step.SelectedSkill));

            Assert.Contains(
                sequentialPlan.Steps,
                step =>
                    expectedFunctions.Contains(step.Name) &&
                    expectedSkills.Contains(step.SkillName));

            foreach (var expectedFunction in expectedFunctions)
            {
                // Assert.Contains(
                //     plan.Root.Steps,
                //     step => step.SelectedFunction == expectedFunction);
                Assert.Contains(
                    sequentialPlan.Steps,
                    step => step.Name == expectedFunction);
            }

            foreach (var expectedSkill in expectedSkills)
            {
                // Assert.Contains(
                //     plan.Root.Steps,
                //     step => step.SelectedSkill == expectedSkill);
                Assert.Contains(
                    sequentialPlan.Steps,
                    step => step.SkillName == expectedSkill);
            }

            // Assert.Equal(goal, plan.Root.Description);
            Assert.Equal(goal, sequentialPlan.Description);
        }
        else
        {
            Assert.Fail("Plan was not created successfully.");
        }

        // Assert
        //     var plan = context.Variables.ToPlan();
        // Assert.NotNull(plan);
        // Assert.NotNull(plan.Id);
        // Assert.Equal(GoalText, plan.Root.Description);
        // Assert.StartsWith("<goal>\nSolve the equation x^2 = 2.\n</goal>", plan.PlanString, StringComparison.OrdinalIgnoreCase);
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
