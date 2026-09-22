_Please, complete the following task:_ 

**Tasks 1 (week 1):**

Choose and configure any message broker and setup it
- Azure Service Bus
- RabbitMQ
- Kafka
- Other

**Task 2 (week 2):**

Create API for interaction with the chosen message broker.

**Functional Requirements:**

Implement the interaction between Catalog and Cart services via a message broker. When a user or external system changes the property’s value of any item in the catalog (e.g., name or price), it will be necessary to update this item in the cart.

**Recommendations**

1. Choose any message broker
2. Create a client with listener and publisher parts
3. Call publisher from catalog service
4. Call listener from cart service

**Non-functional Requirements (NRF):**

The new integration solution should guarantee message delivery between two services. In the case of failure, the solution should include correct work with the delayed messages.

**NB! Score board:** 

* 1-59 – First task has been completed.  
* 60-89 – Second task has been completed.
* 90-100 – A mentee can lead a discussion on the “Self-check questions” topics. 