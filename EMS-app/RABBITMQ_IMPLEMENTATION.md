# RabbitMQ Event-Driven Architecture Implementation

## Overview

This document describes the RabbitMQ-based event-driven architecture implemented for the EMS microservices backend. The system uses a topic-based pub/sub messaging pattern for asynchronous communication between services.

## Architecture

### Components

1. **Shared.Events** - Class library containing event contracts
2. **Auth-Service** - Event publisher for user registration events
3. **User-Service** - Event consumer for creating user profiles, and publisher for device user creation events
4. **Device-Service** - Event consumer for creating user records in device context
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
User-Service publishes DeviceUserCreateRequested event
  ↓
RabbitMQ routes event to device-service-queue (routing key: user.created.device)
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

**Note**: Device-service uses `QueueName`: `device-service-queue`

### Development vs Docker

- **Development**: Set `HostName` to `localhost`
- **Docker**: Set `HostName` to `rabbitmq` (container name)

## Events

### UserRegisteredEvent

Published by auth-service when a new user registers in the system.

```json
{
    "UserId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "Username": "johndoe",
    "Address": "123 Main St",
    "RegisteredAt": "2024-11-22T00:00:00Z",
    "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Routing Key**: `user.registered`  
**Queue**: `user-service-queue`

### DeviceUserCreateRequestedEvent

Published by user-service after successfully creating a user profile, consumed by device-service.

```json
{
    "UserId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "Username": "johndoe",
    "Address": "123 Main St",
    "CreatedAt": "2024-11-25T10:30:00Z",
    "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Routing Key**: `user.created.device`  
**Queue**: `device-service-queue`

## Infrastructure Components

### Auth-Service

- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### User-Service

- **IEventConsumer** - Interface for consuming events
- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **UserRegisteredConsumerService** - Background service for consuming user registration events and publishing device user creation events
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### Device-Service

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **DeviceUserCreatedConsumerService** - Background service for consuming device user creation events
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

Both user-service and device-service implement idempotency:
- **User-Service**: Checks if a user profile already exists (by AuthId) before creating a new one
- **Device-Service**: Checks if a user record already exists (by AuthId) before creating a new one
- **Database Constraint**: Device-service has a unique index on AuthId (`IX_Users_AuthId_Unique`) to prevent duplicate inserts at the database level

## Running the System

### With Docker Compose

```bash
cd EMS-app
docker-compose up
```

Services will be available at:
- Auth-Service: http://auth.docker.localhost
- User-Service: http://user.docker.localhost
- Device-Service: http://device.docker.localhost
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
cd device-service && dotnet run
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
Published DeviceUserCreateRequested event: UserId={guid}, RoutingKey=user.created.device, CorrelationId={guid}
```

### Verify Device User Created

Check the device-service logs for:
```
User created successfully in device-service: UserId={guid}, DeviceUserId={guid}, CorrelationId={guid}
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
- Check for errors in user-service/device-service logs
- Ensure queue is bound to exchange with correct routing key

### Duplicate User Profiles

The system is designed to be idempotent - duplicate events should not create duplicate profiles. Check:
- User-service logs for "User already exists" messages
- Device-service logs for "User already exists in device-service" messages
- Database for duplicate AuthId values (prevented by unique index in device-service)

## Database Schema

### Device-Service Users Table

| Column   | Type           | Constraints                        |
|----------|----------------|------------------------------------|
| Id       | uniqueidentifier | Primary Key                       |
| AuthId   | uniqueidentifier | Unique Index (IX_Users_AuthId_Unique) |
| Username | nvarchar(max)  | Not Null                          |
| Address  | nvarchar(max)  | Not Null                          |

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
