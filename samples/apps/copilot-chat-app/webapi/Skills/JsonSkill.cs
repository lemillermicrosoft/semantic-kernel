// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace SemanticKernel.Service.Skills;

public partial class JsonSkill
{
    private readonly ISKFunction _selectFunction;

    // For-Each
    public JsonSkill(ISKFunction selectFunction)
    {
        this._selectFunction = selectFunction;
    }


    [SKFunction(description: "Selects one or more properties from a JSON object or array. Use when input is likely to be large.")]
    [SKFunctionName("Select")]
    [SKFunctionContextParameter(Name = "input", Description = "Input to iterate")]
    [SKFunctionContextParameter(Name = "properties", Description = "Properties to select")]
    public async Task<SKContext> SelectAsync(SKContext context)
    {
        context.Variables.Get("properties", out var properties);
        var forEachContext = context.Variables.Clone();
        if (!context.Variables.Get("input", out var input))
        {
            context.Log.LogError("Select: input not specified");
            return context;
        }

        var lines = TextChunker.SplitMarkDownLines(input, 512);
        var paragraphs = TextChunker.SplitMarkdownParagraphs(lines, 3000);

        var invokeContext = Utilities.CopyContextWithVariablesClone(context);

        string r = "";
        foreach (var p in paragraphs)
        {
            invokeContext.Variables.Update(p);
            var result = await this._selectFunction.InvokeAsync(invokeContext);
            if (result != null)
            {
                r += "\n" + result.Result.Replace("[END RESULT]", "", true, System.Globalization.CultureInfo.InvariantCulture).Trim();
                r = r.Trim();
            }
        }

        context.Variables.Update(r);
        return context;
    }
}
