﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Planning;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Model;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class BotController : ControllerBase
{
    private readonly ILogger<BotController> _logger;
    private readonly IMemoryStore _memoryStore;
    private readonly ChatSessionRepository _chatRepository;
    private readonly ChatMessageRepository _chatMessageRepository;
    private readonly BotSchemaOptions _botSchemaOptions;
    private readonly AIServiceOptions _embeddingOptions;
    private readonly DocumentMemoryOptions _documentMemoryOptions;

    /// <summary>
    /// The constructor of BotController.
    /// </summary>
    /// <param name="memoryStore">The memory store.</param>
    /// <param name="chatRepository">The chat session repository.</param>
    /// <param name="chatMessageRepository">The chat message repository.</param>
    /// <param name="aiServiceOptions">The AI service options where we need the embedding settings from.</param>
    /// <param name="botSchemaOptions">The bot schema options.</param>
    /// <param name="documentMemoryOptions">The document memory options.</param>
    /// <param name="logger">The logger.</param>
    public BotController(
        IMemoryStore memoryStore,
        ChatSessionRepository chatRepository,
        ChatMessageRepository chatMessageRepository,
        IOptionsSnapshot<AIServiceOptions> aiServiceOptions,
        IOptions<BotSchemaOptions> botSchemaOptions,
        IOptions<DocumentMemoryOptions> documentMemoryOptions,
        ILogger<BotController> logger)
    {
        this._logger = logger;
        this._memoryStore = memoryStore;
        this._chatRepository = chatRepository;
        this._chatMessageRepository = chatMessageRepository;
        this._botSchemaOptions = botSchemaOptions.Value;
        this._embeddingOptions = aiServiceOptions.Get(AIServiceOptions.EmbeddingPropertyName);
        this._documentMemoryOptions = documentMemoryOptions.Value;
    }

    /// <summary>
    /// Upload a bot.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="bot">The bot object from the message body</param>
    /// <returns>The HTTP action result.</returns>
    [Authorize]
    [HttpPost]
    [Route("bot/upload")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadAsync(
        [FromServices] IKernel kernel,
        [FromQuery] string userId,
        [FromBody] Bot bot)
    {
        // TODO: We should get userId from server context instead of from request for privacy/security reasons when support multiple users.
        this._logger.LogDebug("Received call to upload a bot");

        if (!IsBotCompatible(
                externalBotSchema: bot.Schema,
                externalBotEmbeddingConfig: bot.EmbeddingConfigurations,
                embeddingOptions: this._embeddingOptions,
                botSchemaOptions: this._botSchemaOptions))
        {
            return this.BadRequest("Incompatible schema. " +
                                   $"The supported bot schema is {this._botSchemaOptions.Name}/{this._botSchemaOptions.Version} " +
                                   $"for the {this._embeddingOptions.DeploymentOrModelId} model from {this._embeddingOptions.AIService}. " +
                                   $"But the uploaded file is with schema {bot.Schema.Name}/{bot.Schema.Version} " +
                                   $"for the {bot.EmbeddingConfigurations.DeploymentOrModelId} model from {bot.EmbeddingConfigurations.AIService}.");
        }

        string chatTitle = $"{bot.ChatTitle}";
        string chatId = string.Empty;

        // Upload chat history into chat repository and embeddings into memory.
        try
        {
            // 1. Create a new chat and get the chat id.
            var newChat = new ChatSession(userId, chatTitle);
            await this._chatRepository.CreateAsync(newChat);
            chatId = newChat.Id;

            string oldChatId = bot.ChatHistory.First().ChatId;

            // 2. Update the app's chat storage.
            foreach (var message in bot.ChatHistory)
            {
                var chatMessage = new ChatMessage(message.UserId, message.UserName, chatId, message.Content, ChatMessage.AuthorRoles.Participant)
                {
                    Timestamp = message.Timestamp
                };
                await this._chatMessageRepository.CreateAsync(chatMessage);
            }

            // 3. Update the memory.
            await this.BulkUpsertMemoryRecordsAsync(oldChatId, chatId, bot.Embeddings);

            // 4. Update the document memory.
            await this.BulkUpsertMemoryRecordsAsync(oldChatId, chatId, bot.DocumentEmbeddings);
        }
        catch
        {
            // TODO: Revert changes if any of the actions failed
            throw;
        }

        return this.Accepted();
    }

    /// <summary>
    /// Download a bot.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance.</param>
    /// <param name="chatId">The chat id to be downloaded.</param>
    /// <param name="userId">The id of the current user and its home tenant.</param>
    /// <returns>The serialized Bot object of the chat id.</returns>
    [Authorize]
    [HttpGet]
    [Route("bot/download/{chatId:guid}/{userId:regex(([[a-z0-9]]+-)+[[a-z0-9]]+\\.([[a-z0-9]]+-)+[[a-z0-9]]+)}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> DownloadAsync(
        [FromServices] IKernel kernel,
        Guid chatId,
        string userId)
    {
        // TODO: get thh userId from the AAD token/claim.
        this._logger.LogDebug("Received call to download a bot");
        var memory = await this.CreateBotAsync(
            kernel: kernel,
            chatId: chatId,
            userId: userId,
            planOnly: false);

        return JsonSerializer.Serialize(memory);
    }


    /// <summary>
    /// Download a lesson bot.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance.</param>
    /// <param name="chatId">The chat id to be downloaded.</param>
    /// <param name="userId">The id of the current user and its home tenant.</param>
    /// <returns>The serialized Bot object of the chat id.</returns>
    [Authorize]
    [HttpGet]
    [Route("lessonbot/download/{chatId:guid}/{userId:regex(([[a-z0-9]]+-)+[[a-z0-9]]+\\.([[a-z0-9]]+-)+[[a-z0-9]]+)}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> DownloadLessonAsync(
        [FromServices] IKernel kernel,
        Guid chatId,
        string userId)
    {
        // TODO: get thh userId from the AAD token/claim.
        this._logger.LogDebug("Received call to download a bot");
        var memory = await this.CreateBotAsync(
            kernel: kernel,
            chatId: chatId,
            userId: userId,
            planOnly: true);

        return JsonSerializer.Serialize(memory);
    }

    /// <summary>
    /// Check if an external bot file is compatible with the application.
    /// </summary>
    /// <remarks>
    /// If the embeddings are not generated from the same model, the bot file is not compatible.
    /// </remarks>
    /// <param name="externalBotSchema">The external bot schema.</param>
    /// <param name="externalBotEmbeddingConfig">The external bot embedding configuration.</param>
    /// <param name="embeddingOptions">The embedding options.</param>
    /// <param name="botSchemaOptions">The bot schema options.</param>
    /// <returns>True if the bot file is compatible with the app; otherwise false.</returns>
    private static bool IsBotCompatible(
        BotSchemaOptions externalBotSchema,
        BotEmbeddingConfig externalBotEmbeddingConfig,
        AIServiceOptions embeddingOptions,
        BotSchemaOptions botSchemaOptions)
    {
        // The app can define what schema/version it supports before the community comes out with an open schema.
        return externalBotSchema.Name.Equals(botSchemaOptions.Name, StringComparison.OrdinalIgnoreCase)
               && externalBotSchema.Version == botSchemaOptions.Version
               && externalBotEmbeddingConfig.AIService == embeddingOptions.AIService
               && externalBotEmbeddingConfig.DeploymentOrModelId.Equals(embeddingOptions.DeploymentOrModelId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get memory from memory store and append the memory records to a given list.
    /// It will update the memory collection name in the new list if the newCollectionName is provided.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel instance.</param>
    /// <param name="collectionName">The current collection name. Used to query the memory storage.</param>
    /// <param name="embeddings">The embeddings list where we will append the fetched memory records.</param>
    /// <param name="newCollectionName">The new collection name when appends to the embeddings list.</param>
    private static async Task GetMemoryRecordsAndAppendToEmbeddingsAsync(
        IKernel kernel,
        string collectionName,
        List<KeyValuePair<string, List<MemoryQueryResult>>> embeddings,
        string newCollectionName = "")
    {
        List<MemoryQueryResult> collectionMemoryRecords = await kernel.Memory.SearchAsync(
            collectionName,
            "abc", // dummy query since we don't care about relevance. An empty string will cause exception.
            limit: 999999999, // temp solution to get as much as record as a workaround.
            minRelevanceScore: -1, // no relevance required since the collection only has one entry
            withEmbeddings: true,
            cancellationToken: default
        ).ToListAsync();

        embeddings.Add(new KeyValuePair<string, List<MemoryQueryResult>>(
            string.IsNullOrEmpty(newCollectionName) ? collectionName : newCollectionName,
            collectionMemoryRecords));
    }

    /// <summary>
    /// Prepare the bot information of a given chat.
    /// </summary>
    /// <param name="kernel">The semantic kernel object.</param>
    /// <param name="chatId">The chat id of the bot</param>
    /// <param name="userId">The id of the current user and its home tenant.</param>
    /// <param name="planOnly"></param>
    /// <returns>A Bot object that represents the chat session.</returns>
    private async Task<Bot> CreateBotAsync(
        IKernel kernel,
        Guid chatId,
        string userId,
        bool planOnly)
    {
        var chatIdString = chatId.ToString();
        var bot = new Bot
        {
            // get the bot schema version
            Schema = this._botSchemaOptions,

            // get the embedding configuration
            EmbeddingConfigurations = new BotEmbeddingConfig
            {
                AIService = this._embeddingOptions.AIService,
                DeploymentOrModelId = this._embeddingOptions.DeploymentOrModelId
            }
        };

        // TODO Specify lesson some way
        var memories = kernel.Memory.SearchAsync($"{chatIdString}-LearningSkill.LessonPlans", query: "lesson", limit: 1, minRelevanceScore: 0.0)
            .ToEnumerable()
            .ToList();
        if (!planOnly || memories.Count == 0)
        {
            // no lessons to save
            // get the chat title
            ChatSession chat = await this._chatRepository.FindByIdAsync(chatIdString);
            bot.ChatTitle = $"{chat.Title} - Clone";

            // get the chat history
            bot.ChatHistory = await this.GetAllChatMessagesAsync(chatIdString);
        }
        else
        {
            var lessonPlanJson = memories[0].Metadata.AdditionalMetadata;
            var lessonPlan = Plan.FromJson(lessonPlanJson);
            var lessonStepIndex = lessonPlan.NextStepIndex;

            bot.ChatTitle = lessonPlan.Name;
            bot.ChatHistory = new List<ChatMessage>()
            {
                // TODO - How to use lessonPLan.Name for all future messages?
                new ChatMessage(userId, lessonPlan.Name, chatIdString, lessonPlan.Description, ChatMessage.AuthorRoles.Bot),

                // TODO how to shows the learning plan with buttons to start or save.
                new ChatMessage(userId, lessonPlan.Name, chatIdString, lessonPlanJson, ChatMessage.AuthorRoles.Bot)
            };

            // TODO automatically make it so the plan is ready to instruct.
        }

        // get the memory collections associated with this chat
        // TODO: filtering memory collections by name might be fragile.
        var chatCollections = (await kernel.Memory.GetCollectionsAsync())
            .Where(collection => collection.StartsWith(chatIdString, StringComparison.OrdinalIgnoreCase));

        foreach (var collection in chatCollections)
        {
            // TODO: Final Validation. LessonPlan follows naming pattern so this should work in some fashion.
            await GetMemoryRecordsAndAppendToEmbeddingsAsync(kernel: kernel, collectionName: collection, embeddings: bot.Embeddings);
        }

        // get the document memory collection names (global scope)
        await GetMemoryRecordsAndAppendToEmbeddingsAsync(
            kernel: kernel,
            collectionName: this._documentMemoryOptions.GlobalDocumentCollectionName,
            embeddings: bot.DocumentEmbeddings);

        // get the document memory collection names (user scope)
        await GetMemoryRecordsAndAppendToEmbeddingsAsync(
            kernel: kernel,
            collectionName: this._documentMemoryOptions.UserDocumentCollectionNamePrefix + userId,
            embeddings: bot.DocumentEmbeddings,
            // replace userId with chat id.
            // Note: workaround solution: doc memory will become chat session scope during export, until we make doc memory a chat session scope by default.
            newCollectionName: this._documentMemoryOptions.UserDocumentCollectionNamePrefix + chatIdString);

        return bot;
    }

    /// <summary>
    /// Get chat messages of a given chat id.
    /// </summary>
    /// <param name="chatId">The chat id</param>
    /// <returns>The list of chat messages in descending order of the timestamp</returns>
    private async Task<List<ChatMessage>> GetAllChatMessagesAsync(string chatId)
    {
        // TODO: We might want to set limitation on the number of messages that are pulled from the storage.
        return (await this._chatMessageRepository.FindByChatIdAsync(chatId))
            .OrderByDescending(m => m.Timestamp).ToList();
    }

    /// <summary>
    /// Bulk upsert memory records into memory store.
    /// </summary>
    /// <param name="oldChatId">The original chat id of the memory records.</param>
    /// <param name="chatId">The new chat id that will replace the original chat id.</param>
    /// <param name="embeddings">The list of embeddings of the chat id.</param>
    /// <returns>The function doesn't return anything.</returns>
    private async Task BulkUpsertMemoryRecordsAsync(string oldChatId, string chatId, List<KeyValuePair<string, List<MemoryQueryResult>>> embeddings)
    {
        foreach (var collection in embeddings)
        {
            foreach (var record in collection.Value)
            {
                if (record != null && record.Embedding != null)
                {
                    var newCollectionName = collection.Key.Replace(oldChatId, chatId, StringComparison.OrdinalIgnoreCase);

                    MemoryRecord data = MemoryRecord.LocalRecord(
                        id: record.Metadata.Id,
                        text: record.Metadata.Text,
                        embedding: record.Embedding.Value,
                        description: record.Metadata.Description, additionalMetadata: record.Metadata.AdditionalMetadata);

                    if (!await this._memoryStore.DoesCollectionExistAsync(newCollectionName, default))
                    {
                        await this._memoryStore.CreateCollectionAsync(newCollectionName, default);
                    }

                    await this._memoryStore.UpsertAsync(newCollectionName, data, default);
                }
            }
        }
    }
}
