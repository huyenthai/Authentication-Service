using Authentication_Service.Business.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Authentication_Service.Events
{
    public class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IConfiguration config;
        private  IConnection connection;
        private  IChannel channel;

        public RabbitMqPublisher(IConfiguration config)
        {
            this.config = config;
        }
        public virtual async Task InitAsync()
        {
            var factory = new ConnectionFactory() { HostName = config["RabbitMq:Host"] };
            connection = await factory.CreateConnectionAsync();
            channel = await connection.CreateChannelAsync();
            await channel.QueueDeclareAsync(queue:"UserCreatedQueue",
                durable:true,
                exclusive:false,
                autoDelete:false,
                arguments:null);
        }
        public virtual async Task PublishUserCreatedAsync(UserCreatedEvent evt)
        {
            var message = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "UserCreatedQueue",
                body: body);
        }

    }
}
