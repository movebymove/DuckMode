using DuckMode.Core.Contracts;

namespace DuckMode.AI;

public class StubAiClient : IAiClient
{
    public Task<AiResponse> ChatAsync(IEnumerable<AiMessage> history, CancellationToken cancellationToken)
    {
        var last = history.LastOrDefault()?.Content ?? string.Empty;
        var reply = string.IsNullOrWhiteSpace(last)
            ? "Chào bạn! Hôm nay bạn có những task nào?"
            : $"Mình đã ghi nhận: {last}. Nếu chưa có deadline, vui lòng cho mình thời gian nhé!";
        return Task.FromResult(new AiResponse(reply));
    }
}




