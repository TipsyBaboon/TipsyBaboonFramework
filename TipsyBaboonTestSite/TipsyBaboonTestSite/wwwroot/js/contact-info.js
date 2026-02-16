// Contact Info Field - Complex field with JSON-backed hidden field
// Updates hidden field on blur with JSON-encoded values

(function() {
    'use strict';
    
    // Use document-level event delegation for blur events
    document.addEventListener('blur', function(e) {
        var input = e.target.closest('.contact-info-input');
        if (input) {
            handleInputBlur(input);
        }
    }, true); // Use capture phase for blur events
    
    function handleInputBlur(inputElement) {
        var targetId = inputElement.getAttribute('data-target');
        var property = inputElement.getAttribute('data-property');
        
        if (!targetId || !property) {
            return;
        }
        
        var hiddenField = document.getElementById(targetId);
        if (!hiddenField) {
            return;
        }
        
        // Parse current JSON value
        var currentJson = hiddenField.value || '{}';
        var data;
        try {
            data = JSON.parse(currentJson);
        } catch (e) {
            data = {};
        }
        
        // Update the specific property
        data[property] = inputElement.value;
        
        // Write back to hidden field
        hiddenField.value = JSON.stringify(data);
        
        // Trigger change event for save coordinator (use jQuery for compatibility)
        if (window.jQuery) {
            jQuery(hiddenField).trigger('change').trigger('input');
        } else {
            hiddenField.dispatchEvent(new Event('change', { bubbles: true }));
            hiddenField.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }
    
    // Export for manual initialization if needed
    window.TipsyBaboon = window.TipsyBaboon || {};
    window.TipsyBaboon.InitializeContactInfoFields = function() {
        // No-op - event delegation handles everything automatically
    };
})();
