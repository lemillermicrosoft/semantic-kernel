// Copyright (c) Microsoft. All rights reserved.

using System.IO;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Skills;

public class PDFFileSkill
{
    //   _GLOBAL_FUNCTIONS_.ReadFile:
    //     description: Reads the content of a file as text
    //     inputs:
    //     - input: the path or name of the file to read

    [SKFunction("Reads the content of a file as text")]
    [SKFunctionInput(Description = "the path or name of the file to read")]
    [SKFunctionName("ReadPdfFile")]
    private SKContext ReadPdfFile(string input, SKContext context)
    {
        var fileContent = string.Empty;

        using var reader = File.OpenRead(input);

        using var pdfDocument = PdfDocument.Open(reader);
        foreach (var page in pdfDocument.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            fileContent += text;
        }

        context.Variables.Update(fileContent);
        return context;
    }
}
