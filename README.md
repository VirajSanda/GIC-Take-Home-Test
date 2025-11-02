# Microservices Demo Project

This project demonstrates a microservices architecture using .NET Core, implementing two main services: User Service and Order Service, with asynchronous communication via Apache Kafka.

## Project Structure

```
├── TakeHomeTest.sln
├── docker-compose.yml
├── src/
│   ├── OrderService/          # Service handling order management
│   ├── UserService/          # Service handling user management
│   └── Shared/              # Shared libraries and models
└── tests/
    ├── OrderService.Tests/   # Order service unit tests
    ├── UserService.Tests/    # User service unit tests
    └── Shared.Test/         # Integration tests
```

## Services

### User Service

- Manages user-related operations
- Exposes REST API endpoints for user management
- Consumes order-related events from Kafka
- Uses Entity Framework Core for data persistence

### Order Service

- Handles order processing and management
- Exposes REST API endpoints for order operations
- Consumes user-related events from Kafka
- Uses Entity Framework Core for data persistence

### Shared Library

- Contains common models and events
- Provides shared utilities and extensions
- Implements common interfaces and base classes

## Technologies Used

- .NET Core
- Entity Framework Core
- Apache Kafka for event-driven communication
- Docker for containerization
- REST APIs
- Unit Testing (xUnit)

## Getting Started

### Prerequisites

- .NET Core SDK
- Docker Desktop
- Apache Kafka (provided via Docker Compose)

### Running the Application

1. Clone the repository:

```bash
git clone <repository-url>
cd TakeHomeTest
```

2. Start the Docker containers:

```bash
docker-compose up -d
```

3. The services will be available at:
   - User Service: http://localhost:5001
   - Order Service: http://localhost:5002

## Testing

To run the tests:

```bash
dotnet test
```

## API Documentation

### User Service Endpoints

- GET /api/users - Get all users
- POST /api/users - Create a new user

### Order Service Endpoints

- GET /api/orders - Get all orders
- POST /api/orders - Create a new order

## Event Flow

1. User Creation:

   - UserService creates a user
   - Publishes UserCreatedEvent
   - OrderService consumes the event and updates its user database

2. Order Creation:
   - OrderService creates an order
   - Publishes OrderCreatedEvent
   - UserService consumes the event and updates user's order history

## Contributing

Please read the contributing guidelines before submitting any pull requests.

## License

[Add your license information here]
