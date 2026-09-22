**Tasks 1 (week 1):**

Create REST based WEB API for **Catalog Service**.

**Functional Requirements:**

1. The following actions should be supported:
    - List of categories
    - List of products (filtration by category id and pagination)
    - Add category
    - Add product
    - Update category
    - Update product
    - Delete product
    - Delete category (with the related products)
        

**Non-functional Requirements (NFR):**

1. Testability
    - Show by example that you can cover your code with Unit and Integration tests, no need to cover all the lines
2. Richardson maturity model
    - All endpoints should correspond to at least the 2nd level of the maturity model.
    - Few of the endpoints should follow the 3rd level.
3. OpenAPI Specification
    - Each API version should have its own documentation.
    

**Task 2 (week 2):**

Create REST based WEB API for **Cart Service**.

**Functional Requirements:**

1. **Version 1** - API should support the following actions:
    - **Get cart info.** Input params: cart unique Key (string). Returns a cart model (cart key + list of cart items).
    - **Add item to cart.** Input params: cart unique Key (string) + cart item model. Returns 200 if item was added to the cart. If there was no cart for specified key – creates it. Otherwise returns a corresponding HTTP code.
    - **Delete item from cart.** Input params: cart unique key (string) and item id (int). Returns 200 if item was deleted, otherwise returns corresponding HTTP code.

2. **Version 2** – the same as Version 1 but with the following changes: a. **Get cart info.** Returns a list of cart items instead of cart model.
    

**Non-functional Requirements (NFR):**
1. Testability
2. Extensibility (via Versioning)
3. Self-Documented API.
    - API documentation should be generated during the build. XML-Docs could be used for providing details of the endpoints.

**Score board:**

* 1-59 – Constrains & functional requirements have been met (make sure to follow Clean Code practices).
* 60-89 – Non-functional requirements have been met.
* 90-100 – The written answers to the ‘Self-check questions’ doesn’t have significant issues.