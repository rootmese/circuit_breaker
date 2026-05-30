using System.Threading.Tasks;

namespace CircuitBreaker.Sample
{
    public interface IMyService
    {
        Task<string> GetDataAsync();
    }
}
