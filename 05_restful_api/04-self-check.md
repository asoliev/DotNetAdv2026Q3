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
	- Answer: OData (Open Data Protocol) is a standardized way to expose and query data over HTTP. Alongside ordinary resource operations, it defines conventions for query options, metadata, and representations, so clients can discover a schema and ask the server for selected or related data. Common query options include `$filter` (restrict rows), `$select` (choose fields), `$orderby` (sort), `$top` and `$skip` (page), and `$expand` (include related resources). For example, `GET /Products?$filter=Price gt 50&$orderby=Name&$top=10&$select=Id,Name,Price` asks for the first ten matching products with only those fields. The service still needs to validate and authorize queries, limit expensive operations and page sizes, and avoid exposing fields or relationships clients should not access.
	- Choose OData when the API serves data-oriented use cases—such as reporting, analytics, or enterprise applications—with multiple clients that need flexible, server-side filtering and projection. Its standard conventions can reduce the need to invent a different query language for every endpoint. Avoid it when the API has a small set of fixed operations, requires carefully curated response shapes, or has a workflow-oriented domain (for example, placing an order); in those cases, explicit endpoints are often easier to secure, document, optimize, and evolve. OData's flexibility comes with implementation, performance, security, and learning costs, and clients may become coupled to the exposed data model.
	- Detailed example: An authorized reporting client wants the two most recent open orders worth at least 100, with only each order's ID, date, total, and the customer's name. It can request:

		`GET /odata/Orders?$filter=Status eq 'Open' and Total ge 100&$select=Id,CreatedAt,Total&$expand=Customer($select=Name)&$orderby=CreatedAt desc&$top=2&$skip=0&$count=true`

		Here, `$filter` selects matching orders, `$select` limits order fields, `$expand` includes the related customer but only its name, `$orderby` sorts newest first, `$top` and `$skip` page the results, and `$count=true` asks for the total number of matches. A simplified OData JSON response might look like:

		```json
		{
		  "@odata.context": "/odata/$metadata#Orders(Id,CreatedAt,Total,Customer(Name))",
		  "@odata.count": 18,
		  "value": [
		    {
		      "Id": 731,
		      "CreatedAt": "2026-10-28T14:30:00Z",
		      "Total": 245.50,
		      "Customer": { "Name": "Northwind Market" }
		    },
		    {
		      "Id": 728,
		      "CreatedAt": "2026-10-27T09:15:00Z",
		      "Total": 125.00,
		      "Customer": { "Name": "Contoso Retail" }
		    }
		  ],
		  "@odata.nextLink": "/odata/Orders?$skip=2&..."
		}
		```

		The `@odata.nextLink` gives the client a continuation URL for the next page; the client should follow that URL rather than manually constructing it. In a real service, allow only supported query options and fields, cap page sizes, and consider the cost of filters and expansions. A shopping-cart API is usually clearer with explicit operations such as `POST /carts/{id}/items` than with arbitrary client-built data queries.

9. What is Richardson Maturity Model? Is it always a good idea to reach the 3rd level of maturity?
	- Answer: The Richardson Maturity Model (RMM), popularized by Leonard Richardson, is a way to discuss how an HTTP API uses REST-related ideas. Level 0 is a single endpoint and a single interaction style, often resembling RPC over HTTP. Level 1 introduces separate resources and resource identifiers. Level 2 uses HTTP methods and status codes according to their semantics—for instance, `GET` to retrieve, `POST` to create, and `404 Not Found` when a resource is missing. Level 3 adds hypermedia controls: responses include links or other controls that tell clients which actions or transitions are currently available. The levels are a useful conceptual model, not a compliance checklist or a measure of overall API quality; security, consistency, usability, and documentation matter at every level.
	- Level 3 is not automatically the right target. Hypermedia can let clients discover next actions and reduce hard-coded workflow assumptions, which is valuable when workflows or available actions vary over time. But it requires a representation format and client conventions that both sides understand, and many teams and tools do not need or support that extra indirection. If clients already know a stable set of routes and the API has straightforward operations, a well-designed Level 2 API may be the clearer, cheaper choice. Choose Level 3 when discoverable, evolving workflows provide concrete value—not just to claim a higher maturity level.
	- Examples using an order API:
		- **Level 0 — one endpoint / RPC-style:** The client sends `POST /api` with `{"action":"getOrder","orderId":42}`. The endpoint and HTTP method stay the same while the action is encoded in the request body.
		- **Level 1 — resources:** The API gives orders their own URIs, such as `POST /orders/42` to address order 42, but may still use `POST` for different operations rather than applying HTTP methods consistently.
		- **Level 2 — HTTP methods and status codes:** The client retrieves an order with `GET /orders/42` and receives `200 OK` (or `404 Not Found` if it does not exist). It can request cancellation with `POST /orders/42/cancel`, receiving an appropriate response such as `200 OK` or `409 Conflict` if the order can no longer be canceled.
		- **Level 3 — hypermedia controls (HATEOAS):** HATEOAS means **Hypermedia as the Engine of Application State**. The response does more than return the order's current state: it also advertises relevant links and actions, using relationship names such as `self`, `payment`, or `cancel`. The client reads those controls and follows the one appropriate to its goal, rather than constructing the next URL from a route pattern or assuming every operation is available. For example, a pending order might be represented in a Siren-style format like this:

			```json
			{
			  "class": ["order"],
			  "properties": { "id": 42, "status": "pending", "total": 49.99 },
			  "links": [
			    { "rel": ["self"], "href": "/orders/42" },
			    { "rel": ["payment"], "href": "/orders/42/payment" }
			  ],
			  "actions": [
			    {
			      "name": "cancel",
			      "title": "Cancel this order",
			      "method": "POST",
			      "href": "/orders/42/cancel"
			    }
			  ]
			}
			```

			The client can display or invoke the advertised `cancel` action using its supplied method and URL. If the order has already shipped, the server can return a representation without that action and instead advertise relevant next steps, such as tracking. This makes the available workflow discoverable and lets the server change link destinations or allowed transitions without requiring clients to hard-code them. The media type and action conventions still need to be documented and supported by clients; hypermedia does not eliminate the need for shared semantics.

10. What does pros and cons REST have in comparison with other web API types?
	- Answer: REST is simple, cache-friendly, widely supported, and good for resource-oriented CRUD and public HTTP APIs. Compared with RPC or gRPC, it is usually less efficient for highly chatty or strongly typed internal service calls. Compared with GraphQL, it is simpler to cache and reason about, but less flexible for clients that need custom-shaped responses. Compared with message-based APIs, it is easier for synchronous request/response flows, but less suitable for event-driven workflows.
	- Example: A public product catalog fits REST well; a high-throughput internal telemetry stream often fits gRPC or messaging better.