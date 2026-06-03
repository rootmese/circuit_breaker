using CircuitBreaker.Core;
using Polly.CircuitBreaker;
using CircuitBreakerState = CircuitBreaker.Core.CircuitState;

namespace CircuitBreaker.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAction_ReturnsResult()
    {
        // Arrange
        var breaker = CircuitBreakerFactory.Create(new CircuitBreakerOptions(), "TestResource");
        var expected = "Success";

        // Act
        var result = await breaker.ExecuteAsync(async () => await Task.FromResult(expected));

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ThrowsException()
    {
        // Arrange
        var breaker = CircuitBreakerFactory.Create(new CircuitBreakerOptions(), "TestResource");
        var exception = new InvalidOperationException("Test error");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await breaker.ExecuteAsync(async () =>
            {
                await Task.Delay(1);
                throw exception;
            });
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var breaker = CircuitBreakerFactory.Create(new CircuitBreakerOptions(), "TestResource");
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);
        var exceptionThrown = false;

        // Act
        try
        {
            await breaker.ExecuteAsync(async ct =>
            {
                await Task.Delay(1000, ct);
                return "Should not complete";
            }, cts.Token);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            exceptionThrown = true;
        }

        // Assert
        Assert.True(exceptionThrown, "CancellationToken should have been propagated");
    }

    [Fact]
    public async Task State_StartsAsClosed()
    {
        // Arrange & Act
        var breaker = CircuitBreakerFactory.Create(new CircuitBreakerOptions(), "TestResource");

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, breaker.State);
    }

    [Fact]
    public async Task OnOpened_CallbackInvoked_WhenCircuitOpens()
    {
        // Arrange
        var callbackInvoked = false;
        TimeSpan breakDuration = TimeSpan.Zero;

        var breaker = CircuitBreakerFactory.Create(
            new CircuitBreakerOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(1),
                BreakDuration = TimeSpan.FromSeconds(2),
                OnOpened = d =>
                {
                    callbackInvoked = true;
                    breakDuration = d;
                }
            },
            "TestResource");

        // Act
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await breaker.ExecuteAsync(async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Simulated failure");
                });
            }
            catch { }
        }

        await Task.Delay(500);

        // Assert
        Assert.True(callbackInvoked, "OnOpened callback should have been invoked");
        Assert.Equal(TimeSpan.FromSeconds(2), breakDuration);
    }

    [Fact]
    public async Task ExecuteAsync_NoResult_Completes()
    {
        // Arrange
        var breaker = CircuitBreakerFactory.Create(new CircuitBreakerOptions(), "TestResource");
        var executed = false;

        // Act
        await breaker.ExecuteAsync(async () =>
        {
            executed = true;
            await Task.Delay(10);
        });

        // Assert
        Assert.True(executed);
    }
}
