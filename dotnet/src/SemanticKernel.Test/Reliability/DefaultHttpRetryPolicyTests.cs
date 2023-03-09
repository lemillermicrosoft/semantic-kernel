// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Reliability;
using Moq;
using Xunit;
using static Microsoft.SemanticKernel.Configuration.KernelConfig;

namespace SemanticKernelTests.Reliability;

public class DefaultHttpRetryPolicyTests
{
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ItRetriesOnceOnRetryableStatusAsync(HttpStatusCode statusCode)
    {
        // Arrange
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig());
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(statusCode);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(statusCode, response.StatusCode);
    }

    [Fact]
    public async Task NoMaxMaxRetryCountCallsOnceAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig() { MaxRetryCount = 0 }, mockDelayProvider.Object, mockTimeProvider.Object);
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
    public async Task NegativeMaxRetryCountThrowsAsync()
    {
        // Act
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            var httpRetryConfig = new HttpRetryConfig() { MaxRetryCount = -1 };
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ItRetriesOnceOnExceptionWithExponentialBackoffAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig() { UseExponentialBackoff = true }, mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ItRetriesExponentiallyWithExponentialBackoffAsync()
    {
        // Arrange
        var HttpRetryConfig = new HttpRetryConfig
        {
            MaxRetryCount = 3,
            UseExponentialBackoff = true,
            MinRetryDelay = TimeSpan.FromMilliseconds(500),
        };

        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();

        var currentTime = DateTimeOffset.UtcNow;
        mockTimeProvider.SetupSequence(x => x.GetCurrentTime())
            .Returns(() => currentTime)
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(5))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(510))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(1015))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(1520));

        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        var retry = new DefaultHttpRetryPolicy(HttpRetryConfig, mockDelayProvider.Object, mockTimeProvider.Object);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        mockTimeProvider.Verify(x => x.GetCurrentTime(), Times.Exactly(4));
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None), Times.Once);
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(1000), CancellationToken.None), Times.Once);
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(2000), CancellationToken.None), Times.Once);

        action.Verify(a => a(), Times.Exactly(4));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ItRetriesOnceOnExceptionWithRetryValueAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig(), mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.TooManyRequests,
            Headers = { RetryAfter = new RetryConditionHeaderValue(new TimeSpan(0, 0, 0, 1)) },
        };
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(new TimeSpan(0, 0, 0, 1), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task ItRetriesCustomCountAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig() { MaxRetryCount = 3 }, mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        action.Verify(a => a(), Times.Exactly(4));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task NoExceptionNoRetryAsync()
    {
        // Arrange
        var log = new Mock<ILogger>();
        var retry = new DefaultHttpRetryPolicy();
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.OK);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        await retry.ExecuteWithRetryAsync(action.Object, log.Object);

        // Assert
        action.Verify(a => a(), Times.Once);
    }

    [Fact]
    public async Task ItDoesNotExecuteOnCancellationTokenAsync()
    {
        // Arrange
        var retry = new DefaultHttpRetryPolicy();
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
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig(), mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>(), new CancellationToken(false));

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await response).StatusCode);
    }

    [Fact]
    public async Task ItRetriesWithMinRetryDelayAsync()
    {
        // Arrange
        var HttpRetryConfig = new HttpRetryConfig
        {
            MinRetryDelay = TimeSpan.FromMilliseconds(500)
        };

        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();

        var currentTime = DateTimeOffset.UtcNow;
        mockTimeProvider.SetupSequence(x => x.GetCurrentTime())
            .Returns(() => currentTime)
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(5))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(510));

        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        var retry = new DefaultHttpRetryPolicy(HttpRetryConfig, mockDelayProvider.Object, mockTimeProvider.Object);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        mockTimeProvider.Verify(x => x.GetCurrentTime(), Times.Exactly(2));
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None), Times.Once);

        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ItRetriesWithMaxRetryDelayAsync()
    {
        // Arrange
        var HttpRetryConfig = new HttpRetryConfig
        {
            MinRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(500)
        };

        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();

        var currentTime = DateTimeOffset.UtcNow;
        mockTimeProvider.SetupSequence(x => x.GetCurrentTime())
            .Returns(() => currentTime)
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(5))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(505));

        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(2000)) } };
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        var retry = new DefaultHttpRetryPolicy(HttpRetryConfig, mockDelayProvider.Object, mockTimeProvider.Object);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>(), CancellationToken.None);

        // Assert
        mockTimeProvider.Verify(x => x.GetCurrentTime(), Times.Exactly(2));
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None), Times.Once);

        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromMilliseconds(2000), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task ItRetriesWithMaxTotalDelayAsync()
    {
        // Arrange
        var HttpRetryConfig = new HttpRetryConfig
        {
            MaxRetryCount = 5,
            MinRetryDelay = TimeSpan.FromMilliseconds(50),
            MaxRetryDelay = TimeSpan.FromMilliseconds(50),
            MaxTotalRetryTime = TimeSpan.FromMilliseconds(350)
        };

        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();

        var currentTime = DateTimeOffset.UtcNow;
        mockTimeProvider.SetupSequence(x => x.GetCurrentTime())
            .Returns(() => currentTime)
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(5))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(55))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(110))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(165))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(220))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(275))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(330));

        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        var retry = new DefaultHttpRetryPolicy(HttpRetryConfig, mockDelayProvider.Object, mockTimeProvider.Object);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>(), CancellationToken.None);

        // Assert
        mockTimeProvider.Verify(x => x.GetCurrentTime(), Times.Exactly(6)); // one for the initial call, and one for each of 5 attempts
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None), Times.Exactly(5));

        action.Verify(a => a(), Times.Exactly(6));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ItRetriesFewerWithMaxTotalDelayAsync()
    {
        // Arrange
        var HttpRetryConfig = new HttpRetryConfig
        {
            MaxRetryCount = 5,
            MinRetryDelay = TimeSpan.FromMilliseconds(60),
            MaxRetryDelay = TimeSpan.FromMilliseconds(60),
            MaxTotalRetryTime = TimeSpan.FromMilliseconds(100)
        };

        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();

        var currentTime = DateTimeOffset.UtcNow;
        mockTimeProvider.SetupSequence(x => x.GetCurrentTime())
            .Returns(() => currentTime)
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(5))
            .Returns(() => currentTime + TimeSpan.FromMilliseconds(65));

        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        var retry = new DefaultHttpRetryPolicy(HttpRetryConfig, mockDelayProvider.Object, mockTimeProvider.Object);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>(), CancellationToken.None);

        // Assert
        mockTimeProvider.Verify(x => x.GetCurrentTime(), Times.Exactly(3)); // one for the initial call, and one for each of 2 attempts
        mockDelayProvider.Verify(x => x.DelayAsync(TimeSpan.FromMilliseconds(60), CancellationToken.None), Times.Once);

        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ItRetriesOnRetryableStatusCodesAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig() { RetryableStatusCodes = new List<HttpStatusCode> { HttpStatusCode.Unauthorized } },
            mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        using var mockResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        action.Setup(a => a()).ReturnsAsync(mockResponse);

        // Act
        var response = await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>());

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ItDoesNotRetryOnNonRetryableStatusCodesAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig() { RetryableStatusCodes = new List<HttpStatusCode> { HttpStatusCode.Unauthorized } },
            mockDelayProvider.Object, mockTimeProvider.Object);
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
    public async Task ItRetriesOnRetryableExceptionsAsync()
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig() { RetryableExceptionTypes = new List<Type> { typeof(InvalidOperationException) } },
            mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        action.Setup(a => a()).ThrowsAsync(new InvalidOperationException());

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>()));

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
    }

    [Theory]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(WebException))]
    [InlineData(typeof(HttpRequestException))]
    public async Task ItRetriesOnRetryableExceptionsByDefaultAsync(Type exceptionType)
    {
        // Arrange
        var mockDelayProvider = new Mock<DefaultHttpRetryPolicy.IDelayProvider>();
        var mockTimeProvider = new Mock<DefaultHttpRetryPolicy.ITimeProvider>();
        var retry = new DefaultHttpRetryPolicy(new HttpRetryConfig(),
            mockDelayProvider.Object, mockTimeProvider.Object);
        var action = new Mock<Func<Task<HttpResponseMessage>>>();
        action.Setup(a => a()).ThrowsAsync(Activator.CreateInstance(exceptionType) as Exception);

        // Act
        await Assert.ThrowsAsync(exceptionType, async () => await retry.ExecuteWithRetryAsync(action.Object, Mock.Of<ILogger>()));

        // Assert
        action.Verify(a => a(), Times.Exactly(2));
    }
}
