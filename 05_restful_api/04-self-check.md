_Questions for the self-check:_

1. Explain the difference between terms: REST and RESTful. What are the six constraints?
	- Answer: REST is an architectural style for distributed systems. RESTful means an API follows REST constraints and resource-oriented design. The six constraints are: client-server, stateless, cacheable, uniform interface, layered system, and optional code-on-demand.
	- Example: `GET /products/42` is RESTful when it uses a resource path, standard HTTP semantics, and stateless requests.

2. HTTP Request Methods (the difference) and HTTP Response codes. What is idempotency? Is HTTP the only protocol supported by the REST?
	- Answer: HTTP methods differ by intent: `GET` reads, `POST` creates or processes, `PUT` replaces, `PATCH` partially updates, `DELETE` removes, `HEAD` and `OPTIONS` inspect metadata or capabilities. Common response codes: `200` OK, `201` Created, `204` No Content, `400` Bad Request, `401` Unauthorized, `403` Forbidden, `404` Not Found, `409` Conflict, `500` Server Error. Idempotency means repeating the same request has the same effect as sending it once; `GET`, `PUT`, and `DELETE` are typically idempotent, `POST` usually is not. REST is not limited to HTTP, but HTTP is the dominant protocol used in practice.
	- Example: Sending `DELETE /products/42` twice should still leave product `42` deleted.

3. What are the advantages of statelessness in RESTful services?
	- Answer: Statelessness improves scalability, reliability, and testability because each request contains all required context. Servers do not need to keep session state, so requests can be routed to any instance, and failures are easier to recover from.
	- Example: Any instance behind a load balancer can handle `GET /orders/15` without checking server memory for a user session.

4. How can caching be organized in RESTful services?
	- Answer: Caching can be organized with HTTP cache headers such as `Cache-Control`, `ETag`, `Last-Modified`, and `Expires`. Public responses can be cached by browsers, proxies, CDN edges, or API gateways. Cache invalidation should be deliberate and tied to resource updates.
	- Example: A `GET /categories` response can include `ETag: "abc123"`, and the client can revalidate with `If-None-Match`.

5. How can versioning be organized in RESTful services?
	- Answer: Versioning can be done through the URL path, query string, custom headers, or media types. In ASP.NET Core, URL segment versioning is common because it is simple to document and route. The best choice is the one that is explicit, easy to consume, and does not break old clients.
	- Example: `/api/v1/products` and `/api/v2/products` can coexist while clients migrate gradually.

6. What are the best practices of resource naming?
	- Answer: Resource names should be noun-based, plural when representing collections, lowercase, and consistent. Use hierarchy only when the relationship is real, for example `/categories/{id}/products`. Avoid verbs in paths, ambiguous abbreviations, and deep nesting without need.
	- Example: Prefer `GET /products` over `GET /getProducts`.

7. What are OpenAPI and Swagger? What implementations/libraries for .NET exist? When would you prefer to generate API docs automatically and when manually?
	- Answer: OpenAPI is a machine-readable specification for describing HTTP APIs. Swagger is the original ecosystem around that spec, now commonly used to mean the UI and tooling. In .NET, common options include Swashbuckle.AspNetCore, NSwag, and Microsoft.AspNetCore.OpenApi in newer ASP.NET Core versions. Generate docs automatically when the API is code-first and changes often; write or supplement them manually when you need richer narrative, business rules, or curated examples that code cannot express well.
	- Example: Swagger UI can be generated from controllers, while a hand-written section may explain an unusual business rule like a payment cutoff.

8. What is OData? When will you choose to follow it and when not?
	- Answer: OData is a REST-adjacent protocol and query standard that adds rich querying, filtering, sorting, paging, and metadata conventions. It is useful for data-heavy APIs where clients need flexible server-side querying. It is usually a bad fit for small, simple, or tightly controlled public APIs because it adds complexity and broadens the surface area.
	- Example: A reporting API may benefit from OData-like filters, while a simple cart service usually does not.

9. What is Richardson Maturity Model? Is it always a good idea to reach the 3rd level of maturity?
	- Answer: The Richardson Maturity Model describes REST maturity in four levels: Level 0 uses one endpoint like RPC, Level 1 introduces resources, Level 2 uses proper HTTP verbs and status codes, and Level 3 adds hypermedia controls. Reaching Level 3 is not always necessary; many APIs are perfectly good at Level 2, and hypermedia can add complexity without enough value for the use case.
	- Example: A well-designed `/products/{id}` API with correct verbs and status codes can be strong Level 2 even without HATEOAS links.

10. What does pros and cons REST have in comparison with other web API types?
	- Answer: REST is simple, cache-friendly, widely supported, and good for resource-oriented CRUD and public HTTP APIs. Compared with RPC or gRPC, it is usually less efficient for highly chatty or strongly typed internal service calls. Compared with GraphQL, it is simpler to cache and reason about, but less flexible for clients that need custom-shaped responses. Compared with message-based APIs, it is easier for synchronous request/response flows, but less suitable for event-driven workflows.
	- Example: A public product catalog fits REST well; a high-throughput internal telemetry stream often fits gRPC or messaging better.