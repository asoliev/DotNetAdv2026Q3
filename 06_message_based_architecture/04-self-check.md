_Questions for the self-check:_

1. What is Message Based Architecture? What is the difference between Message Based Architecture and Event Based Architecture?’

    Message Based Architecture is an approach where services communicate by sending messages through a channel instead of calling each other directly. It is usually focused on decoupled request/response or command-style communication. Event Based Architecture is a subset where services publish facts about something that already happened, and multiple consumers can react to that event asynchronously.


2. What is Message Broker? How do message brokers work?

    A message broker is middleware that receives messages from producers, stores or routes them, and delivers them to consumers. It typically handles queueing, routing, retries, and acknowledgments so the sender and receiver do not need to be online at the same time.


3. When should you use message brokers?

    Use a message broker when you need asynchronous communication, loose coupling, buffering during load spikes, integration between independent services, or reliable delivery with retry handling.


4. Name and describe any distribution pattern.

    Publish/subscribe is a common distribution pattern. A publisher sends one message to a broker, and the broker forwards it to all subscribers interested in that topic or routing key.


5. What are the advantages and disadvantages of using message broker?

    Advantages: loose coupling, asynchronous processing, better scalability, resilience, and easier integration across services.

    Disadvantages: more infrastructure, harder debugging, eventual consistency, message duplication risks, and operational overhead.


6. What is the difference between Queue and Topic?

    A queue usually delivers each message to one consumer instance, which is useful for load balancing work. A topic is used for fan-out or filtering, where the same message can be delivered to multiple subscribers based on routing rules.


7. What are the typical failures in MBA? How can you address them? What is Saga pattern?

    Typical failures include consumer downtime, lost messages, duplicate delivery, timeouts, and partial business completion. They are handled with persistence, acknowledgments, retries, dead-letter queues, idempotent consumers, and correlation IDs. Saga is a pattern for coordinating multi-step distributed work using a sequence of local transactions and compensating actions when one step fails.
