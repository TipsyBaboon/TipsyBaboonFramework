# TipsyBaboon.Core

Infrastructure contracts and base services for the TipsyBaboon RAD framework.

## What is TipsyBaboon?

TipsyBaboon is an infrastructure-first rapid application development framework for ASP.NET Core. **Stop rewriting infrastructure and focus on your business domain.**

The framework provides:
- **Model Registry** - Automatic discovery and registration of models
- **Attribute-Based Configuration** - Declarative model metadata via C# attributes
- **Storage Contracts** - Provider-agnostic data access interfaces
- **Lifecycle Hooks** - BeforeChange, AfterSave, and BeforeGet extensibility points
- **Permission System** - Role-based access with ownership levels
- **Change Tracking** - Automatic audit trail for all records

## Installation

```bash
dotnet add package TipsyBaboon.Core
dotnet add package TipsyBaboon.SqlServer
dotnet add package TipsyBaboon.UI
```

## Quick Start

```csharp
[ModuleName("MyApp")]
[UIEntity("Contact")]
public class Contact : TipsyBaboonModel
{
    [RecordName]
    [Required]
    [UIDisplay(ShowInList = true)]
    public string Name { get; set; }
    
    [UIDisplay(ShowInList = true)]
    public string Email { get; set; }
}
```

That's it! The framework auto-generates:
- Database schema
- CRUD pages (Index, Detail, Create)
- Generic REST API
- Permission management
- Change history

## License

MIT License - Copyright © 2026 Tipsy Baboon
