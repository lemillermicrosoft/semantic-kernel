// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Skills.MsGraph.Models;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Services;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class ContentModerationController : ControllerBase
{
    private readonly ILogger<ContentModerationController> _logger;
    private readonly ContentModerationOptions _options;
    private readonly AzureContentModerator _contentModerator;

    /// <summary>
    /// The constructor of ContentModerationController.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="contentModerationOptions">The content moderation options.</param>
    /// <param name="contentModerator">The content moderation service.</param>
    public ContentModerationController(
        ILogger<ContentModerationController> logger,
        IOptions<ContentModerationOptions> contentModerationOptions,
        AzureContentModerator contentModerator)
    {
        this._logger = logger;
        this._contentModerator = contentModerator;
        this._options = contentModerationOptions.Value;
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet]
    [Route("contentModerator/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public bool ContentModerationStatus()
    {
        return this._options.Enabled;
    }
}
