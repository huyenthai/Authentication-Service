using Authentication_Service.Events;

namespace Authentication_Service.Business.Interfaces
{
    public interface IRabbitMqPublisher
    {
        Task InitAsync();
        Task PublishUserCreatedAsync(UserCreatedEvent evt);
    }
}
