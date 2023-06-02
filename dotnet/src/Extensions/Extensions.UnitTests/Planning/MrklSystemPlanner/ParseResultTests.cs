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
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;

namespace SemanticKernel.Extensions.UnitTests.Planning.MrklSystemPlanner;

public sealed class ParseResultTests
{
    // protected virtual SystemStep ParseResult(string input)
    // {
    //     var result = new SystemStep
    //     {
    //         OriginalResponse = input
    //     };

    //     Regex untilAction = new("(.*)(?=Action:)", RegexOptions.Singleline);
    //     Match untilActionMatch = untilAction.Match(input);

    //     if (input.StartsWith("Final Answer:", StringComparison.OrdinalIgnoreCase))
    //     {
    //         result.FinalAnswer = input;
    //         return result;
    //     }

    //     // Otherwise look for "Final Answer:" with the text after captured
    //     Regex finalAnswer = new("Final Answer:(.*)", RegexOptions.Singleline);
    //     Match finalAnswerMatch = finalAnswer.Match(input);

    //     if (finalAnswerMatch.Success)
    //     {
    //         result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
    //         return result;
    //     }

    //     if (untilActionMatch.Success)
    //     {
    //         result.Thought = untilActionMatch.Value.Trim();
    //     }
    // Capture everything After first 'Action:' and in between optional ``` and ``` that come after that 'Action:', with whitespace/newlines allowed.
    //     // There also may be text before the first 'Action:' that we want to capture.
    //     // Regex actionRegex = new("Action:(.*?)(?:```)(.*?)?(.*?)(?:```)?$", RegexOptions.Singleline);
    //     // Match actionMatch = actionRegex.Match(input);
    //     Regex actionRegex = new Regex(@"Action:\s*((?:```[\s\S]*?```)|(?:\{[\s\S]*?\}))", RegexOptions.Singleline);
    //     Match actionMatch = actionRegex.Match(input);

    //     if (actionMatch.Success)
    //     {
    //         var json = actionMatch.Groups[1].Value.Trim();
    //         try
    //         {
    //             var systemStepResults = JsonSerializer.Deserialize<SystemStep>(json);

    //             if (systemStepResults == null)
    //             {
    //                 // TODO New error code maybe?
    //                 throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, "The system step deserialized to a null object");
    //             }

    //             result.Action = systemStepResults.Action;
    //             result.ActionInput = systemStepResults.ActionInput;
    //         }
    //         catch (Exception e)
    //         {
    //             // TODO New error code maybe?
    //             throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, $"System step parsing error, invalid JSON: {json}", e);
    //         }
    //     }
    //     else
    //     {
    //         // TODO New error code maybe?
    //         // throw new PlanningException(PlanningException.ErrorCodes.InvalidPlan, $"no action found in response: {input}");

    //         // Actually, this just means there was only a thought, so we should just carry on.
    //     }

    //     if (result.Action == "Final Answer")
    //     {
    //         result.FinalAnswer = result.ActionInput;
    //     }

    //     return result;
    // }

    [Theory]
    [InlineData("Final Answer: 42", "42")]
    [InlineData("Final Answer:42", "42")]
    [InlineData("I think I have everything I need.\nFinal Answer: 42", "42")]
    [InlineData("I think I have everything I need.\nFinal Answer: 42\n", "42")]
    [InlineData("I think I have everything I need.\nFinal Answer: 42\n\n", "42")]
    [InlineData("I think I have everything I need.\nFinal Answer:42\n\n\n", "42")]
    [InlineData("I think I have everything I need.\nFinal Answer:\n 42\n\n\n", "42")]
    public void WhenInputIsFinalAnswerReturnsFinalAnswer(string input, string expected)
    {
        // Arrange
        var kernel = new Mock<IKernel>();
        kernel.Setup(x => x.Log).Returns(new Mock<ILogger>().Object);

        var planner = new Microsoft.SemanticKernel.Planning.MrklSystemPlanner(kernel.Object);

        // Act
        var result = planner.ParseResult(input);

        // Assert
        Assert.Equal(expected, result.FinalAnswer);
    }

    [Theory]
    [InlineData("To answer the first part of the question, I need to search for Leo DiCaprio's girlfriend on the web. To answer the second part, I need to find her current age and use a calculator to raise it to the 0.43 power.\nAction:\n{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}", "Search", "Leo DiCaprio's girlfriend")]
    [InlineData("To answer the first part of the question, I need to search the web for Leo DiCaprio's girlfriend. To answer the second part, I need to find her current age and use the calculator tool to raise it to the 0.43 power.\nAction:\n```\n{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}\n```", "Search", "Leo DiCaprio's girlfriend")]
    [InlineData("To answer the first part of the question, I need to search for Leo DiCaprio's girlfriend on the web. To answer the second part, I need to find her current age and use a calculator to raise it to the 0.43 power.\nAction:\n{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}", "Search", "Leo DiCaprio's girlfriend")]
    [InlineData("To answer the first part of the question, I need to search the web for Leo DiCaprio's girlfriend. To answer the second part, I need to find her current age and use the calculator tool to raise it to the 0.43 power.\nAction:\n```\n{\n  \"action\": \"Search\",\n  \"action_input\": \"Leo DiCaprio's girlfriend\"\n}\n```", "Search", "Leo DiCaprio's girlfriend")]
    // [InlineData("The web search result is a snippet from a Wikipedia article that says Leo DiCaprio's girlfriend is Camila Morrone, an Argentine-American model and actress. I need to find out her current age, which might be in the same article or another source. I can use the WebSearch.Search function again to search for her name and age.\n\nAction: {\n  \"action\": \"WebSearch.Search\",\n  \"action_input\": \"Camila Morrone age\",\n  \"action_variables\": {\"count\": 1}\n}", "WebSearch.Search", "Camila Morrone age", "count", "1")] // NEGATIVE TEST CASE
    [InlineData("The web search result is a snippet from a Wikipedia article that says Leo DiCaprio's girlfriend is Camila Morrone, an Argentine-American model and actress. I need to find out her current age, which might be in the same article or another source. I can use the WebSearch.Search function again to search for her name and age.\n\nAction: {\n  \"action\": \"WebSearch.Search\",\n  \"action_input\": \"Camila Morrone age\",\n  \"action_variables\": {\"count\": \"1\"}\n}", "WebSearch.Search", "Camila Morrone age", "count", "1")]
    public void ParseActionReturnsAction(string input, string expectedAction, string expectedInput, params string[] expectedVariables)
    {
        Dictionary<string, string>? expectedDictionary = null;
        for (int i = 0; i < expectedVariables.Length; i += 2)
        {
            expectedDictionary ??= new Dictionary<string, string>();
            expectedDictionary.Add(expectedVariables[i], expectedVariables[i + 1]);
        }

        // Arrange
        var kernel = new Mock<IKernel>();
        kernel.Setup(x => x.Log).Returns(new Mock<ILogger>().Object);

        var planner = new Microsoft.SemanticKernel.Planning.MrklSystemPlanner(kernel.Object);

        // Act
        var result = planner.ParseResult(input);

        // Assert
        Assert.Equal(expectedAction, result.Action);
        Assert.Equal(expectedInput, result.ActionInput);
        Assert.Equal(expectedDictionary, result.ActionVariables);
    }

    // Method to create Mock<ISKFunction> objects
    private static Mock<ISKFunction> CreateMockFunction(FunctionView functionView)
    {
        var mockFunction = new Mock<ISKFunction>();
        mockFunction.Setup(x => x.Describe()).Returns(functionView);
        mockFunction.Setup(x => x.Name).Returns(functionView.Name);
        mockFunction.Setup(x => x.SkillName).Returns(functionView.SkillName);
        return mockFunction;
    }
}
