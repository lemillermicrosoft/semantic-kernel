// Copyright (c) Microsoft. All rights reserved.

using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.CoreSkills;

/// <summary>
/// Read and write from a file.
/// </summary>
/// <example>
/// Usage: kernel.ImportSkill("file", new FileIOSkill());
/// Examples:
/// {{file.readAsync $path }} => "hello world"
/// {{file.writeAsync}}
/// </example>
public class FileIOSkill
{
    /// <summary>
    /// Read a file
    /// </summary>
    /// <example>
    /// {{file.readAsync $path }} => "hello world"
    /// </example>
    /// <param name="path"> Source file </param>
    /// <returns> File content </returns>
    [SKFunction("Read a file")]
    [SKFunctionInput(Description = "Source file")]
    public async Task<string> ReadAsync(string path)
    {
        using var reader = File.OpenText(path);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Write a file
    /// </summary>
    /// <example>
    /// {{file.writeAsync}}
    /// </example>
    /// <param name="context">
    /// Contains the 'path' for the Destination file and 'content' of the file to write.
    /// </param>
    /// <returns> An awaitable task </returns>
    [SKFunction("Write a file")]
    [SKFunctionContextParameter(Name = "path", Description = "Destination file")]
    [SKFunctionContextParameter(Name = "content", Description = "File content")]
    public async Task WriteAsync(SKContext context)
    {
        byte[] text = Encoding.UTF8.GetBytes(context["content"]);
        using var writer = File.OpenWrite(context["path"]);
        await writer.WriteAsync(text, 0, text.Length).ConfigureAwait(false);
    }

    //     _GLOBAL_FUNCTIONS_.GetFolderContents:
    //     description: Gets the list of files and subfolders in a given folder
    //     inputs:
    //     - input: the path or name of the folder to scan
    [SKFunction("Gets the list of files and subfolders in a given folder")]
    [SKFunctionInput(Description = "the path or name of the folder to scan")]
    [SKFunctionName("GetFolderContents")]
    public Task<SKContext> GetFolderContentsAsync(string input, SKContext context)
    {
        var files = Directory.GetFiles(input);
        var folders = Directory.GetDirectories(input);
        context.Variables["files"] = JsonSerializer.Serialize(files);
        context.Variables.Update(JsonSerializer.Serialize(files));
        context.Variables["folders"] = JsonSerializer.Serialize(files);
        return Task.FromResult(context);
    }

    //   _GLOBAL_FUNCTIONS_.FilterByExtension:
    //     description: Filters a list of files by their extension
    //     inputs:
    //     - input: the list of files to filter
    //     - extension: the extension to filter by, such as .pdf or .docx
    [SKFunction("Filters a list of files by their extension")]
    [SKFunctionInput(Description = "the list of files to filter")]
    [SKFunctionContextParameter(Name = "extension", Description = "the extension to filter by, such as .pdf or .docx")]
    [SKFunctionName("FilterByExtension")]
    public Task<SKContext> FilterByExtensionAsync(string input, SKContext context)
    {
        var files = JsonSerializer.Deserialize<string[]>(input);
        var extension = context["extension"];
        var filteredFiles = files.Where(f => Path.GetExtension(f) == extension);
        context.Variables["files"] = JsonSerializer.Serialize(filteredFiles);
        context.Variables.Update(JsonSerializer.Serialize(filteredFiles));
        return Task.FromResult(context);
    }
}
