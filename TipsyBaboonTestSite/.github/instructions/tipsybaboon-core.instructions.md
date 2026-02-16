---
applyTo: "TipsyBaboon.Core/**/*.cs"
---

# TipsyBaboon.Core Knowledge Base

## Entity Lifecycle Hooks

### IEntityWithSaveAction (Pre-Save Hook)

**Location:** `TipsyBaboon.Core\Models\IEntityWithSaveAction.cs`

**Purpose:** Execute validation or data preparation logic before a database operation occurs. Used when the entity does not need a generated ID.

**Signature:**
```csharp
Task<SaveResponseType> BeforeChangeAsync(ChangeAction action, string? committingUser = null, CancellationToken cancellationToken = default);
```

**Execution Point:** Called **before** INSERT, UPDATE, or DELETE SQL commands are executed.

**Return Values:**
- `SaveResponseType.Continue` - Proceed with normal save operation
- `SaveResponseType.ChangeApplied` - Handler took care of the save; skip database operation
- `SaveResponseType.Error` - Abort the operation and throw an exception

**Common Use Cases:**
- Input validation before save
- Business rule enforcement
- Audit log preparation
- Data transformation/normalization
- Related entity cleanup on delete

### IEntityWithPostSaveAction (Post-Save Hook)

**Location:** `TipsyBaboon.Core\Models\IEntityWithSaveAction.cs`

**Purpose:** Execute operations after a database operation completes successfully and the entity ID is populated. Used when you need to create dependent structures or trigger downstream workflows.

**Signature:**
```csharp
Task AfterSaveAsync(ChangeAction action, string? committingUser = null, CancellationToken cancellationToken = default);
```

**Execution Point:** Called **after** INSERT, UPDATE, or DELETE SQL commands succeed.

**Return Type:** `Task` (void async) - Exceptions will propagate and should be handled by caller.

**Common Use Cases:**
- Creating join table entries after parent ID exists
- Building hierarchical relationships requiring the saved entity's ID
- Triggering event notifications with saved entity context
- Creating audit records that reference the saved entity
- Cascading creates to dependent entities

## SqlServer Implementation Wiring

**Location:** `TipsyBaboon.SqlServer\Storage\TipsyBaboonModelStore.cs`

### Insert Flow
1. Call `IEntityWithSaveAction.BeforeChangeAsync(ChangeAction.Create)` if implemented
2. If response is `ChangeApplied`, return immediately (skip DB operation)
3. Execute INSERT statement
4. Call `IEntityWithPostSaveAction.AfterSaveAsync(ChangeAction.Create)` if implemented
5. Return saved entity

### Update Flow
1. Retrieve existing entity
2. Call `IEntityWithSaveAction.BeforeChangeAsync(ChangeAction.Edit)` if implemented
3. If response is `ChangeApplied`, return immediately (skip DB operation)
4. Execute UPDATE statement
5. Call `IEntityWithPostSaveAction.AfterSaveAsync(ChangeAction.Edit)` if implemented
6. Return saved entity

### Delete Flow
1. Retrieve existing entity
2. Call `IEntityWithSaveAction.BeforeChangeAsync(ChangeAction.Delete)` if implemented
3. If response is `ChangeApplied`, return immediately (skip DB operation)
4. Execute DELETE statement
5. Call `IEntityWithPostSaveAction.AfterSaveAsync(ChangeAction.Delete)` if implemented
6. Return success status

## Current Implementations

### UserRoleAssignment and RoleUserAssignment

**Location:** `TipsyBaboon.Core\Models\Identity\UserRoleAssignment.cs` and `RoleUserAssignment.cs`

Both implement `IEntityWithSaveAction` to manage the `UserRole` join table entries when the `IsAssigned` checkbox is toggled in the UI. The hook creates or deletes the underlying `UserRole` record and returns `SaveResponseType.ChangeApplied` to prevent the view entity itself from being saved.

**Pattern:** View entity with managed join table - BeforeChangeAsync handles the persistence logic for the underlying relationship table.
