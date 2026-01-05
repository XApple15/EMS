# RabbitMQ Event-Driven Architecture Implementation

## Overview

This document describes the RabbitMQ-based event-driven architecture implemented for the EMS microservices backend. The system uses a topic-based pub/sub messaging pattern for asynchronous communication between services.

## Architecture

### Components

1. **Shared.Events** - Class library containing event contracts
2. **Auth-Service** - Event publisher for user registration events
3. **User-Service** - Event consumer for creating user profiles
4. **RabbitMQ** - Message broker with topic exchange

### User Registration Flow

```
Client -> Auth-Service (POST /Auth/Register)
  ↓
Auth-Service creates user credentials
  ↓
Auth-Service publishes UserRegistered event to RabbitMQ
  ↓
RabbitMQ routes event to user-service-queue
  ↓
User-Service consumes event and creates user profile
  ↓
Both services complete independently
```

## Configuration

### RabbitMQ Settings

Both services are configured in `appsettings.json`:

```json
{
  "RabbitMq": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "UserName": "admin",
    "Password": "admin123",
    "ExchangeName": "user.events",
    "ExchangeType": "topic",
    "QueueName": "user-service-queue",
    "MaxRetryAttempts": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

### Development vs Docker

- **Development**: Set `HostName` to `localhost`
- **Docker**: Set `HostName` to `rabbitmq` (container name)

## Events

### UserRegisteredEvent

Published when a new user registers in the system.

```csharp
{
    "UserId": "guid",
    "Email": "user@example.com",
    "Username": "username",
    "FirstName": "John",
    "LastName": "Doe",
    "RegisteredAt": "2024-11-22T00:00:00Z",
    "CorrelationId": "guid"
}
```

**Routing Key**: `user.registered`

## Infrastructure Components

### Auth-Service

- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### User-Service

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **UserRegisteredConsumerService** - Background service for consuming events
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

## Features

### Reliability

- **Durable queues and exchanges** - Survive broker restarts
- **Persistent messages** - Not lost on broker crash
- **Manual acknowledgment** - Messages only removed after successful processing
- **Automatic recovery** - Connections automatically recover after failures
- **Retry logic** - Failed operations retried with exponential backoff
- **Message requeue** - Failed messages requeued for retry

### Observability

- **Correlation IDs** - Track requests across services
- **Structured logging** - All events logged with context
- **RabbitMQ Management UI** - Available at http://localhost:15672

### Idempotency

The user-service checks if a user profile already exists before creating a new one, ensuring duplicate events don't create duplicate profiles.

## Running the System

### With Docker Compose

```bash
cd EMS-app
docker-compose up
```

Services will be available at:
- Auth-Service: http://auth.docker.localhost
- User-Service: http://user.docker.localhost
- RabbitMQ Management: http://localhost:15672 (admin/admin123)

### Local Development

1. Start RabbitMQ:
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=admin \
  -e RABBITMQ_DEFAULT_PASS=admin123 \
  rabbitmq:3-management
```

2. Update appsettings.Development.json to use `localhost`

3. Start services:
```bash
cd auth-service && dotnet run
cd user-service && dotnet run
```

## Testing

### Register a User

```bash
curl -X POST http://auth.docker.localhost/Auth/Register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["Client"]
  }'
```

Expected Response:
```json
{
  "message": "User created successfully",
  "id": "user-id-guid",
  "correlationId": "correlation-guid"
}
```

### Verify User Profile Created

Check the user-service logs for:
```
User profile created successfully: UserId={guid}, ProfileId={guid}, CorrelationId={guid}
```

Or check the RabbitMQ Management UI to see message flow.

## Troubleshooting

### RabbitMQ Connection Issues

- Check RabbitMQ is running: `docker ps | grep rabbitmq`
- Check service logs for connection errors
- Verify hostname/credentials in appsettings.json

### Messages Not Being Consumed

- Check RabbitMQ Management UI for queue depth
- Verify consumer service is running
- Check for errors in user-service logs
- Ensure queue is bound to exchange with correct routing key

### Duplicate User Profiles

The system is designed to be idempotent - duplicate events should not create duplicate profiles. Check:
- User-service logs for "User already exists" messages
- Database for duplicate AuthId values

## Load Balancing Architecture

### Overview

The Load Balancing Service implements a scalable ingestion pipeline for distributing device data across multiple Monitoring Service replicas. This architecture enables horizontal scaling of the monitoring service to handle high-throughput device data.

### Architecture Diagram

```
Simulator Service
      ↓
  (publishes to)
      ↓
Central Device Data Queue (device-data-queue)
      ↓
  (consumed by)
      ↓
Load Balancer Service
      ↓
  (distributes via replica selection)
      ↓
Per-Replica Ingest Queues
  - ingest-queue-1
  - ingest-queue-2
  - ingest-queue-3
      ↓
Monitoring Service Replicas
  - Replica 1 (consumes ingest-queue-1)
  - Replica 2 (consumes ingest-queue-2)
  - Replica 3 (consumes ingest-queue-3)
```

### Components

#### 1. Load Balancer Service

**Purpose**: Acts as a central message router that consumes all device data from a central queue and distributes it to replica-specific ingest queues.

**Key Features**:
- Single consumer of the central device data queue
- Pluggable replica selection strategies
- High-throughput message processing with error handling
- Metrics tracking for load distribution

**Replica Selection Strategies**:

1. **Consistent Hashing** (Default)
   - Uses MD5 hash of device ID for stable routing
   - Ensures same device always routes to same replica
   - Minimal reassignment when replicas are added/removed
   - Best for: Predictable routing and replica affinity

2. **Weighted Round-Robin**
   - Distributes load based on replica weights
   - Higher weight = more messages
   - Best for: Replicas with different capacities

3. **Load-Based**
   - Routes to replica with lowest current load
   - Requires external load monitoring
   - Best for: Dynamic load balancing with health monitoring

#### 2. Simulator Service Updates

- Now publishes messages directly to the central `device-data-queue`
- Uses `PublishToQueueAsync` for direct queue publishing
- No exchange routing for device data (uses default exchange)

#### 3. Monitoring Service Updates

- Each replica consumes from its own ingest queue (`ingest-queue-{replica-id}`)
- Uses `StartConsumingFromQueueAsync` for direct queue consumption
- Configurable `ReplicaId` in `appsettings.json`
- Multiple replicas can run independently

### Configuration

#### Load Balancer Service (`appsettings.json`)

```json
{
  "LoadBalancer": {
    "Strategy": "ConsistentHashing",
    "CentralQueueName": "device-data-queue",
    "IngestQueuePattern": "ingest-queue-{0}",
    "ExchangeName": "device.data",
    "ExchangeType": "direct",
    "Replicas": [
      { "Id": "1", "Weight": 1, "IsHealthy": true },
      { "Id": "2", "Weight": 1, "IsHealthy": true },
      { "Id": "3", "Weight": 2, "IsHealthy": true }
    ]
  }
}
```

#### Monitoring Service Replica Configuration

```json
{
  "RabbitMq": {
    "ReplicaId": "1",
    "IngestQueuePattern": "ingest-queue-{0}",
    "DeviceDataExchange": "device.data"
  }
}
```

### Message Flow

1. **Device Data Generation**
   - Simulator Service generates `SimulatorDataEvent` messages
   - Published to `device-data-queue` via default exchange

2. **Load Balancing**
   - Load Balancer consumes from `device-data-queue`
   - Applies replica selection strategy based on device ID
   - Publishes to selected replica's ingest queue (e.g., `ingest-queue-1`)

3. **Replica Processing**
   - Each Monitoring Service replica consumes only from its ingest queue
   - Processes messages independently
   - Aggregates hourly consumption data
   - Stores results in database

### Benefits

1. **Horizontal Scalability**: Add more monitoring service replicas to handle increased load
2. **Even Load Distribution**: Consistent hashing ensures balanced distribution
3. **Replica Affinity**: Same device always routes to same replica (important for stateful operations)
4. **High Availability**: Load balancer can route around unhealthy replicas
5. **Flexibility**: Easy to switch between different load balancing strategies

### Running with Docker Compose

The system includes a load-balancer-service in docker-compose:

```bash
cd EMS-app
docker-compose up
```

Services available:
- Load Balancer: http://loadbalancer.docker.localhost (internal)
- Monitoring Service: http://monitoring.docker.localhost
- Simulator Service: http://simulator.docker.localhost
- RabbitMQ Management: http://localhost:15672

### Testing the Load Balancer

1. **Publish Device Data**:
```bash
curl -X POST http://simulator.docker.localhost/Simulator/publish \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "test-device-123",
    "dataCount": 10
  }'
```

2. **Monitor RabbitMQ**:
- Visit http://localhost:15672 (admin/admin123)
- Check `device-data-queue` for incoming messages
- Check `ingest-queue-1`, `ingest-queue-2`, `ingest-queue-3` for distributed messages

3. **View Load Balancer Metrics**:
- Check load-balancer-service logs for distribution statistics
- Metrics logged every minute showing message counts per replica

### Scaling Monitoring Service

To add more replicas, update `docker-compose.yml`:

```yaml
monitoring-service-2:
  image: ${DOCKER_REGISTRY-}monitoringservice
  environment:
    - RabbitMq__ReplicaId=2
  # ... rest of config
```

And add the replica to Load Balancer configuration:

```json
{
  "LoadBalancer": {
    "Replicas": [
      { "Id": "1", "Weight": 1 },
      { "Id": "2", "Weight": 1 },  // New replica
      { "Id": "3", "Weight": 2 }
    ]
  }
}
```

## Future Enhancements

- **Dead Letter Queues** - Handle permanently failed messages
- **Circuit Breaker** - Prevent cascading failures
- **Message Schema Versioning** - Support event evolution
- **Additional Events** - UserUpdated, UserDeleted, etc.
- **Device-Service Integration** - Subscribe to user events
- **Distributed Tracing** - OpenTelemetry integration
- **Dynamic Replica Discovery** - Auto-detect and register new replicas
- **Load Monitoring** - Real-time replica health and load tracking
- **Message Prioritization** - Priority queues for critical devices

## Dependencies

- RabbitMQ.Client 7.2.0
- .NET 8.0
- Entity Framework Core 9.0.10
- RabbitMQ 3.x (management plugin enabled)
