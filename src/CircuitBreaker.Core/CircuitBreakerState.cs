using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Manages the state transitions of the Circuit Breaker using a distributed cache store.
    /// </summary>
    public class CircuitBreakerState
    {
        public enum CircuitState { Closed, Open, HalfOpen }

        private readonly IDistributedCache _cache;
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;

        public string ResourceName { get; }

        public CircuitBreakerState(IDistributedCache cache, string resourceName, int failureThreshold = 2, TimeSpan? resetTimeout = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            ResourceName = string.IsNullOrWhiteSpace(resourceName) ? throw new ArgumentException("Resource name cannot be null or empty.", nameof(resourceName)) : resourceName;
            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(10);
        }

        private string GetStateKey() => $"cb:{ResourceName}:state";
        private string GetFailuresKey() => $"cb:{ResourceName}:failures";
        private string GetOpenedTimeKey() => $"cb:{ResourceName}:openedTime";

        public async Task<CircuitState> GetCurrentStateAsync()
        {
            var stateStr = await _cache.GetStringAsync(GetStateKey()) ?? "Closed";
            if (Enum.TryParse<CircuitState>(stateStr, true, out var state))
            {
                return state;
            }
            return CircuitState.Closed;
        }

        public async Task<bool> AllowCallAsync()
        {
            var state = await GetCurrentStateAsync();
            if (state == CircuitState.Open)
            {
                var openedTimeStr = await _cache.GetStringAsync(GetOpenedTimeKey());
                if (long.TryParse(openedTimeStr, out var openedTimeMs))
                {
                    var openedTime = DateTimeOffset.FromUnixTimeMilliseconds(openedTimeMs);
                    if (DateTimeOffset.UtcNow - openedTime > _resetTimeout)
                    {
                        await _cache.SetStringAsync(GetStateKey(), CircuitState.HalfOpen.ToString());
                        Console.WriteLine($"[CB - {ResourceName}] Half-Open: testing service recovery");
                        return true;
                    }
                }
                return false;
            }
            return true;
        }

        public async Task RecordSuccessAsync()
        {
            await _cache.SetStringAsync(GetStateKey(), CircuitState.Closed.ToString());
            await _cache.SetStringAsync(GetFailuresKey(), "0");
            Console.WriteLine($"[CB - {ResourceName}] Circuit closed again");
        }

        public async Task RecordFailureAsync()
        {
            var failuresStr = await _cache.GetStringAsync(GetFailuresKey()) ?? "0";
            int.TryParse(failuresStr, out var failures);
            failures++;

            await _cache.SetStringAsync(GetFailuresKey(), failures.ToString());

            var state = await GetCurrentStateAsync();
            if (failures >= _failureThreshold && state != CircuitState.Open)
            {
                await _cache.SetStringAsync(GetStateKey(), CircuitState.Open.ToString());
                await _cache.SetStringAsync(GetOpenedTimeKey(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                Console.WriteLine($"[CB - {ResourceName}] Circuit is OPEN!");
            }
        }
    }
}
