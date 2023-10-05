// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.TypeChat;

namespace Microsoft.SemanticKernel.Planners.TypeChat;

/// <summary>
/// Program translator that translates user requests into programs that call APIs defined by Microsoft Semantic Kernel Plugins
/// </summary>
internal class PluginProgramTranslator
{
    private readonly IKernel _kernel;
    private readonly ProgramTranslator _translator;
    private readonly PluginApi _pluginApi;
    private SchemaText _pluginSchema;

    /// <summary>
    /// Create a new translator that will produce programs that can call all skills and
    /// plugins registered with the given semantic kernel
    /// </summary>
    /// <param name="kernel"></param>
    /// <param name="model"></param>
    public PluginProgramTranslator(IKernel kernel, ModelInfo model)
    {
        Verify.NotNull(kernel);
        this._kernel = kernel;
        // this._pluginApi = new PluginApi(this._kernel);
        // this._pluginSchema = this._pluginApi.TypeInfo.ExportSchema(this._pluginApi.TypeName);
        // this._translator = new ProgramTranslator(
        //     this._kernel.LanguageModel(model),
        //     new ProgramValidator(new PluginProgramValidator(this._pluginApi.TypeInfo)),
        //     this._pluginSchema
        // );
    }
    /// <summary>
    /// Translator being used
    /// </summary>
    public ProgramTranslator Translator => this._translator;
    /// <summary>
    /// Kernel this translator is working with
    /// </summary>
    public IKernel Kernel => this._kernel;
    /// <summary>
    /// The "API" formed by the various plugins registered with the kernel
    /// </summary>
    public PluginApi Api => _pluginApi;
    public SchemaText Schema => _pluginSchema;

    public Task<Program> TranslateAsync(string input, CancellationToken cancelToken)
    {
        return _translator.TranslateAsync(input, cancelToken);
    }
}

