// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.TemplateEngine;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Services;
using SemanticKernel.Service.Skills;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service;

internal static class FunctionLoadingExtensions
{
    /// <summary>
    /// Register local semantic skills with the kernel.
    /// </summary>
    internal static void RegisterSemanticSkills(
        this IKernel kernel,
        string skillsDirectory,
        ILogger logger)
    {
        string[] subDirectories = Directory.GetDirectories(skillsDirectory);

        foreach (string subDir in subDirectories)
        {
            try
            {
                kernel.ImportSemanticSkillFromDirectory(skillsDirectory, Path.GetFileName(subDir)!);
            }
            catch (TemplateException e)
            {
                logger.LogError("Could not load skill from {Directory}: {Message}", subDir, e.Message);
            }
        }
    }

    /// <summary>
    /// Register local semantic skills with the kernel.
    /// </summary>
    internal static IDictionary<string, ISKFunction> RegisterNamedSemanticSkills(
        this IKernel kernel,
        string? skillsDirectory = null,
        ILogger? logger = null,
        params string[] skillDirectoryNames
    )
    {
        skillsDirectory ??= SampleSkillsPath();

        return kernel.ImportSemanticSkillFromDirectory(skillsDirectory, skillDirectoryNames);
    }

    /// <summary>
    /// Scan the local folders from the repo, looking for "webapi/skills" folder.
    /// </summary>
    /// <returns>The full path to webapi/skills</returns>
    internal static string SampleSkillsPath()
    {
        const string Parent = "webapi";
        const string Folder = "skills";

        static bool SearchPath(string pathToFind, out string result, int maxAttempts = 10)
        {
            var currDir = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            bool found;
            do
            {
                result = Path.Join(currDir, pathToFind);
                found = Directory.Exists(result);
                currDir = Path.GetFullPath(Path.Combine(currDir, ".."));
            } while (maxAttempts-- > 0 && !found);

            return found;
        }

        return !SearchPath(Parent + Path.DirectorySeparatorChar + Folder, out string path)
               && !SearchPath(Folder, out path)
            ? throw new DirectoryNotFoundException("Skills directory not found. The app needs the skills from the repo to work.")
            : path;
    }

    /// <summary>
    /// Register native skills with the kernel.
    /// </summary>
    internal static void RegisterNativeSkills(
        this IKernel kernel,
        IKernel chatKernel,
        IKernel assistantKernel,
        ChatSessionRepository chatSessionRepository,
        ChatMessageRepository chatMessageRepository,
        PromptSettings promptSettings,
        CopilotChatPlanner planner,
        PlannerOptions plannerOptions,
        DocumentMemoryOptions documentMemoryOptions,
        AzureContentModerator contentModerator,
        ILogger logger)
    {
        // Hardcode your native function registrations here

        var timeSkill = new TimeSkill();
        kernel.ImportSkill(timeSkill, nameof(TimeSkill));

        // I was hoping this wouldn't be needed but it is...
        var chatSkill = new ChatSkill(
            kernel: kernel,
            actionKernel: chatKernel,
            assistantKernel: assistantKernel,
            chatMessageRepository: chatMessageRepository,
            chatSessionRepository: chatSessionRepository,
            promptSettings: promptSettings,
            planner: planner,
            plannerOptions: plannerOptions,
            contentModerator: contentModerator,
            logger: logger
        );
        kernel.ImportSkill(chatSkill, nameof(ChatSkill));

        // TODO - This is bringing in too much -- Move DoChat into it's own skill?
        chatKernel.ImportSkill(chatSkill, nameof(ChatSkill));

        var documentMemorySkill = new DocumentMemorySkill(promptSettings, documentMemoryOptions);
        kernel.ImportSkill(documentMemorySkill, nameof(DocumentMemorySkill));
    }
}
