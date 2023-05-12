// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ImageGeneration;
using Microsoft.SemanticKernel.Diagnostics;

namespace Microsoft.SemanticKernel.Connectors.HuggingFace.TextToImage;

/// <summary>
/// HuggingFace text completion service.
/// </summary>
public sealed class HuggingFaceTextToImage : IImageGeneration, IDisposable
{
    private const string HttpUserAgent = "Microsoft-Semantic-Kernel";
    private const string HuggingFaceApiEndpoint = "https://api-inference.huggingface.co/models";

    private readonly string _model;
    private readonly Uri _endpoint;
    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler? _httpClientHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceTextToImage"/> class.
    /// </summary>
    /// <param name="endpoint">Endpoint for service API call.</param>
    /// <param name="model">Model to use for service API call.</param>
    /// <param name="httpClientHandler">Instance of <see cref="HttpClientHandler"/> to setup specific scenarios.</param>
    public HuggingFaceTextToImage(Uri endpoint, string model, HttpClientHandler httpClientHandler)
    {
        Verify.NotNull(endpoint);
        Verify.NotNullOrWhiteSpace(model);

        this._endpoint = endpoint;
        this._model = model;

        this._httpClient = new(httpClientHandler);

        this._httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceTextToImage"/> class.
    /// Using default <see cref="HttpClientHandler"/> implementation.
    /// </summary>
    /// <param name="endpoint">Endpoint for service API call.</param>
    /// <param name="model">Model to use for service API call.</param>
    public HuggingFaceTextToImage(Uri endpoint, string model)
    {
        Verify.NotNull(endpoint);
        Verify.NotNullOrWhiteSpace(model);

        this._endpoint = endpoint;
        this._model = model;

        this._httpClientHandler = new() { CheckCertificateRevocationList = true };
        this._httpClient = new(this._httpClientHandler);

        this._httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceTextToImage"/> class.
    /// Using HuggingFace API for service call, see https://huggingface.co/docs/api-inference/index.
    /// </summary>
    /// <param name="apiKey">HuggingFace API key, see https://huggingface.co/docs/api-inference/quicktour#running-inference-with-api-requests.</param>
    /// <param name="model">Model to use for service API call.</param>
    /// <param name="httpClientHandler">Instance of <see cref="HttpClientHandler"/> to setup specific scenarios.</param>
    /// <param name="endpoint">Endpoint for service API call.</param>
    public HuggingFaceTextToImage(string apiKey, string model, HttpClientHandler httpClientHandler, string endpoint = HuggingFaceApiEndpoint)
        : this(new Uri(endpoint), model, httpClientHandler)
    {
        Verify.NotNullOrWhiteSpace(apiKey);

        this._httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceTextToImage"/> class.
    /// Using HuggingFace API for service call, see https://huggingface.co/docs/api-inference/index.
    /// Using default <see cref="HttpClientHandler"/> implementation.
    /// </summary>
    /// <param name="apiKey">HuggingFace API key, see https://huggingface.co/docs/api-inference/quicktour#running-inference-with-api-requests.</param>
    /// <param name="model">Model to use for service API call.</param>
    /// <param name="endpoint">Endpoint for service API call.</param>
    public HuggingFaceTextToImage(string apiKey, string model, string endpoint = HuggingFaceApiEndpoint)
        : this(new Uri(endpoint), model)
    {
        Verify.NotNullOrWhiteSpace(apiKey);

        this._httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    /// <inheritdoc/>
    public async Task<string> GenerateImageAsync(
        string description,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var imageGenerationRequest = new TextToImageRequest
            {
                Input = description
            };

            using var httpRequestMessage = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{this._endpoint}/{this._model}"),
                Content = new StringContent(JsonSerializer.Serialize(imageGenerationRequest))
            };

            var response = await this._httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new AIException(
                    AIException.ErrorCodes.ServiceError,
                    $"Failed to call {this._model} model. {response.StatusCode}.");
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            return $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        }
        catch (Exception e) when (e is not AIException && !e.IsCriticalException())
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._httpClient.Dispose();
        this._httpClientHandler?.Dispose();
    }
}
