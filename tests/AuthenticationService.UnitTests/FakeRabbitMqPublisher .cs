using Authentication_Service.Events;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationService.UnitTests
{
    public class FakeRabbitMqPublisher : RabbitMqPublisher
    {
        public bool WasPublishCalled { get; private set; }
        public FakeRabbitMqPublisher() : base(new ConfigurationBuilder().Build()) { }

        public override Task InitAsync()
        {
            return Task.CompletedTask;
        }

        public override Task PublishUserCreatedAsync(UserCreatedEvent evt)
        {
            WasPublishCalled = true;
            return Task.CompletedTask;
        } 
    }

}
