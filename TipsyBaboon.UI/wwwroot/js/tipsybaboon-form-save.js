(function($) {
    'use strict';
    
    var DEBOUNCE_DELAY = 800;
    var EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
    var FIELD_SELECTOR = '[data-field]';

    function TipsyBaboonForm($form, options) {
        this.$form = $form;
        
        // Read data attributes
        var apiPrefix = $form.data('api-prefix');
        var module = $form.data('module');
        var model = $form.data('model');
        var keyProperties = $form.data('key-properties');
        
        this.options = $.extend({
            isCreate: $form.data('is-create') === true || $form.data('is-create') === 'true',
            autoSave: $form.data('auto-save') === true || $form.data('auto-save') === 'true',
            apiPrefix: apiPrefix,
            module: module,
            model: model,
            keyProperties: keyProperties || ['Id'],
            detailUrlBase: null,
            fullScreenRedirect: false
        }, options);

        if (!this.options.apiPrefix) throw new Error('[TipsyBaboonForm] data-api-prefix is required on record-form element');
        if (!this.options.module) throw new Error('[TipsyBaboonForm] data-module is required on record-form element');
        if (!this.options.model) throw new Error('[TipsyBaboonForm] data-model is required on record-form element');

        this.originalValues = {};
        this.debounceTimers = {};
        this.isDirty = false;
        this.pendingNavigation = null;
        this.savedSinceOpen = false;

        this.$modal = $form.closest('.modal');
        this.bsModal = this.$modal.length ? bootstrap.Modal.getInstance(this.$modal[0]) : null;

        this.init();
    }

    TipsyBaboonForm.prototype = {

        init: function () {
            this.storeOriginalValues();
            this.attachEvents();
            this.initPrivacySelector();
            this.createUnsavedModal();
            this.updateDirtyState();
            
            // Emit form:ready event for field controls that initialize after form (e.g., RolePrivilege)
            this.$form.trigger('form:ready');
        },

        storeOriginalValues: function() {
            var self = this;
            this.originalValues = {};
            this.$form.find('input, select, textarea').each(function() {
                var $el = $(this);
                var name = $el.attr('name');
                if (!name || name.startsWith('__') || self.shouldExclude($el)) return;

                self.originalValues[name] =
                    $el.attr('type') === 'checkbox' ? $el.is(':checked') :
                    $el.attr('type') === 'radio' ? ($el.is(':checked') ? $el.val() : undefined) :
                    $el.val();
            });
        },

        shouldExclude: function($el) {
            return $el.is(':disabled, [readonly]') || $el.closest('[data-subform="true"]').length > 0;
        },

        attachEvents: function() {
            var self = this;

            this.$form.on('change.tbform input.tbform', FIELD_SELECTOR + ':not(input[type="checkbox"]' + FIELD_SELECTOR
                + ', input[type="radio"], ' + FIELD_SELECTOR
                + ', select' + FIELD_SELECTOR
                + ', input[type="hidden"]' + FIELD_SELECTOR
                + ', ' + FIELD_SELECTOR + ':hidden)', function() {
                $(this).attr('data-dirty', 'true');
            });

            // Mark checkboxes, radios, selects, and hidden inputs as dirty on change
            this.$form.on('change.tbform', 'input[type="checkbox"]' + FIELD_SELECTOR
                + ', input[type="radio"]' + FIELD_SELECTOR
                + ', select' + FIELD_SELECTOR
                + ', input[type="hidden"]' + FIELD_SELECTOR
                + ', ' + FIELD_SELECTOR + ':hidden', function() {
                $(this).attr('data-dirty', 'true');
            });

            this.options.autoSave && this.$form.on('blur.tbform', FIELD_SELECTOR, function() {
                var $field = $(this);
                if ($field.attr('data-dirty') !== 'true') return;
                var $wrapper = $field.closest('[data-module][data-model][data-record-keys]');
                if (!$wrapper.length) {
                    return;
                }
                var recordKeys = $wrapper.attr('data-record-keys');
                if (self.isNewRecord(recordKeys)) {
                    return;
                }
                var editKey = self.getEditKey($field);
                editKey && self.scheduleDebouncedSave(editKey);
            });

            this.options.autoSave && this.$form.on('change.tbform', 'input[type="checkbox"]' + FIELD_SELECTOR
                + ', input[type="radio"], ' + FIELD_SELECTOR
                + ', select' + FIELD_SELECTOR
                + ', input[type="hidden"]' + FIELD_SELECTOR
                + ', ' + FIELD_SELECTOR + ':hidden'
                , function () {
                var $field = $(this);
                if ($field.attr('data-dirty') !== 'true') return;
                var $wrapper = $field.closest('[data-module][data-model][data-record-keys]');
                if (!$wrapper.length) {
                    return;
                }
                var recordKeys = $wrapper.attr('data-record-keys');
                if (self.isNewRecord(recordKeys)) {
                    return;
                }
                var editKey = self.getEditKey($field);
                editKey && self.scheduleDebouncedSave(editKey);
            });

            this.$form.on('input.tbform change.tbform', 'input, select, textarea', function () {
                self.updateDirtyState();
            });

            // Click handler for save button - check both form and modal footer
            var $saveButtons = this.$form.find('.btn-save');
            this.$modal.length && ($saveButtons = $saveButtons.add(this.$modal.find('.modal-footer .btn-save')));
            
            $saveButtons.on('click.tbform', function(e) {
                e.preventDefault();
                self.save();
            });

            // Modal close lifecycle
            if (this.$modal.length) {
                this.$modal.on('hidden.bs.modal.tbform', function() {
                    self.fireLifecycleEvent('form:close', { savedBeforeClose: self.savedSinceOpen });
                    self.clearPendingEdits();
                    self.isDirty = false;
                    self.savedSinceOpen = false;
                });
            }

            !this.options.autoSave && this.attachNavigationGuard();
        },

        attachNavigationGuard: function() {
            var self = this;

            // Intercept link clicks and show styled modal for unsaved changes
            $(document).on('click.tbform', 'a[href]:not([href^="#"]):not([target="_blank"])', function(e) {
                if (!self.hasPendingEdits()) return;
                e.preventDefault();
                self.pendingNavigation = $(this).attr('href');
                self.showUnsavedModal();
            });
            
            // Note: No beforeunload - we use styled modals only, no browser dialogs
        },

        initPrivacySelector: function() {
            var $container = this.$modal.length ? this.$modal.find('.modal-content') : this.$form.closest('.container-fluid');
            var $level = $container.find('[data-privacy-level]');
            var $group = $container.find('[data-privacy-group]');
            var $levelHidden = this.$form.find('[data-privacy-level-hidden]');
            var $groupHidden = this.$form.find('[data-privacy-group-hidden]');
            var self = this;

            if (!$level.length) return;

            var syncVisibility = function() {
                var isGroup = $level.val() === '1';
                $group.toggle(isGroup);
                !isGroup && ($group.val(''), $groupHidden.val(''), self.$form.find('input[name="GroupId"]').val(''));
            };

            var syncHidden = function() {
                $levelHidden.val($level.val());
                $group.length && $groupHidden.length && $groupHidden.val($group.val());
                $group.length && !$groupHidden.length && $group.val() && (
                    self.$form.find('input[name="GroupId"]').length
                        ? self.$form.find('input[name="GroupId"]').val($group.val())
                        : $('<input>', { type: 'hidden', name: 'GroupId', 'data-privacy-group-hidden': '' }).val($group.val()).appendTo(self.$form)
                );
            };

            $level.on('change.tbform', function() { syncVisibility(); syncHidden(); self.updateDirtyState(); });
            $group.on('change.tbform', function() { syncHidden(); self.updateDirtyState(); });
            syncVisibility();
        },

        toggleSaveButton: function(enabled) {
            var $btn = this.$form.find('[type="submit"], .btn-save');
            this.$modal.length && ($btn = $btn.add(this.$modal.find('.btn-save')));
            $btn.prop('disabled', !enabled).toggleClass('disabled', !enabled);
        },

        updateDirtyState: function() {
            if (this.options.isCreate) {
                this.isDirty = true;
                this.toggleSaveButton(true);
                return;
            }

            var newDirty = Object.keys(this.getChangedFields()).length > 0 || this.hasPendingEdits();
            if (newDirty === this.isDirty) return;

            this.isDirty = newDirty;
            this.toggleSaveButton(newDirty);
            this.$form.trigger('form:dirty', { isDirty: newDirty });
        },

        getChangedFields: function() {
            var self = this;
            var changed = {};
            this.$form.find('input, select, textarea').each(function() {
                var $el = $(this);
                var name = $el.attr('name');
                if (!name || name.startsWith('__') || self.shouldExclude($el)) return;

                var current =
                    $el.attr('type') === 'checkbox' ? $el.is(':checked') :
                    $el.attr('type') === 'radio' ? ($el.is(':checked') ? $el.val() : undefined) :
                    $el.val();

                current !== undefined && !TipsyBaboon.Common.DeepEquals(current, self.originalValues[name]) &&
                    (changed[name] = current);
            });
            return changed;
        },

        isNewRecord: function(recordKeys) {
            if (!recordKeys) return true;
            try {
                var keys = typeof recordKeys === 'string' ? JSON.parse(recordKeys) : recordKeys;
            } catch (e) {
                throw new Error('[TipsyBaboonForm] Invalid record-keys JSON: ' + recordKeys);
            }
            return Object.keys(keys).length === 0 || Object.keys(keys).some(function(k) {
                return keys[k] == null || keys[k] === '' || keys[k] === EMPTY_GUID;
            });
        },

        getEditKey: function($field) {
            var $wrapper = $field.closest('[data-module][data-model][data-record-keys]');
            if (!$wrapper.length) return null;
            var level = $wrapper.parents('[data-module][data-model][data-record-keys]').length;
            var rk = $wrapper.attr('data-record-keys');
            return [level, $wrapper.attr('data-module'), $wrapper.attr('data-model'),
                typeof rk === 'string' ? rk : JSON.stringify(rk)].join('|');
        },

        getFieldValue: function($field) {
            if ($field.attr('type') === 'checkbox') return $field.is(':checked');
            var raw = $field.val();
            return $field.attr('data-is-json') ? JSON.parse(raw) : raw;
        },

        scheduleDebouncedSave: function(editKey) {
            var self = this;
            this.debounceTimers[editKey] && clearTimeout(this.debounceTimers[editKey]);
            this.debounceTimers[editKey] = setTimeout(function() {
                delete self.debounceTimers[editKey];
                self.saveByEditKey(editKey);
            }, DEBOUNCE_DELAY);
        },

        flushTimers: function() {
            var self = this;
            $.each(this.debounceTimers, function(k) { clearTimeout(self.debounceTimers[k]); });
            this.debounceTimers = {};
        },

        collectAllChanges: function() {
            var self = this;
            var changesByLevel = {};

            // For create forms, collect ALL fields; for edits, only dirty fields
            var selector = this.options.isCreate 
                ? FIELD_SELECTOR 
                : FIELD_SELECTOR + '[data-dirty="true"]';

            this.$form.find(selector).each(function() {
                var $field = $(this);
                var $wrapper = $field.closest('[data-module][data-model][data-record-keys]');
                if (!$wrapper.length) return; // Field not in a wrapper, skip

                var level = $wrapper.parents('[data-module][data-model][data-record-keys]').length;
                var module = $wrapper.attr('data-module');
                var model = $wrapper.attr('data-model');
                var fieldName = $field.attr('data-field');
                var recordKeys = $wrapper.attr('data-record-keys');
                var rkString = typeof recordKeys === 'string' ? recordKeys : JSON.stringify(recordKeys);
                var recordKey = module + '|' + model + '|' + rkString;

                changesByLevel[level] = changesByLevel[level] || {};
                changesByLevel[level][recordKey] = changesByLevel[level][recordKey] || {
                    level: level, module: module, model: model,
                    recordKeys: typeof recordKeys === 'string' ? JSON.parse(recordKeys) : recordKeys,
                    fields: {}, elements: [],
                    parentField: $wrapper.closest('[data-parent-field]').attr('data-parent-field') || null
                };

                changesByLevel[level][recordKey].fields[fieldName] = self.getFieldValue($field);
                changesByLevel[level][recordKey].elements.push($field);
            });

            return changesByLevel;
        },

        saveByEditKey: function(editKey) {
            var changes = this.collectAllChanges();
            var parts = editKey.split('|');
            var level = parseInt(parts[0], 10);
            var targetKey = parts.slice(1).join('|');
            var record = (changes[level] || {})[targetKey];

            if (!record || !Object.keys(record.fields).length) return $.Deferred().resolve().promise();

            return this.saveRecord(record).then(function(response) {
                $.each(record.elements, function(_, $f) { $f.removeAttr('data-dirty'); });
                response && response.Message && TipsyBaboon.Common.ShowMessage(response.Message, 'success');
                return response;
            });
        },

        fireLifecycleEvent: function(name, detail) {
            var event = new CustomEvent(name, { bubbles: true, cancelable: true, detail: detail || {} });
            return this.$form[0].dispatchEvent(event);
        },

        save: function() {
            var changes = this.collectAllChanges();
            var levels = Object.keys(changes).map(Number).sort(function(a, b) { return a - b; });
            if (!levels.length) return $.Deferred().resolve().promise();

            var changedFields = {};
            $.each(changes, function(_, records) {
                $.each(records, function(_, record) {
                    $.extend(changedFields, record.fields);
                });
            });

            if (!this.fireLifecycleEvent('form:beforeSave', { fields: changedFields })) {
                return $.Deferred().reject('cancelled').promise();
            }

            return this.saveLevels(0, levels, changes, {});
        },

        saveLevels: function(index, levels, changes, parentIdMap) {
            if (index >= levels.length) {
                this.$form.trigger('form:saved', { parentIdMap: parentIdMap });
                this.handleSaveSuccess({ Success: true, Data: parentIdMap });
                return $.Deferred().resolve().promise();
            }

            var self = this;
            var records = changes[levels[index]];
            var toSave = [];
            var promises = [];

            $.each(records, function(_, record) {
                if (!record.fields || !Object.keys(record.fields).length) return;

                record.parentField && parentIdMap[record.parentField] && (
                    record.fields[record.parentField] = parentIdMap[record.parentField],
                    record.recordKeys && (record.recordKeys[record.parentField] = parentIdMap[record.parentField])
                );

                toSave.push(record);
                promises.push(self.saveRecord(record));
            });

            if (!promises.length) return this.saveLevels(index + 1, levels, changes, parentIdMap);

            return $.when.apply($, promises).then(function() {
                var responses = promises.length === 1 ? [arguments[0]] : Array.prototype.slice.call(arguments);

                $.each(responses, function(i, response) {
                    if (!response || !response.Data || !toSave[i]) return;
                    
                    // Capture ALL properties from response.Data into parentIdMap
                    // This handles any key column name (Id, SupplierId, CustomKey, etc.)
                    $.each(response.Data, function(propName, propValue) {
                        if (propValue === undefined || propValue === null) return;
                        if (typeof propValue === 'object') return; // Skip complex objects
                        parentIdMap[propName] = propValue;
                        parentIdMap[toSave[i].model + '.' + propName] = propValue;
                        parentIdMap[toSave[i].model + propName] = propValue;
                    });
                });

                $.each(toSave, function(_, record) {
                    $.each(record.elements, function(_, $f) { $f.removeAttr('data-dirty'); });
                });

                index === 0 && TipsyBaboon.Common.ShowMessage('Saved successfully', 'success');
                return self.saveLevels(index + 1, levels, changes, parentIdMap);
            });
        },

        saveRecord: function(record) {
            if (!record.fields || !Object.keys(record.fields).length) return $.Deferred().resolve().promise();


            // Top-level (level 0): use explicit isCreate flag
            // Nested items (level > 0): always PATCH - they came from database
            var isNew = (record.level === 0) ? this.options.isCreate : false;
            
            // Construct URL from prefix + module + model
            var recordModule = (record.level === 0) ? this.options.module : record.module;
            var recordModel = (record.level === 0) ? this.options.model : record.model;
            var url = '/' + this.options.apiPrefix + '/' + encodeURIComponent(recordModule) + '/' + encodeURIComponent(recordModel);

            var payload = $.extend({}, record.recordKeys, record.fields);
            
            // For create operations, include ALL form fields (including hidden ones)
            if (isNew && record.level === 0) {
                var self = this;
                this.$form.find('input[type="hidden"][data-locked="true"]').each(function() {
                    var $field = $(this);
                    var name = $field.attr('name');
                    if (name && !name.startsWith('__') && !payload.hasOwnProperty(name)) {
                        payload[name] = $field.val();
                    }
                });
            }


            return $.ajax({
                url: url,
                method: isNew ? 'POST' : 'PATCH',
                contentType: 'application/json',
                data: JSON.stringify(payload),
                dataType: 'json'
            }).done(function(response) {
            }).catch(function(xhr) {
                console.error('[TipsyBaboonForm] Save failed:', xhr);
                var msg = 'Failed to save changes';
                try { msg = (xhr.responseJSON || JSON.parse(xhr.responseText)).Message || msg; } catch(e) {}
                TipsyBaboon.Common.ShowError(msg);
                throw new Error(msg);
            });
        },

        handleSaveSuccess: function(response) {
            if (!response || !response.Success) {
                TipsyBaboon.Common.ShowError((response && response.Message) || 'Save failed');
                return;
            }

            this.savedSinceOpen = true;
            this.storeOriginalValues();
            this.updateDirtyState();
            
            // If full-screen redirect is enabled and this was a create, redirect to detail page
            // Check this FIRST before modal handling, so modal creates can redirect to full-screen detail
            if (this.options.fullScreenRedirect && this.options.isCreate && response.Data) {
                this.handleFullScreenPostCreate(response);
            }
            // If in modal, handle modal-specific post-save
            else if (this.$modal.length && this.bsModal) {
                this.handleModalPostSave(response);
            }
        },

        handleFullScreenPostCreate: function(response) {
            // Get the key value using the schema's key property name
            var keyProp = Array.isArray(this.options.keyProperties) && this.options.keyProperties.length > 0 
                ? this.options.keyProperties[0] 
                : 'Id';
            var id = response.Data[keyProp];
            
            if (id && this.options.detailUrlBase) {
                var detailUrl = this.options.detailUrlBase + '/' + encodeURIComponent(id);
                window.location.href = detailUrl;
            }
        },

        handleModalPostSave: function(response) {
            var self = this;
            
            // Check if this is a lookup-initiated create (should just close, not open edit form)
            var urlParams = new URLSearchParams(window.location.search);
            var isLookupCreate = urlParams.get('isLookupCreate') === 'true';

            if (this.options.isCreate && response.Data && !isLookupCreate) {
                // Get the key value using the schema's key property name (first key for single-key models)
                var keyProp = Array.isArray(this.options.keyProperties) && this.options.keyProperties.length > 0 
                    ? this.options.keyProperties[0] 
                    : 'Id';
                var id = response.Data[keyProp];
                id && this.options.detailUrlBase && (function() {
                    var editUrl = self.options.detailUrlBase + '/' + encodeURIComponent(id);
                    self.$modal.one('hidden.bs.modal', function() {
                        TipsyBaboon.UI.OpenModal(editUrl, function() { self.refreshGrids(); });
                    });
                    setTimeout(function() { self.bsModal.hide(); }, 300);
                })();
                return;
            }

            setTimeout(function() {
                self.bsModal.hide();
                self.$modal.one('hidden.bs.modal', function() { self.refreshGrids(); });
            }, 300);
        },

        refreshGrids: function() {
            TipsyBaboon.UI && TipsyBaboon.UI.GridInstances && $.each(TipsyBaboon.UI.GridInstances, function(_, grid) {
                grid.Refresh && grid.Refresh();
            });
        },

        hasPendingEdits: function() {
            return this.$form.find('[data-dirty="true"]').length > 0;
        },

        clearPendingEdits: function() {
            this.flushTimers();
            this.$form.find('[data-dirty="true"]').removeAttr('data-dirty');
        },

        createUnsavedModal: function() {
            if ($('#unsaved-changes-modal').length || this.options.autoSave) return;

            var self = this;
            $('<div>', { id: 'unsaved-changes-modal', 'class': 'unsaved-changes-modal', css: { display: 'none' } }).append(
                $('<div>', { 'class': 'unsaved-changes-overlay' }),
                $('<div>', { 'class': 'unsaved-changes-content' }).append(
                    $('<h3>', { text: 'Unsaved Changes' }),
                    $('<p>', { text: 'Your changes have not been saved. Would you like to save before continuing?' }),
                    $('<div>', { 'class': 'unsaved-changes-buttons' }).append(
                        $('<button>', { type: 'button', 'class': 'btn btn-secondary', text: 'Discard', 'data-action': 'discard' }),
                        $('<button>', { type: 'button', 'class': 'btn btn-primary', text: 'Save', 'data-action': 'save' }),
                        $('<button>', { type: 'button', 'class': 'btn btn-outline-secondary', text: 'Cancel', 'data-action': 'cancel' })
                    )
                )
            ).on('click', '[data-action]', function() {
                self.handleUnsavedAction($(this).data('action'));
            }).appendTo('body');
        },

        showUnsavedModal: function() { $('#unsaved-changes-modal').show(); },
        hideUnsavedModal: function() { $('#unsaved-changes-modal').hide(); },

        handleUnsavedAction: function(action) {
            this.hideUnsavedModal();
            var self = this;

            var actions = {
                discard: function() {
                    self.clearPendingEdits();
                    self.pendingNavigation && (window.location.href = self.pendingNavigation);
                },
                save: function() {
                    self.save().then(function() {
                        self.pendingNavigation && (window.location.href = self.pendingNavigation);
                    });
                }
            };

            (actions[action] || $.noop)();
            this.pendingNavigation = null;
        },

        destroy: function() {
            this.$form.off('.tbform');
            this.$modal.length && this.$modal.off('.tbform');
            $(document).off('.tbform');
            $(window).off('.tbform');
            this.flushTimers();
            this.$form.removeData('tipsyBaboonForm');
        }
    };

    $.fn.tipsyBaboonForm = function(options) {
        return this.each(function() {
            var $el = $(this);
            $el.data('tipsyBaboonForm') || $el.data('tipsyBaboonForm', new TipsyBaboonForm($el, options || {}));
        });
    };

    window.TipsyBaboon = window.TipsyBaboon || {};
    window.TipsyBaboon.FormSave = {
        hasPendingEdits: function() {
            return $('[data-dirty="true"]').length > 0;
        },

        clearPendingEdits: function() {
            $('[data-dirty="true"]').removeAttr('data-dirty');
        },

        savePendingEditsForContainer: function($container) {
            var $recordForm = $container.closest('[data-save-orchestrator]');
            var instance = $recordForm.length && $recordForm.data('tipsyBaboonForm');
            return instance ? instance.save() : $.Deferred().resolve().promise();
        }
    };

})(jQuery);
