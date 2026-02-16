---
applyTo: "**/*"
---

# Iterative Process & Project Documentation Rules

These rules support large, multi-phase iterative work. They are user-triggered only (never automatic).

## Authoritative Ruleset (YAML)

```yaml
---
version: 1
entry_point: false
applies_when: iterative_or_multi_phase_work

rules:
	- id: iterative_process
		intent: "Enable safe, bounded iterative development cycles (max 3) with agent planning and review."
		triggers:
			- type: on_iterative_request
		actions:
			- type: initialize_iteration
				max_cycles: 3
				state_key: iterative_process_state
			- type: loop_phases
				phases:
					- name: plan
						action: {type: generate_plan, outputs: [plan_text, file_targets]}
					- name: implement
						action: {type: implement_plan, mode: "apply_patch_or_commands", outputs: [changes_summary, patch_path]}
					- name: review
						action: {type: invoke_ruleset, target: review_process_auto, outputs: [review_report]}
			- type: evaluate_iteration
				criteria:
					continue_if: "review_report.has_actionable_findings"
					stop_if: "cycles_exceeded_or_no_actionable_findings"
		constraints:
			- "Limit to max_cycles (default 3). Each review must cite file/line. Does not auto-merge. Implies project documentation is active."

	- id: review_process_auto
		intent: "Provide repeatable, evidence-backed automated review with structured report and actionable findings."
		triggers:
			- type: on_review_request
		actions:
			- type: perform_requirement_check
				outputs: [requirements_met, missing_items]
			- type: implementation_verification
				checks: [undefined_symbols, inconsistent_typing, broken_workflow]
				outputs: [issues]
			- type: downstream_impact_analysis
				when: {condition: "not_new_feature"}
				actions: [{type: search_callers, scope: repo, outputs: [callers_found]}]
			- type: maintainability_check
				outputs: [findings]
			- type: issue_categorization
				categories:
					High: "Security, correctness, data corruption, critical crashes"
					Medium: "Incorrect behavior, performance regressions, substantial bugs"
					Low: "Stylistic issues, minor inefficiencies, non-blocking suggestions"
					Weak_Paradigm: "Significant maintainability/paradigm issues requiring refactor"
				outputs: [categorized_issues]
			- type: generate_review_report
				format: json
				path: "agents/previews/reviews/{timestamp}-{pr_or_branch}.json"
		constraints:
			- "Auto-run, produces reports, does not block merges by default. Skips downstream analysis for new_feature. All findings must cite file paths/snippets."

	- id: project_documentation_contract
		intent: "Support large iterative work: ensure work can resume in new thread with no prior context via agent-owned SSoT docs. (Explicit approval required for now, may become automatic after refactors.)"
		triggers:
			- type: pre_execution
		actions:
			- type: require_conformance
				enforce: true
				text: |
					Project docs location: `agents/docs/<project-name>/`
					
					Required files:
					- README.md          # TOC listing all project files
					- knowledge-base.md  # Spec/SSoT: boundaries, constraints, concepts, interfaces
					- task-list.md       # Agent work items (resumable)
					
					**task-list.md**: Human-reviewable, updated at phase boundaries. Each task includes:
					- Status: ⭕ Not Started | ✏️ Implemented (agent done, not user-verified) | ✅ User Confirmed (actual completion)
					- Next action/acceptance criteria, files/symbols touched, commands executed, blockers/assumptions
					
					**File-based vs in-memory tracking:**
					- Use task-list.md (file) for: multi-phase projects, multi-session work, iterative process
					- Use todo tool (in-memory) for: single-session tasks, quick fixes, no formal docs
					
					**Agent must get explicit user approval before creating/modifying `agents/docs/*` or top-level README.md**
		constraints:
			- "Order phases/tasks to preserve system stability and compile-ability. Update task-list.md after each phase."

	- id: epic_structure
		intent: "Support multi-project workflows: ensure epics are properly structured with clear documentation and tracking."
		triggers:
			- type: on_project_creation
			- type: on_epic_update
		actions:
			- type: enforce_structure
				description: "Epics: master projects with multiple sub-projects. Include top-level README to track sub-projects, status, milestones. Store project-specific docs in named folders by project."
```
