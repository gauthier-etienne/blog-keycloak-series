# Blog Keycloak Series - Part 1: Project Setup & Basic Authentication

## Overview

This is **Part 1** of the Blog Keycloak Series, demonstrating how to integrate Keycloak authentication with a .NET Aspire Blazor application.

In this part, we set up the foundational project structure and implement basic OpenID Connect (OIDC) authentication using Keycloak as the identity provider.

## Project Structure

```
blog-keycloak-series/
├── blog-keycloak-series.AppHost/       # .NET Aspire orchestrator
├── blog-keycloak-series.Web/           # Blazor Server web application
├── blog-keycloak-series.ApiService/    # API service backend
├── blog-keycloak-series.Domain/        # Shared domain models
└── blog-keycloak-series.ServiceDefaults/ # Shared service configurations
```

## Technologies Used

- **.NET 10** - Latest .NET framework
- **.NET Aspire 13.1** - Cloud-ready stack for distributed applications
- **Blazor Server** - Interactive web UI framework
- **Keycloak** - Open-source identity and access management
- **OpenID Connect (OIDC)** - Authentication protocol

## Key Features in Part 1

- ✅ .NET Aspire project setup with Keycloak hosting integration
- ✅ Blazor Server application with interactive components
- ✅ OpenID Connect authentication configuration
- ✅ Cookie-based session management
- ✅ PKCE (Proof Key for Code Exchange) security

## Keycloak Configuration

The application connects to a Keycloak realm named `movie-library` with the following settings:

| Setting | Value |
|---------|-------|
| Realm | `movie-library` |
| Client ID | `movielibraryweb` |
| Authority | `http://localhost:8888/realms/movie-library` |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for running Keycloak via Aspire)
- IDE: Visual Studio 2022+ or VS Code with C# DevKit

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/gauthier-etienne/blog-keycloak-series.git
   cd blog-keycloak-series
   ```

2. **Configure User Secrets** (for client secret)
   ```bash
   cd blog-keycloak-series.Web
   dotnet user-secrets set "Keycloak:ClientSecret" "your-client-secret"
   ```

3. **Run the Aspire AppHost**
   ```bash
   cd blog-keycloak-series.AppHost
   dotnet run
   ```

4. **Access the application**
   - Open the Aspire Dashboard (usually `https://localhost:17000`)
   - Navigate to the Web application endpoint

## Authentication Flow (OIDC + PKCE)

The application uses the **Authorization Code Flow with PKCE** (Proof Key for Code Exchange) for enhanced security:

```
┌──────────┐                    ┌──────────────┐                    ┌──────────────┐
│  Browser │                    │  Blazor App  │                    │   Keycloak   │
└────┬─────┘                    └──────┬───────┘                    └──────┬───────┘
     │                                 │                                   │
     │  1. Request protected resource  │                                   │
     │────────────────────────────────▶│                                   │
     │                                 │                                   │
     │                                 │  2. Generate PKCE:                │
     │                                 │     • code_verifier (random)      │
     │                                 │     • code_challenge = SHA256(cv) │
     │                                 │                                   │
     │  3. Redirect to /authorize      │                                   │
     │◀────────────────────────────────│                                   │
     │     + code_challenge            │                                   │
     │     + code_challenge_method=S256│                                   │
     │                                 │                                   │
     │  4. Authorization request ──────────────────────────────────────────▶
     │     (code_challenge included)   │                                   │
     │                                 │                                   │
     │  5. User authenticates ◀────────────────────────────────────────────▶
     │     (login form)                │                                   │
     │                                 │                                   │
     │  6. Redirect with auth code     │                                   │
     │◀────────────────────────────────────────────────────────────────────│
     │                                 │                                   │
     │  7. Auth code to app            │                                   │
     │────────────────────────────────▶│                                   │
     │                                 │                                   │
     │                                 │  8. Token request ───────────────▶│
     │                                 │     + auth_code                   │
     │                                 │     + code_verifier (proof)       │
     │                                 │                                   │
     │                                 │  9. Keycloak validates:           │
     │                                 │     SHA256(code_verifier) ==      │
     │                                 │     code_challenge ?              │
     │                                 │                                   │
     │                                 │  10. Tokens returned ◀────────────│
     │                                 │      (ID + Access + Refresh)      │
     │                                 │                                   │
     │  11. Session cookie set         │                                   │
     │◀────────────────────────────────│                                   │
     │      (tokens stored server-side)│                                   │
     │                                 │                                   │
     │  12. Authenticated response     │                                   │
     │◀────────────────────────────────│                                   │
     │                                 │                                   │
```

### Why PKCE?

PKCE protects against **authorization code interception attacks** by ensuring that only the client that initiated the flow can exchange the code for tokens. Even if an attacker intercepts the authorization code, they cannot use it without the original `code_verifier`.

## NuGet Packages

- `Aspire.Hosting.Keycloak` - Keycloak hosting integration for Aspire
- `Aspire.Keycloak.Authentication` - Keycloak authentication client
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` - OIDC support

## What's Next?

In **Part 2**, we will explore:
- Social login integration with **Google**
- Social login integration with **GitHub**
- Configuring Keycloak identity providers
- Linking social accounts to local users

---

## License

This project is part of a blog series for educational purposes.

## Author

Etienne Gauthier - [GitHub](https://github.com/gauthier-etienne)
