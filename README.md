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

## Use of AI

- During the documentation and commenting, AI promts were used to get the best outcome
- Copilot and other AI tools used to refactor code and reviewed before accepting the suggetions.
- During Unit test development, used AI to cover most of the code coverage and to write unit test cases.
- Also verify architectural and code base design, and with the effecting suggestions to achive the better structure.

## Additional
<img width="1624" height="834" alt="Heath Checks" src="https://github.com/user-attachments/assets/828b7342-3460-43ef-a6d1-2a89401a2aff" />
<img width="1896" height="835" alt="Kafka UI" src="https://github.com/user-attachments/assets/f68449bd-c2bd-446f-87b6-d2c3f2f9af71" />

## Contributing

Please read the contributing guidelines before submitting any pull requests.

## License

[Add your license information here]
