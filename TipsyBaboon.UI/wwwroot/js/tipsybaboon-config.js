/**
 * TipsyBaboon Configuration Page JavaScript
 * Handles auto-save on change with debouncing for configuration settings.
 */
(function() {
    'use strict';

    // Ensure namespace exists
    window.TipsyBaboon = window.TipsyBaboon || {};

    /**
     * Configuration page module
     */
    TipsyBaboon.Config = {
        options: {
            formSelector: '#configForm',
            saveUrl: '/Configuration?handler=Save',
            debounceMs: 500
        },
        saveTimers: {},

        /**
         * Initialize configuration page functionality
         * @param {Object} options - Configuration options
         * @param {string} options.formSelector - CSS selector for the form
         * @param {string} options.saveUrl - URL to POST save requests to
         * @param {number} [options.debounceMs=500] - Debounce delay in milliseconds
         */
        Init: function(options) {
            var self = this;
            this.options = Object.assign({}, this.options, options);

            // Initialize tooltips if Bootstrap is available
            this.InitTooltips();

            // Attach change handlers
            this.AttachHandlers();
        },

        /**
         * Initialize Bootstrap tooltips
         */
        InitTooltips: function() {
            if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
                var tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
                tooltipTriggerList.forEach(function(tooltipTriggerEl) {
                    new bootstrap.Tooltip(tooltipTriggerEl);
                });
            }
        },

        /**
         * Attach change handlers to all config inputs
         */
        AttachHandlers: function() {
            var self = this;
            var form = document.querySelector(this.options.formSelector);
            if (!form) return;

            // Support both old-style (.config-input with data-property) and new-style (data-field from FieldRenderer)
            var inputs = form.querySelectorAll('.config-input, [data-field]');
            inputs.forEach(function(input) {
                input.addEventListener('change', function() {
                    self.OnInputChange(this);
                });
                input.addEventListener('input', function() {
                    self.OnInputChange(this);
                });
            });
        },

        /**
         * Handle input change with debouncing
         * @param {HTMLElement} input - The input element that changed
         */
        OnInputChange: function(input) {
            var self = this;
            // Support both data-property (old) and data-field (FieldRenderer)
            var propertyName = input.dataset.field || input.dataset.property;
            if (!propertyName) return;
            
            var statusEl = document.querySelector('.save-status[data-property="' + propertyName + '"]');

            // Clear existing timer
            if (this.saveTimers[propertyName]) {
                clearTimeout(this.saveTimers[propertyName]);
            }

            // Show saving status
            if (statusEl) {
                statusEl.style.display = 'block';
                statusEl.innerHTML = '<span class="spinner-border" role="status" aria-hidden="true"></span> Saving...';
                statusEl.classList.remove('text-danger');
            }

            // Debounce save
            this.saveTimers[propertyName] = setTimeout(function() {
                self.SaveProperty(propertyName, input, statusEl);
            }, this.options.debounceMs);
        },

        /**
         * Save a single property value
         * @param {string} propertyName - Name of the property to save
         * @param {HTMLElement} input - The input element
         * @param {HTMLElement} statusEl - Status display element
         */
        SaveProperty: function(propertyName, input, statusEl) {
            var self = this;
            var value;

            if (input.type === 'checkbox') {
                value = input.checked.toString();
            } else {
                value = input.value;
            }

            var typeName = document.getElementById('selectedType')?.value;
            var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            if (!token) {
                console.error('[TipsyBaboon.Config] Anti-forgery token not found for save request');
            }

            fetch(this.options.saveUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token || ''
                },
                body: JSON.stringify({
                    typeName: typeName,
                    propertyName: propertyName,
                    value: value
                })
            })
            .then(function(response) {
                if (!response.ok) throw new Error('Server returned ' + response.status + ': ' + response.statusText);
                return response.json();
            })
            .then(function(data) {
                if (data.success) {
                    if (statusEl) {
                        statusEl.innerHTML = '<i class="bi bi-check-circle-fill text-success"></i> Saved';
                        statusEl.classList.remove('text-danger');
                        setTimeout(function() {
                            statusEl.style.display = 'none';
                        }, 2000);
                    }
                } else {
                    if (statusEl) {
                        statusEl.innerHTML = '<i class="bi bi-exclamation-triangle-fill text-danger"></i> ' + (data.error || 'Save failed');
                        statusEl.classList.add('text-danger');
                    }
                }
            })
            .catch(function(error) {
                console.error('Save error:', error);
                var errorMsg = '<i class="bi bi-exclamation-triangle-fill text-danger"></i> Error: ' + error.message;
                if (statusEl) {
                    statusEl.innerHTML = errorMsg;
                    statusEl.classList.add('text-danger');
                    statusEl.style.display = '';
                } else {
                    TipsyBaboon.Common.ShowError('Failed to save property: ' + error.message);
                }
            });
        }
    };

})();
