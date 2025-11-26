# RabbitMQ Event-Driven Architecture Implementation

## Overview

This document describes the RabbitMQ-based event-driven architecture implemented for the EMS microservices backend. The system uses both topic-based and fanout pub/sub messaging patterns for asynchronous communication between services.

## Architecture

### Components

1. **Shared.Events** - Class library containing event contracts
2. **Auth-Service** - Event publisher for user registration events and logs
3. **User-Service** - Event consumer for creating user profiles and logs subscriber
4. **Device-Service** - Event consumer for user events and logs subscriber
5. **RabbitMQ** - Message broker with topic and fanout exchanges

### Exchanges

The system uses two types of exchanges:

1. **user.events** (Topic Exchange) - For routing specific events using routing keys
2. **logs** (Fanout Exchange) - For broadcasting log messages to all subscribers

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

All services are configured in `appsettings.json`:

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
    "LogsExchangeName": "logs",
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

## Logs Fanout Exchange

The system includes a fanout exchange named `logs` for broadcasting log-like or broadcast-style messages to all subscribers. This follows the official RabbitMQ .NET tutorial pattern for publish/subscribe.

### How It Works

1. **Producer** publishes messages to the `logs` exchange with an empty routing key
2. **Consumers** each declare a server-named exclusive queue and bind it to the `logs` exchange
3. Messages are broadcast to all bound queues (all consumers receive all messages)
4. Auto-acknowledgment (`autoAck: true`) is used for simple fire-and-forget semantics

### Producer Pattern (EmitLog)

```csharp
// Declare fanout exchange
await channel.ExchangeDeclareAsync(exchange: "logs", type: ExchangeType.Fanout);

// Publish with empty routing key
await channel.BasicPublishAsync(exchange: "logs", routingKey: string.Empty, body: body);
```

### Consumer Pattern (ReceiveLogs)

```csharp
// Declare fanout exchange
await channel.ExchangeDeclareAsync(exchange: "logs", type: ExchangeType.Fanout);

// Declare server-named exclusive queue
QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
string queueName = queueDeclareResult.QueueName;

// Bind to exchange with empty routing key
await channel.QueueBindAsync(queue: queueName, exchange: "logs", routingKey: string.Empty);

// Consume with auto-acknowledgment
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (model, ea) =>
{
    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($" [x] {message}");
};
await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);
```

### Usage

Inject `ILogsPublisher` to publish log messages:

```csharp
await _logsPublisher.PublishLogAsync("info: User registration completed");
```

The `LogsConsumerService` background service automatically subscribes to and processes log messages.

## Infrastructure Components

### Auth-Service

- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **ILogsPublisher** - Interface for publishing to the logs fanout exchange
- **RabbitMqLogsPublisher** - Fanout exchange publisher implementation
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### User-Service

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **ILogsPublisher** - Interface for publishing to the logs fanout exchange
- **RabbitMqLogsPublisher** - Fanout exchange publisher implementation
- **ILogsConsumer** - Interface for consuming from the logs fanout exchange
- **RabbitMqLogsConsumer** - Fanout exchange consumer with server-named queues and autoAck
- **UserRegisteredConsumerService** - Background service for consuming user registration events
- **LogsConsumerService** - Background service for consuming broadcast log messages
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### Device-Service

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **ILogsPublisher** - Interface for publishing to the logs fanout exchange
- **RabbitMqLogsPublisher** - Fanout exchange publisher implementation
- **ILogsConsumer** - Interface for consuming from the logs fanout exchange
- **RabbitMqLogsConsumer** - Fanout exchange consumer with server-named queues and autoAck
- **DeviceUserCreatedConsumerService** - Background service for consuming device user creation events
- **LogsConsumerService** - Background service for consuming broadcast log messages
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

## Future Enhancements

- **Dead Letter Queues** - Handle permanently failed messages
- **Circuit Breaker** - Prevent cascading failures
- **Message Schema Versioning** - Support event evolution
- **Additional Events** - UserUpdated, UserDeleted, etc.
- **Device-Service Integration** - Subscribe to user events
- **Distributed Tracing** - OpenTelemetry integration

## Dependencies

- RabbitMQ.Client 7.2.0
- .NET 8.0
- Entity Framework Core 9.0.10
- RabbitMQ 3.x (management plugin enabled)
