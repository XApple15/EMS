using load_balancer_service.Configuration;
using load_balancer_service.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace load_balancer_service.Tests
{
    public class ConsistentHashingSelectorTests
    {
        private readonly Mock<ILogger<ConsistentHashingSelector>> _mockLogger;
        private readonly ConsistentHashingSelector _selector;

        public ConsistentHashingSelectorTests()
        {
            _mockLogger = new Mock<ILogger<ConsistentHashingSelector>>();
            _selector = new ConsistentHashingSelector(_mockLogger.Object);
        }

        [Fact]
        public void SelectReplica_WithNoReplicas_ReturnsNull()
        {
            // Arrange
            var replicas = new List<Replica>();

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SelectReplica_WithSingleReplica_ReturnsThatReplica()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", IsHealthy = true }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("1", result.Id);
        }

        [Fact]
        public void SelectReplica_WithUnhealthyReplicas_ReturnsNull()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", IsHealthy = false },
                new Replica { Id = "2", IsHealthy = false }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SelectReplica_WithMultipleReplicas_ReturnsConsistentSelection()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", IsHealthy = true },
                new Replica { Id = "2", IsHealthy = true },
                new Replica { Id = "3", IsHealthy = true }
            };

            var messageKey = "device-123";

            // Act - select same key multiple times
            var result1 = _selector.SelectReplica(messageKey, replicas);
            var result2 = _selector.SelectReplica(messageKey, replicas);
            var result3 = _selector.SelectReplica(messageKey, replicas);

            // Assert - should always return the same replica for the same key
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotNull(result3);
            Assert.Equal(result1.Id, result2.Id);
            Assert.Equal(result2.Id, result3.Id);
        }

        [Fact]
        public void SelectReplica_WithDifferentKeys_DistributesLoad()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", IsHealthy = true },
                new Replica { Id = "2", IsHealthy = true },
                new Replica { Id = "3", IsHealthy = true }
            };

            var selections = new Dictionary<string, int>
            {
                { "1", 0 },
                { "2", 0 },
                { "3", 0 }
            };

            // Act - select with 100 different keys
            for (int i = 0; i < 100; i++)
            {
                var result = _selector.SelectReplica($"device-{i}", replicas);
                if (result != null)
                {
                    selections[result.Id]++;
                }
            }

            // Assert - each replica should get some load (not perfectly even but distributed)
            Assert.True(selections["1"] > 0, "Replica 1 should receive some messages");
            Assert.True(selections["2"] > 0, "Replica 2 should receive some messages");
            Assert.True(selections["3"] > 0, "Replica 3 should receive some messages");
        }
    }
}
