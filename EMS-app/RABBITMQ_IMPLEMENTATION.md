# RabbitMQ Event-Driven Architecture Implementation

## Overview

This document describes the RabbitMQ-based event-driven architecture implemented for the EMS microservices backend. The system uses a topic-based pub/sub messaging pattern for asynchronous communication between services.

## Architecture

### Components

1. **Shared.Events** - Class library containing event contracts
2. **Auth-Service** - Event publisher for user registration events
3. **User-Service** - Event consumer for creating user profiles, and publisher for device-service user creation
4. **Device-Service** - Event consumer for creating user records
5. **RabbitMQ** - Message broker with topic exchange

### User Registration Flow

```
Client -> Auth-Service (POST /Auth/Register)
  ↓
Auth-Service creates user credentials
  ↓
Auth-Service publishes UserRegistered event to RabbitMQ
  ↓
RabbitMQ routes event to user-service-queue (routing key: user.registered)
  ↓
User-Service consumes event and creates user profile
  ↓
User-Service publishes DeviceUserCreateRequested event to RabbitMQ
  ↓
RabbitMQ routes event to device-service-queue (routing key: user.device.create)
  ↓
Device-Service consumes event and creates user record
  ↓
All services complete independently
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
    "MaxRetryAttempts": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

**Note**: Device-service uses `QueueName: "device-service-queue"` to avoid consuming its own published events.

### Development vs Docker

- **Development**: Set `HostName` to `localhost`
- **Docker**: Set `HostName` to `rabbitmq` (container name)

## Events

### UserRegisteredEvent

Published by auth-service when a new user registers in the system.

```json
{
    "UserId": "guid",
    "Username": "username",
    "Address": "123 Main St",
    "RegisteredAt": "2024-11-22T00:00:00Z",
    "CorrelationId": "guid"
}
```

**Routing Key**: `user.registered`
**Exchange**: `user.events`
**Consumed By**: user-service

### DeviceUserCreateRequested

Published by user-service after successfully creating a user profile. Consumed by device-service to create a corresponding user record.

```json
{
    "UserId": "550e8400-e29b-41d4-a716-446655440000",
    "Email": "user@example.com",
    "Username": "johndoe",
    "Address": "123 Main Street",
    "RegisteredAt": "2024-11-25T12:00:00Z",
    "CorrelationId": "660e8400-e29b-41d4-a716-446655440001"
}
```

**Routing Key**: `user.device.create`
**Exchange**: `user.events`
**Consumed By**: device-service

**Idempotency**: Device-service uses a unique index on `AuthId` (mapped from `UserId`) to ensure duplicate events do not create duplicate records. The handler checks for existing users before inserting and gracefully handles unique constraint violations.

## Infrastructure Components

### Auth-Service

- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### User-Service

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **UserRegisteredConsumerService** - Background service for consuming UserRegistered events and publishing DeviceUserCreateRequested events
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### Device-Service

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **DeviceUserCreatedConsumerService** - Background service for consuming DeviceUserCreateRequested events
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

All services implement idempotent message handling:

- **User-Service**: Checks if a user profile already exists by AuthId before creating a new one, ensuring duplicate events don't create duplicate profiles.
- **Device-Service**: Uses a unique index on `AuthId` (IX_Users_AuthId) in the database. The handler checks for existing users before inserting and gracefully handles unique constraint violations for concurrent message processing.

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
Published DeviceUserCreateRequested event: UserId={guid}, CorrelationId={guid}
```

Check the device-service logs for:
```
User created in device-service: UserId={guid}, ProfileId={guid}, CorrelationId={guid}
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
- Device-service logs for "User already exists in device-service" messages
- Database for duplicate AuthId values (unique index prevents duplicates in device-service)

## Future Enhancements

- **Dead Letter Queues** - Handle permanently failed messages
- **Circuit Breaker** - Prevent cascading failures
- **Message Schema Versioning** - Support event evolution
- **Additional Events** - UserUpdated, UserDeleted, etc.
- **Distributed Tracing** - OpenTelemetry integration

## Dependencies

- RabbitMQ.Client 7.2.0
- .NET 8.0
- Entity Framework Core 9.0.10
- RabbitMQ 3.x (management plugin enabled)
