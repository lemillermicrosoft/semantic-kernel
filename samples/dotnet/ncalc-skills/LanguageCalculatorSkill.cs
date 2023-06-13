// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using NCalc;

namespace NCalcSkills;

/// <summary>
/// Skill that enables the comprehension of mathematical problems presented in English / natural-language text, followed by the execution of the necessary calculations to solve those problems.
/// </summary>
/// <example>
/// usage :
/// var kernel = new KernelBuilder().WithLogger(ConsoleLogger.Log).Build();
/// var question = "what is the square root of 625";
/// var calculatorSkill = kernel.ImportSkill(new LanguageCalculatorSkill(kernel));
/// var summary = await kernel.RunAsync(questions, calculatorSkill["Calculate"]);
/// Console.WriteLine("Result :");
/// Console.WriteLine(summary.Result);
/// </example>
public class LanguageCalculatorSkill
{
    private readonly ISKFunction _mathTranslator;

    private const string MathTranslatorPrompt =
        @"Translate a math problem into a expression that can be executed using .net NCalc library. Use the output of running this code to answer the question.
Available functions: Abs, Acos, Asin, Atan, Ceiling, Cos, Exp, Floor, IEEERemainder, Log, Log10, Max, Min, Pow, Round, Sign, Sin, Sqrt, Tan, and Truncate. in and if are also supported.

Question: $((Question with math problem.))
expression:``` $((single line mathematical expression that solves the ))```

[Examples]
Question: What is 37593 * 67?
expression:```37593 * 67```

Question: what is 3 to the 2nd power?
expression:```Pow(3, 2)```

Question: what is sine of 0 radians?
expression:```Sin(0)```

Question: what is sine of 45 degrees?
expression:```Sin(45 * Pi /180 )```

Question: how many radians is 45 degrees?
expression:``` 45 * Pi / 180 ```

Question: what is the square root of 81?
expression:```Sqrt(81)```

Question: what is the angle whose sine is the number 1?
expression:```Asin(1)```

[End of Examples]

Question: {{ $input }}
";

    public LanguageCalculatorSkill(IKernel kernel)
    {
        // TODO it'd be great to create this in a cloned kernel so that we don't have to worry about it being consumed by planners.
        this._mathTranslator = kernel.CreateSemanticFunction(
            MathTranslatorPrompt,
            skillName: nameof(LanguageCalculatorSkill),
            functionName: "TranslateMathProblem",
            description: "Used by 'Calculator' function.",
            maxTokens: 256,
            temperature: 0.0,
            topP: 1);
    }

    [SKFunction("Useful for getting the result of a non-trivial math expression.")]
    [SKFunctionName("Calculator")]
    [SKFunctionInput(Description = "A valid mathematical expression that could be executed by a calculator capable of more advanced math functions like sin/cosine/floor.")]
    public async Task<string> CalculateAsync(string input, SKContext context)
    {
        //this._mathTranslator.RequestSettings.ResultsPerPrompt = 0;
        var answer = await this._mathTranslator.InvokeAsync(input).ConfigureAwait(false);
        //Console.WriteLine(answer.Result);
        if (answer.ErrorOccurred)
        {
            throw new InvalidOperationException("error in calculator for input " + input + " " + answer.LastErrorDescription);
        }

        string pattern = @"```\s*(.*?)\s*```";

        Match match = Regex.Match(answer.Result, pattern, RegexOptions.Singleline);
        if (match.Success)
        {
            var result = EvaluateMathExpression(match);
            return result;
        }

        Console.WriteLine(input);
        var e = new InvalidOperationException(
            $"Input value [{input}] could not be understood, received following {answer.Result} ");

        return await Task.FromException<string>(e).ConfigureAwait(false);
    }

    private static string EvaluateMathExpression(Match match)
    {
        var textExpressions = match.Groups[1].Value;
        var expr = new Expression(textExpressions, EvaluateOptions.IgnoreCase);
        expr.EvaluateParameter += delegate (string name, ParameterArgs args)
        {
            args.Result = name.ToLower(System.Globalization.CultureInfo.CurrentCulture) switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                _ => args.Result
            };
        };

        try
        {
            if (expr.HasErrors())
            {
                return "Error:" + expr.Error + " could not evaluate " + textExpressions;
            }

            var result = expr.Evaluate();
            return "Answer:" + result.ToString();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("could not evaluate " + textExpressions, e);
        }
    }
}
