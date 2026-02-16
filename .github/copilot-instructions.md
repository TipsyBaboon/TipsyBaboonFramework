# Copilot Instructions (Repo)

This file is authoritative agent policy.

This repository uses the Markdown entry-point as the authoritative ruleset:
- `.github/copilot-instructions.md`

All agent rules are consolidated into this file to avoid cross-file discovery issues in older models.
When updating agent rules, edit this file only.

Agents should prefer the Markdown entry-point for discovery and edits; if other formats exist, keep them synchronized with the `.md` source.

## Primary Copy/Refactor Workflow (Tooling)

When you need to move/copy content between files (especially when consolidating or performing a copy+refactor workflow),
do **not** manually retype/recreate content from memory.

Use the repository tool:

- `tools/copy-insert-block.ps1`

This is the **primary method** for copying blocks between files. The intent is to keep the final file faithful to the
original source text first, and only then do modifications/refactors.

Examples:

- Copy line range and insert at a line:
	- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartLine 10 -SourceEndLine 42 -InsertAtLine 5`
- Copy between markers and insert after an anchor:
	- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartPattern "BEGIN" -SourceEndPattern "END" -InsertAfterPattern "# Insert Here" -Occurrence 1`
- Preview with no write:
	- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartLine 1 -SourceEndLine 5 -InsertAtLine 1 -WhatIf`

## Authoritative Ruleset (YAML)

```yaml
---
version: 1
entry_point: true
rule_groups:
	- name: rule-writing
		tags: [case_based]
		description: "Rule authoring/validation conventions and entry-point requirements."
	- name: workflow
		tags: [always_apply]
		description: "Discovery phase and reading-over-reporting. (Iterative/multi-phase: see .github/instructions/iterative-process.instructions.md)"
	- name: knowledge-base
		tags: [always_apply]
		description: "Agent-maintained path-specific knowledge (.instructions.md) for undiscoverable details."
	- name: proof-by-process
		tags: [always_apply]
		description: "Deterministic, context-efficient programming practices."
	- name: guardrails
		tags: [always_apply]
		description: "Transactional edits, rollback protection, and blast-radius discipline."
	- name: free-agency
		tags: [always_apply]
		description: "Context-aware autonomy: root-cause, examples, outlier-fix strategy."
	- name: tooling
		tags: [always_apply]
		description: "Repository tooling conventions (copy/insert as primary copy+refactor method)."
	- name: coding-standards
		tags: [always_apply]
		description: "Type safety, naming conventions, and code style rules for all languages."

rules:
	- id: time_anchor
		intent: "Prevent time-related errors by grounding all relative time references in current context."
		triggers:
			- type: pre_research
			- type: pre_processing
		actions:
			- type: anchor_time
				description: "Before responding, anchor to current date/time/turn. Adjust relative time references or state time is unknown if ambiguous."
	- id: comprehension_checkpoint
		intent: "Keep rules in context and prevent silent drift during long tasks."
		triggers:
			- type: on_user_input
			- type: pre_edit
			- type: pre_execution
		actions:
			- type: generate_checklist
				description: "Emit '✅ Following: {rule_groups}' listing applicable rule groups, then turn-specific summary in own words or 'No Blockers.'"
				output_format: "✅ Following: {rules}\n{summary_or_no_blockers}"
		constraints:
			- "List only rule_groups names (no filenames). Summary must be turn-specific, no verbatim quotes. Must emit before substantive actions."

	- id: entry_point_toc
		intent: "Keep stable in-file grouping index for older models."
		triggers:
			- type: validation
		actions:
			- type: validate_presence
				target: rule_groups
				message: "Entry-point must include rule_groups index with one-line descriptions (names only)."

	# --- Rule-writing
	- id: include_intent
		intent: "Promote clarity by explicitly stating rule intent."
		triggers:
			- type: validation
		actions:
			- type: enforce_field
				field: intent
				required: true

	- id: rephrase_negations
		intent: "Prevent ambiguous 'DO NOT' rules; use actionable alternatives."
		triggers:
			- type: validation
		actions:
			- type: transform_text
				pattern: "DO NOT"
				replacement_format: "DO {alternative} INSTEAD OF {prohibited}"

	- id: order_success_before_exceptions
		intent: "Improve compliance by ordering success path first, exceptions last (agents follow later text more strongly)."
		triggers:
			- type: authoring
			- type: validation
		actions:
			- type: enforce_ordering
				order: ["Success case first", "Exception cases last"]

	- id: require_comprehension_checkpoint
		intent: "Ensure agents always run comprehension checkpoint before actions."
		triggers:
			- type: validation
		actions:
			- type: validate_presence
				target: rule_id
				value: "comprehension_checkpoint"
				scope: entry_point_files

	- id: require_entry_point_toc
		intent: "Ensure entry-point provides in-file rule groups index for quick discovery."
		triggers:
			- type: validation
		actions:
			- type: validate_presence
				target: rule_groups
				message: "Entry-point must contain rule_groups index (names only, one-line descriptions)."

	# --- Workflow
	# Note: For iterative/multi-phase work (iterative_process, review_process_auto, project_documentation_contract, epic_structure),
	# see .github/instructions/iterative-process.instructions.md

	- id: discovery_phase_required
		intent: "Prevent tunnel vision by requiring repository-wide discovery when codebase is evolving."
		triggers:
			- type: pre_execution
			- type: pre_edit
			- type: on_bug_fix_request
		actions:
			- type: require_conformance
				enforce: true
				text: |
					Before implementing (except trivial edits):
					1. Check path-specific instructions (`.github/instructions/*.instructions.md`)
					2. Search repository for related usages (callers, tests, comparable implementations)
					3. Identify boundaries/callers when changing interfaces
					
					Discovery is READ-oriented; keep findings concise. Emit: "✓ Discovery complete"
					Skip for: typo fixes, changes <5 lines with zero dependencies.
		constraints:
			- "Read representative examples rather than producing verbose discovery reports. Preserve context budget for actual code."

	- id: prefer_reading_over_reporting
		intent: "Maximize context efficiency by keeping codebase code in context rather than agent-generated summaries."
		triggers:
			- type: pre_execution
			- type: research_phase
		actions:
			- type: require_conformance
				enforce: true
				text: |
					**DO**: Read actual source files and keep them in context (semantic_search, grep_search, read_file)
					**DON'T**: Create verbose summaries, architecture diagrams, or meta-documentation
					
					Model learns codebase style better from actual code than descriptions of code.
					Brief confirmation only: "✓ Discovery complete"
		constraints:
			- "Discovery confirmations must be ≤3 words unless user explicitly requests detail."

	# --- Knowledge base
	- id: maintain_path_specific_instructions
		intent: "Agents maintain path-specific knowledge for undiscoverable details; keep minimal to avoid bloat."
		enabled: true
		triggers:
			- type: always_apply
		actions:
			- type: require_conformance
				enforce: true
				text: |
					Use `.github/instructions/*.instructions.md` for knowledge bases scoped to specific codebases/modules.
					Each file includes frontmatter with `applyTo` glob patterns. Path-specific instructions automatically
					combine with repository-wide rules.
					
					Before implementing changes that could require rediscovery later:
					- Consider whether path-specific instructions already contain the needed fact
					- If relevant instruction files exist for current path, check them first

					Agents are authorized to create, update, and prune `.github/instructions/*` automatically.
					Proactively prune or correct changed/stale information.
					
					**Minimal-bloat principle:** Document ONLY:
					- Core contracts (interfaces, boundaries, invariants)
					- Undiscoverable details (remote invocations, cross-module dependencies, non-obvious conventions)
					- Information NOT visible when reading the code directly
					
					Do NOT document: implementation details visible in code, obvious patterns, things discoverable via search.
	# --- Proof by process
	- id: proof_by_process
		intent: "Deterministic, context-efficient programming practices."
		triggers:
			- type: always_apply
		actions:
			- type: require_input_validation
				text: "Input validation is mandatory at boundary before processing. Reject, sanitize, or request correction for invalid input. No downstream defensive checks expected after validation."
			- type: require_conformance
				enforce: true
				text: "Prefer short, linear implementations. Favor map/filter/reduce or LINQ chains and ternary expressions over deep branching. Structure code to mirror problem domain. Use single authoritative data structure as source of truth. Inline small operations; avoid thin wrappers. Present user-friendly error feedback in UI with actionable remediation."

	# --- Guardrails
	- id: transactional_agent_edits
		intent: "Allow safe agent operation while preserving reproducibility and preventing silent rollback."
		triggers:
			- type: pre_edit
			- type: post_apply_patch
		actions:
			- type: require_conformance
				enforce: true
				text: "Agents MUST NOT create, checkout, or switch branches (including agents/working) automatically. All git operations require explicit user instruction."

	- id: no_rollback_guard
		intent: "Reduce risk of accidental loss when switching between IDEs (advisory only)."
		triggers:
			- type: pre_commit
		actions:
			- type: validate_commit_message
				requires_substring: "archived:"
				warn_only: true
			- type: check_recent_patches
				lookback_minutes: 120
				warn_if_deleting_recent_files: true

	- id: no_git_commands
		intent: "Prevent agents from executing git commands autonomously; require explicit user instruction."
		triggers:
			- type: pre_execution
		actions:
			- type: prohibit_commands
				commands: ["git"]
				note: "Execute only exact git command requested by user, nothing more."

	- id: free_agency_auto_correct_typos
		intent: "Improve UX by inferring intended identifiers or process names when intent is clear from context."
		triggers:
			- type: on_user_input
			- type: pre_execution
		actions:
			- type: require_conformance
				enforce: true
				behavior: "auto-correct-if-confident; clarify-if-ambiguous"
				text: "If user-provided identifier appears to be obvious typo (small edit distance, consistent with nearby code), prefer inferred correct form without asking. When ambiguous, ask one clarifying question."

	- id: free_agency_challenge_inductive_errors
		intent: "Prevent subtle mistakes by questioning requests that are surprising given available context."
		triggers:
			- type: pre_execution
			- type: decision_point
		actions:
			- type: require_conformance
				enforce: true
				on_violation: "clarify"
				text: "If requested action is plausible syntactically but unlikely given repository patterns/recent discussion/stated goals, raise concise evidence-backed question before proceeding. Avoid pedantry: only challenge when real risk exists."

	- id: free_agency_seek_root_cause
		intent: "Ensure enduring fixes by diagnosing underlying causes when responding to bug reports or failures."
		triggers:
			- type: pre_execution
			- type: on_error_report
		actions:
			- type: require_conformance
				enforce: true
				require_documentation: true
				text: "Treat user's reported scope as symptom location. Perform targeted investigation beyond report to identify root causes. Prefer minimally invasive fixes addressing root cause when confident. Document reasoning from symptom to root cause and fix."

	- id: free_agency_use_codebase_for_examples
		intent: "Leverage repository context to produce accurate, idiomatic solutions and diagnostics."
		triggers:
			- type: pre_execution
			- type: research_phase
		actions:
			- type: require_conformance
				enforce: true
				prefer_in_repo: true
				require_citations: true
				text: "Search codebase for example usages, tests, comparable patterns when diagnosing or providing examples. Prefer in-repo idioms; cite file paths/excerpts. If codebase lacks examples, use best-practice patterns and mark as external."

	- id: free_agency_fix_outlier_not_pattern
		intent: "Preserve intended behavior by narrowly addressing anomalies unless pattern is proven faulty."
		triggers:
			- type: pre_execution
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				require_tests: true
				text: "Propose targeted fixes addressing specific failing case. Only generalize to whole pattern if evidence shows pattern consistently fails. Add tests capturing outlier behavior to prevent regressions."
	- id: free_agency_surface_out_of_scope_errors
		intent: "Handle discovered issues responsibly: fix safely within scope, otherwise escalate succinctly."
		triggers:
			- type: pre_execution
			- type: code_change
			- type: pre_edit
		actions:
			- type: require_conformance
				enforce: true
				behavior: "correct-in-scope; escalate-out-of-scope"
				text: "When detecting errors: if within agreed scope with low risk/minimal blast radius, fix it with brief note in change description and tests. If outside scope, escalate to user with file, line range, description, reproduction steps, risk assessment, suggested remediation."
	- id: free_agency_blast_radius_impact_analysis
		intent: "In codebases optimizing toward PBP minimalism, prevent spaghetti by tracing to origin and pruning extraneous code. Conservative assumptions belong in established codebases with strong contracts."
		triggers:
			- type: pre_execution
			- type: pre_change
			- type: post_implementation
		actions:
			- type: require_conformance
				enforce: true
				conservative_assumption: false
				text: |
					For changes crossing boundaries (client↔server, model↔view, config↔runtime):
					1. Trace full data flow from origin to consumption
					2. Verify each field/property/method is actually used at consumption points
					3. Remove fields existing in intermediate layers but never consumed
					4. Search for external usage before assuming fields are needed
					
					When adding new fields/properties/metadata:
					- Add only what is immediately needed and consumed
					- Do not pass through fields "just in case" or for potential future use
					- Verify JavaScript actually reads field before adding to model
					- Check Razor views actually reference property before keeping it

	# --- Tooling
	- id: tooling_primary_copy_insert_block
		intent: "Reduce drift and errors by copying exact source text first, then refactoring in-place."
		triggers:
			- type: always_apply
			- type: pre_edit
		actions:
			- type: require_conformance
				enforce: true
				text: "When copying/moving text blocks between files (especially for consolidation or copy+refactor), use repo tool `tools/copy-insert-block.ps1` as primary method. Do not manually retype/reconstruct large blocks. First copy exact source block, then refactor/modify."

	# --- Coding Standards
	- id: coding_standards_no_dynamic_types
		intent: "Enforce type safety for security and maintainability; loose types are prohibited in server code running at highest security standards."
		triggers:
			- type: always_apply
			- type: pre_edit
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				text: "NEVER use dynamic types in server-side code (C#, TypeScript server code). Always define explicit typed models. Dynamic types prohibited for security/maintainability. If encountered, refactor to explicit typed models."

	- id: coding_standards_naming_conventions
		intent: "Maintain readability and prevent name collisions with library code by using consistent, distinctive naming patterns."
		triggers:
			- type: always_apply
			- type: pre_edit
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				text: |
					**PascalCase (all languages):** ClassNames, PropertyNames, Methods/Functions (application code), Interfaces (I prefix), Enums
					**camelCase (all languages):** Local variables, parameters, private fields (may use _camelCase in C#)
					**Exception for JS/TS Framework Code:** External libraries use camelCase for public APIs; framework code should match ecosystem.
					**Rationale:** PascalCase in application code avoids collisions with library includes.

	- id: coding_standards_documentation
		intent: "Enable automated XML/JSDoc on public APIs; write clean self-documenting code with appropriate documentation."
		triggers:
			- type: always_apply
			- type: pre_edit
			- type: code_change
		actions:
			- type: require_conformance
				enforce: true
				text: |
					**XML docs / JSDoc:** Add automatically on public APIs, interfaces, and non-obvious methods.
					Keep docs concise — describe intent and contracts, not implementation.
					
					**Inline comments:** Use sparingly for non-obvious logic only. Prefer clear naming over comments.
					
					**Agent discovery docs (.instructions.md):** Follow minimal-bloat principle from knowledge-base rule.
					Document core contracts and undiscoverable details only — skip anything visible in code.
