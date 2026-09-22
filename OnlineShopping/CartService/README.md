# Task 1: Cart Service

This folder contains the week 1 solution for the Cart Service assignment.

## Solution overview

The solution is organized as a logically layered .NET setup:

- `CartService.Bll` contains the business rules and the cart model.
- `CartService.Dal` contains the LiteDB persistence implementation and mapping code.
- `CartService.Tests` contains unit and integration-style tests.

The cart id is generated on the client side, and the service supports getting cart items, adding items, and removing items.

The persistence layer uses LiteDB so the cart can be stored without a relational database.

## Project structure notes

- The business layer does not know anything about LiteDB.
- The data-access layer depends on the business layer and only handles storage concerns.
- The tests show that the design can be verified both as isolated business logic and as a persistence-backed flow.

## NFR notes

### Testability

Testability is demonstrated by separating the business logic from the persistence implementation.

- The unit tests cover cart rules such as merging quantities and removing items.
- The integration-style test covers saving and loading a cart through the LiteDB repository.
- The architecture makes it possible to test business logic without starting a full application.

### Extensibility

The design can be extended with limited impact if new requirements appear.

- A different NoSQL database can replace LiteDB by introducing another DAL implementation.
- New cart operations can be added in the business layer without changing the test structure.
- Additional item metadata can be supported by extending the domain model and the DAL mapping.

### Cost of extension

- Changing the storage engine mainly affects the DAL.
- Adding new business rules mainly affects the BLL.
- Changing the cart item shape may require changes in both the BLL model and the DAL mapper.

## Validation

The solution was validated with `dotnet test` and the Task 1 test suite passed successfully.