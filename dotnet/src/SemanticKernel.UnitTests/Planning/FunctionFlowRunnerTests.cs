// Copyright (c) Microsoft. All rights reserved.

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

