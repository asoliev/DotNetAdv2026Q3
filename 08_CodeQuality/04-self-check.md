_Questions for the self-check:_

1. Which NFRs are affected by the code quality? What are the code quality metrics?

Code quality mainly affects maintainability, reliability, security, performance, and testability. In practice, the common code quality metrics are code smells, bugs, vulnerabilities, security hotspots, coverage, duplication, complexity, and rule violations.

2. What are the goals of static code analysis? How many code analyzers can be used on a project?

Static code analysis is used to find defects early, enforce coding standards, improve security and maintainability, and reduce manual review effort. A project can use multiple analyzers at the same time, as long as they do not conflict and the team keeps the rules aligned.

3. How to ensure every team member will follow a style guide? How to apply style guide for the legacy projects?

The style guide should be enforced automatically with shared configuration files such as `.editorconfig`, `Directory.Build.props`, formatters, analyzers, and CI checks. For legacy projects, introduce the style rules gradually, fix the most important warnings first, and apply automatic formatting plus code cleanup in small steps instead of trying to rewrite everything at once.

4. Which are the pros and cons of the SonarQube analyzer? Think of a criterion to use it on a project?

Pros: it gives one central place for code quality, highlights bugs and security issues, tracks technical debt, and can be enforced in CI through quality gates. Cons: it can produce false positives, may require extra setup and tuning, and can be too strict for small or experimental projects. A good criterion to use it is whether the project is important enough to justify automated quality enforcement and whether the team is ready to fix the findings it produces.

5. What are the quality gates?

Quality gates are pass/fail conditions applied to an analysis result. They define whether a project is acceptable based on metrics such as reliability, security, maintainability, coverage, and duplication. If the project does not meet the thresholds, the gate fails.
