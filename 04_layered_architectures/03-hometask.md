_Please, complete the following task:_ 

**Task 1 (week 1):**

Create BLL (business logic layer) and DAL (data-access layer) for **Cart Service.** Any implementation of the layered architecture can be used. Layers should be logically separated (via separate folders/namespaces).

**Functional Requirements:**

1. Single entity – Cart
2. Cart has a unique id which is maintained (generated) on the client-side.
3. The following actions should be supported:
    - Get list of items of the cart object.
    - Add item to cart.
    - Remove item from the cart.
        
4. Each item contains the following info:
    - Id – required, id of the item in external system (see Task 2), int.
    - Name – required, plain text.
    - Image – optional, URL and alt text.
    - Price – required, money.
    - Quantity – quantity of items in the cart.
        

**Non-functional Requirements (NFR):**

1. Testability (show by example that you can cover your code with Unit and Integration tests, no need to cover all the lines)
2. Extensibility (describe for what points/costs your code can be extended)
    

**Constraints:**

1. NoSQL database for persistence layer (for example - [https://www.litedb.org/](https://www.litedb.org/)).
2. Layers should be logically separated.
    

**Tasks 2 (week 2):**

Create BLL (business logic layer) and DAL (data-access layer) for **Catalog Service**. You must follow Clean Architecture with physical layers separation (via separate DLLs).

**Constraints:**

1. SQL database for persistence layer (for example - Microsoft SQL Server Database File).
2. Layers should be physically separated.
    

**Functional Requirements:**

1. Key entities: Category, Product.
2. Category has:
    1. Name – required, plain text, max length = 50.
    2. Image – optional, URL.
    3. Parent Category – optional
3. The following operations are allowed for Category: get/list/add/update/delete.
4. Product has:
    - Name – required, plain text, max length = 50.
    - Description – optional, can contain html.
    - Image – optional, URL.
    - Category – required, one item can belong to only one category.
    - Price – required, money.
    - Amount – required, positive int.
5. The following operations are allowed for Item: get/list/add/update/delete.

**Non-functional Requirements (NFR):**

1. Testability (show by example that you can cover your code with Unit and Integration tests, no need to cover all the lines)
2. Extensibility (describe for what points/costs your code can be extended)

**Score board:**

* 1-59 – Functional requirements for both tasks have been met (make sure to follow Clean Code practices).
* 60-89 ­– Non-functional requirements for both tasks have been met.
* 90-100 – The written answers to the ‘Self-check questions’ have been provided without significant issues.