# Blog Keycloak Series - Part 2: Social Login with Google and GitHub

## Overview

This is **Part 2** of the Blog Keycloak Series, demonstrating how to integrate social identity providers (Google and GitHub) with Keycloak and your .NET Aspire Blazor application.

Building on the foundation from [Part 1](README-Part1.md), this part shows how to enable seamless social login, allowing users to authenticate using their existing Google or GitHub accounts instead of creating separate credentials.

## What is Social Login?

**Social Login** (also called **Social Sign-On**) enables users to authenticate using their existing accounts from popular identity providers. Instead of managing yet another username and password, users can:

- Sign in with their Google account
- Sign in with their GitHub account
- Automatically create an account in your system on first login
- Trust the identity verification that Google or GitHub has already performed

## Project Structure

The social login integration builds on the existing structure from Part 1:

```
blog-keycloak-series/
├── blog-keycloak-series.AppHost/       # .NET Aspire orchestrator (updated)
├── blog-keycloak-series.Web/           # Blazor Server web application
├── blog-keycloak-series.ApiService/    # API service backend
├── blog-keycloak-series.Domain/        # Shared domain models
└── blog-keycloak-series.ServiceDefaults/ # Shared service configurations
```

## Technologies Used

- **.NET 10** - Latest .NET framework
- **.NET Aspire 13.1** - Cloud-ready stack
- **Blazor Server** - Interactive web UI
- **Keycloak** - Identity provider with social login support
- **OpenID Connect (OIDC)** - Authentication protocol
- **OAuth 2.0** - Authorization framework

## Part 2 Features

- ✅ Configure Google identity provider in Keycloak
- ✅ Configure GitHub identity provider in Keycloak
- ✅ Capture user information from social providers
- ✅ Manage user attributes and metadata

## Prerequisites

### Required

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for Keycloak)
- IDE: Visual Studio 2022+ or VS Code with C# DevKit
- Completion of [Part 1](README-Part1.md)

### Social Provider Accounts & Credentials

#### Google OAuth 2.0 Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Create OAuth 2.0 credentials:
   - Go to **APIs & Services** > **Credentials**
   - Click **Create Credentials** > **OAuth client ID**
   - Choose **Web application**
   - Configure authorized redirect URIs:
     ```
     http://localhost:8888/realms/movie-library/broker/google/endpoint
     http://localhost:8888/realms/movie-library/broker/google/endpoint/callback
     ```
   - Copy the **Client ID** and **Client Secret**

#### GitHub OAuth Application

1. Go to GitHub Settings > [Developer settings > OAuth Apps](https://github.com/settings/developers)
2. Click **New OAuth App**
3. Fill in the form:
   - **Application name**: Your app name
   - **Homepage URL**: `http://localhost:3000`
   - **Authorization callback URL**: 
     ```
     http://localhost:8888/realms/movie-library/broker/github/endpoint
     ```
4. Copy the **Client ID** and generate a **Client Secret**

## Keycloak Configuration

### Step 1: Add Google Identity Provider

1. Open the Keycloak Admin Console
2. Navigate to **movie-library** realm
3. Go to **Identity Providers** > **Create provider** > **Google**
4. Configure:
   - **Client ID**: [Your Google Client ID]
   - **Client Secret**: [Your Google Client Secret]
   - **Default Scopes**: Leave as default or customize
5. Click **Save**

### Step 2: Add GitHub Identity Provider

1. In the same **Identity Providers** section, click **Create provider** > **GitHub**
2. Configure:
   - **Client ID**: [Your GitHub Client ID]
   - **Client Secret**: [Your GitHub Client Secret]
   - **Default Scopes**: `user:email`
   - Optionally check **Store tokens** to store the GitHub access token
3. Click **Save**

### Step 3: Add Identity Providers to Client

After adding the identity providers, you need to associate them with your client:

1. Navigate to **Clients** > **movielibraryweb**
2. Go to the **Identity Provider Mappers** tab
3. For each provider (Google and GitHub), add mappers if needed to customize how attributes are mapped

### Step 4: Configure First Broker Login Flow (Optional)

This step allows you to customize the experience when a user logs in with a social provider for the first time:

1. Go to **Flows** (previously called "Authentication")
2. Find the **first broker login** flow
3. Configure actions such as:
   - Automatically create a user account
   - Link to existing accounts based on email
   - Prompt for username/email

## Application Configuration

### Update Keycloak Realm Export

The realm configuration includes the new identity provider settings. When you export the Keycloak realm, it will include:

```json
{
  "identityProviders": [
    {
      "alias": "google",
      "displayName": "Google",
      "providerId": "google",
      "enabled": true,
      "config": {
        "clientId": "${GOOGLE_CLIENT_ID}",
        "clientSecret": "${GOOGLE_CLIENT_SECRET}"
      }
    },
    {
      "alias": "github",
      "displayName": "GitHub",
      "providerId": "github",
      "enabled": true,
      "config": {
        "clientId": "${GITHUB_CLIENT_ID}",
        "clientSecret": "${GITHUB_CLIENT_SECRET}"
      }
    }
  ]
}
```
## Testing the Social Login Integration

### Local Testing with Google

1. **Create a test Google account** or use an existing one
2. **Configure OAuth Consent Screen** in Google Cloud Console
3. **Add test users** to your OAuth app (if in development mode)
4. **Run the application**:
   ```bash
   cd blog-keycloak-series.AppHost
   dotnet run
   ```
5. **Navigate to login page** and click "Sign In with Google"
6. **Authenticate** with your Google account
7. **Verify** that you're logged in with the correct identity

### Local Testing with GitHub

1. **Create a test GitHub account** or use an existing one
2. **Ensure the OAuth app redirect URI is correct** in GitHub settings
3. **Run the application**
4. **Navigate to login page** and click "Sign In with GitHub"
5. **Authorize** the application to access your GitHub account
6. **Verify** that you're logged in with the correct identity

## Common Issues and Troubleshooting

### Issue: "Invalid redirect URI"

**Solution**: Ensure the redirect URI in your social provider settings matches exactly:
- Uses `http://` not `https://` for local development
- Includes the full path: `/realms/movie-library/broker/{provider}/endpoint`
- No trailing slashes unless specified

### Issue: "User creation failed" on first login

**Solution**: Configure the first broker login flow:
1. Go to **Flows** in Keycloak
2. Ensure the **first broker login** flow has the "Create User If Unique" action enabled

### Issue: "Email not verified" with GitHub

**Solution**: GitHub doesn't always provide verified email addresses. Configure Keycloak to:
1. Accept unverified emails from GitHub
2. Require email verification as a separate step
3. Auto-link accounts by email if configured

### Issue: Social provider buttons not appearing

**Solution**: 
- Verify identity providers are **Enabled** in Keycloak
- Check browser console for JavaScript errors
- Ensure CSS is loaded correctly
- Verify the identity provider alias matches your redirect logic

## Best Practices

### Security

✅ **Always use HTTPS in production** (HTTP is only for local development)

✅ **Never commit credentials** to version control; use environment variables or user secrets

✅ **Validate and sanitize** all user data received from social providers

✅ **Use PKCE** (which is already configured in Part 1)

✅ **Implement account linking carefully** - verify email ownership before linking

✅ **Enable multi-factor authentication** for enhanced security with social accounts

### User Experience

✅ **Display social provider** information after login (which account was used)

✅ **Allow users to link multiple** identity providers to one account

✅ **Provide an easy way** to unlink accounts

✅ **Remember user's social provider** choice (optional) for faster subsequent logins

✅ **Handle errors gracefully** with user-friendly messages

### Compliance

✅ **Store user data securely** according to GDPR, CCPA, and other regulations

✅ **Obtain proper consent** before using data from social providers

✅ **Provide privacy policies** that clearly state how user data is handled

✅ **Allow data deletion** (right to be forgotten) requests

✅ **Log authentication events** for audit trails

## Next Steps

Part 3 will cover:
- Securing APIs with Bearer Tokens
- Protecting backend endpoints
- Token validation and verification
- Implementing authorization in API services

## Resources

- [Keycloak Identity Providers Documentation](https://www.keycloak.org/docs/latest/server_admin/#identity-providers)
- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [GitHub OAuth Application Documentation](https://docs.github.com/en/developers/apps/building-oauth-apps)
- [OpenID Connect](https://openid.net/connect/)
- [OAuth 2.0 in Plain English](https://www.oauth.com/)

## Conclusion

You now have a fully functional social login system integrated with your Keycloak and .NET Aspire application. Users can authenticate seamlessly using their Google or GitHub accounts, improving both security and user experience.

In the next part, we'll focus on securing APIs with bearer tokens and implementing proper authorization in backend services.

---

**Questions or feedback?** Feel free to open an issue on GitHub or reach out via comments on the blog post.
