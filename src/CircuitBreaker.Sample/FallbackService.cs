using System;
using System.Threading.Tasks;

namespace CircuitBreaker.Sample
{
    public class FallbackService : IMyService
    {
        public Task<string> GetDataAsync()
        {
            Console.WriteLine("[FALLBACK] Degraded mode activated");
            return Task.FromResult("Cached/offline data");
        }
    }
}
