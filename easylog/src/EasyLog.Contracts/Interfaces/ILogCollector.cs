using EasyLog.Contracts.Models;

namespace EasyLog.Contracts.Interfaces;

public interface ILogCollector
{
    IAsyncEnumerable<string> CollectAsync(CollectionRequest request, CancellationToken cancellationToken = default);
}

