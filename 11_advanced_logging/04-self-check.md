_Questions for the self-check:_

1. Why Serviceability and Observability NFRs are important? Who would you cover them with? 

	Serviceability and observability make it possible to operate, diagnose, and restore a system efficiently, especially when failures span multiple services. They reduce incident impact and help teams detect performance or reliability problems before they become widespread. Define and review these non-functional requirements with developers, operations/SRE, QA, support, and product stakeholders; agree on measurable targets such as availability, latency, alerting, and time to diagnose or recover.

2. What is the difference between logging and tracing? What are the peculiarities of logging and tracing in a distributed environment? 

	Logging records discrete events, usually with a timestamp, severity, message, and structured fields. Tracing follows one operation across components as a tree of timed spans, showing its path and where time was spent. In a distributed system, events and spans are produced by different services, so they need shared trace/correlation identifiers and propagated context to be connected. Account for clock differences, asynchronous work, high telemetry volume, and trace sampling; structured logs linked to trace IDs make investigation easier.

3. What is the Correlation Context? How can we pass it through all hierarchy of the services? 

	Correlation context is metadata that identifies and relates work as it moves across service and process boundaries. It commonly includes trace and span identifiers, and may include baggage for small cross-service values. Propagate it in standard headers, such as W3C Trace Context (`traceparent` and `tracestate`), across HTTP requests and message-broker headers. Use framework or OpenTelemetry instrumentation to extract and inject the context, and preserve it when starting asynchronous work. Avoid putting secrets or sensitive data in baggage.

4. Which APM to choose? Think of some criteria. 

	Choose an APM (Application Performance Management) that fits the application's platforms and deployment environment and provides the signals and workflows the team needs: metrics, logs, distributed traces, dependency maps, dashboards, and actionable alerts. Compare instrumentation effort and integrations, scalability and sampling controls, data residency and security, retention, pricing at expected volume, operational burden, support, and compatibility with open standards such as OpenTelemetry. A short proof of concept using representative services and an incident scenario is more useful than choosing by feature list alone.

5. What is Open Telemetry?

	OpenTelemetry (OTel) is a vendor-neutral, open-source set of APIs, SDKs, conventions, and tools for generating, collecting, and exporting telemetry such as traces, metrics, and logs. Applications can instrument once and export data using OTLP to compatible backends, reducing dependence on a particular vendor. OpenTelemetry is not itself an APM backend; a separate observability platform or collector pipeline stores, queries, and visualizes the data.