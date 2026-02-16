# TipsyBaboon TestSite

A demonstration site for the TipsyBaboon RAD framework, used for testing framework features and serving as a reference implementation.

## Purpose

This site demonstrates:
- TipsyBaboon framework integration
- Generic CRUD operations
- Identity/authentication setup
- UI module functionality
- External OAuth provider integration (Google, Microsoft)
- Database schema synchronization

## Database Setup

### Northwind Database Required

This test site uses the **Northwind** sample database. Northwind is a well-known Microsoft sample database commonly used for testing and demonstrations.

**Download and Install:**
1. Download Northwind script from Microsoft or community sources
2. Create database: `CREATE DATABASE Northwind`
3. Run the Northwind.sql script against your SQL Server instance
4. Update connection string in `appsettings.json` if needed (default: `localhost`, Windows Authentication)

**Connection String:**
```json
"TipsyBaboonCore": "Server=localhost;Database=Northwind;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

### AdventureWorks Database (Optional)

The test site also supports the **AdventureWorksLT** sample database to demonstrate multi-database module capabilities.

**Quick Setup:**
Use the provided PowerShell script to automatically download and restore AdventureWorksLT:
```powershell
.\tools\setup-adventureworks.ps1
```

**Manual Setup:**
1. Download AdventureWorksLT backup from [Microsoft SQL Server Samples](https://github.com/Microsoft/sql-server-samples/releases)
2. Restore the `.bak` file using SQL Server Management Studio or:
   ```sql
   RESTORE DATABASE [AdventureWorksLT]
   FROM DISK = N'C:\path\to\AdventureWorksLT2022.bak'
   WITH MOVE 'AdventureWorksLT2022_Data' TO 'C:\...\AdventureWorksLT.mdf',
        MOVE 'AdventureWorksLT2022_Log' TO 'C:\...\AdventureWorksLT_log.ldf'
   ```
3. Connection string is already configured in `appsettings.json`

**Connection String:**
```json
"AdventureWorks": "Server=localhost;Database=AdventureWorksLT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

**Note:** If AdventureWorks is not installed, the module will be skipped gracefully. The site will still function normally with just Northwind.

## External Authentication Setup

The site is configured to support Google and Microsoft OAuth login. To enable:

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Create OAuth 2.0 credentials:
   - Application type: Web application
   - Authorized redirect URIs: `https://localhost:5001/signin-google`
5. Copy Client ID and Client Secret to `appsettings.Development.json`:

```json
"Google": {
  "ClientId": "YOUR-GOOGLE-CLIENT-ID.apps.googleusercontent.com",
  "ClientSecret": "YOUR-GOOGLE-CLIENT-SECRET"
}
```

### Microsoft OAuth Setup

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to Azure Active Directory → App registrations
3. Create new registration:
   - Name: TipsyBaboonTestSite
   - Redirect URI: `https://localhost:5001/signin-microsoft`
4. Copy Application (client) ID
5. Create a client secret under Certificates & secrets
6. Copy values to `appsettings.Development.json`:

```json
"Microsoft": {
  "ClientId": "YOUR-MICROSOFT-CLIENT-ID",
  "ClientSecret": "YOUR-MICROSOFT-CLIENT-SECRET"
}
```

### Testing Without External Auth

You can also test using the standard ASP.NET Core Identity registration/login:
1. Run the application
2. Click "Register" to create a local account
3. Use email/password authentication

## Running the Site

### Prerequisites
- .NET 10.0 SDK
- SQL Server (LocalDB, Express, or full edition)
- Node.js and npm (for frontend build)
- Northwind database installed
- AdventureWorksLT database (optional - for multi-database demo)

### First Run

1. Restore NuGet packages:
   ```powershell
   dotnet restore
   ```

2. Install npm packages:
   ```powershell
   npm install
   ```

3. Build frontend assets:
   ```powershell
   npx gulp build
   ```

4. Run the application:
   ```powershell
   dotnet run
   ```

5. Navigate to `https://localhost:5001`

On first run, TipsyBaboon will automatically:
- Sync database schema
- Create required tables for Governance and UI modules
- Set up Identity tables

## Configuration Files

- **appsettings.json** - Base configuration (checked into source control)
- **appsettings.Development.json** - Development overrides with placeholder OAuth settings
- Create **appsettings.user.json** - For your personal OAuth secrets (gitignored)

## Features Demonstrated

### Generic CRUD
Access any registered model via:
- UI: `/Tipsy/{Module}/{ModelName}`
- API: `/api/Baboon/{Module}/{ModelName}`

### Identity Management
- User registration and login
- Role management
- Permission system
- External authentication providers

### Framework Features
- Automatic schema sync
- Convention-based UI generation
- Relationship rendering
- Field-level permissions
- Configuration management

## Development Notes

This site uses:
- **Bootstrap 5** for UI
- **jQuery** for client-side interactions
- **Gulp** for frontend build pipeline
- **Sass** for stylesheets
- **TipsyBaboon** framework packages from NuGet

## Troubleshooting

**Database Connection Issues:**
- Verify SQL Server is running
- Check connection string server name
- Ensure Northwind database exists
- Verify Windows Authentication or update to SQL Auth

**OAuth Issues:**
- Check redirect URIs match exactly (https, port, path)
- Verify client secrets haven't expired
- Ensure APIs are enabled in cloud console
- Test with local registration/login first

**Build Issues:**
- Run `npm install` to restore packages
- Run `npx gulp build` to compile frontend
- Check Node.js version compatibility

## Contributing

This is a test/demo site for the TipsyBaboon framework. For framework issues or enhancements, see the main framework projects:
- TipsyBaboon.Core
- TipsyBaboon.SqlServer
- TipsyBaboon.UI
