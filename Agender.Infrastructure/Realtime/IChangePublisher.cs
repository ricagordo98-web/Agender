namespace Agender.Infrastructure.Realtime;

public interface IChangePublisher
{
    void Publish(string scopeId, string actorId);
}
