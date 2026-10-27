_Questions for the self-check:_

1. What are the cons and pros of the Monolith architectural style?

   Pros of a monolithic architecture:
   - Simpler to design, develop, test, and deploy at the beginning.
   - Easier to debug and trace because everything runs as one application.
   - Lower operational complexity and fewer network boundaries.
   - Better performance in many cases because components communicate in-process.
   - Transactions are easier to manage when all logic uses a single database.

   Cons of a monolithic architecture:
   - Harder to scale specific parts of the system independently.
   - A bug or issue in one module can affect the whole application.
   - The codebase becomes large and difficult to maintain over time.
   - Release cycles are slower because changes often require redeploying the entire system.
   - It is harder to adopt different technologies and evolve modules independently.

2. What are the cons and pros of the Microservices architectural style?

   Pros of microservices:
   - Independent deployment and scaling of each service.
   - Teams can work on separate services in parallel.
   - Better fault isolation: one service can fail without bringing down the whole system.
   - More flexibility in choosing technologies per service.
   - Easier to evolve individual components without changing the entire application.

   Cons of microservices:
   - More complex architecture: inter-service communication, networking, and monitoring are harder.
   - Distributed transactions and data consistency are more difficult to manage.
   - Increased operational overhead: containers, orchestration, CI/CD, logging, and observability.
   - Debugging and testing are more complex because multiple services are involved.
   - Network latency and communication overhead may reduce performance.

3. What is the difference between SOA and Microservices?

   SOA (Service-Oriented Architecture) is an enterprise-wide integration approach focused on reusable business services. It often uses an ESB and tends to be more standardized and coarse-grained.

   Microservices are a smaller, more decentralized style where each service focuses on one business capability and is usually independently deployable. They communicate through lightweight APIs, often REST or gRPC, and usually own their own database.

   Main differences:
   - SOA is more enterprise-centric and standardized; microservices are more product- and team-centric.
   - SOA services are typically larger and more reusable; microservices are smaller and more focused.
   - SOA often relies on shared infrastructure such as an ESB; microservices favor lightweight integration.
   - SOA is more about enterprise integration, while microservices are about scalable independent service delivery.

4. [Open question] What does hybrid architectural style mean? Think of your current and previous projects and try to describe which architectural styles they most likely followed.

   A hybrid architectural style means combining several patterns instead of using only one. In real systems, teams often mix monolithic and microservice components, add event-driven integration, use APIs, or include serverless functions for specific tasks.

   Example: a company may keep a monolith for core business operations but expose APIs, move some workloads to background workers, and use microservices for highly scalable or independently managed features. This approach balances simplicity with flexibility.

   In this projects, a typical hybrid architecture may look like: monolith for the main application, plus API gateway, message broker, background jobs, and a few microservices for payment, notifications, or reporting.

5. Name several examples of the distributed architectures. What do ACID and BASE terms mean.

   Examples of distributed architectures:
   - Client-server architecture
   - N-tier architecture
   - Service-oriented architecture (SOA)
   - Microservices architecture
   - Event-driven architecture
   - Peer-to-peer architecture
   - Hybrid distributed architecture
   - Serverless architecture

   ACID is a set of properties for reliable transaction processing:
   - Atomicity: either all parts of a transaction succeed or none do.
   - Consistency: data remains valid after the transaction.
   - Isolation: transactions do not interfere with each other.
   - Durability: committed data is not lost.

   BASE is a more relaxed model used in distributed and NoSQL systems:
   - Basically Available: the system remains available most of the time.
   - Soft State: data may change over time without constant writes.
   - Eventual Consistency: the system converges to a consistent state after some delay.

   ACID prioritizes correctness and consistency, while BASE prioritizes availability and scalability.

6. Name several use cases where Serverless architecture would be beneficial.

   Serverless is useful for workloads that are event-driven, bursty, or intermittent. Common use cases include:
   - REST APIs and backend services with unpredictable traffic.
   - File upload and image processing pipelines.
   - Scheduled jobs and ETL tasks.
   - Notification systems and message consumers.
   - Event-driven workflows such as order processing or payment confirmation.
   - Webhooks and integrations with third-party services.
   - IoT data ingestion and stream processing.
   - Short-running data transformation tasks.

   Serverless is attractive because it reduces infrastructure management and you pay only for the compute time actually used.