# New Proof-by-Process Rules to Add

Add these rules after `proof_by_process` (after line 531) and before `# --- Guardrails` (before line 533):

```yaml
	- id: proof_by_process_reference_ssot
		description: "Reference single source of truth directly without intermediate structures."
		intent: "Prevent unnecessary data copying and maintain code clarity by accessing authoritative data structures directly."
		triggers:
			- type: always_apply
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				title: "Reference SSoT Directly - No Intermediate Structures"
				text: |
					Do not create intermediate variables, restructured objects, or cached copies of data unless there is a
					measurable performance reason or clear semantic transformation.
					
					**Bad:**
					```javascript
					var config = { ...options, gridId: id, apiUrl: url };  // Why? Just use options
					var rowData = GetRowData(id);  // Why search? You already have state.data
					```
					
					**Good:**
					```javascript
					function RenderToolbar() {
					    return CreateButton(options.actions.newButton);  // Use options directly
					}
					
					function RenderRow(id) {
					    return CreateRowElement(state.data[id]);  // Access SSoT directly
					}
					```
					
					**When intermediate structures ARE needed:**
					- Normalizing external API responses at boundary (once)
					- Indexing arrays by ID for O(1) lookups (performance)
					- Semantic transformation (e.g., raw API data ? view model with computed fields)
					
					**When they are NOT needed:**
					- Spreading an object just to add a property
					- Copying state for "safety" when mutation is the pattern
					- Creating getters/setters that just return/set without logic
			- type: require_conformance
				enforce: true
				title: "Cache Collections at Initialization, Not Per-Operation"
				text: |
					If you need to iterate a collection repeatedly, cache it ONCE at initialization. Do not re-query on every operation.
					
					**Bad:**
					```javascript
					function GetChangedFields() {
					    var inputs = form.querySelectorAll('input, select, textarea'); // Re-queries DOM every call!
					}
					```
					
					**Good:**
					```javascript
					var formInputs = null;
					
					function Init(options) {
					    formInputs = Array.from(form.querySelectorAll('input, select, textarea'))
					        .filter(input => input.name && !input.name.startsWith('__'));
					}
					
					function GetChangedFields() {
					    return formInputs.reduce((changed, input) => { /* ... */ }, {});
					}
					```
					
					**When to cache:** Collections that don't change after initialization (form inputs, grid columns)
					**When NOT to cache:** State that changes (current row data, filter values)
			- type: require_conformance
				enforce: true
				title: "Normalize External Data Once at Entry Point"
				text: |
					When receiving data from external sources (APIs, user input), normalize it ONCE at the boundary.
					All downstream code assumes normalized format.
					
					**Bad:**
					```javascript
					function RenderRow(row) {
					    var id = row.id || row.Id;  // Checking both every time!
					}
					```
					
					**Good:**
					```javascript
					function LoadData() {
					    fetch(apiUrl).then(r => r.json()).then(result => {
					        ValidateAndNormalize(result.items); // Once, here
					        state.data = IndexById(result.items);
					        Render();
					    });
					}
					
					function ValidateAndNormalize(items) {
					    items.forEach(item => {
					        if (!item.id && !item.Id) throw new Error('Item missing ID');
					        if (!item.id) item.id = item.Id;
					        delete item.Id;
					    });
					}
					
					function RenderRow(row) {
					    return row.id; // Known to exist, normalized
					}
					```
		constraints:
			- "This rule is always applied unless performance profiling shows a need for intermediate structures."

	- id: proof_by_process_no_defensive_coding
		description: "Strictly prohibit defensive coding patterns that mask errors instead of surfacing them."
		intent: "Force errors to surface immediately at their source, making bugs obvious and fixable rather than hidden."
		triggers:
			- type: always_apply
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				title: "No Defensive Coding - Fail Fast"
				text: |
					Defensive coding is STRICTLY FORBIDDEN because it masks problems instead of fixing them at the source.
					
					**Never write:**
					- `if (obj) { obj.property }`  // If obj could be null, the caller is broken
					- `value = row.id || row.Id`  // Contract violation should fail, not fall back
					- `if (!array || !array.length)` // If array could be undefined, validate at boundary
					- `try { riskyOperation() } catch { /* ignore */ }` // Never swallow errors silently
					
					**Philosophy:**
					- Internal code is within our sphere of control and TRUSTED
					- If an object doesn't exist, that's a 404 upstream�already handled by framework
					- If a property is missing, the API contract is broken�fix the source, don't patch
					- If iteration fails, the data structure is wrong�surface the error immediately
					
					**Only validate at boundaries:**
					- User input (forms, query params, uploaded files)
					- External API responses (third-party services, not your own APIs)
					
					All internal code assumes valid state. If state is invalid, LET IT FAIL so you fix the root cause.
			- type: require_conformance
				enforce: true
				title: "Fail Fast - Surface Errors Immediately"
				text: |
					When an assumption is violated, throw immediately. Do not return null, log and continue, or use fallback values.
					
					**Good:**
					```javascript
					function RenderRow(id) {
					    var row = state.data[id];
					    return CreateRowElement(row.name, row.value);
					}
					```
					
					**Bad:**
					```javascript
					function RenderRow(id) {
					    var row = state.data[id];
					    if (!row) return ''; // Masks the bug�why is id invalid?
					    return CreateRowElement(row.name || 'Unknown', row.value || 0);
					}
					```
					
					The bad version hides three separate bugs. The good version exposes them immediately.
		constraints:
			- "This rule has no exceptions for internal code."
			- "Validation at external boundaries (user input, external APIs) is required, not defensive."

	- id: proof_by_process_trust_boundaries
		description: "Define trust boundaries and enforce validation at untrusted entry points only."
		intent: "Clarify when validation is required (external boundaries) vs forbidden (internal code), preventing confusion between security validation and defensive coding."
		triggers:
			- type: always_apply
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				title: "Validate at Trust Boundaries - Client AND Server"
				text: |
					**Trust Boundaries Require Validation:**
					
					1. **User Input (Client-Side)** - Validate for UX (immediate feedback)
					   - Form inputs, query params, file uploads
					   - Provide clear error messages to guide user
					   - This is convenience, NOT security
					
					2. **User Input (Server-Side)** - Validate for SECURITY (client is untrusted)
					   - ALL data from HTTP requests (body, query, headers, cookies)
					   - File uploads, JSON payloads, form data
					   - Assume client validation was bypassed via debugger/Postman
					   - Reject invalid data with 400 Bad Request
					
					3. **External APIs** - Validate responses from third-party services
					   - Check structure, required fields, data types
					   - Fail fast if contract is violated
					
					**Trusted Zones - NO Validation:**
					
					1. **Internal Server Code** - After initial validation, trust the data
					   - Between services in your application
					   - Database results (schema enforces constraints)
					   - Internal method calls with validated parameters
					
					2. **Client Code After Normalization** - After boundary validation, trust the structure
					   - After LoadData() validates/normalizes API response
					   - Between functions in the same module
					
					**Philosophy:**
					- Client validation = UX convenience (users can bypass)
					- Server validation = Security requirement (must never trust client)
					- Internal code = Trusted (fail fast if assumptions violated)
			- type: require_conformance
				enforce: true
				title: "Server-Side Validation is Mandatory for All User Input"
				text: |
					Every API endpoint that accepts user data MUST validate:
					- Required fields are present
					- Data types are correct
					- Values are within allowed ranges/constraints
					- Business rules are satisfied
					
					**Example (C# API Controller):**
					```csharp
					[HttpPost]
					public IActionResult CreateUser([FromBody] CreateUserRequest request)
					{
					    if (string.IsNullOrWhiteSpace(request.Email))
					        return BadRequest("Email is required");
					    
					    if (!IsValidEmail(request.Email))
					        return BadRequest("Email format is invalid");
					    
					    var user = userService.CreateUser(request.Email, request.Name);
					    return Ok(user);
					}
					
					public User CreateUser(string email, string name)
					{
					    // NO validation here - caller is trusted
					    return new User { Email = email, Name = name };
					}
					```
					
					**Example (JavaScript Client):**
					```javascript
					function ValidateForm() {
					    var email = emailInput.value.trim();
					    if (!email) {
					        ShowError('Email is required');
					        return false;
					    }
					    return true;
					}
					
					function SaveUser() {
					    if (!ValidateForm()) return;
					    var data = { email: emailInput.value, name: nameInput.value };
					    ApiRequest('/api/users', { method: 'POST', body: JSON.stringify(data) })
					        .then(HandleSuccess);
					}
					```
			- type: require_conformance
				enforce: true
				title: "Trust Boundaries vs Defensive Coding"
				text: |
					**This is NOT defensive coding (required at trust boundaries):**
					```csharp
					[HttpPost]
					public IActionResult UpdateUser([FromBody] UpdateUserRequest request)
					{
					    if (request == null) return BadRequest("Request body required");
					    if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email required");
					}
					```
					
					```javascript
					function LoadData() {
					    fetch(externalApiUrl).then(r => r.json()).then(data => {
					        if (!Array.isArray(data.items)) throw new Error('Invalid API response');
					    });
					}
					```
					
					**This IS defensive coding (forbidden in internal code):**
					```csharp
					public void ProcessUser(User user)
					{
					    if (user == null) return; // FORBIDDEN - caller is trusted
					    if (string.IsNullOrEmpty(user.Email)) return; // FORBIDDEN
					}
					```
					
					```javascript
					function RenderRow(row) {
					    if (!row) return ''; // FORBIDDEN - why is row null?
					    var name = row.name || 'Unknown'; // FORBIDDEN
					}
					```
					
					**Key Distinction:**
					- Boundary validation = Security/contract enforcement at entry points
					- Defensive coding = Masking bugs by checking things that should never happen
		constraints:
			- "All API endpoints accepting user input MUST validate on server side."
			- "Client validation is for UX; server validation is for security."
			- "After validation at boundary, downstream code trusts the data."

	- id: code_is_documentation
		description: "Code is self-documenting; comments are redundant unless explaining non-obvious domain knowledge."
		intent: "Reduce visual noise and force clear naming instead of relying on comments to explain poor names."
		triggers:
			- type: always_apply
			- type: pre_edit
		actions:
			- type: require_conformance
				enforce: true
				title: "No Redundant Comments"
				text: |
					If a function, variable, or class name clearly states what it does, do NOT add a comment restating it.
					
					**Delete these:**
					```javascript
					// Get changed fields
					function GetChangedFields() { }
					
					// Toggle row selection
					function ToggleRowSelection(id) { }
					
					// Store original values of all form inputs
					function StoreOriginalValues() { }
					```
					
					**Keep these (non-obvious domain knowledge):**
					```javascript
					// Debounce to 500ms because rapid edits can trigger API rate limits
					debouncedSave = Debounce(Edit.Save, 500);
					
					// Skip __RequestVerificationToken and other ASP.NET form fields
					if (name.startsWith('__')) return;
					```
					
					**When to comment:**
					- Explaining WHY a non-obvious choice was made (business rule, performance trade-off)
					- Documenting external contracts (API shapes from third-party services)
					- Warning about gotchas or edge cases that aren't clear from code structure
					
					**When NOT to comment:**
					- Restating what the code literally does (function name should say this)
					- Describing parameter types (use TypeScript or JSDoc types, not prose)
					- Explaining trivial operations (map, filter, basic conditionals)
		constraints:
			- "Preserve existing comments only if they add non-obvious context."
			- "When refactoring, delete comments that restate obvious code."
```

## Update to coding_standards_no_automatic_comments

This project defers to the repository-level `coding_standards_no_automatic_comments` rule in `/.github/copilot-instructions.md`.
Do not duplicate or override the central rule; follow the repo-level behavior (no automatic comments unless explicitly requested).
