using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// A custom DelegatingHandler to intercept HttpClient requests and run them through a Circuit Breaker.
    /// </summary>
    public class CircuitBreakerDelegatingHandler : DelegatingHandler
    {
        private readonly ICircuitBreaker _breaker;

        public CircuitBreakerDelegatingHandler(ICircuitBreaker breaker)
        {
            _breaker = breaker ?? throw new ArgumentNullException(nameof(breaker));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!await _breaker.AllowCallAsync())
            {
                Console.WriteLine("[CB Handler] Execution blocked (circuit is open)");
                throw new CircuitBrokenException("Circuit is open. HTTP request blocked.");
            }

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    await _breaker.RecordSuccessAsync();
                }
                else
                {
                    await _breaker.RecordFailureAsync();
                }

                return response;
            }
            catch (Exception)
            {
                await _breaker.RecordFailureAsync();
                throw;
            }
        }
    }
}
