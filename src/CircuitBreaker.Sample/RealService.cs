using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CircuitBreaker.Sample
{
    public class RealService : IMyService
    {
        private static int _counter = 0;

        public async Task<string> GetDataAsync()
        {
            _counter++;
            Console.WriteLine($"[REAL] Attempt {_counter}");

            // Simulates failure on the first 3 calls
            if (_counter <= 3)
            {
                throw new HttpRequestException("Simulated network failure");
            }

            await Task.Delay(100);
            return "Real data obtained successfully!";
        }
    }
}
