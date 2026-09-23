# Task 2: Catalog Service

This folder contains the week 2 solution for the Catalog Service assignment.

## Solution overview

The solution follows a Clean Architecture-style layout with physical separation between layers:

- `CatalogService.Domain` contains the core entities and validation rules.
- `CatalogService.Application` contains use cases and repository contracts.
- `CatalogService.Infrastructure` contains the SQLite persistence implementation.
- `CatalogService.Tests` contains unit and integration tests.

The solution covers Category and Product entities, together with add, update, delete, and list/get operations.

The infrastructure layer uses SQLite so the persistence layer stays SQL-based while remaining easy to run locally.

The API also publishes product change events to RabbitMQ so the cart service can react to catalog updates.

## Project structure notes

- Domain objects are independent from SQL and persistence details.
- Application services depend on repository interfaces, not on the database implementation.
- Infrastructure implements those interfaces and owns the SQL mapping and connection logic.
- The API publishes product events to RabbitMQ, keeping catalog write operations and cart synchronization decoupled.
- Tests cover both domain/application behavior and the repository path.

## NFR notes

### Testability

Testability is achieved by keeping the core logic separated from the database implementation.

- The unit tests validate domain rules such as name length and required relationships.
- The application tests validate use-case behavior such as checking parent categories and product category existence.
- The integration tests validate that categories and products can be saved and loaded through the SQLite repository.

### Extensibility

The architecture can be extended with predictable cost because the responsibilities are isolated.

- A different SQL provider can replace SQLite by implementing new infrastructure repositories.
- New business rules can be added in the domain or application layer without changing the repository contracts.
- Additional API or UI layers can be added later without changing the core domain model.

### Cost of extension

- Database replacement mainly affects the Infrastructure project.
- New use cases mainly affect the Application project.
- New entity rules mainly affect the Domain project.
- A new host or API mainly affects the outermost layer only.

## Validation

The solution was validated with `dotnet test` and the Task 2 test suite passed successfully.

RabbitMQ must be running locally before exercising the cross-service messaging flow.