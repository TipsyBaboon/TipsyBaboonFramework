// Model Record - Hide unsupported features (Layouts, Permissions) when not used by model
(function() {
    'use strict';

    // Listen for form:ready event (dogfooding framework pattern)
    document.addEventListener('form:ready', function(e) {
        var $form = e.target.closest ? e.target.closest('[record-form]') : null;
        if (!$form) return;

        // Only run this for ModelRecord forms (check data-model attribute)
        var module = $form.getAttribute('data-module');
        var model = $form.getAttribute('data-model');
        if (module !== 'Governance' || model !== 'ModelRecord') return;

        // Read the feature flags from the form inputs
        var hasFormsInput = $form.querySelector('input[name="HasForms"]');
        var hasLocalPermissionsInput = $form.querySelector('input[name="HasLocalPermissions"]');

        if (!hasFormsInput || !hasLocalPermissionsInput) return;

        var hasForms = hasFormsInput.value === 'true' || hasFormsInput.checked;
        var hasLocalPermissions = hasLocalPermissionsInput.value === 'true' || hasLocalPermissionsInput.checked;

        // Find the Layouts field container (child grid for Layouts property)
        var layoutsField = $form.querySelector('[data-property-name="Layouts"]');
        if (layoutsField) {
            var layoutsCardBody = layoutsField.closest('.card-body');
            if (!hasForms) {
                // Hide the Layouts field and show placeholder
                layoutsField.style.display = 'none';
                
                var placeholder = document.createElement('div');
                placeholder.className = 'alert alert-info';
                placeholder.setAttribute('data-feature-placeholder', 'layouts');
                placeholder.innerHTML = '<i class="bi bi-info-circle me-2"></i>This model doesn\'t use layout forms';
                
                if (layoutsCardBody) {
                    layoutsCardBody.insertBefore(placeholder, layoutsField);
                }
            } else {
                // Show the Layouts field and remove placeholder if it exists
                layoutsField.style.display = '';
                var existingPlaceholder = layoutsCardBody ? layoutsCardBody.querySelector('[data-feature-placeholder="layouts"]') : null;
                if (existingPlaceholder) {
                    existingPlaceholder.remove();
                }
            }
        }

        // Find the Permissions group section (entire card)
        var permissionsField = $form.querySelector('[data-property-name="ModelPermissions"]');
        if (permissionsField) {
            var permissionsCard = permissionsField.closest('.card');
            if (!hasLocalPermissions) {
                // Hide the entire Permissions card and show placeholder
                if (permissionsCard) {
                    permissionsCard.style.display = 'none';
                    
                    // Create placeholder card to replace it
                    var placeholderCard = document.createElement('div');
                    placeholderCard.className = 'card h-100';
                    placeholderCard.setAttribute('data-feature-placeholder', 'permissions');
                    placeholderCard.innerHTML = 
                        '<div class="card-header"><h6 class="mb-0">Permissions</h6></div>' +
                        '<div class="card-body">' +
                        '<div class="alert alert-info mb-0">' +
                        '<i class="bi bi-info-circle me-2"></i>This model doesn\'t use local permissions (inherits from another model)' +
                        '</div>' +
                        '</div>';
                    
                    permissionsCard.parentNode.insertBefore(placeholderCard, permissionsCard);
                }
            } else {
                // Show the Permissions card and remove placeholder if it exists
                if (permissionsCard) {
                    permissionsCard.style.display = '';
                    var parentCol = permissionsCard.parentNode;
                    var existingPlaceholder = parentCol ? parentCol.querySelector('[data-feature-placeholder="permissions"]') : null;
                    if (existingPlaceholder) {
                        existingPlaceholder.remove();
                    }
                }
            }
        }

        // Listen for changes to the feature flags to dynamically update visibility
        if (hasFormsInput) {
            hasFormsInput.addEventListener('change', function() {
                // Re-trigger evaluation by dispatching form:ready
                $form.dispatchEvent(new CustomEvent('form:ready', { bubbles: true }));
            });
        }

        if (hasLocalPermissionsInput) {
            hasLocalPermissionsInput.addEventListener('change', function() {
                // Re-trigger evaluation by dispatching form:ready
                $form.dispatchEvent(new CustomEvent('form:ready', { bubbles: true }));
            });
        }
    });
})();
