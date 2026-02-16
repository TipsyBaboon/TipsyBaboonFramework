# TipsyBaboon.SqlServer

SQL Server data provider for TipsyBaboon framework.

## Features

- **Schema Synchronization** - Automatic table creation and migration
- **Dynamic Query Building** - Supports complex filters, sorts, and includes
- **Change Tracking** - Built-in audit logging with before/after snapshots
- **Transaction Support** - ACID-compliant operations
- **Bulk Operations** - Efficient batch inserts and deletes
- **View Support** - Define SQL views from C# classes

## Installation

```bash
dotnet add package TipsyBaboon.Core
dotnet add package TipsyBaboon.SqlServer
```

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddTipsyBaboon(config => 
{
    config.UseSqlServer();
    config.AddModule("MyApp", connStr, "My Application");
});
```

## Requirements

- TipsyBaboon.Core >= 1.1.0
- Microsoft.Data.SqlClient >= 6.1.0
- .NET 10.0+

## License

MIT License - Copyright © 2026 Tipsy Baboon
