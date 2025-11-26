# RabbitMQ Event-Driven Architecture Implementation

## Overview

This document describes the RabbitMQ-based event-driven architecture implemented for the EMS microservices backend. The system uses a topic-based pub/sub messaging pattern for asynchronous communication between services.

## Architecture

### Components

1. **Shared.Events** - Class library containing event contracts
2. **Auth-Service** - Event publisher for user registration events
3. **User-Service** - Event consumer for creating user profiles, publisher for device user creation
4. **Device-Service** - Event consumer for creating device user records
5. **Simulator-Service** - Event publisher for simulator telemetry data
6. **Monitoring-Service** - Event consumer for processing simulator data
7. **RabbitMQ** - Message broker with topic exchanges

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
User-Service publishes DeviceUserCreateRequested event
  ↓
RabbitMQ routes event to device-service-queue
  ↓
Device-Service consumes event and creates device user record
  ↓
All services complete independently
```

### Simulator to Monitoring Flow

```
Client -> Simulator-Service (POST /Simulator/publish or /Simulator/generate)
  ↓
Simulator-Service creates SimulatorDataEvent
  ↓
Simulator-Service publishes event to RabbitMQ (simulator.events exchange)
  ↓
RabbitMQ routes event to monitoring-service-queue
  ↓
Monitoring-Service consumes event and processes telemetry data
  ↓
Both services complete independently
```

## Configuration

### RabbitMQ Settings

Services are configured in `appsettings.json`:

#### User Events (Auth, User, Device Services)

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

#### Simulator Events (Simulator, Monitoring Services)

```json
{
  "RabbitMq": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "UserName": "admin",
    "Password": "admin123",
    "ExchangeName": "simulator.events",
    "ExchangeType": "topic",
    "QueueName": "monitoring-service-queue",
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

### DeviceUserCreateRequestedEvent

Published when a user is created in user-service for device-service to create a corresponding record.

```json
{
    "UserId": "guid",
    "Username": "username",
    "Address": "123 Main St",
    "CreatedAt": "2024-11-25T10:30:00Z",
    "CorrelationId": "guid"
}
```

**Routing Key**: `user.created.device`
**Exchange**: `user.events`

### SimulatorDataEvent

Published by the simulator service containing device telemetry data.

```json
{
    "DeviceId": "guid",
    "DeviceName": "Smart Meter 001",
    "ConsumptionValue": 125.5,
    "Unit": "kWh",
    "Timestamp": "2024-11-26T10:30:00Z",
    "CorrelationId": "guid"
}
```

**Routing Key**: `simulator.data`
**Exchange**: `simulator.events`

## Infrastructure Components

### Auth-Service (Publisher)

- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### User-Service (Consumer & Publisher)

- **IEventConsumer** - Interface for consuming events
- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **UserRegisteredConsumerService** - Background service for consuming user events
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### Device-Service (Consumer)

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **DeviceUserCreatedConsumerService** - Background service for consuming device user events
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management

### Simulator-Service (Publisher)

- **IEventPublisher** - Interface for publishing events
- **RabbitMqEventPublisher** - RabbitMQ implementation with retry logic
- **IRabbitMqConnectionFactory** - Factory for creating connections
- **RabbitMqConnectionFactory** - Thread-safe connection management
- **SimulatorController** - REST API for publishing simulator data

### Monitoring-Service (Consumer)

- **IEventConsumer** - Interface for consuming events
- **RabbitMqEventConsumer** - RabbitMQ implementation with message acknowledgment
- **SimulatorDataConsumerService** - Background service for consuming simulator events
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

Services check if records already exist before creating new ones, ensuring duplicate events don't create duplicate records.

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
- Simulator-Service: http://simulator.docker.localhost
- Monitoring-Service: http://monitoring.docker.localhost
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
cd simulator-service && dotnet run
cd monitoring-service && dotnet run
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

### Publish Simulator Data

```bash
curl -X POST http://simulator.docker.localhost/Simulator/publish \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "device-001",
    "deviceName": "Smart Meter 001",
    "consumptionValue": 125.5,
    "unit": "kWh"
  }'
```

### Generate Random Simulator Data

```bash
curl -X POST http://simulator.docker.localhost/Simulator/generate
```

Expected Response:
```json
{
  "message": "Simulator data generated and published successfully",
  "correlationId": "correlation-guid",
  "data": {
    "deviceId": "device-guid",
    "deviceName": "Smart Meter 042",
    "consumptionValue": 234.56,
    "unit": "kWh",
    "timestamp": "2024-11-26T10:30:00Z",
    "correlationId": "correlation-guid"
  }
}
```

### Verify Monitoring Service Processing

Check the monitoring-service logs for:
```
SimulatorData event processed successfully: DeviceId={guid}, ConsumptionValue={value} {unit}, CorrelationId={guid}
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
- Check for errors in service logs
- Ensure queue is bound to exchange with correct routing key

### Duplicate Records

The system is designed to be idempotent - duplicate events should not create duplicate records. Check:
- Service logs for "already exists" messages
- Database for duplicate entries

## Exchanges and Queues

| Exchange | Type | Queues |
|----------|------|--------|
| user.events | topic | user-service-queue, device-service-queue |
| simulator.events | topic | monitoring-service-queue |

| Queue | Routing Keys | Consumer |
|-------|--------------|----------|
| user-service-queue | user.registered | User-Service |
| device-service-queue | user.created.device | Device-Service |
| monitoring-service-queue | simulator.data | Monitoring-Service |

## Environment Variables

For production deployments, configure RabbitMQ settings via environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| RabbitMq__HostName | RabbitMQ host | rabbitmq |
| RabbitMq__Port | AMQP port | 5672 |
| RabbitMq__UserName | Username | admin |
| RabbitMq__Password | Password | admin123 |
| RabbitMq__ExchangeName | Exchange name | varies by service |
| RabbitMq__QueueName | Queue name | varies by service |

## Dependencies

- RabbitMQ.Client 7.2.0
- .NET 8.0
- Entity Framework Core 9.0.10
- RabbitMQ 3.x (management plugin enabled)
