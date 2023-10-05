// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.TypeChat;

namespace Microsoft.SemanticKernel.Planners;

/// <summary>
/// Validates programs produced by PluginProgramTranslator.
/// Ensures that function calls are to existing plugins with matching parameters
/// </summary>
public class PluginProgramValidator : ProgramVisitor, IProgramValidator
{
    PluginApiTypeInfo _typeInfo;

    public PluginProgramValidator(PluginApiTypeInfo typeInfo)
    {
        _typeInfo = typeInfo;
    }

    public Result<Microsoft.TypeChat.Program> ValidateProgram(Microsoft.TypeChat.Program program)
    {
        try
        {
            Visit(program);
            return program;
        }
        catch (Exception ex)
        {
            return Result<Microsoft.TypeChat.Program>.Error(program, ex.Message);
        }
    }

    protected override void VisitFunction(FunctionCall functionCall)
    {
        try
        {
            // Verify function exists
            var name = PluginFunctionName.Parse(functionCall.Name);
            // var typeInfo = _typeInfo[name]; TODO
            var typeInfo = new FunctionView(name.FunctionName, name.PluginName, "a description");// todo
            // Verify that parameter counts etc match
            ValidateArgCounts(functionCall, typeInfo, functionCall.Args);
            // Continue visiting to handle any nested function calls
            base.VisitFunction(functionCall);
            return;
        }
        catch (ProgramException)
        {
            throw;
        }
        catch { }
        ProgramException.ThrowFunctionNotFound(functionCall.Name);
    }

    void ValidateArgCounts(FunctionCall call, FunctionView typeInfo, Expression[] args)
    {
        int expectedCount = (typeInfo.Parameters != null) ? typeInfo.Parameters.Count : 0;
        int actualCount = (args != null) ? args.Length : 0;
        if (actualCount != expectedCount)
        {
            // TODO this is the bug with bing sometimes
            ProgramException.ThrowArgCountMismatch(call, expectedCount, actualCount);
        }
    }
}
