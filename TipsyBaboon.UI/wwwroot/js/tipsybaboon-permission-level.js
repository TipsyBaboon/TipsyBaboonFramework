// Permission Level Field - Icon-based dropdown handler using event delegation
// Works for both form fields and dynamically loaded grid cells

(function() {
    'use strict';
    
    // Use document-level event delegation for all permission level dropdowns
    // This handles dynamically loaded content without needing re-initialization
    
    document.addEventListener('click', function(e) {
        // Handle any permission-level-option click (works for both form and grid)
        var option = e.target.closest('.permission-level-option');
        if (option) {
            e.preventDefault();
            
            // Determine if it's in a grid dropdown or form dropdown
            var gridDropdown = option.closest('.permission-level-grid-dropdown');
            var formDropdown = option.closest('.permission-level-dropdown');
            var dropdown = gridDropdown || formDropdown;
            
            if (dropdown) {
                handleDropdownClick(option, dropdown, !!gridDropdown);
            }
            return;
        }
    });
    
    function handleDropdownClick(optionElement, dropdown, isGrid) {
        var button = dropdown.querySelector('.dropdown-toggle');
        var hiddenInput = dropdown.querySelector('input[type="hidden"]');
        var items = dropdown.querySelectorAll('.permission-level-option');
        
        if (!button || !hiddenInput) {
            return;
        }
        
        var value = optionElement.getAttribute('data-value');
        var icon = optionElement.getAttribute('data-icon');
        
        // Update hidden input value
        hiddenInput.value = value;
        
        // Update button icon
        var iconElement = button.querySelector('i.bi');
        if (iconElement) {
            iconElement.className = 'bi ' + icon;
        }
        
        // Update button title
        button.setAttribute('title', optionElement.textContent.trim());
        
        // Update active state on menu items
        items.forEach(function(item) { 
            item.classList.remove('active'); 
        });
        optionElement.classList.add('active');
        
        // Trigger change event for form validation / grid update
        hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
    }
    
    // Export for manual triggering if needed
    window.TipsyBaboon = window.TipsyBaboon || {};
    window.TipsyBaboon.InitializePermissionLevelFields = function() {
        // No-op - event delegation handles everything automatically
    };
})();
