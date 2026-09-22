_Please, complete the following task:_ 

Create API Gateway which combines both **Catalog & Cart Service** API endpoints so clients can query everything from a single endpoint.

**Functional Requirements:**

1. Extend catalog service with new endpoint which returns a list of product properties for the specific Product. Dictionary of key/value pairs (for example: category = Samsung, model = s10). _Note! You can hardcode return data to save time._
2. Add an API Gateway in front of Catalog & Cart Service API.
3. On an API Gateway level and an aggregate endpoint which would aggregate returned details of Product and Product properties for the specific product id.
    

**Non-functional Requirements (NFR):**

1. Security. All Endpoints from Catalog service which mutates data (POST/PUT/PATCH/DELETE) should be accessible by the admins (Role=admin) only. Use JWT Bearer type of authentication for this.
2. Performance. All Endpoints from the Catalog service which return list of entities (categories, products) should have 1 minute cache.
3. Self-documented. API Gateway should have swagger-based documentation (reuse documentation from the original APIs if it is possible).
    

**Constraints:**

Soft! Use Ocelot as an API Gateway.

**NB!** Scoreboard:

* 1-59 – Constrains & functional requirements have been met (make sure to follow Clean Code practices).
* 60-89 – Non-functional requirements have been met.
* 90-100 – The written answers to the ‘Self-check questions’ have been provided without significant issues.