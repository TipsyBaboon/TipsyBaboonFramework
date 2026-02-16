# TipsyBaboon.UI

Razor Pages UI components for TipsyBaboon framework.

## Features

- **Auto-Generated Pages** - Index, Detail, Create pages for all models
- **Generic CRUD API** - RESTful endpoints for all entities
- **Bootstrap 5 UI** - Responsive, mobile-ready interfaces
- **Field Renderers** - Extensible field rendering engine
- **Inline Editing** - Edit records in grids without navigation
- **Modal Dialogs** - Quick create/edit in popups
- **Permission UI** - Role and permission management pages
- **Change History** - Visual diff views showing field-level changes
- **Layout Editor** - Customize page layouts visually
- **Custom Actions** - Add buttons with custom JavaScript handlers

## Installation

```bash
dotnet add package TipsyBaboon.Core
dotnet add package TipsyBaboon.SqlServer
dotnet add package TipsyBaboon.UI
```

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTipsyBaboon(config => 
{
    config.UseSqlServer();
    config.AddModule("MyApp", connStr);
});

var app = builder.Build();
app.UseTipsyBaboon();
app.UseEndpoints(endpoints => endpoints.MapTipsyBaboonPages());
app.Run();
```

## Custom Field Renderers

```csharp
FieldRenderer.Register<Color>(new FieldRegistrationParams<Color>
{
    PartialPath = "~/Pages/Fields/_ColorPickerField.cshtml",
    RequiredScripts = new List<string> { "/js/color-picker.js" }
});
```

## Requirements

- TipsyBaboon.Core >= 1.1.0
- .NET 10.0+
- Bootstrap 5 (consumer-provided CSS)

## License

MIT License - Copyright © 2026 Tipsy Baboon
