using Authentication_Service.Data;
using Authentication_Service.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;


namespace Authentication_Service.Events
{
    public class UserDeletedConsumer : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly IConfiguration config;
        private IConnection connection;
        private IChannel channel;

        public UserDeletedConsumer(IServiceProvider services, IConfiguration config)
        {
            this.services = services;
            this.config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = config["RabbitMq:Host"] };
            connection = await factory.CreateConnectionAsync();
            channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "UserDeletedQueue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<UserDeletedEvent>(body);

                    if (!string.IsNullOrWhiteSpace(evt?.Email))
                    {
                        using var scope = services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

                        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == evt.Email);
                        Console.WriteLine($"[AuthService] Received deletion event for username: {evt.Email}");

                        if (user != null)
                        {
                            db.Users.Remove(user);
                            await db.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        Console.WriteLine("[AuthService WARNING] Received event with empty username.");
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                        return;

                    }


                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AuthService ERROR] UserDeletedEvent failed: {ex.Message}");
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await channel.BasicConsumeAsync("UserDeletedQueue", autoAck: false, consumer);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (channel != null) await channel.CloseAsync();
            if (connection != null) await connection.CloseAsync();
            await base.StopAsync(cancellationToken);
        }
        
        private class UserDeletedEvent
        {
            public string Email { get; set; }
        }
    }

}
