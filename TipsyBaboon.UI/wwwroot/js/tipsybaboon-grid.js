/**
 * TipsyBaboon Editable Grid (tipsybaboon-grid.js)
 * 
 * This file handles EDITABLE grids with inline editing capabilities.
 * 
 * SPLIT RATIONALE:
 * The grid implementation was split into two separate files because editable and readonly
 * grids have significantly different functionality:
 * 
 * EDITABLE GRID (this file):
 * - Inline cell editing with various editor types (text, checkbox, number, select, json)
 * - Pending edit tracking and dirty row state
 * - AutoSave functionality
 * - No column reordering/visibility management (optimized for editing workflow)
 * 
 * READONLY GRID (tipsybaboon-grid-readonly.js):
 * - Column reordering via drag-and-drop
 * - Column visibility toggles
 * - Grid state persistence (sort, columns, filters saved to user preferences)
 * - Header context menu for column management
 * - No inline editing capabilities
 * 
 * Both grids share:
 * - Context menu with Open, Open in New Window, Delete actions
 * - Modal view support (CreateUseModal, EditUseModal, EnableModalView)
 * - Toolbar actions (new, delete, custom)
 * - Pagination and sorting
 * - Row selection and navigation
 * 
 * MODAL OPTIONS (from SchemaDefinition attributes):
 * - EnableModalView: Global default for modal behavior
 * - CreateUseModal: Override for create action (null = use EnableModalView)
 * - EditUseModal: Override for edit/detail action (null = use EnableModalView)
 */
(function($) {
    'use strict';

    window.TipsyBaboon = window.TipsyBaboon || {};
    window.TipsyBaboon.UI = window.TipsyBaboon.UI || {};
    window.TipsyBaboon.UI.GridInstances = window.TipsyBaboon.UI.GridInstances || {};

    var Formatters = {
        Date: function(v) { return v ? new Date(v).toLocaleDateString() : ''; },
        DateTime: function(v) { return v ? new Date(v).toLocaleString() : ''; },
        Boolean: function(v) {
            var icon = v ? 'bi-check-circle-fill text-success' : 'bi-x-circle';
            return '<i class="bi ' + icon + '"></i>';
        }
    };

    var EditorBuilders = {
        checkbox: function(name, fieldAttr, value) {
            return '<input type="checkbox" class="form-check-input" name="' + name + '" ' + fieldAttr +
                (value === true || value === 'true' || value === 1 ? ' checked' : '') + ' />';
        },
        number: function(name, fieldAttr, value) {
            return '<input type="number" class="form-control" name="' + name + '" ' + fieldAttr +
                ' value="' + (value != null ? value : '') + '" style="width: 80px;" />';
        },
        select: function(name, fieldAttr, value, col) {
            return '<select class="form-select" name="' + name + '" ' + fieldAttr + '>' +
                $.map(col.EnumOptions || {}, function(label, key) {
                    return '<option value="' + key + '"' +
                        (String(value) === key || String(value) === label ? ' selected' : '') + '>' + label + '</option>';
                }).join('') + '</select>';
        },
        json: function(name, fieldAttr, value) {
            var jsonStr = value != null ? EscapeHtml(typeof value === 'object' ? JSON.stringify(value) : String(value)) : '';
            return '<input type="hidden" name="' + name + '" ' + fieldAttr +
                ' data-is-json="true" value="' + jsonStr + '" />';
        },
        text: function(name, fieldAttr, value) {
            return '<input type="text" class="form-control" name="' + name + '" ' + fieldAttr +
                ' value="' + (value != null ? EscapeHtml(String(value)) : '') + '" />';
        }
    };

    var EscapeHtml = TipsyBaboon.Common.EscapeHtml;

    window.TipsyBaboon.UI.TipsyBaboonGrid = function(config) {
        var self = this;
        config = $.extend({
            GridId: '', BodyEndpoint: '', ApiEndpoint: '', ApiRoutePrefix: 'api', Columns: [],
            ToolbarActions: [], RowActions: [], PageSize: 20,
            EnablePaging: true, EnableModalView: true, EnableRowNavigation: true,
            CreateUseModal: null, EditUseModal: null, DetailUrlOverride: null,
            IsEditable: true, ModuleName: '', EntityTypeName: '', FormId: '',
            FieldName: '', ParentField: '', ParentIdPropertyName: '', ParentEntityId: '',
            PageRoutePrefix: '', InitialData: null, EditableColumnNames: [],
            AutoSave: false
        }, config);

        config.EditableColumnNames = config.EditableColumnNames || [];

        var state = {
            allData: [], data: [], dataMap: {}, selectedIds: [], focusedRowId: null,
            currentPage: 1, totalPages: 1, totalRecords: 0, filteredRecords: 0,
            sortColumn: null, sortDirection: 'asc',
            multiSort: [],
            filters: {}, columnFilters: {},
            pendingEdits: {}, isLoading: false
        };

        var filterDebounceTimer = null;

        var $container = $('#' + config.GridId);
        var $toolbar = $container.find('.grid-toolbar');
        var $content = $container.find('.grid-content');
        var $tbody = $content.find('tbody');
        var $pagination = $container.find('.grid-pagination');
        var contextMenu = null;

        window.TipsyBaboon.UI.GridInstances[config.GridId] = self;

        var GetAntiForgeryToken = TipsyBaboon.Common.GetAntiForgeryToken;

        function GetRowId(row) {
            return row._rowId || config.Columns
                .filter(function(c) { return c.IsKey; })
                .map(function(c) { return GetFieldValue(row, c.Field); })
                .join('~') || row.Id || row.id;
        }

        function GetFieldValue(row, field) {
            return field.indexOf('.') === -1
                ? row[field]
                : field.split('.').reduce(function(obj, key) { return obj && obj[key]; }, row);
        }

        function FormatCellValue(value, col, row) {
            return col.FormatterType && Formatters[col.FormatterType]
                ? Formatters[col.FormatterType](value, row)
                : (value != null ? EscapeHtml(String(value)) : '');
        }

        function BuildDetailUrl(rowId, isModal) {
            var url;
            if (config.DetailUrlOverride) {
                url = config.DetailUrlOverride.replace('{id}', rowId);
            } else {
                var prefix = config.PageRoutePrefix ? '/' + config.PageRoutePrefix : '';
                url = prefix + '/' + config.ModuleName + '/' + config.EntityTypeName + '/detail/' + rowId;
            }
            return isModal ? url + '?isModal=true' : url;
        }

        function ShouldUseModalForEdit() {
            return config.EditUseModal !== null && config.EditUseModal !== undefined ? config.EditUseModal : config.EnableModalView;
        }

        function ShouldUseModalForCreate() {
            return config.CreateUseModal !== null && config.CreateUseModal !== undefined ? config.CreateUseModal : config.EnableModalView;
        }

        function VisibleColumns() {
            return config.Columns.filter(function(c) { return !c.Hidden; });
        }

        function RenderHeaders() {
            var $thead = $content.find('thead');
            if (!$thead.length) return;

            var cols = VisibleColumns();
            var html = '<tr>';

            cols.forEach(function(col) {
                var sortInfo = state.multiSort.findIndex(function(s) { return s.field === col.Field; });
                var sortIcon = '';
                if (sortInfo !== -1) {
                    var s = state.multiSort[sortInfo];
                    sortIcon = state.multiSort.length > 1
                        ? ' <span class="sort-ordinal">' + (sortInfo + 1) + '</span>' + (s.direction === 'asc' ? '↑' : '↓')
                        : (s.direction === 'asc' ? ' ↑' : ' ↓');
                }

                var alignment = '';
                if (col.Alignment === 'center') alignment = ' text-center';
                else if (col.Alignment === 'right') alignment = ' text-end';

                var widthStyle = col.Width ? 'width:' + col.Width : '';

                html += '<th data-field="' + col.Field + '" class="sortable' + alignment + '" style="' + widthStyle + '">' +
                    '<span class="header-text">' + EscapeHtml(col.Header || col.DisplayName || col.Field) + '</span>' +
                    '<span class="sort-icon">' + sortIcon + '</span></th>';
            });

            html += '</tr>';
            $thead.html(html);
        }

        function RenderFilterRow() {
            var $filterRow = $content.find('.grid-filter-row');
            var cols = VisibleColumns();

            var html = '<tr class="grid-filter-row">';
            cols.forEach(function(col) {
                var filterValue = state.columnFilters[col.Field] || '';
                var input = BuildFilterInput(col, filterValue);
                html += '<td>' + input + '</td>';
            });
            html += '</tr>';

            if ($filterRow.length) {
                $filterRow.replaceWith(html);
            } else {
                $content.find('thead').after(html);
            }

            AttachFilterEvents();
        }

        function BuildFilterInput(col, value) {
            var fieldAttr = 'data-filter-field="' + col.Field + '"';

            if (col.FormatterType === 'Boolean') {
                return '<select class="form-select grid-filter-input" ' + fieldAttr + '>' +
                    '<option value="">All</option>' +
                    '<option value="true"' + (value === 'true' ? ' selected' : '') + '>Yes</option>' +
                    '<option value="false"' + (value === 'false' ? ' selected' : '') + '>No</option>' +
                    '</select>';
            }
            if (col.EnumOptions) {
                var opts = '<option value="">All</option>';
                Object.keys(col.EnumOptions).forEach(function(key) {
                    opts += '<option value="' + key + '"' + (value === key ? ' selected' : '') + '>' +
                        EscapeHtml(col.EnumOptions[key]) + '</option>';
                });
                return '<select class="form-select grid-filter-input" ' + fieldAttr + '>' + opts + '</select>';
            }
            return '<input type="text" class="form-control grid-filter-input" ' +
                fieldAttr + ' value="' + EscapeHtml(value) + '" placeholder="Filter...">';
        }

        function AttachFilterEvents() {
            $content.find('.grid-filter-input').off('input change').on('input change', function() {
                var $input = $(this);
                var field = $input.data('filter-field');
                var value = $input.val();

                clearTimeout(filterDebounceTimer);
                filterDebounceTimer = setTimeout(function() {
                    if (value) {
                        state.columnFilters[field] = value;
                    } else {
                        delete state.columnFilters[field];
                    }
                    state.currentPage = 1;
                    ApplyClientSideOperations();
                }, 300);
            });
        }

        function InitializeGridElements() {
            $toolbar.length && RenderToolbar();
            AttachToolbarEvents();
            AttachTableEvents();
        }

        function RenderToolbar() {
            var actionsHtml = config.ToolbarActions.map(function(action) {
                var btnClass = { 'new': 'btn-success', 'delete': 'btn-danger' }[action.Id] || 'btn-info';
                return '<button class="btn ' + btnClass + ' me-1" data-action-id="' + action.Id + '"' +
                    (action.RequiresSelection ? ' data-requires-selection disabled' : '') +
                    ' title="' + action.Label + '">' +
                    (action.Icon ? '<i class="' + action.Icon + '"></i>' : action.Label) + '</button>';
            }).join('');

            $toolbar.html('<div class="d-flex align-items-center justify-content-end w-100">' + actionsHtml + '</div>');
        }

        function RenderTableBody(isFullRender) {
            isFullRender && $tbody.empty();
            var fragment = document.createDocumentFragment();

            $.each(state.data, function(_, row) {
                var rowId = GetRowId(row);
                var $existingRow = isFullRender ? $() : $tbody.find('tr[data-row-id="' + rowId + '"]');

                $existingRow.length
                    ? UpdateRow($existingRow, row)
                    : (function() {
                        var $tr = $(BuildRowHtml(row, rowId));
                        AttachRowEvents($tr);
                        fragment.appendChild($tr[0]);
                    })();
            });

            isFullRender && $tbody.append(fragment);
            RemoveStaleRows();
        }

        function BuildRowHtml(row, rowId) {
            var isSelected = state.selectedIds.indexOf(rowId) !== -1;
            var hasPendingEdits = state.pendingEdits[rowId] !== undefined;

            var keyAttrs = config.Columns
                .filter(function(c) { return c.IsKey; })
                .map(function(c) { return ' data-key-' + c.Field.toLowerCase() + '="' + (GetFieldValue(row, c.Field) || '') + '"'; })
                .join('');

            var cells = VisibleColumns().map(function(col) {
                var value = state.pendingEdits[rowId] && state.pendingEdits[rowId][col.Field] !== undefined
                    ? state.pendingEdits[rowId][col.Field]
                    : GetFieldValue(row, col.Field);

                var tdClass = col.FormatterType === 'Boolean' ? ' class="text-center"' : '';
                var content = col.IsEditable
                    ? BuildEditableCell(col, value, rowId, row)
                    : FormatCellValue(value, col, row);

                return '<td' + tdClass + ' data-field="' + col.Field + '">' + content + '</td>';
            }).join('');

            return '<tr data-row-id="' + rowId + '"' + keyAttrs +
                ' class="grid-row' + (isSelected ? ' table-active' : '') + (hasPendingEdits ? ' grid-row-dirty' : '') +
                '" style="cursor: pointer;">' + cells + '</tr>';
        }

        function BuildEditableCell(col, value, rowId, row) {
            var fieldName = config.FieldName
                ? config.FieldName + '[' + rowId + '].' + col.Field
                : col.Field;

            var recordKeys = BuildRecordKeysJson(row);
            var wrapperAttrs = 'data-module="' + (config.ModuleName || '') + '" ' +
                'data-model="' + (config.EntityTypeName || '') + '" ' +
                'data-record-keys=\'' + EscapeHtml(recordKeys) + '\'';
            config.ParentField && (wrapperAttrs += ' data-parent-field="' + config.ParentField + '"');

            var builder = EditorBuilders[col.EditorType] || EditorBuilders.text;
            var fieldAttr = 'data-field="' + col.Field + '"';

            return '<div ' + wrapperAttrs + '>' + builder(fieldName, fieldAttr, value, col) + '</div>';
        }

        function BuildRecordKeysJson(row) {
            var keys = {};
            $.each(config.Columns, function(_, col) {
                col.IsKey && (keys[col.Field] = GetFieldValue(row, col.Field));
            });
            return JSON.stringify(keys);
        }

        function UpdateRow($row, row) {
            var rowId = GetRowId(row);
            $row.toggleClass('table-active', state.selectedIds.indexOf(rowId) !== -1)
                .toggleClass('grid-row-dirty', state.pendingEdits[rowId] !== undefined);

            $.each(config.Columns, function(_, col) {
                var value = state.pendingEdits[rowId] && state.pendingEdits[rowId][col.Field] !== undefined
                    ? state.pendingEdits[rowId][col.Field]
                    : GetFieldValue(row, col.Field);

                var $td = $row.find('td[data-field="' + col.Field + '"]');
                var content = col.IsEditable
                    ? BuildEditableCell(col, value, rowId, row)
                    : FormatCellValue(value, col, row);

                $td.html(content);
            });
        }

        function RemoveStaleRows() {
            $tbody.find('tr').each(function() {
                var $row = $(this);
                state.dataMap[$row.data('row-id')] || $row.remove();
            });
        }

        function UpdateRowSelection(rowId, isSelected) {
            $tbody.find('tr[data-row-id="' + rowId + '"]').toggleClass('table-active', isSelected);
        }

        function UpdateAllRowSelections() {
            $tbody.find('tr').each(function() {
                var $row = $(this);
                $row.toggleClass('table-active', state.selectedIds.indexOf($row.data('row-id')) !== -1);
            });
        }

        function UpdateSortIndicators() {
            $content.find('th.sortable').each(function() {
                var $th = $(this);
                var field = $th.data('field');
                var sortIdx = state.multiSort.findIndex(function(s) { return s.field === field; });

                var sortIcon = '';
                if (sortIdx !== -1) {
                    var s = state.multiSort[sortIdx];
                    sortIcon = state.multiSort.length > 1
                        ? ' <span class="sort-ordinal">' + (sortIdx + 1) + '</span>' + (s.direction === 'asc' ? '↑' : '↓')
                        : (s.direction === 'asc' ? ' ↑' : ' ↓');
                }

                $th.find('.sort-icon').html(sortIcon);
            });
        }

        function UpdateToolbarState() {
            $toolbar.find('[data-requires-selection]').prop('disabled', !state.focusedRowId);
        }

        function RenderPagination() {
            if (!config.EnablePaging) { $pagination.empty(); return; }

            var maxButtons = 5;
            var half = Math.floor(maxButtons / 2);
            var start = Math.max(1, state.currentPage - half);
            var end = Math.min(state.totalPages, state.currentPage + half);
            
            // Adjust to always show up to 5 buttons
            if (end - start + 1 < maxButtons) {
                if (start === 1) {
                    end = Math.min(state.totalPages, start + maxButtons - 1);
                } else if (end === state.totalPages) {
                    start = Math.max(1, end - maxButtons + 1);
                }
            }
            
            var pages = [];
            for (var i = start; i <= end; i++) pages.push(i);

            var pageSizeOptions = [10, 25, 50, 100];
            var pageSizeHtml = '<select class="form-select form-select-sm d-inline-block" style="width: auto;" data-page-size-selector>' +
                pageSizeOptions.map(function(size) {
                    return '<option value="' + size + '"' + (config.PageSize === size ? ' selected' : '') + '>' + size + '</option>';
                }).join('') +
                '</select>';

            var html = '<div class="d-flex align-items-center">' +
                '<div class="me-2">' + pageSizeHtml + '</div>' +
                '<nav><ul class="pagination pagination-sm mb-0">' +
                (state.currentPage > 1 && start > 1 ? '<li class="page-item"><a class="page-link" data-page="1"><i class="bi bi-chevron-bar-left"></i></a></li>' : '') +
                '<li class="page-item' + (state.currentPage === 1 ? ' disabled' : '') + '">' +
                '<a class="page-link" data-page="' + (state.currentPage - 1) + '"><i class="bi bi-chevron-left"></i></a></li>' +
                pages.map(function(p) {
                    return '<li class="page-item' + (p === state.currentPage ? ' active' : '') + '">' +
                        '<a class="page-link" data-page="' + p + '">' + p + '</a></li>';
                }).join('') +
                '<li class="page-item' + (state.currentPage === state.totalPages ? ' disabled' : '') + '">' +
                '<a class="page-link" data-page="' + (state.currentPage + 1) + '"><i class="bi bi-chevron-right"></i></a></li>' +
                (state.currentPage < state.totalPages && end < state.totalPages ? '<li class="page-item"><a class="page-link" data-page="' + state.totalPages + '"><i class="bi bi-chevron-bar-right"></i></a></li>' : '') +
                '</ul></nav>' +
                '<div class="ms-auto">Total: ' + state.filteredRecords + '</div>' +
                '</div>';

            $pagination.html(html);
            AttachPaginationEvents();
        }

        function ShowLoading() {
            var cols = VisibleColumns().length;
            $tbody.html('<tr><td colspan="' + cols + '" class="text-center py-4">' +
                '<div class="spinner-border text-primary"></div>' +
                '<span class="ms-2">Loading...</span></td></tr>');
        }

        function ShowEmpty() {
            $tbody.html('<tr class="grid-empty-row"><td colspan="' + VisibleColumns().length +
                '" class="text-center py-4"><i class="bi bi-inbox-fill fs-1 mb-2 d-block"></i>No data available</td></tr>');
        }

        function ShowError(message) {
            $tbody.html('<tr class="grid-error-row"><td colspan="' + VisibleColumns().length +
                '" class="text-center text-danger py-4"><i class="bi bi-exclamation-triangle-fill fs-1 mb-2 d-block"></i>' +
                (message || 'Failed to load data') + '</td></tr>');
        }

        function ShowAuthError() {
            $tbody.html('<tr><td colspan="' + VisibleColumns().length +
                '" class="text-center text-warning py-4">You are not authorized to view this data.</td></tr>');
        }

        function AttachToolbarEvents() {
            $toolbar.off('click', '[data-action-id]').on('click', '[data-action-id]', function() {
                ExecuteAction($(this).data('action-id'));
            });
        }

        function AttachTableEvents() {
            $content.on('click', 'th.sortable', function(e) {
                ToggleSort($(this).data('field'), e.shiftKey);
            });

            $container.on('click', 'tbody tr input, tbody tr select, tbody tr textarea, tbody tr button', function(e) {
                e.stopPropagation();
            });

            $container.on('field:saved', function(_, data) {
                $container.find('[data-field="' + data.field + '"][data-record-keys]')
                    .closest('tr').removeClass('grid-row-dirty record-dirty');
            });
        }

        function AttachRowEvents($row) {
            $row.on('click', function(e) {
                $(e.target).is('input, select, textarea, button, a') || SelectRow($(this).data('row-id'));
            });

            config.EnableRowNavigation && $row.on('dblclick', function(e) {
                if ($(e.target).is('input, select, textarea, button, a')) return;
                var rowId = $(this).data('row-id');
                ShouldUseModalForEdit()
                    ? TipsyBaboon.UI.OpenModal(BuildDetailUrl(rowId, true), function() { LoadData(); })
                    : (window.location.href = BuildDetailUrl(rowId, false));
            });

            config.EnableRowNavigation && $row.on('contextmenu', function(e) {
                e.preventDefault();
                ShowContextMenu($(this).data('row-id'), e);
            });
        }

        function AttachPaginationEvents() {
            $pagination.on('click', '.page-link', function(e) {
                e.preventDefault();
                var page = parseInt($(this).data('page'), 10);
                page > 0 && page <= state.totalPages && GoToPage(page);
            });
            $pagination.on('change', '[data-page-size-selector]', function() {
                config.PageSize = parseInt($(this).val(), 10);
                state.currentPage = 1;
                ApplyClientSideOperations();
            });
        }

        function LoadData() {
            if (!config.BodyEndpoint) return;

            state.isLoading = true;
            ShowLoading();

            var params = {
                formId: config.FormId, gridId: config.GridId,
                isEditable: true, fieldName: config.FieldName || '',
                pageSize: 999999
            };

            config.EditableColumnNames.length && (params.editableColumns = config.EditableColumnNames.join(','));
            config.ParentIdPropertyName && config.ParentEntityId && (params[config.ParentIdPropertyName] = config.ParentEntityId);
            $.extend(params, state.filters);

            LoadBodyEndpoint(params);
        }

        function LoadBodyEndpoint(params) {
            var jqxhr = $.ajax({ url: config.BodyEndpoint, data: params, dataType: 'html' });

            jqxhr.done(function(html) {
                state.isLoading = false;
                var $newTbody = $(html);
                $tbody.replaceWith($newTbody);
                $tbody = $content.find('tbody');

                state.allData = [];
                state.dataMap = {};
                $tbody.find('tr[data-row-id]').each(function() {
                    var $row = $(this);
                    var rowData = { $row: $row };

                    $.each(config.Columns, function(_, col) {
                        var $td = $row.find('td[data-field="' + col.Field + '"]');
                        if ($td.length) {
                            rowData[col.Field] = ExtractCellValue($td, col);
                        }
                    });

                    state.allData.push(rowData);
                    state.dataMap[$row.data('row-id')] = rowData;
                    AttachRowEvents($row);
                });

                state.totalRecords = state.allData.length;
                var enableRowNav = jqxhr.getResponseHeader('X-Enable-Row-Navigation');
                enableRowNav !== null && (config.EnableRowNavigation = enableRowNav === 'true');

                RenderFilterRow();
                ApplyClientSideOperations();
            });

            jqxhr.fail(function(xhr) {
                state.isLoading = false;
                state.allData = [];
                state.data = [];
                state.dataMap = {};
                (xhr.status === 401 || xhr.status === 403) ? ShowAuthError() : ShowError(xhr.statusText || 'Failed to load data');
            });
        }

        function ExtractCellValue($td, col) {
            var $input = $td.find('input, select, textarea');
            if ($input.length) {
                if ($input.is(':checkbox')) {
                    return $input.is(':checked');
                }
                return $input.val();
            }
            return $td.text().trim();
        }

        function ApplyClientSideOperations() {
            var filtered = FilterData(state.allData);
            var sorted = SortData(filtered);
            state.data = sorted;
            state.filteredRecords = sorted.length;
            state.totalPages = Math.max(1, Math.ceil(state.filteredRecords / config.PageSize));

            if (state.currentPage > state.totalPages) {
                state.currentPage = state.totalPages;
            }

            ApplyVisibility();
            UpdateSortIndicators();
            RenderPagination();
            UpdateToolbarState();
        }

        function FilterData(data) {
            var filters = state.columnFilters;
            if (!Object.keys(filters).length) return data.slice();

            return data.filter(function(row) {
                return Object.keys(filters).every(function(field) {
                    var filterVal = String(filters[field]).toLowerCase();
                    var rowVal = row[field];

                    if (rowVal === null || rowVal === undefined) {
                        return filterVal === '';
                    }

                    var col = config.Columns.find(function(c) { return c.Field === field; });
                    if (col && col.FormatterType === 'Boolean') {
                        var boolStr = rowVal === true || rowVal === 'true' || rowVal === 1 ? 'true' : 'false';
                        return filterVal === '' || boolStr === filterVal;
                    }

                    if (col && col.EnumOptions) {
                        return filterVal === '' || String(rowVal) === filterVal;
                    }

                    return String(rowVal).toLowerCase().indexOf(filterVal) !== -1;
                });
            });
        }

        function SortData(data) {
            if (!state.multiSort.length) return data;

            return data.slice().sort(function(a, b) {
                for (var i = 0; i < state.multiSort.length; i++) {
                    var s = state.multiSort[i];
                    var aVal = a[s.field];
                    var bVal = b[s.field];

                    if (aVal === bVal) continue;
                    if (aVal === null || aVal === undefined) return s.direction === 'asc' ? 1 : -1;
                    if (bVal === null || bVal === undefined) return s.direction === 'asc' ? -1 : 1;

                    var aNum = parseFloat(aVal);
                    var bNum = parseFloat(bVal);
                    var cmp;
                    if (!isNaN(aNum) && !isNaN(bNum)) {
                        cmp = aNum - bNum;
                    } else {
                        cmp = String(aVal).localeCompare(String(bVal));
                    }

                    if (cmp !== 0) return s.direction === 'asc' ? cmp : -cmp;
                }
                return 0;
            });
        }

        function ApplyVisibility() {
            var startIdx = (state.currentPage - 1) * config.PageSize;
            var endIdx = startIdx + config.PageSize;

            var visibleSet = new Set(state.data);

            state.allData.forEach(function(row) {
                if (!visibleSet.has(row)) {
                    row.$row.hide();
                }
            });

            state.data.forEach(function(row, idx) {
                $tbody.append(row.$row);
                if (idx >= startIdx && idx < endIdx) {
                    row.$row.show();
                } else {
                    row.$row.hide();
                }
            });

            if (state.filteredRecords === 0) {
                var existingEmpty = $tbody.find('.grid-empty-row');
                if (!existingEmpty.length) {
                    ShowEmpty();
                }
            } else {
                $tbody.find('.grid-empty-row').remove();
            }
        }

        function SelectRow(rowId) {
            var previousId = state.focusedRowId;
            state.selectedIds = [rowId];
            state.focusedRowId = rowId;
            previousId && previousId !== rowId && UpdateRowSelection(previousId, false);
            UpdateRowSelection(rowId, true);
            UpdateToolbarState();
        }

        function ToggleAllRows(checked) {
            state.selectedIds = checked ? $.map(state.data, function(row) { return GetRowId(row); }) : [];
            state.focusedRowId = state.selectedIds[0] || null;
            UpdateAllRowSelections();
            UpdateToolbarState();
        }

        function NavigateToNew() {
            var prefix = config.PageRoutePrefix ? '/' + config.PageRoutePrefix : '';
            var url = prefix + '/' + config.ModuleName + '/' + config.EntityTypeName + '/create';
            var params = [];
            config.ParentIdPropertyName && config.ParentEntityId &&
                params.push(config.ParentIdPropertyName + '=' + encodeURIComponent(config.ParentEntityId));

            ShouldUseModalForCreate()
                ? (params.push('isModal=true'), TipsyBaboon.UI.OpenModal(url + '?' + params.join('&')))
                : (window.location.href = url + (params.length ? '?' + params.join('&') : ''));
        }

        function DeleteRow(rowId) {
            TipsyBaboon.Common.Confirm('Are you sure you want to delete this item? This action cannot be undone.', 'Confirm Delete')
                .then(function(confirmed) { confirmed && ExecuteDelete(rowId); });
        }

        function ExecuteDelete(rowId) {
            var deleteUrl = config.ApiEndpoint + '/' + encodeURIComponent(rowId);
            $.ajax({ url: deleteUrl, method: 'DELETE', dataType: 'json', headers: { 'RequestVerificationToken': GetAntiForgeryToken() } })
                .done(function(result) {
                    if (!result.Success) {
                        TipsyBaboon.Common.Alert(result.Message || 'Unknown error', 'Delete Error');
                        return;
                    }

                    $tbody.find('tr[data-row-id="' + rowId + '"]').fadeOut(200, function() {
                        $(this).remove();
                        delete state.dataMap[rowId];
                        state.allData = $.grep(state.allData, function(row) { return GetRowId(row) !== rowId; });
                        state.data = $.grep(state.data, function(row) { return GetRowId(row) !== rowId; });
                        state.focusedRowId === rowId && (state.focusedRowId = null);
                        state.selectedIds = $.grep(state.selectedIds, function(id) { return id !== rowId; });
                        state.totalRecords = state.allData.length;
                        state.filteredRecords = state.data.length;
                        state.totalPages = Math.max(1, Math.ceil(state.filteredRecords / config.PageSize));

                        if (state.currentPage > state.totalPages) {
                            state.currentPage = state.totalPages;
                        }

                        state.filteredRecords === 0 ? ShowEmpty() : ApplyVisibility();
                        RenderPagination();
                        UpdateToolbarState();
                    });
                })
                .fail(function(xhr) {
                    var msg = xhr.responseJSON?.Message || xhr.statusText || 'Unknown error';
                    TipsyBaboon.Common.Alert('Failed to delete item: ' + msg, 'Delete Failed');
                });
        }

        function ExecuteAction(actionId) {
            var ActionHandlers = {
                'new': function() { NavigateToNew(); },
                'delete': function() { state.focusedRowId && DeleteRow(state.focusedRowId); }
            };

            var handler = ActionHandlers[actionId];
            if (handler) { handler(); return; }

            var action = config.ToolbarActions.filter(function(a) { return a.Id === actionId; })[0];
            if (!action || !action.Handler) return;

            var fn = typeof action.Handler === 'function' ? action.Handler : window[action.Handler];
            typeof fn === 'function' && fn(state.selectedIds, self);
        }

        function ShowContextMenu(rowId, event) {
            contextMenu = contextMenu || new TipsyBaboon.UI.ContextMenu(
                config.GridId + '_contextMenu',
                { buttons: config.RowActions },
                {
                    EnableModalView: config.EnableModalView,
                    EditUseModal: config.EditUseModal,
                    ModuleName: config.ModuleName,
                    EntityTypeName: config.EntityTypeName,
                    GridId: config.GridId,
                    PageRoutePrefix: config.PageRoutePrefix
                }
            );
            contextMenu.Show(rowId, event, state.dataMap[rowId]);
        }

        function ToggleSort(field, addToSort) {
            var existingIdx = state.multiSort.findIndex(function(s) { return s.field === field; });

            if (addToSort) {
                if (existingIdx !== -1) {
                    state.multiSort[existingIdx].direction =
                        state.multiSort[existingIdx].direction === 'asc' ? 'desc' : 'asc';
                } else {
                    state.multiSort.push({ field: field, direction: 'asc' });
                }
            } else {
                if (existingIdx !== -1 && state.multiSort.length === 1) {
                    state.multiSort[0].direction =
                        state.multiSort[0].direction === 'asc' ? 'desc' : 'asc';
                } else {
                    state.multiSort = [{ field: field, direction: 'asc' }];
                }
            }

            state.sortColumn = state.multiSort[0]?.field || null;
            state.sortDirection = state.multiSort[0]?.direction || 'asc';
            ApplyClientSideOperations();
        }

        function GoToPage(page) {
            state.currentPage = page;
            ApplyClientSideOperations();
        }

        self.Refresh = function() { LoadData(); };
        self.GetSelectedIds = function() { return state.selectedIds.slice(); };
        self.GetSelectedRows = function() {
            return $.grep(state.data, function(row) { return state.selectedIds.indexOf(GetRowId(row)) !== -1; });
        };
        self.SetFilter = function(key, value) { state.filters[key] = value; state.currentPage = 1; LoadData(); };
        self.ClearFilters = function() { state.filters = {}; state.currentPage = 1; LoadData(); };
        self.GetState = function() {
            return {
                data: state.data.slice(), selectedIds: state.selectedIds.slice(),
                currentPage: state.currentPage, totalPages: state.totalPages, totalRecords: state.totalRecords
            };
        };

        self.UpdateRowData = function(rowId, newData) {
            state.dataMap[rowId] && (
                $.extend(state.dataMap[rowId], newData),
                $tbody.find('tr[data-row-id="' + rowId + '"]').each(function() { UpdateRow($(this), state.dataMap[rowId]); })
            );
        };

        self.GetPendingEdits = function() {
            return $.map(state.pendingEdits, function(fields, rowId) {
                var edit = $.extend({ Id: rowId }, fields);
                config.ParentIdPropertyName && config.ParentEntityId && (edit[config.ParentIdPropertyName] = config.ParentEntityId);
                return edit;
            });
        };

        self.HasPendingEdits = function() {
            return TipsyBaboon.FormSave.hasPendingEdits();
        };

        self.ClearPendingEdits = function() {
            TipsyBaboon.FormSave.clearPendingEdits();
            $tbody.find('.grid-row-dirty, .record-dirty').removeClass('grid-row-dirty record-dirty');
            $container.trigger('grid:dirty', { gridId: config.GridId, isDirty: false });
        };

        self.SaveAllPendingEdits = function() {
            return TipsyBaboon.FormSave.savePendingEditsForContainer($container);
        };

        self.GetConfig = function() { return config; };

        self.DeleteRow = function(rowId) { DeleteRow(rowId); };

        function InitializeWithInlineData() {
            if (!config.InitialData || !config.InitialData.length) { ShowEmpty(); return; }

            state.allData = config.InitialData;
            state.dataMap = {};
            $.each(state.allData, function(_, row) { state.dataMap[GetRowId(row)] = row; });
            state.totalRecords = state.allData.length;
            state.isLoading = false;

            RenderTableBody(true);
            RenderFilterRow();
            ApplyClientSideOperations();
        }

        InitializeGridElements();
        config.InitialData ? InitializeWithInlineData() : LoadData();
    };

    window.TipsyBaboon.UI.ContextMenu = function(id, options, gridOptions) {
        var self = this;
        var buttons = (options && options.buttons) || [];
        gridOptions = gridOptions || {};
        var $menu = null;
        var currentRowId = null;
        var currentRowData = null;
        var documentClickHandler = null;

        function CreateMenuElement() {
            if ($menu) {
                $menu.remove();
                $menu = null;
            }

            var prefix = gridOptions.PageRoutePrefix ? '/' + gridOptions.PageRoutePrefix : '';
            var shouldUseModal = gridOptions.EditUseModal !== null && gridOptions.EditUseModal !== undefined ? gridOptions.EditUseModal : gridOptions.EnableModalView;

            var builtinItems =
                '<li><a class="dropdown-item" data-button-id="open"><i class="bi bi-folder2-open me-2"></i>Open</a></li>' +
                (shouldUseModal !== false
                    ? '<li><a class="dropdown-item" data-button-id="open-new-window"><i class="bi bi-box-arrow-up-right me-2"></i>Open in New Window</a></li>'
                    : '');

            var customButtons = buttons.filter(function(btn) { return btn.Id !== 'delete'; });
            
            // Always include delete as a built-in action
            var deleteItem = '<li><hr class="dropdown-divider"></li>' +
                '<li><a class="dropdown-item text-danger" data-button-id="delete"><i class="bi bi-trash me-2"></i>Delete</a></li>';

            var customItems = customButtons.length
                ? '<li><hr class="dropdown-divider"></li>' +
                    customButtons.map(function(btn) {
                        var iconClass = { 'new': 'text-success' }[btn.Id] || 'text-info';
                        return '<li><a class="dropdown-item" data-button-id="' + btn.Id + '">' +
                            (btn.Icon ? '<i class="' + btn.Icon + ' me-2 ' + iconClass + '"></i>' : '') +
                            btn.Label + '</a></li>';
                    }).join('')
                : '';

            $menu = $('<div>', { id: id, 'class': 'context-menu', css: { position: 'fixed', display: 'none', zIndex: 10000 } })
                .html('<ul class="dropdown-menu show">' + builtinItems + customItems + deleteItem + '</ul>')
                .appendTo('body');

            var BuiltinHandlers = {
                'open': function() {
                    var baseUrl = prefix + '/' + gridOptions.ModuleName + '/' + gridOptions.EntityTypeName + '/detail/' + currentRowId;
                    shouldUseModal
                        ? TipsyBaboon.UI.OpenModal(baseUrl + '?isModal=true', function() {
                            var grid = TipsyBaboon.UI.GridInstances[gridOptions.GridId];
                            grid && grid.Refresh();
                        })
                        : (window.location.href = baseUrl);
                },
                'open-new-window': function() {
                    window.open(prefix + '/' + gridOptions.ModuleName + '/' + gridOptions.EntityTypeName + '/detail/' + currentRowId, '_blank');
                },
                'delete': function() {
                    var grid = TipsyBaboon.UI.GridInstances[gridOptions.GridId];
                    grid && grid.DeleteRow(currentRowId);
                }
            };

            $menu.on('click', '[data-button-id]', function(e) {
                e.preventDefault();
                e.stopPropagation();
                var buttonId = $(this).data('button-id');

                var builtin = BuiltinHandlers[buttonId];
                if (builtin) { 
                    builtin(); 
                    self.Hide(); 
                    return; 
                }

                var btn = buttons.filter(function(b) { return b.Id === buttonId; })[0];
                if (btn) {
                    var fn = typeof btn.Action === 'function' ? btn.Action
                        : (btn.Handler && typeof window[btn.Handler] === 'function' ? window[btn.Handler] : null);
                    fn && fn(currentRowId, currentRowData);
                }
                self.Hide();
            });

            // Remove old document handler if it exists
            if (documentClickHandler) {
                $(document).off('click', documentClickHandler);
            }

            documentClickHandler = function(e) {
                if ($menu && $menu.is(':visible') && !$.contains($menu[0], e.target)) {
                    self.Hide();
                }
            };
            $(document).on('click', documentClickHandler);
        }

        this.Show = function(rowId, event, rowData) {
            CreateMenuElement();
            currentRowId = rowId;
            currentRowData = rowData;

            $menu.css({ left: event.clientX, top: event.clientY }).show();

            var rect = $menu[0].getBoundingClientRect();
            rect.right > window.innerWidth && $menu.css('left', event.clientX - rect.width);
            rect.bottom > window.innerHeight && $menu.css('top', event.clientY - rect.height);
        };

        this.Hide = function() {
            if ($menu) {
                $menu.remove();
                $menu = null;
            }
            if (documentClickHandler) {
                $(document).off('click', documentClickHandler);
                documentClickHandler = null;
            }
            currentRowId = null;
            currentRowData = null;
        };

        this.Dispose = function() {
            self.Hide();
        };
    };

})(jQuery);
