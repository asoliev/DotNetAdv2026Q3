# Solution

## Task 1. Determine the architectural style

The current solution is best described as a two-tier client/server, database-centric architecture: several independent fat clients integrated through one shared database.

Why this fits:

- All applications (warehouse desktop, manager web, customer web, courier mobile, customer mobile, reporting app) directly access the same SQL database.
- There is no separate business logic layer, API layer, or service layer between the clients and the database.
- The system is tightly coupled: every client knows the database schema and business rules are spread across multiple applications.
- The architecture is centered around one shared database rather than independent modules or services.

This is not a clean microservice or SOA design because there are no independent services, no message bus, and no decoupled application components. It is a two-tier design in which the database itself is the integration mechanism — the classic Shared Database integration pattern.

It is also not a monolith in the strict sense: a monolith is one deployable application, while here there are seven independently deployed applications. What is shared is the data store, not the codebase. Because the duplicated logic can in practice only be changed together, through the schema, this shape is often called a distributed monolith.

In short: the existing architecture is a two-tier, database-centric client/server architecture in which multiple clients integrate through one shared database.

## Task 2. Architectural diagram from scratch

A better design for this project would be a service-based architecture with an API gateway and event-driven integration, with a clear API layer and separate business domains.

### Proposed architecture

- Client applications:
  - Warehouse desktop
  - Manager web
  - Customer web
  - Courier mobile
  - Customer mobile
- API layer / gateway:
  - Handles authentication, routing, request validation, and access control
- Business services:
  - Catalog service
  - Order service
  - Warehouse service
  - Delivery service
  - User/Customer service
  - Payment/Checkout service
  - Reporting service
- Data layer:
  - A separate database (or at minimum a separate schema) per service; no service reads another service's tables
- Event bus / asynchronous integration:
  - For order updates, warehouse events, delivery events, etc.
- Reporting app:
  - Reads data from reporting database / data warehouse / analytics stream, not directly from operational DB

### Diagram

[Architecture Diagram](./images/03-architecture-diagram.drawio)

## Pros and cons of both solutions

### Current architecture: two-tier, shared database

Pros:
- Simple to implement at the beginning
- Low initial cost
- Easy to deploy for a small team
- Fast to build for a single market

Cons:
- Tight coupling between all apps and the database
- Business logic duplicated in multiple clients
- Difficult to maintain and evolve
- Security problems: every app has direct DB access
- Hard to scale individual components
- Adding new countries/markets becomes complicated
- Hard to test changes safely
- Reporting app depends directly on operational data and can cause performance issues

### Proposed architecture: service-based with an API gateway and events

Pros:
- Clear separation of responsibilities
- Easier maintenance and testing
- Better security: clients do not access the database directly
- Easier scaling of specific services
- Supports future expansion into multiple countries
- Easier integration with third-party systems
- Better observability, monitoring, and fault isolation
- Allows gradual modernization and independent deployment

Cons:
- More complex to design and implement
- Requires more infrastructure and team coordination
- Higher initial cost
- More effort for integration and deployment
- Need strong API contracts and governance

## Final conclusion

The existing system is a two-tier, database-centric client/server architecture because all client applications are tightly connected to the same database and there is no service abstraction. For a future-ready retail business that plans to expand into several countries, the better design is a service-based architecture with an API layer, separate business services, and an event-driven communication model.

This approach improves:
- maintainability
- security
- scalability
- flexibility for international expansion

This solution is a more sustainable architecture for growth and long-term product evolution.
