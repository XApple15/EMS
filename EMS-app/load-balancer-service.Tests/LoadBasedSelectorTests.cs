using load_balancer_service.Configuration;
using load_balancer_service.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace load_balancer_service.Tests
{
    public class LoadBasedSelectorTests
    {
        private readonly Mock<ILogger<LoadBasedSelector>> _mockLogger;
        private readonly LoadBasedSelector _selector;

        public LoadBasedSelectorTests()
        {
            _mockLogger = new Mock<ILogger<LoadBasedSelector>>();
            _selector = new LoadBasedSelector(_mockLogger.Object);
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
                new Replica { Id = "1", LoadPercentage = 50, IsHealthy = true }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("1", result.Id);
        }

        [Fact]
        public void SelectReplica_SelectsReplicaWithLowestLoad()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", LoadPercentage = 75, IsHealthy = true },
                new Replica { Id = "2", LoadPercentage = 30, IsHealthy = true },
                new Replica { Id = "3", LoadPercentage = 50, IsHealthy = true }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("2", result.Id); // Replica 2 has lowest load (30%)
        }

        [Fact]
        public void SelectReplica_SkipsUnhealthyReplicas()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", LoadPercentage = 10, IsHealthy = false }, // Lowest but unhealthy
                new Replica { Id = "2", LoadPercentage = 50, IsHealthy = true },
                new Replica { Id = "3", LoadPercentage = 75, IsHealthy = true }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("2", result.Id); // Should select replica 2 (lowest healthy load)
        }

        [Fact]
        public void SelectReplica_WithAllReplicasUnhealthy_ReturnsNull()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", LoadPercentage = 10, IsHealthy = false },
                new Replica { Id = "2", LoadPercentage = 20, IsHealthy = false }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SelectReplica_WithZeroLoadReplica_SelectsThatReplica()
        {
            // Arrange
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", LoadPercentage = 0, IsHealthy = true },
                new Replica { Id = "2", LoadPercentage = 25, IsHealthy = true },
                new Replica { Id = "3", LoadPercentage = 50, IsHealthy = true }
            };

            // Act
            var result = _selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("1", result.Id);
        }
    }
}
