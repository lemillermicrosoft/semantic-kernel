// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.UnitTests.Planning;

public class FunctionFlowRunnerTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public FunctionFlowRunnerTests(ITestOutputHelper testOutputHelper)
    {
        this._testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task ItExecuteXmlPlanAsyncValidEmptyPlanAsync()
    {
        // Arrange
        var kernelMock = this.CreateKernelMock(out _, out _, out _);
        var kernel = kernelMock.Object;
        var target = new FunctionFlowRunner(kernel);

        var emptyPlanSpec = @"
<goal>Some goal</goal>
<plan>
</plan>
";

        // Act
        var result = await target.ExecuteXmlPlanAsync(this.CreateSKContext(kernel), emptyPlanSpec);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Variables[SkillPlan.PlanKey]);
    }

    [Theory]
    [InlineData("<goal>Some goal</goal><plan>")]
    public async Task ItExecuteXmlPlanAsyncFailWhenInvalidPlanXmlAsync(string invalidPlanSpec)
    {
        // Arrange
        var kernelMock = this.CreateKernelMock(out _, out _, out _);
        var kernel = kernelMock.Object;
        var target = new FunctionFlowRunner(kernel);

        // Act
        var exception = await Assert.ThrowsAsync<PlanningException>(async () =>
        {
            await target.ExecuteXmlPlanAsync(this.CreateSKContext(kernel), invalidPlanSpec);
        });

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(PlanningException.ErrorCodes.InvalidPlan, exception.ErrorCode);
    }

    [Fact]
    public async Task ItExecuteXmlPlanAsyncAndFailWhenSkillOrFunctionNotExistsAsync()
    {
        // Arrange
        var kernelMock = this.CreateKernelMock(out _, out _, out _);
        var kernel = kernelMock.Object;
        var target = new FunctionFlowRunner(kernel);

        SKContext? result = null;
        // Act
        var exception = await Assert.ThrowsAsync<PlanningException>(async () =>
        {
            result = await target.ExecuteXmlPlanAsync(this.CreateSKContext(kernel), @"
<goal>Some goal</goal>
<plan>
    <function.SkillA.FunctionB/>
</plan>");
        });

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(PlanningException.ErrorCodes.InvalidPlan, exception.ErrorCode);
    }

    [Fact]
    public async Task ItExecuteXmlPlanAsyncFailWhenElseComesWithoutIfAsync()
    {
        // Arrange
        var kernelMock = this.CreateKernelMock(out _, out _, out _);
        var kernel = kernelMock.Object;
        var target = new FunctionFlowRunner(kernel);

        SKContext? result = null;
        // Act
        var exception = await Assert.ThrowsAsync<PlanningException>(async () =>
        {
            result = await target.ExecuteXmlPlanAsync(this.CreateSKContext(kernel), @"
<goal>Some goal</goal>
<plan>
<else>
    <function.SkillA.FunctionB/>
</else>
</plan>");
        });

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(PlanningException.ErrorCodes.InvalidPlan, exception.ErrorCode);
    }

    /// <summary>
    /// Potential tests scenarios with Functions, Ifs and Else (Nested included)
    /// </summary>
    /// <param name="inputPlanSpec">Plan input</param>
    /// <param name="expectedPlanOutput">Expected plan output</param>
    /// <param name="conditionResult">Condition result</param>
    /// <returns>Unit test result</returns>
    [Theory]
    [InlineData(
        "<goal>Some goal</goal><plan></plan>",
        "<goal>Some goal</goal><plan></plan>")]
    [InlineData(
        "<goal>Some goal</goal><plan><function.SkillA.FunctionB /></plan>",
        "<goal>Some goal</goal><plan></plan>")]
    [InlineData(
        "<goal>Some goal</goal><plan><function.SkillA.FunctionB /><function.SkillA.FunctionC /></plan>",
        "<goal>Some goal</goal><plan>  <function.SkillA.FunctionC /></plan>")]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <function.SkillA.FunctionB />
  <function.SkillA.FunctionC />
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
</plan>",
        @"<goal>Some goal</goal>
<plan>
  <function.SkillA.FunctionC />
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
</plan>")]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
</plan>",
        @"<goal>Some goal</goal>
<plan>
    <function.SkillA.FunctionD />
</plan>", true)]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
</plan>",
        @"<goal>Some goal</goal>
<plan>
</plan>", false)]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
  <else>
    <function.SkillX.FunctionW />
  </else>
</plan>",
        @"<goal>Some goal</goal>
<plan>
  <function.SkillX.FunctionW />
</plan>", false)]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
  <else>
    <function.SkillB.FunctionH />
  </else>
  <if condition=""$b equals c"">
    <function.SkillD.FunctionG />
  </if>
</plan>",
        @"<goal>Some goal</goal>
<plan>
  <function.SkillA.FunctionD />
  <if condition=""$b equals c"">
    <function.SkillD.FunctionG />
  </if>
</plan>", true)]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
  </if>
  <else>
    <function.SkillB.FunctionH />
  </else>
  <if condition=""$b equals c"">
    <function.SkillD.FunctionG />
  </if>
</plan>",
        @"<goal>Some goal</goal>
<plan>
  <function.SkillB.FunctionH />
  <if condition=""$b equals c"">
    <function.SkillD.FunctionG />
  </if>
</plan>", false)]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
    <if condition=""$b equals c"">
      <function.SkillD.FunctionG />
    </if>
  </if>
  <else>
    <function.SkillB.FunctionH />
  </else>
</plan>",
        @"<goal>Some goal</goal>
<plan>
  <function.SkillA.FunctionD />
  <if condition=""$b equals c"">
    <function.SkillD.FunctionG />
  </if>
</plan>", true)]
    [InlineData(
        @"<goal>Some goal</goal>
<plan>
  <if condition=""$a equals b"">
    <function.SkillA.FunctionD />
    <if condition=""$b equals c"">
      <function.SkillD.FunctionG />
    </if>
  </if>
  <else>
    <function.SkillB.FunctionH />
  </else>
</plan>",
        @"<goal>Some goal</goal>
<plan>
  <function.SkillB.FunctionH />
</plan>", false)]
    public async Task ItExecuteXmlPlanAsyncAndReturnsAsExpectedAsync(string inputPlanSpec, string expectedPlanOutput, bool? conditionResult = null)
    {
        // Arrange
        var kernelMock = this.CreateKernelMock(out _, out var skillMock, out _);
        var kernel = kernelMock.Object;
        var mockFunction = new Mock<ISKFunction>();

        var ifStructureResultContext = this.CreateSKContext(kernel);
        ifStructureResultContext.Variables.Update( /*lang=json,strict*/ "{\"valid\": true}");

        var evaluateConditionResultContext = this.CreateSKContext(kernel);
        evaluateConditionResultContext.Variables.Update($"{{\"valid\": true, \"condition\": {((conditionResult ?? false) ? "true" : "false")}}}");

        mockFunction.Setup(f => f.InvokeAsync(It.Is<string>(i => i.StartsWith("<if")),
                It.IsAny<SKContext?>(), It.IsAny<CompleteRequestSettings?>(),
                It.IsAny<ILogger?>(),
                It.IsAny<CancellationToken?>()))
            .ReturnsAsync(ifStructureResultContext);

        mockFunction.Setup(f => f.InvokeAsync(It.Is<string>(i => i.StartsWith("$a equals b")),
                It.IsAny<SKContext?>(), It.IsAny<CompleteRequestSettings?>(),
                It.IsAny<ILogger?>(),
                It.IsAny<CancellationToken?>()))
            .ReturnsAsync(evaluateConditionResultContext);

        skillMock.Setup(s => s.HasSemanticFunction(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        skillMock.Setup(s => s.GetSemanticFunction(It.IsAny<string>(), It.IsAny<string>())).Returns(mockFunction.Object);
        kernelMock.Setup(k => k.RunAsync(It.IsAny<ContextVariables>(), It.IsAny<ISKFunction>())).ReturnsAsync(this.CreateSKContext(kernel));
        kernelMock.Setup(k => k.RegisterSemanticFunction(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SemanticFunctionConfig>()))
            .Returns(mockFunction.Object);

        var target = new FunctionFlowRunner(kernel);

        SKContext? result = null;
        // Act
        result = await target.ExecuteXmlPlanAsync(this.CreateSKContext(kernel), inputPlanSpec);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.ErrorOccurred);
        Assert.True(result.Variables.ContainsKey(SkillPlan.PlanKey));
        Assert.Equal(
            NormalizeSpacesBeforeFunctions(expectedPlanOutput),
            NormalizeSpacesBeforeFunctions(result.Variables[SkillPlan.PlanKey]));

        // Removes line breaks and spaces before <function, <if, </if, <else, </else, </plan
        static string NormalizeSpacesBeforeFunctions(string input)
        {
            return Regex.Replace(input, @"\s+(?=<function|<[/]*if|<[/]*else|</plan)", string.Empty, RegexOptions.IgnoreCase)
                .Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    private SKContext CreateSKContext(
        IKernel kernel,
        ContextVariables? variables = null,
        CancellationToken cancellationToken = default)
    {
        return new SKContext(variables ?? new ContextVariables(), kernel.Memory, kernel.Skills, kernel.Log, cancellationToken);
    }

    private Mock<IKernel> CreateKernelMock(
        out Mock<ISemanticTextMemory> semanticMemoryMock,
        out Mock<IReadOnlySkillCollection> mockSkillCollection,
        out Mock<ILogger> mockLogger)
    {
        semanticMemoryMock = new Mock<ISemanticTextMemory>();
        mockSkillCollection = new Mock<IReadOnlySkillCollection>();
        mockLogger = new Mock<ILogger>();

        var kernelMock = new Mock<IKernel>();
        kernelMock.SetupGet(k => k.Skills).Returns(mockSkillCollection.Object);
        kernelMock.SetupGet(k => k.Log).Returns(mockLogger.Object);
        kernelMock.SetupGet(k => k.Memory).Returns(semanticMemoryMock.Object);

        return kernelMock;
    }
}
// namespace SemanticKernel.UnitTests.Planning;

// public class FunctionFlowRunnerTests
// // {
// //     public FunctionFlowRunnerTests(ITestOutputHelper output)
// //     {
// //         // Load configuration
// //         this._configuration = new ConfigurationBuilder()
// //             .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
// //             .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
// //             .AddEnvironmentVariables()
// //             .AddUserSecrets<FunctionFlowRunnerTests>()
// //             .Build();
// //     }

// //     [Fact]
// //     public void CanCallToPlanFromXml()
// //     {
// //         // Arrange
// //         AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
// //         Assert.NotNull(azureOpenAIConfiguration);

// //         IKernel kernel = Kernel.Builder
// //             .Configure(config =>
// //             {
// //                 config.AddAzureOpenAICompletionBackend(
// //                     label: azureOpenAIConfiguration.Label,
// //                     deploymentName: azureOpenAIConfiguration.DeploymentName,
// //                     endpoint: azureOpenAIConfiguration.Endpoint,
// //                     apiKey: azureOpenAIConfiguration.ApiKey);
// //                 config.SetDefaultCompletionBackend(azureOpenAIConfiguration.Label);
// //             })
// //             .Build();
// //         kernel.ImportSkill(new EmailSkill(), "email");
// //         var summarizeSkill = TestHelpers.GetSkill("SummarizeSkill", kernel);
// //         var writerSkill = TestHelpers.GetSkill("WriterSkill", kernel);

// //         _ = kernel.Config.AddOpenAICompletionBackend("test", "test", "test");
// //         var functionFlowRunner = new FunctionFlowRunner(kernel);
// //         var planString =
// // @"<goal>
// // Summarize an input, translate to french, and e-mail to John Doe
// // </goal>
// // <plan>
// //     <function.SummarizeSkill.Summarize/>
// //     <function.WriterSkill.Translate language=""French"" setContextVariable=""TRANSLATED_SUMMARY""/>
// //     <function.email.GetEmailAddressAsync input=""John Doe"" setContextVariable=""EMAIL_ADDRESS""/>
// //     <function.email.SendEmailAsync input=""$TRANSLATED_SUMMARY"" email_address=""$EMAIL_ADDRESS""/>
// // </plan>";

// //         // Act
// //         var plan = functionFlowRunner.ToPlanFromXml(planString);

// //         // Assert
// //         Assert.NotNull(plan);
// //         Assert.Equal("Summarize an input, translate to french, and e-mail to John Doe", plan.Goal);
// //         Assert.Equal(4, plan.Steps.Count);
// //         Assert.Collection(plan.Steps,
// //             step =>
// //             {
// //                 Assert.Equal("SummarizeSkill", step.SelectedSkill);
// //                 Assert.Equal("Summarize", step.SelectedFunction);
// //             },
// //             step =>
// //             {
// //                 Assert.Equal("WriterSkill", step.SelectedSkill);
// //                 Assert.Equal("Translate", step.SelectedFunction);
// //                 Assert.Equal("French", step.NamedParameters["language"]);
// //                 // Assert.Equal("TRANSLATED_SUMMARY", step.NamedParameters["setContextVariable"]);
// //             },
// //             step =>
// //             {
// //                 Assert.Equal("email", step.SelectedSkill);
// //                 Assert.Equal("GetEmailAddressAsync", step.SelectedFunction);
// //                 Assert.Equal("John Doe", step.NamedParameters["input"]);
// //                 // Assert.Equal("EMAIL_ADDRESS", step.NamedParameters["setContextVariable"]);
// //             },
// //             step =>
// //             {
// //                 Assert.Equal("email", step.SelectedSkill);
// //                 Assert.Equal("SendEmailAsync", step.SelectedFunction);
// //                 // Assert.Equal("TRANSLATED_SUMMARY", step.NamedParameters["input"]);
// //                 // Assert.Equal("EMAIL_ADDRESS", step.NamedParameters["email_address"]);
// //             }
// //         );
// //     }

// //     private readonly IConfigurationRoot _configuration;
// }
