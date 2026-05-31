using System;
using System.Threading.Tasks;
using CircuitBreaker.Core;
using Polly.CircuitBreaker;

namespace CircuitBreaker.Sample
{
    public class MyServiceDecorator : IMyService
    {
        private readonly IMyService _realService;
        private readonly ICircuitBreaker _breaker;

        public MyServiceDecorator(IMyService realService, ICircuitBreaker breaker)
        {
            _realService = realService ?? throw new ArgumentNullException(nameof(realService));
            _breaker = breaker ?? throw new ArgumentNullException(nameof(breaker));
        }

        public async Task<string> GetDataAsync()
        {
            try
            {
                return await _breaker.ExecuteAsync(() => _realService.GetDataAsync());
            }
            catch (BrokenCircuitException ex)
            {
                Console.WriteLine($"[DECORATOR] Blocked by circuit breaker: {ex.Message}");
                throw;
            }
        }
    }
}
