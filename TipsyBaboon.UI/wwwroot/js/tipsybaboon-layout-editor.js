(function ($) {
    'use strict';

    var $container = $('#layout-editor');
    if (!$container.length) return;

    var apiUrl = $container.data('api-url');
    var displayName = $container.data('display-name');
    var $canvas = $('#layout-canvas');
    var $toolbox = $('#available-fields-list');
    var $commonTools = $('#common-tools');
    var $availableCount = $('#available-count');
    var $noFieldsMsg = $('#no-fields-message');
    var $btnSave = $('#btn-save-layout');
    var $btnSave2 = $('#btn-save-layout-2');
    var $btnReset = $('#btn-reset-layout');
    var $btnReset2 = $('#btn-reset-layout-2');

    var layoutData = { Sections: [], AvailableFields: [] };
    var isDirty = false;
    var tracker = new ObjectTracker();
    var editingSection = null;
    var editingField = null;

    Init();

    function ObjectTracker() {
        var tracked = [];
        var counter = 0;
        var key = '$$objectId';

        this.track = function (obj) {
            if (typeof obj === 'object' && obj[key] == null) {
                obj[key] = 't-' + (++counter);
                tracked.push(obj);
            }
            return obj[key];
        };

        this.find = function (id) {
            for (var i = 0; i < tracked.length; i++) {
                if (tracked[i][key] === id) return tracked[i];
            }
            return null;
        };

        this.getId = function (obj) {
            return obj ? obj[key] : null;
        };

        this.remove = function (obj) {
            var idx = tracked.indexOf(obj);
            if (idx >= 0) tracked.splice(idx, 1);
            if (obj) delete obj[key];
        };

        this.reset = function () {
            tracked = [];
            counter = 0;
        };
    }

    function Init() {
        $btnSave.on('click', SaveLayout);
        $btnSave2.on('click', SaveLayout);
        $btnReset.on('click', ResetLayout);
        $btnReset2.on('click', ResetLayout);
        $('#btn-add-section').on('click', AddSection);

        $('#section-modal').on('hide.bs.modal', ApplySectionChanges);
        $('#field-settings-modal').on('hide.bs.modal', ApplyFieldChanges);

        LoadLayout();
    }

    function LoadLayout() {
        $canvas.html('<div class="col-12 text-center py-5"><div class="spinner-border text-primary"></div><p class="mt-2 text-muted">Loading layout...</p></div>');

        $.get(apiUrl)
            .done(function (data) {
                tracker.reset();
                layoutData = data;
                layoutData.Sections.forEach(function (s) {
                    tracker.track(s);
                    s.Properties.forEach(function (p) { tracker.track(p); });
                });
                layoutData.AvailableFields.forEach(function (f) { tracker.track(f); });
                RenderAll();
                SetDirty(false);
            })
            .fail(function (err) {
                $canvas.html('<div class="col-12 alert alert-danger">Failed to load layout: ' + (err.statusText || 'Unknown error') + '</div>');
            });
    }

    function RenderAll() {
        RenderCanvas();
        RenderToolbox();
        ApplySorting();
    }

    function RenderCanvas() {
        if (layoutData.Sections.length === 0) {
            $canvas.html('<div class="col-12 alert alert-info"><i class="bi bi-info-circle me-2"></i>No sections. Drag "New Section" from the toolbox.</div>');
            return;
        }

        var html = '';
        layoutData.Sections.sort(function (a, b) { return a.Order - b.Order; })
            .forEach(function (section) {
                var id = tracker.getId(section);
                html += '<div id="' + id + '" class="col-' + section.ColumnSize + ' layout-section mb-2" style="padding: 0 .125em;">';
                html += '<div class="card">';

                html += '<div class="card-header section-handle d-flex justify-content-between align-items-center" style="cursor: move;">';
                html += '<span class="h6 mb-0">' + EscapeHtml(section.Title) + '</span>';
                html += '<div class="section-controls">';
                html += '<button type="button" class="btn btn-link p-0 btn-edit-section" title="Edit section"><i class="bi bi-pencil"></i></button>';
                html += '<button type="button" class="btn btn-link p-0 ms-2 btn-delete-section" title="Remove section"><i class="bi bi-trash"></i></button>';
                html += '<button type="button" class="btn btn-link p-0 ms-2 btn-minimize-section" title="Minimize"><i class="bi bi-dash"></i></button>';
                html += '</div>';
                html += '</div>';

                html += '<div class="card-wrapper" style="display: ' + (section.Minimized ? 'none' : 'block') + ';">';
                html += '<div class="card-body p-2">';
                html += '<div class="row section-items" data-section-id="' + id + '">';

                if (section.Properties.length === 0) {
                    html += '<div class="col-12 drop-placeholder text-center py-4 border border-dashed rounded">';
                    html += '<i class="bi bi-plus-circle me-1"></i> Drop fields here';
                    html += '</div>';
                } else {
                    section.Properties.sort(function (a, b) { return a.Order - b.Order; })
                        .forEach(function (prop) {
                            html += RenderLayoutField(prop, section);
                        });
                }

                html += '</div>';
                html += '</div>';
                html += '</div>';

                html += '</div>';
                html += '</div>';
            });

        $canvas.html(html);
        BindSectionEvents();
    }

    function RenderLayoutField(prop, section) {
        var id = tracker.getId(prop);
        var sectionId = tracker.getId(section);
        var isSpacer = prop.PropertyName.indexOf('__Spacer') === 0;

        // Scale column size relative to section width (a col-6 field in a col-6 section = full width)
        var scaledColSize = Math.min(12, Math.round(prop.ColumnSize * 12 / section.ColumnSize));

        var html = '<div id="' + id + '" class="col-' + scaledColSize + ' field-item" data-parent-id="' + sectionId + '" style="display: inline-flex;">';
        html += '<div class="card flex-fill" style="min-height: 3rem; max-height: 3rem; height: 3rem; overflow: hidden;">';
        html += '<div class="portlet-handler" style="height: 100%;">';
        html += '<div class="d-flex" style="height: 100%;">';
        html += '<div class="text-center d-flex align-items-center justify-content-center handle" style="cursor: grab; width: 2.5rem; min-width: 2.5rem;">';
        html += '<i class="bi bi-grip-vertical"></i>';
        html += '</div>';
        html += '<div class="flex-fill' + (isSpacer ? ' bg-light' : '') + '">';
        html += '<div class="card-body py-1 px-2 d-flex align-items-center" style="height: 100%; overflow: hidden;">';
        html += '<div class="flex-fill">';
        html += '<span class="field-controls float-end">';
        html += '<button type="button" class="btn btn-link p-0 btn-edit-field" title="Settings"><i class="bi bi-gear"></i></button>';
        if (!prop.IsRequired || isSpacer) {
            html += '<button type="button" class="btn btn-link p-0 ms-1 btn-remove-field" title="Remove"><i class="bi bi-trash"></i></button>';
        }
        html += '</span>';

        if (isSpacer) {
            html += '<span class="field-label">Spacer</span>';
        } else {
            if (prop.ShowLabel) {
                html += '<small class="field-label">' + EscapeHtml(prop.DisplayName) + '</small>';
            }
            html += '<div class="field-property">' + EscapeHtml(prop.PropertyName) + '</div>';
        }

        html += '</div>';
        html += '</div>';
        html += '</div>';
        html += '</div>';
        html += '</div>';
        html += '</div>';
        html += '</div>';

        return html;
    }

    function RenderToolbox() {
        var fields = layoutData.AvailableFields || [];
        $availableCount.text(fields.length);

        if (fields.length === 0) {
            $toolbox.html('');
            $noFieldsMsg.removeClass('d-none');
            return;
        }

        $noFieldsMsg.addClass('d-none');

        var groups = {};
        fields.forEach(function (f) {
            var g = f.GroupName || 'Other';
            if (!groups[g]) groups[g] = [];
            groups[g].push(f);
        });

        var html = '';
        Object.keys(groups).sort().forEach(function (groupName) {
            html += '<div class="toolbox-group mb-2">';
            html += '<div class="toolbox-group-header">' + EscapeHtml(groupName) + '</div>';
            groups[groupName].forEach(function (f) {
                var id = tracker.getId(f);
                html += '<div id="' + id + '" class="field-item new-field mb-2" draggable="true">';
                html += '<div class="card" style="min-height: 3rem; max-height: 3rem; height: 3rem;">';
                html += '<div class="d-flex" style="height: 100%;">';
                html += '<div class="text-center d-flex align-items-center justify-content-center handle" style="cursor: grab; width: 2.5rem; min-width: 2.5rem;">';
                html += '<i class="bi bi-grip-vertical"></i>';
                html += '</div>';
                html += '<div class="flex-fill">';
                html += '<div class="card-body py-1 px-2 d-flex align-items-center" style="height: 100%;">';
                html += '<div class="flex-fill">';
                html += '<small class="field-label">' + EscapeHtml(f.DisplayName) + '</small>';
                html += '<div class="field-property">' + EscapeHtml(f.PropertyName) + '</div>';
                html += '</div>';
                html += '</div>';
                html += '</div>';
                html += '</div>';
                html += '</div>';
                html += '</div>';
            });
            html += '</div>';
        });

        $toolbox.html(html);
    }

    function ApplySorting() {
        InitLayoutAreaSortable();
        InitSectionItemsSortable();
        ApplyToolboxDraggables();
    }

    function InitLayoutAreaSortable() {
        try {
            $canvas.sortable('destroy');
        } catch (e) {
            // Not yet initialized
        }
        $canvas.sortable({
            items: '.layout-section',
            handle: '.section-handle',
            placeholder: 'layout-section-placeholder col-12',
            tolerance: 'pointer',
            disabled: false,
            helper: function (ev, element) {
                var $clone = $(element).clone();
                return $clone;
            },
            start: function (ev, ui) {
                var width = ui.item.outerWidth();
                ui.helper.width(width);
                var section = tracker.find($(ui.item).attr('id'));
                if (section) {
                    ui.placeholder.addClass('col-' + section.ColumnSize);
                    ui.placeholder.css({ 'min-height': '100px', 'background': 'rgba(var(--bs-success-rgb), 0.1)', 'border': '2px dashed var(--bs-primary)' });
                }
            },
            stop: function (ev, ui) {
                ui.item.css('width', '');
                UpdateSectionOrder();
                SetDirty(true);
            }
        });
    }

    function InitSectionItemsSortable() {
        $('.section-items').each(function () {
            try {
                $(this).sortable('destroy');
            } catch (e) {
                // Not yet initialized
            }
        });
        $('.section-items').sortable({
            items: '.field-item',
            handle: '.handle',
            connectWith: '.section-items',
            placeholder: 'field-placeholder col-4',
            tolerance: 'pointer',
            disabled: false,
            helper: function (ev, element) {
                var $clone = $(element).clone();
                return $clone;
            },
            start: function (ev, ui) {
                var width = ui.item.outerWidth();
                ui.helper.width(width);
                var field = tracker.find($(ui.item).attr('id'));
                if (field) {
                    ui.placeholder.addClass('col-' + field.ColumnSize);
                    ui.placeholder.css({ 'display': 'inline-flex', 'min-height': '50px', 'background': 'rgba(var(--bs-success-rgb), 0.1)', 'border': '2px dashed var(--bs-success)' });
                }
            },
            receive: function (ev, ui) {
                var isNew = ui.item.hasClass('new-field');
                var newSectionId = $(this).data('section-id');
                var newSection = tracker.find(newSectionId);

                if (!newSection) return;

                if (isNew) {
                    var toolType = ui.item.data('tool-type');
                    var sourceField = tracker.find($(ui.item).attr('id'));

                    var newProp;
                    if (toolType === 'Spacer') {
                        newProp = {
                            PropertyName: '__Spacer_' + GenerateGuid().substring(0, 8),
                            DisplayName: 'Spacer',
                            Order: 0,
                            ColumnSize: 6,
                            ShowLabel: false,
                            CssClass: null,
                            IsSpacer: true
                        };
                    } else if (sourceField) {
                        newProp = {
                            PropertyName: sourceField.PropertyName,
                            DisplayName: sourceField.DisplayName,
                            Order: 0,
                            ColumnSize: sourceField.ColumnSize || 6,
                            ShowLabel: true,
                            CssClass: null
                        };
                        var idx = layoutData.AvailableFields.indexOf(sourceField);
                        if (idx >= 0) layoutData.AvailableFields.splice(idx, 1);
                    }

                    if (newProp) {
                        tracker.track(newProp);
                        newSection.Properties.push(newProp);
                    }

                    ui.item.remove();
                    if (dragState && dragState.original) {
                        dragState.original.appendTo(dragState.parent);
                        dragState = null;
                    }

                    UpdateFieldOrder(newSection);
                    SetDirty(true);
                    setTimeout(function () { RenderAll(); }, 50);
                } else {
                    // Existing field moved between sections
                    var fieldId = $(ui.item).attr('id');
                    var field = tracker.find(fieldId);
                    var oldSectionId = $(ui.item).data('parent-id');
                    var oldSection = tracker.find(oldSectionId);

                    if (oldSection && field && oldSectionId !== newSectionId) {
                        // Remove from old section
                        var idx = oldSection.Properties.indexOf(field);
                        if (idx >= 0) oldSection.Properties.splice(idx, 1);
                        
                        // Add to new section
                        if (newSection.Properties.indexOf(field) < 0) {
                            newSection.Properties.push(field);
                        }

                        UpdateFieldOrder(oldSection);
                        UpdateFieldOrder(newSection);
                        SetDirty(true);
                        setTimeout(function () { RenderAll(); }, 50);
                    }
                }
            },
            stop: function (ev, ui) {
                ui.item.css('width', '');
                var isNew = ui.item.hasClass('new-field');
                if (isNew) return;

                // For same-section reordering only
                var fieldId = $(ui.item).attr('id');
                var field = tracker.find(fieldId);
                var thisSectionId = $(this).data('section-id');
                var thisSection = tracker.find(thisSectionId);

                if (thisSection) {
                    UpdateFieldOrder(thisSection);
                    SetDirty(true);
                }
            }
        });
    }

    var dragState = null;

    function ApplyToolboxDraggables() {
        $toolbox.find('.field-item').draggable({
            connectToSortable: '.section-items',
            handle: '.handle',
            helper: function (ev) {
                var $clone = $(this).clone();
                $clone.addClass('col-6');
                $clone.css('display', 'inline-flex');
                return $clone;
            },
            revert: 'invalid',
            start: function (ev, ui) {
                var width = $(ev.currentTarget).outerWidth();
                ui.helper.width(width);
                dragState = { original: $(ev.currentTarget), parent: $(ev.currentTarget).parent() };
            }
        });

        $commonTools.find('.field-item[data-tool-type="Spacer"]').draggable({
            connectToSortable: '.section-items',
            handle: '.handle',
            helper: function (ev) {
                var $clone = $(this).clone();
                $clone.addClass('col-6');
                $clone.css('display', 'inline-flex');
                return $clone;
            },
            revert: 'invalid',
            start: function (ev, ui) {
                var width = $(ev.currentTarget).outerWidth();
                ui.helper.width(width);
                dragState = { original: $(ev.currentTarget), parent: $(ev.currentTarget).parent() };
            }
        });
    }

    function AddSection() {
        var newSection = {
            Id: GenerateGuid(),
            Name: 'New Section',
            Title: 'New Section',
            Description: null,
            Order: layoutData.Sections.length,
            ColumnSize: 12,
            IsCollapsible: true,
            IsInitiallyCollapsed: false,
            CssClass: null,
            Properties: []
        };
        tracker.track(newSection);
        layoutData.Sections.push(newSection);
        SetDirty(true);
        RenderAll();
    }

    function BindSectionEvents() {
        $canvas.find('.btn-edit-section').on('click', function (e) {
            e.stopPropagation();
            var sectionId = $(this).closest('.layout-section').attr('id');
            var section = tracker.find(sectionId);
            if (section) ShowSectionModal(section);
        });

        $canvas.find('.btn-delete-section').on('click', function (e) {
            e.stopPropagation();
            var sectionId = $(this).closest('.layout-section').attr('id');
            var section = tracker.find(sectionId);
            if (section) DeleteSection(section);
        });

        $canvas.find('.btn-minimize-section').on('click', function (e) {
            e.stopPropagation();
            var $section = $(this).closest('.layout-section');
            var section = tracker.find($section.attr('id'));
            if (section) ToggleSectionMinimize(section, $section);
        });

        $canvas.find('.section-handle').on('dblclick', function (e) {
            var $section = $(this).closest('.layout-section');
            var section = tracker.find($section.attr('id'));
            if (section) ToggleSectionMinimize(section, $section);
        });

        $canvas.find('.btn-edit-field').on('click', function (e) {
            e.stopPropagation();
            var fieldId = $(this).closest('.field-item').attr('id');
            var field = tracker.find(fieldId);
            if (field) ShowFieldModal(field);
        });

        $canvas.find('.btn-remove-field').on('click', function (e) {
            e.stopPropagation();
            var $field = $(this).closest('.field-item');
            var fieldId = $field.attr('id');
            var sectionId = $field.data('parent-id');
            var field = tracker.find(fieldId);
            var section = tracker.find(sectionId);
            if (field && section) RemoveField(field, section);
        });
    }

    function ToggleSectionMinimize(section, $section) {
        section.Minimized = !section.Minimized;
        $section.find('.card-wrapper').slideToggle(200);
    }

    function ShowSectionModal(section) {
        editingSection = section;
        $('#section-modal-title').text(section.Title);
        $('#section-title-input').val(section.Title);
        $('#section-column-size').val(section.ColumnSize);
        var modal = new bootstrap.Modal(document.getElementById('section-modal'));
        modal.show();
    }

    function ApplySectionChanges() {
        if (!editingSection) return;
        var title = $('#section-title-input').val().trim();
        if (title) {
            editingSection.Title = title;
            editingSection.Name = title;
        }
        editingSection.ColumnSize = parseInt($('#section-column-size').val()) || 12;
        editingSection = null;
        SetDirty(true);
        RenderCanvas();
        ApplySorting();
    }

    function DeleteSection(section) {
        if (section.Properties.length > 0) {
            if (!confirm('Remove section "' + section.Title + '" and return ' + section.Properties.length + ' field(s) to the toolbox?')) return;
        }

        section.Properties.forEach(function (p) {
            if (p.PropertyName.indexOf('__Spacer') !== 0) {
                layoutData.AvailableFields.push({
                    PropertyName: p.PropertyName,
                    DisplayName: p.DisplayName,
                    GroupName: 'Returned',
                    ColumnSize: p.ColumnSize,
                    IsRequired: false,
                    ClrTypeName: ''
                });
                tracker.track(layoutData.AvailableFields[layoutData.AvailableFields.length - 1]);
            }
        });

        var idx = layoutData.Sections.indexOf(section);
        if (idx >= 0) layoutData.Sections.splice(idx, 1);
        tracker.remove(section);

        SetDirty(true);
        RenderAll();
    }

    function ShowFieldModal(field) {
        editingField = field;
        var isSpacer = field.PropertyName.indexOf('__Spacer') === 0;
        $('#field-modal-title').text(isSpacer ? 'Spacer Settings' : 'Field Settings');
        $('#field-display-name').val(isSpacer ? 'Spacer' : field.DisplayName);
        $('#field-column-size').val(field.ColumnSize);
        $('#field-show-label').prop('checked', field.ShowLabel);
        $('#field-show-label').closest('.form-check').toggle(!isSpacer);
        var modal = new bootstrap.Modal(document.getElementById('field-settings-modal'));
        modal.show();
    }

    function ApplyFieldChanges() {
        if (!editingField) return;
        editingField.ColumnSize = parseInt($('#field-column-size').val()) || 12;
        editingField.ShowLabel = $('#field-show-label').prop('checked');
        editingField = null;
        SetDirty(true);
        RenderCanvas();
        ApplySorting();
    }

    function RemoveField(field, section) {
        var idx = section.Properties.indexOf(field);
        if (idx >= 0) section.Properties.splice(idx, 1);

        if (field.PropertyName.indexOf('__Spacer') !== 0) {
            layoutData.AvailableFields.push({
                PropertyName: field.PropertyName,
                DisplayName: field.DisplayName,
                GroupName: 'Returned',
                ColumnSize: field.ColumnSize,
                IsRequired: false,
                ClrTypeName: ''
            });
            tracker.track(layoutData.AvailableFields[layoutData.AvailableFields.length - 1]);
        }

        tracker.remove(field);
        SetDirty(true);
        RenderAll();
    }

    function UpdateSectionOrder() {
        var order = $canvas.sortable('toArray');
        order.forEach(function (id, idx) {
            var section = tracker.find(id);
            if (section) section.Order = idx;
        });
    }

    function UpdateFieldOrder(section) {
        var $container = $('[data-section-id="' + tracker.getId(section) + '"]');
        var order = $container.sortable('toArray');
        order.forEach(function (id, idx) {
            var field = tracker.find(id);
            if (field) field.Order = idx;
        });
        section.Properties.sort(function (a, b) { return a.Order - b.Order; });
    }

    function SaveLayout() {
        $btnSave.prop('disabled', true).html('<span class="spinner-border me-1"></span> Saving...');

        var sections = layoutData.Sections.map(function (s) {
            return {
                Id: s.Id,
                Name: s.Name,
                Title: s.Title,
                Description: s.Description,
                Order: s.Order,
                ColumnSize: s.ColumnSize,
                IsCollapsible: s.IsCollapsible,
                IsInitiallyCollapsed: s.IsInitiallyCollapsed,
                CssClass: s.CssClass,
                Properties: s.Properties.map(function (p) {
                    return {
                        Id: p.Id || GenerateGuid(),
                        PropertyName: p.PropertyName,
                        DisplayName: p.DisplayName,
                        Order: p.Order,
                        ColumnSize: p.ColumnSize,
                        ShowLabel: p.ShowLabel,
                        CssClass: p.CssClass
                    };
                })
            };
        });

        $.ajax({
            method: 'PUT',
            url: apiUrl,
            headers: { 'RequestVerificationToken': GetAntiForgeryToken() },
            contentType: 'application/json',
            data: JSON.stringify(sections)
        })
        .done(function () {
            SetDirty(false);
            ShowToast('Layout saved successfully', 'success');
        })
        .fail(function (err) {
            ShowToast('Failed to save: ' + (err.statusText || 'Unknown error'), 'danger');
        })
        .always(function () {
            $btnSave.html('<i class="bi bi-floppy me-1"></i> Save');
            UpdateSaveButton();
        });
    }

    function ResetLayout() {
        if (!confirm('Reset layout for "' + displayName + '" to default? This cannot be undone.')) return;

        $.ajax({
            method: 'POST',
            url: apiUrl + '/reset',
            headers: { 'RequestVerificationToken': GetAntiForgeryToken() }
        })
        .done(function () {
            ShowToast('Layout reset to default', 'info');
            LoadLayout();
        })
        .fail(function (err) {
            ShowToast('Failed to reset: ' + (err.statusText || 'Unknown error'), 'danger');
        });
    }

    function SetDirty(dirty) {
        isDirty = dirty;
        UpdateSaveButton();
    }

    function UpdateSaveButton() {
        $btnSave.prop('disabled', !isDirty);
        $btnSave2.toggleClass('text-warning', isDirty);
    }

    var EscapeHtml = TipsyBaboon.Common.EscapeHtml;

    var GetAntiForgeryToken = TipsyBaboon.Common.GetAntiForgeryToken;

    function GenerateGuid() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0;
            return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
        });
    }

    function ShowToast(message, type) {
        var $toastContainer = $('#toast-container');
        if (!$toastContainer.length) {
            $toastContainer = $('<div id="toast-container" class="position-fixed top-0 end-0 p-3" style="z-index: 1100;"></div>');
            $('body').append($toastContainer);
        }

        var toastId = 'toast-' + Date.now();
        var html = '<div id="' + toastId + '" class="toast align-items-center text-bg-' + type + ' border-0" role="alert">';
        html += '<div class="d-flex"><div class="toast-body">' + EscapeHtml(message) + '</div>';
        html += '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>';
        html += '</div></div>';

        $toastContainer.append(html);
        var toastEl = document.getElementById(toastId);
        var toast = new bootstrap.Toast(toastEl, { delay: 3000 });
        toast.show();
        $(toastEl).on('hidden.bs.toast', function () { $(this).remove(); });
    }

})(jQuery);
