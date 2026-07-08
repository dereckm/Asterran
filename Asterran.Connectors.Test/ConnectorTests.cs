using Xunit;
using NSubstitute;
using Asterran.Connectors;
using System;

namespace Asterran.Connectors.Test
{
    public class ConnectorTests
    {
        [Fact]
        public void ConnectorInterface_RaisesEventsWhenTriggered()
        {
            // Create a mock ILlmConnector using NSubstitute
            var mockConnector = Substitute.For<ILlmConnector>();
            
            bool eventRaised = false;
            mockConnector.OnActivity += (s, e) => {
                eventRaised = true;
                Assert.Equal("User", e.Source);
                Assert.Equal("Prompt", e.ActivityType);
            };

            // Trigger the event on the substitute mock
            mockConnector.OnActivity += Raise.Event<EventHandler<LlmActivityEventArgs>>(
                mockConnector, 
                new LlmActivityEventArgs { Source = "User", ActivityType = "Prompt" }
            );

            Assert.True(eventRaised);
        }
    }
}
