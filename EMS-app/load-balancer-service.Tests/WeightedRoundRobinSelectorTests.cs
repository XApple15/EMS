using load_balancer_service.Configuration;
using load_balancer_service.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace load_balancer_service.Tests
{
    public class WeightedRoundRobinSelectorTests
    {
        private readonly Mock<ILogger<WeightedRoundRobinSelector>> _mockLogger;

        public WeightedRoundRobinSelectorTests()
        {
            _mockLogger = new Mock<ILogger<WeightedRoundRobinSelector>>();
        }

        [Fact]
        public void SelectReplica_WithNoReplicas_ReturnsNull()
        {
            // Arrange
            var selector = new WeightedRoundRobinSelector(_mockLogger.Object);
            var replicas = new List<Replica>();

            // Act
            var result = selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SelectReplica_WithSingleReplica_ReturnsThatReplica()
        {
            // Arrange
            var selector = new WeightedRoundRobinSelector(_mockLogger.Object);
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", Weight = 1, IsHealthy = true }
            };

            // Act
            var result = selector.SelectReplica("test-key", replicas);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("1", result.Id);
        }

        [Fact]
        public void SelectReplica_WithEqualWeights_DistributesEvenly()
        {
            // Arrange
            var selector = new WeightedRoundRobinSelector(_mockLogger.Object);
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", Weight = 1, IsHealthy = true },
                new Replica { Id = "2", Weight = 1, IsHealthy = true },
                new Replica { Id = "3", Weight = 1, IsHealthy = true }
            };

            var selections = new Dictionary<string, int>
            {
                { "1", 0 },
                { "2", 0 },
                { "3", 0 }
            };

            // Act - select 30 times (should be 10 each)
            for (int i = 0; i < 30; i++)
            {
                var result = selector.SelectReplica($"key-{i}", replicas);
                if (result != null)
                {
                    selections[result.Id]++;
                }
            }

            // Assert - each should get roughly equal distribution
            Assert.InRange(selections["1"], 8, 12);
            Assert.InRange(selections["2"], 8, 12);
            Assert.InRange(selections["3"], 8, 12);
        }

        [Fact]
        public void SelectReplica_WithDifferentWeights_RespectsWeights()
        {
            // Arrange
            var selector = new WeightedRoundRobinSelector(_mockLogger.Object);
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", Weight = 1, IsHealthy = true },
                new Replica { Id = "2", Weight = 2, IsHealthy = true },
                new Replica { Id = "3", Weight = 3, IsHealthy = true }
            };

            var selections = new Dictionary<string, int>
            {
                { "1", 0 },
                { "2", 0 },
                { "3", 0 }
            };

            // Act - select 60 times
            for (int i = 0; i < 60; i++)
            {
                var result = selector.SelectReplica($"key-{i}", replicas);
                if (result != null)
                {
                    selections[result.Id]++;
                }
            }

            // Assert - replica 3 should get most, replica 1 should get least
            Assert.True(selections["3"] > selections["2"], "Replica 3 (weight 3) should get more than Replica 2 (weight 2)");
            Assert.True(selections["2"] > selections["1"], "Replica 2 (weight 2) should get more than Replica 1 (weight 1)");
        }

        [Fact]
        public void SelectReplica_SkipsUnhealthyReplicas()
        {
            // Arrange
            var selector = new WeightedRoundRobinSelector(_mockLogger.Object);
            var replicas = new List<Replica>
            {
                new Replica { Id = "1", Weight = 1, IsHealthy = false },
                new Replica { Id = "2", Weight = 1, IsHealthy = true }
            };

            // Act - select multiple times
            var results = new List<Replica>();
            for (int i = 0; i < 10; i++)
            {
                var result = selector.SelectReplica($"key-{i}", replicas);
                if (result != null)
                {
                    results.Add(result);
                }
            }

            // Assert - should only select replica 2
            Assert.All(results, r => Assert.Equal("2", r.Id));
        }
    }
}
