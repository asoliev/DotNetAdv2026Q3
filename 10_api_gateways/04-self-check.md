_Questions for the self-check:_

1. What does an API gateway do? What is the implementation of the API gateways?

	An API gateway is a single entry point for clients accessing backend services. It routes requests to the appropriate service and can handle cross-cutting concerns such as authentication, rate limiting, TLS termination, caching, logging, and request/response transformations. It can also aggregate responses from several services to reduce client round trips. Business logic should generally remain in backend services.

	It is typically implemented as a reverse proxy with configurable routes and middleware or policies. A request passes through these policies, is forwarded to a backend, and the response returns through the gateway. Gateways are usually replicated behind a load balancer for availability and scale.

	Examples include managed services such as Azure API Management and Amazon API Gateway, and self-hosted products such as Kong and Apache APISIX. In .NET, Ocelot provides API gateway features, while YARP is a reverse-proxy toolkit that can be extended with ASP.NET Core middleware to build a gateway.

2. Compare cloud-based gateways with the self-hosted ones, when choose either of them?

	**Cloud-based managed gateways:** The provider operates the infrastructure and offers capabilities such as monitoring, security policies, and scaling, depending on the product and tier. They reduce operational work and integrate well with cloud services. Tradeoffs include usage or capacity costs, quotas, vendor lock-in, and less control over customization and deployment location.

	**Self-hosted gateways:** Your team runs the gateway on its own infrastructure, which may also be in a public cloud. They offer more control over configuration, plugins, network placement, and data handling. However, your team must manage upgrades, security patches, scaling, monitoring, and high availability. Total cost includes infrastructure and operational effort, not just licensing.

	Choose a managed gateway when fast delivery, cloud integration, and reduced operational responsibility are priorities. Choose a self-hosted gateway when you need specialized behavior, strict infrastructure or data-location control, or deployment in disconnected environments, and have the team to operate it. Neither option is inherently faster or cheaper; compare your workload, required features, and total cost.

3. What is Backend for Frontend (BFF) pattern? What are the pros and cons of it?

	Backend for Frontend means providing a dedicated backend API for a particular frontend experience, such as separate BFFs for a web application and a mobile application. Each BFF adapts and aggregates backend data to match its client's needs. For example, a mobile BFF might return a compact product summary, while a web BFF returns richer product details.

	A general API gateway primarily handles shared routing and policies; a BFF focuses on client-specific API composition. They can coexist, with a gateway routing requests to separate BFFs.

	**Pros:** Fewer client round trips, less over-fetching, simpler frontend code, and APIs that evolve independently for each client. Frontend teams can own their BFF and tailor it to their release cycle.

	**Cons:** More services to deploy, secure, monitor, and maintain; possible duplication between BFFs; an additional network hop; and more complex failure handling when aggregating services. Shared domain rules should stay in backend services to prevent inconsistent business behavior.

4. How can you degrade or improve API Gateway performance?

	**What degrades performance:** Blocking I/O, expensive transformations, large payloads, excessive synchronous logging, repeated remote authentication calls, and too many middleware stages increase request-processing time. Sequential backend calls, cross-region traffic, connection churn, and slow downstream services increase latency. Unbounded concurrency, excessive retries, and insufficient resources can exhaust the gateway or its backends.

	**How to improve performance:**

	- Use asynchronous I/O, pooled outbound connections, and appropriate connection and concurrency limits.
	- Cache suitable responses with correct expiration and cache keys. Do not share user-specific cached data across users.
	- Parallelize independent backend calls with bounded concurrency, reduce unnecessary calls, and keep payloads small. Use compression when bandwidth savings justify the CPU cost.
	- Validate tokens locally when the authentication design permits it, and cache signing keys appropriately without weakening validation.
	- Configure timeouts, circuit breakers, rate limits, and load shedding to protect capacity. Use bounded retries with backoff and jitter only when requests are safe to retry.
	- Keep the gateway close to its backends, remove unnecessary processing, and use buffered logging with sensible sampling.
	- Scale gateway instances horizontally and ensure backend capacity is sufficient; gateway scaling alone cannot fix a downstream bottleneck.

	Verify improvements with representative load tests. Measure throughput, p50/p95/p99 latency, error rates, CPU, memory, and downstream timings. Separate gateway overhead from backend latency before deciding what to optimize.
