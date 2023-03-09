// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Reliability;
using Moq;
using Xunit;

namespace SemanticKernelTests.Reliability;

public class NullRetryPolicyTests
{
    [Fact]
    public async Task ItDoesNotRetryOnExceptionAsync()
    {
        // Arrange
        var retry = new NullRetryPolicy();
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        action.Verify(a => a(), Times.Once);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task NoExceptionNoRetryAsync()
    {
        // Arrange
        var log = new Mock<ILogger>();
        var retry = new NullRetryPolicy();
        var action = new Mock<Func<Task<HttpResponseMessage>>>();

        // Act
        await retry.ExecuteWithRetryAsync(action.Object, log.Object);

        // Assert
        action.Verify(a => a(), Times.Once);
    }

    [Fact]
    public async Task ItDoesNotExecuteOnCancellationTokenAsync()
    {
        // Arrange
        var retry = new NullRetryPolicy();
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>(), new CancellationToken(true)));

        // Assert
        action.Verify(a => a(), Times.Never);
    }

    [Fact]
    public async Task ItDoestExecuteOnFalseCancellationTokenAsync()
    {
        // Arrange
        var retry = new NullRetryPolicy();
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>(), new CancellationToken(false));

        // Assert
        action.Verify(a => a(), Times.Once);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
