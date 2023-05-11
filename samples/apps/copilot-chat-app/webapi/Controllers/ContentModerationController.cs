﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using SemanticKernel.Service.Model;
using SemanticKernel.Service.Services;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class ContentModerationController : ControllerBase
{
    private readonly ILogger<ContentModerationController> _logger;
    private readonly AzureContentModerator _contentModerator;

    /// <summary>
    /// The constructor of ContentModerationController.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="contentModerator">The content moderation service.</param>
    public ContentModerationController(
        ILogger<ContentModerationController> logger,
        AzureContentModerator contentModerator)
    {
        this._logger = logger;
        this._contentModerator = contentModerator;
    }

    /// <summary>
    /// Detect sensitive image content.
    /// </summary>
    /// <param name="base64Image">The base64 encoded image for analysis.</param>
    /// <returns>The HTTP action result.</returns>
    [Authorize]
    [HttpPost]
    [Route("contentModerator/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Dictionary<string, AnalysisResult>>> ImageAnalysisAsync(
        [FromBody] string base64Image)
    {
        return await this._contentModerator.ImageAnalysisAsync(base64Image, default);
    }
}
