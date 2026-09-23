_Questions for the self-check:_

1. What is the difference between authentication and authorization?

    Authentication verifies who a user or system is. Authorization decides what that authenticated identity is allowed to access or do.


2. What authorization approaches can you list? What is role-based access control?

    Common authorization approaches include role-based access control (RBAC), attribute-based access control (ABAC), policy-based access control, and claim-based access control. RBAC grants permissions based on roles assigned to users, such as Admin, Manager, or Reader.

    ABAC makes decisions using attributes such as user department, resource type, request time, or location.
    Policy-based authorization defines rules in a higher-level policy and then evaluates those rules against roles, claims, or attributes.
    Claim-based authorization uses identity claims, such as email, department, or permission flags, to decide access.

    In practice, RBAC is simple and easy to manage, while ABAC and policy-based approaches are more flexible for complex business rules.


3. What exactly is Identity Management (Identity and Access Management)?

    Identity and Access Management (IAM) is the set of processes and technologies used to create, manage, authenticate, authorize, and govern digital identities and their access to resources.

    It covers the full identity lifecycle, from user registration and account provisioning to role assignment, access reviews, password or credential management, and account deprovisioning.

    In practice, IAM helps ensure that the right users get the right access at the right time, and that access is removed when it is no longer needed.

    Typical IAM capabilities include single sign-on, multi-factor authentication, role and policy management, user lifecycle automation, and auditing or compliance reporting. In a company, IAM is usually implemented through an identity provider, directory service, or access management platform that connects employees, applications, and APIs under one security model.

    IAM was not invented by one person or in one moment. It evolved over time as organizations needed a centralized way to manage users, permissions, and trust across many systems. Early building blocks came from directory services, network authentication, and single sign-on, then later expanded into modern identity providers, federation, and cloud-based access management.

    In practice IAM is often delivered as a separate service or platform, such as an identity provider or access management solution, but the same capabilities can also be built into an application or enterprise platform.

    The main IAM building blocks are an identity provider, a user directory, authentication methods, authorization policies, and lifecycle workflows for joining, moving, and leaving the organization. Common related standards and protocols are LDAP, SAML, OAuth 2.0, and OpenID Connect. In larger companies, IAM also includes governance features such as access reviews, privileged access management, and audit logs.

    Most programming languages do not include IAM as a built-in language feature. Instead, their ecosystems provide packages, SDKs, middleware, and provider integrations. For example, .NET has Microsoft Identity and ASP.NET Core authentication packages, Java has Spring Security, Node.js has Passport and auth libraries, and Python has packages such as Authlib and python-jose.


4. What authentication/authorization protocols do you know? What is the difference between OAuth & OpenID?

    Examples include OAuth 2.0, OpenID Connect, SAML, Kerberos, LDAP, and WS-Federation. OAuth is an authorization framework for delegated access, while OpenID Connect is an authentication layer built on top of OAuth 2.0 for verifying user identity.

    In practice, OAuth 2.0 is commonly used to let an app access an API on behalf of a user or another service, OpenID Connect adds sign-in and identity information, SAML is often used for enterprise single sign-on, Kerberos is common in Windows and internal corporate networks, LDAP is widely used for directory lookups and authentication against enterprise directories, and WS-Federation is another federation protocol mostly seen in older Microsoft-centric environments.

    They are standards or rule sets that define how two systems exchange identity or access information. Libraries and SDKs implement those standards for a specific language or framework. For example, an app uses a library to build the required redirect, token request, signature verification, or assertion parsing steps described by the protocol.


5. What is Authentication/Authorization Token.
    What is JWT token?
    What other approaches except authentication/authorization, can we use with security token?

    An authentication or authorization token is a credential issued after login or authorization that is presented to access protected resources.

    In many systems it is a bearer token, which means whoever has it can use it until it expires or is revoked. A JWT (JSON Web Token) is a compact, signed token that carries claims about the user or client in three parts: header, payload, and signature. Other token approaches include opaque tokens, reference tokens, SAML assertions, and API keys.

    In practice, access tokens are used to call APIs, while refresh tokens are used to get a new access token without asking the user to sign in again. Tokens may contain scopes, roles, user IDs, expiration time, and other claims that help the server decide what the caller is allowed to do.

    On the server side, JWTs are usually validated by checking the signature, issuer, audience, and expiration time. Opaque tokens are often validated by looking them up at the authorization server. The main security rule is that tokens should be treated like passwords: keep them short-lived, store them safely, send them only over HTTPS, and revoke them when needed.


6. What is Single Sign-On (SSO)? Name the steps to implement SSO. What are the benefits of SSO?

    SSO lets a user sign in once with a trusted identity provider and then access multiple applications without logging in again.
    Typical steps are: register applications with the identity provider, establish a trust relationship, redirect users to the provider for login, issue a token or assertion after successful authentication, and validate that token in each application. Benefits include fewer logins, better user experience, centralized security policy, and easier account management.

    In a typical flow, the application redirects the user to the identity provider, the provider authenticates the user, and then sends back a token or assertion. The application trusts that token and creates its own session, so the user does not need to repeat the login process for every app.
    SAML usually sends an XML assertion from the identity provider to the application, while OpenID Connect usually returns an ID token and sometimes an access token over OAuth 2.0-style flows. Both let different applications rely on the same central login.

    The main benefits are convenience for users, fewer password resets, easier centralized control for administrators, and better security when combined with MFA and a single place for access policy enforcement.


7. What is the difference between Two-Factor Authentication and Multi-Factor Authentication?

    Two-Factor Authentication (2FA) uses exactly two different factor types, such as password plus a one-time code. Multi-Factor Authentication (MFA) uses two or more factors, so 2FA is a specific case of MFA.

    The usual factor categories are something you know, something you have, and something you are. A password is something you know, a phone or hardware key is something you have, and a fingerprint or face scan is something you are. So a password plus SMS code is 2FA, while a password plus hardware key plus fingerprint is MFA with more than two factors.

    MFA is stronger because stealing one factor is usually not enough to get access.


8. Which of the OAuth flows can be used for user (customer) and which for client (server) authentication?

    For user authentication, the Authorization Code flow, usually with PKCE for public clients, is the standard choice. For client or server-to-server authentication, the Client Credentials flow is used because no end user is involved.

    In more detail, Authorization Code flow is used when a real user signs in through a browser and the application needs delegated access on that user’s behalf. PKCE makes that flow safer for mobile apps, single-page apps, and other public clients that cannot keep a secret.
    Client Credentials flow is used when one service talks directly to another service and authenticates with its own client ID and secret, certificate, or similar credential.

    The older Implicit flow and Resource Owner Password Credentials flow are generally avoided today because they are less secure or give too much responsibility to the client application. For modern apps, Authorization Code with PKCE and Client Credentials are the main recommended choices.
