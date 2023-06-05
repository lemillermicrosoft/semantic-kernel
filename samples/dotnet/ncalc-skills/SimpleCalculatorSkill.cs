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
///  Simple calculator skill
/// </summary>
public class SimpleCalculatorSkill
{
    private readonly ISKFunction _mathTranslator;

    public SimpleCalculatorSkill(IKernel kernel)
    {
        this._mathTranslator = kernel.CreateSemanticFunction(
            "{{$input}}",
            skillName: nameof(SimpleCalculatorSkill),
            functionName: "Calculator",
            description: "A valid mathematical expression that could be executed by a simple calculator.",
            maxTokens: 256,
            temperature: 0.0,
            topP: 1);
    }
}
