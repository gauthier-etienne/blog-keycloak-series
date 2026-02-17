# Keycloak Blog Series: From Zero to Hero 🔐

A comprehensive blog series demonstrating how to integrate **Keycloak** authentication and authorization with **.NET Aspire** and **Blazor** applications.

## 🎯 About This Series

This repository serves as the companion code for a multi-part blog series that takes you from the basics of identity management to advanced Keycloak configurations. Whether you're new to authentication or looking to master Keycloak, this series has you covered.

## 📚 Series Overview

| Part | Topic | Status | README | Blog Post |
|------|-------|--------|--------|--------|
| **Part 1** | Project Setup & Basic OIDC Authentication | ✅ Posted | [README-Part1.md](README-Part1.md) | [Part 1](https://www.etiennegauthier.blog/post/getting-started-with-keycloak-an-introductory-guide-for-aspire-and-blazor-server)
| **Part 2** | Social Login (Google & GitHub) | 🔜 Coming Soon | - | - |
| **Part 3** | Securing APIs with Bearer Tokens | 📋 Planned | - | - |
| **Part 4** | Role-Based Access Control (RBAC) | 📋 Planned | - | - |
| **Part 5** | Token Management & Refresh Strategies | 📋 Planned | - | - |
| **Part 6** | Multi-Tenancy with Keycloak Realms | 📋 Planned | - | - |
| **Part 7** | Custom Themes & Branding | 📋 Planned | - | - |
| **Part 8** | Production Deployment & Best Practices | 📋 Planned | - | - |

## 🛠️ Tech Stack

- **.NET 10** - Latest .NET framework
- **.NET Aspire** - Cloud-ready stack for distributed applications
- **Blazor Server** - Interactive web UI framework
- **Keycloak** - Open-source Identity and Access Management
- **OpenID Connect (OIDC)** - Authentication protocol
- **OAuth 2.0** - Authorization framework

## 🏗️ Project Structure

```
blog-keycloak-series/
├── blog-keycloak-series.AppHost/         # .NET Aspire orchestrator
├── blog-keycloak-series.Web/             # Blazor Server web application
├── blog-keycloak-series.ApiService/      # Backend API service
├── blog-keycloak-series.Domain/          # Shared domain models
├── blog-keycloak-series.ServiceDefaults/ # Shared service configurations
├── README.md                             # This file
└── README-Part{N}.md                     # Part-specific documentation
```

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for running Keycloak)
- IDE: [Visual Studio 2022+](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) with C# DevKit

### Running the Application

1. **Clone the repository**
   ```bash
   git clone https://github.com/gauthier-etienne/blog-keycloak-series.git
   cd blog-keycloak-series
   ```

2. **Run the Aspire AppHost**
   ```bash
   cd blog-keycloak-series.AppHost
   dotnet run
   ```

3. **Open the Aspire Dashboard**
   - The dashboard URL will be displayed in the console
   - Navigate to the Web application endpoint

## 📖 What You'll Learn

By following this series, you will gain expertise in:

- ✅ Setting up Keycloak with .NET Aspire
- ✅ Implementing OpenID Connect authentication
- ✅ Understanding PKCE and security best practices
- 🔜 Integrating social identity providers (Google, GitHub, etc.)
- 📋 Configuring role-based and attribute-based access control
- 📋 Securing APIs with JWT bearer tokens
- 📋 Managing token lifecycles and refresh strategies
- 📋 Implementing multi-tenant architectures
- 📋 Customizing Keycloak themes and login pages
- 📋 Deploying Keycloak in production environments

## 🎓 Target Audience

This series is designed for:

- .NET developers new to identity management
- Developers migrating from other identity providers to Keycloak
- Teams looking to implement enterprise-grade authentication
- Anyone wanting to understand OAuth 2.0 and OIDC in practice

## 🔗 Blog Posts

Links to the accompanying blog posts will be added here as they are published.

## 🤝 Contributing

Found an issue or have a suggestion? Feel free to:

- Open an [Issue](https://github.com/gauthier-etienne/blog-keycloak-series/issues)
- Submit a [Pull Request](https://github.com/gauthier-etienne/blog-keycloak-series/pulls)

## 📄 License

This project is for educational purposes as part of a blog series.

## 👤 Author

**Etienne Gauthier**

- GitHub: [@gauthier-etienne](https://github.com/gauthier-etienne)

---

⭐ **Star this repository** if you find it helpful!
