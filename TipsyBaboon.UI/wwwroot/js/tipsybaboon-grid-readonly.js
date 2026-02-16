/**
 * TipsyBaboon Readonly Grid (tipsybaboon-grid-readonly.js)
 * 
 * This file handles READONLY grids with column management and state persistence.
 * 
 * SPLIT RATIONALE:
 * The grid implementation was split into two separate files because editable and readonly
 * grids have significantly different functionality:
 * 
 * READONLY GRID (this file):
 * - Column reordering via drag-and-drop
 * - Column visibility toggles
 * - Grid state persistence (sort, columns, filters saved to user preferences)
 * - Header context menu for column management
 * - No inline editing capabilities
 * 
 * EDITABLE GRID (tipsybaboon-grid.js):
 * - Inline cell editing with various editor types (text, checkbox, number, select, json)
 * - Pending edit tracking and dirty row state
 * - AutoSave functionality
 * - No column reordering/visibility management (optimized for editing workflow)
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

    var EscapeHtml = TipsyBaboon.Common.EscapeHtml;

    window.TipsyBaboon.UI.TipsyBaboonGridReadonly = function(config) {
        var self = this;
        config = $.extend({
            GridId: '', ApiEndpoint: '', Columns: [],
            AllColumns: null, ToolbarActions: [], RowActions: [], PageSize: 20,
            EnablePaging: true, EnableModalView: true, EnableRowNavigation: true,
            CreateUseModal: null, EditUseModal: null,
            ModuleName: '', EntityTypeName: '', FormId: '',
            ParentIdPropertyName: '', ParentEntityId: '',
            PageRoutePrefix: '', ApiRoutePrefix: '', InitialData: null,
            AutoSaveGridLayouts: true
        }, config);

        var state = {
            data: [], dataMap: {}, selectedIds: [], focusedRowId: null,
            currentPage: 1, totalPages: 1, totalRecords: 0,
            sortColumn: null, sortDirection: 'asc',
            multiSort: [],
            columnOrder: [],
            hiddenColumns: [],
            filters: {}, columnFilters: {},
            isLoading: false,
            defaultState: null, savedState: null,
            stateLoaded: false, stateDirty: false
        };

        var $container = $('#' + config.GridId);
        var $toolbar = $container.find('.grid-toolbar');
        var $content = $container.find('.grid-content');
        var $tbody = $content.find('tbody');
        var $pagination = $container.find('.grid-pagination');
        var contextMenu = null;
        var headerContextMenu = null;
        var filterDebounceTimer = null;

        window.TipsyBaboon.UI.GridInstances[config.GridId] = self;

        function InitializeDefaultState() {
            var allCols = config.AllColumns || config.Columns;
            var defaultVisibleFields = new Set((config.Columns || []).map(function(c) { return c.Field; }));

            state.defaultState = {
                columns: allCols.map(function(c, i) {
                    var isVisible = !c.Hidden && c.ShowByDefault !== false && defaultVisibleFields.has(c.Field);
                    return { Field: c.Field, Order: i, Visible: isVisible };
                }),
                sort: []
            };
            state.columnOrder = allCols.map(function(c) { return c.Field; });
            state.hiddenColumns = allCols
                .filter(function(c) { return c.Hidden || c.ShowByDefault === false || !defaultVisibleFields.has(c.Field); })
                .map(function(c) { return c.Field; });
        }

        function GetStateKey() {
            return 'GridState:' + config.GridId;
        }

        var GetAntiForgeryToken = TipsyBaboon.Common.GetAntiForgeryToken;

        function LoadGridState() {
            var apiPrefix = config.ApiRoutePrefix || 'api';
            return $.ajax({
                url: '/' + apiPrefix + '/preferences/' + encodeURIComponent(GetStateKey()),
                method: 'GET',
                dataType: 'json'
            }).then(function(response) {
                if (response && response.value) {
                    try {
                        var savedState = JSON.parse(response.value);
                        state.savedState = savedState;
                        ApplyGridState(savedState);
                    } catch(e) {}
                }
                state.stateLoaded = true;
            }).catch(function() {
                state.stateLoaded = true;
            });
        }

        function SaveGridState() {
            var apiPrefix = config.ApiRoutePrefix || 'api';
            var stateDto = BuildStateDto();
            return $.ajax({
                url: '/' + apiPrefix + '/preferences',
                method: 'PUT',
                contentType: 'application/json',
                data: JSON.stringify({
                    Key: GetStateKey(),
                    Value: JSON.stringify(stateDto)
                }),
                headers: { 'RequestVerificationToken': GetAntiForgeryToken() }
            }).then(function() {
                state.savedState = stateDto;
                state.stateDirty = false;
                UpdateControlButtons();
            });
        }

        function ResetGridState() {
            ApplyGridState(state.defaultState);
            state.stateDirty = false;

            if (config.AutoSaveGridLayouts) {
                $.ajax({
                    url: '/api/preferences',
                    method: 'PUT',
                    contentType: 'application/json',
                    data: JSON.stringify({ Key: GetStateKey(), Value: null }),
                    headers: { 'RequestVerificationToken': GetAntiForgeryToken() }
                });
            }

            RenderHeaders();
            RenderFilterRow();
            LoadData();
            UpdateControlButtons();
        }

        function BuildStateDto() {
            return {
                Columns: state.columnOrder.map(function(field, i) {
                    return {
                        Field: field,
                        Order: i,
                        Visible: state.hiddenColumns.indexOf(field) === -1
                    };
                }),
                Sort: state.multiSort.map(function(s, i) {
                    return { Field: s.field, Direction: s.direction, Ordinal: i };
                })
            };
        }

        function ApplyGridState(stateDto) {
            if (!stateDto) return;

            if (stateDto.Columns && stateDto.Columns.length) {
                state.columnOrder = stateDto.Columns.sort(function(a, b) { return a.Order - b.Order; }).map(function(c) { return c.Field; });
                state.hiddenColumns = stateDto.Columns.filter(function(c) { return !c.Visible; }).map(function(c) { return c.Field; });
            }

            if (stateDto.Sort && stateDto.Sort.length) {
                state.multiSort = stateDto.Sort.sort(function(a, b) { return a.Ordinal - b.Ordinal; }).map(function(s) {
                    return { field: s.Field, direction: s.Direction };
                });
                state.sortColumn = state.multiSort[0]?.field || null;
                state.sortDirection = state.multiSort[0]?.direction || 'asc';
            }
        }

        function MarkStateDirty() {
            state.stateDirty = true;
            UpdateControlButtons();
            config.AutoSaveGridLayouts && SaveGridState();
        }

        function GetRowId(row) {
            return config.Columns
                .filter(function(c) { return c.IsKey; })
                .map(function(c) { return GetFieldValue(row, c.Field); })
                .join('_') || row.Id || row.id;
        }

        function GetFieldValue(row, field) {
            return field.indexOf('.') === -1
                ? row[field]
                : field.split('.').reduce(function(obj, key) { return obj && obj[key]; }, row);
        }

        function FormatCellValue(value, col, row) {
            if (col.FormatterType && Formatters[col.FormatterType]) return Formatters[col.FormatterType](value, row);
            if (col.EnumOptions && col.EnumOptions[value]) return EscapeHtml(col.EnumOptions[value]);
            return value != null ? EscapeHtml(String(value)) : '';
        }

        function BuildDetailUrl(rowId, isModal) {
            var prefix = config.PageRoutePrefix ? '/' + config.PageRoutePrefix : '';
            var url = prefix + '/' + config.ModuleName + '/' + config.EntityTypeName + '/detail/' + rowId;
            return isModal ? url + '?isModal=true' : url;
        }

        function ShouldUseModalForEdit() {
            return config.EditUseModal !== null && config.EditUseModal !== undefined ? config.EditUseModal : config.EnableModalView;
        }

        function ShouldUseModalForCreate() {
            return config.CreateUseModal !== null && config.CreateUseModal !== undefined ? config.CreateUseModal : config.EnableModalView;
        }

        function VisibleColumns() {
            if (!state.columnOrder.length) {
                return (config.AllColumns || config.Columns).filter(function(c) { return !c.Hidden; });
            }
            return state.columnOrder
                .filter(function(field) { return state.hiddenColumns.indexOf(field) === -1; })
                .map(function(field) {
                    return config.Columns.find(function(c) { return c.Field === field; }) ||
                           (config.AllColumns || []).find(function(c) { return c.Field === field; });
                })
                .filter(Boolean);
        }

        function GetColumnByField(field) {
            return config.Columns.find(function(c) { return c.Field === field; }) ||
                   (config.AllColumns || []).find(function(c) { return c.Field === field; });
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

            InitializeColumnDragDrop();
            AttachHeaderEvents();
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
            var filterType = col.FilterType || 'text';
            var fieldAttr = 'data-filter-field="' + col.Field + '"';

            switch (filterType) {
                case 'boolean':
                    return '<select class="form-select grid-filter-input" ' + fieldAttr + '>' +
                        '<option value="">All</option>' +
                        '<option value="true"' + (value === 'true' ? ' selected' : '') + '>Yes</option>' +
                        '<option value="false"' + (value === 'false' ? ' selected' : '') + '>No</option>' +
                        '</select>';
                case 'number':
                    return '<input type="text" class="form-control grid-filter-input" ' +
                        fieldAttr + ' value="' + EscapeHtml(value) + '" placeholder="Filter...">';
                case 'date':
                    return '<input type="text" class="form-control grid-filter-input" ' +
                        fieldAttr + ' value="' + EscapeHtml(value) + '" placeholder="Filter...">';
                case 'enum':
                    var opts = '<option value="">All</option>';
                    if (col.EnumOptions) {
                        Object.keys(col.EnumOptions).forEach(function(key) {
                            opts += '<option value="' + key + '"' + (value === key ? ' selected' : '') + '>' +
                                EscapeHtml(col.EnumOptions[key]) + '</option>';
                        });
                    }
                    return '<select class="form-select grid-filter-input" ' + fieldAttr + '>' + opts + '</select>';
                default:
                    return '<input type="text" class="form-control grid-filter-input" ' +
                        fieldAttr + ' value="' + EscapeHtml(value) + '" placeholder="Filter...">';
            }
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
                    LoadData();
                }, 300);
            });
        }

        function InitializeColumnDragDrop() {
            var $headerRow = $content.find('thead tr').first();
            if (!$headerRow.length || typeof $.fn.sortable !== 'function') return;

            $headerRow.sortable({
                items: 'th',
                axis: 'x',
                cursor: 'move',
                placeholder: 'grid-column-placeholder',
                tolerance: 'pointer',
                start: function(e, ui) {
                    ui.placeholder.width(ui.item.width());
                    ui.placeholder.height(ui.item.height());
                },
                stop: function() {
                    var newOrder = [];
                    $headerRow.find('th').each(function() {
                        newOrder.push($(this).data('field'));
                    });
                    state.columnOrder = newOrder;
                    MarkStateDirty();
                    RenderHeaders();
                    RenderFilterRow();
                    RenderTableBody(true);
                }
            });
        }

        function AttachHeaderEvents() {
            $content.find('thead th.sortable').off('contextmenu').on('contextmenu', function(e) {
                e.preventDefault();
                ShowHeaderContextMenu($(this).data('field'), e);
            });
        }

        function ShowHeaderContextMenu(field, event) {
            if (!headerContextMenu) {
                headerContextMenu = CreateHeaderContextMenu();
            }
            headerContextMenu.Show(field, event);
        }

        function CreateHeaderContextMenu() {
            var $menu = $('<div>', { 'class': 'dropdown-menu grid-header-context-menu', css: { position: 'fixed', zIndex: 10000 } }).append(
                '<a class="dropdown-item" data-action="sort-asc"><i class="bi bi-sort-alpha-down me-2"></i>Sort Ascending</a>',
                '<a class="dropdown-item" data-action="sort-desc"><i class="bi bi-sort-alpha-up me-2"></i>Sort Descending</a>',
                '<a class="dropdown-item" data-action="add-sort"><i class="bi bi-plus-lg me-2"></i>Add to Sort</a>',
                '<a class="dropdown-item" data-action="clear-sort"><i class="bi bi-x-lg me-2"></i>Clear Sort</a>',
                '<div class="dropdown-divider"></div>',
                '<a class="dropdown-item" data-action="hide-column"><i class="bi bi-eye-slash me-2"></i>Hide Column</a>',
                '<a class="dropdown-item" data-action="column-chooser"><i class="bi bi-layout-split me-2"></i>Column Chooser...</a>'
            ).appendTo('body');

            var currentField = null;

            $menu.on('click', '[data-action]', function(e) {
                e.preventDefault();
                var action = $(this).data('action');

                switch (action) {
                    case 'sort-asc':
                        SetSort(currentField, 'asc', false);
                        break;
                    case 'sort-desc':
                        SetSort(currentField, 'desc', false);
                        break;
                    case 'add-sort':
                        SetSort(currentField, 'asc', true);
                        break;
                    case 'clear-sort':
                        ClearSort();
                        break;
                    case 'hide-column':
                        HideColumn(currentField);
                        break;
                    case 'column-chooser':
                        ShowColumnChooser();
                        break;
                }

                $menu.hide();
            });

            $(document).on('click', function(e) {
                !$.contains($menu[0], e.target) && $menu.hide();
            });

            return {
                Show: function(field, event) {
                    currentField = field;
                    var col = GetColumnByField(field);
                    $menu.find('[data-action="hide-column"]').toggle(!!(col && col.CanToggle));

                    $menu.css({ left: event.clientX, top: event.clientY }).show();

                    var rect = $menu[0].getBoundingClientRect();
                    rect.right > window.innerWidth && $menu.css('left', event.clientX - rect.width);
                    rect.bottom > window.innerHeight && $menu.css('top', event.clientY - rect.height);
                }
            };
        }

        function SetSort(field, direction, addToSort) {
            if (addToSort) {
                var existing = state.multiSort.findIndex(function(s) { return s.field === field; });
                if (existing === -1) {
                    state.multiSort.push({ field: field, direction: direction });
                } else {
                    state.multiSort[existing].direction = direction;
                }
            } else {
                state.multiSort = [{ field: field, direction: direction }];
            }

            state.sortColumn = state.multiSort[0]?.field || null;
            state.sortDirection = state.multiSort[0]?.direction || 'asc';

            MarkStateDirty();
            LoadData();
        }

        function ClearSort() {
            state.multiSort = [];
            state.sortColumn = null;
            state.sortDirection = 'asc';
            MarkStateDirty();
            LoadData();
        }

        function HideColumn(field) {
            if (state.hiddenColumns.indexOf(field) === -1) {
                state.hiddenColumns.push(field);
                MarkStateDirty();
                RenderHeaders();
                RenderFilterRow();
                RenderTableBody(true);
            }
        }

        function ShowColumn(field) {
            var idx = state.hiddenColumns.indexOf(field);
            if (idx !== -1) {
                state.hiddenColumns.splice(idx, 1);
                if (state.columnOrder.indexOf(field) === -1) {
                    state.columnOrder.push(field);
                }
                MarkStateDirty();
                RenderHeaders();
                RenderFilterRow();
                RenderTableBody(true);
            }
        }

        function ShowColumnChooser() {
            var allCols = config.AllColumns || config.Columns;
            var toggleableCols = allCols.filter(function(c) { return c.CanToggle !== false; });

            var modalHtml = '<div class="modal fade" tabindex="-1">' +
                '<div class="modal-dialog modal-dialog-scrollable">' +
                '<div class="modal-content">' +
                '<div class="modal-header"><h5 class="modal-title">Select Columns</h5>' +
                '<button type="button" class="btn-close" data-bs-dismiss="modal"></button></div>' +
                '<div class="modal-body">';

            toggleableCols.forEach(function(col) {
                var isVisible = state.hiddenColumns.indexOf(col.Field) === -1;
                modalHtml += '<div class="form-check">' +
                    '<input class="form-check-input column-chooser-check" type="checkbox" ' +
                    'data-field="' + col.Field + '" id="colcheck-' + col.Field + '"' +
                    (isVisible ? ' checked' : '') + '>' +
                    '<label class="form-check-label" for="colcheck-' + col.Field + '">' +
                    EscapeHtml(col.Header || col.DisplayName || col.Field) + '</label></div>';
            });

            modalHtml += '</div><div class="modal-footer">' +
                '<button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>' +
                '<button type="button" class="btn btn-primary column-chooser-apply">Apply</button>' +
                '</div></div></div></div>';

            var $modal = $(modalHtml).appendTo('body');
            var bsModal = new bootstrap.Modal($modal[0]);

            $modal.find('.column-chooser-apply').on('click', function() {
                var newHidden = [];
                var newOrder = state.columnOrder.slice();

                $modal.find('.column-chooser-check').each(function() {
                    var $check = $(this);
                    var field = $check.data('field');
                    if (!$check.is(':checked')) {
                        newHidden.push(field);
                    } else if (newOrder.indexOf(field) === -1) {
                        newOrder.push(field);
                    }
                });

                state.hiddenColumns = newHidden;
                state.columnOrder = newOrder;
                MarkStateDirty();
                RenderHeaders();
                RenderFilterRow();
                RenderTableBody(true);
                bsModal.hide();
            });

            $modal.on('hidden.bs.modal', function() { $modal.remove(); });
            bsModal.show();
        }

        function UpdateControlButtons() {
            var $controls = $container.find('.grid-controls');
            if (!$controls.length) return;

            var $saveBtn = $controls.find('[data-action="save-layout"]');
            var $resetBtn = $controls.find('[data-action="reset-layout"]');

            if (config.AutoSaveGridLayouts) {
                $saveBtn.hide();
            } else {
                $saveBtn.toggle(state.stateDirty).prop('disabled', !state.stateDirty);
            }

            $resetBtn.prop('disabled', !state.stateDirty && !state.savedState);
        }

        function InitializeGridElements() {
            InitializeDefaultState();
            $toolbar.length && RenderToolbar();
            AttachToolbarEvents();
            AttachTableEvents();

            LoadGridState().then(function() {
                RenderHeaders();
                RenderFilterRow();
                UpdateControlButtons();
            });
        }

        function RenderToolbar() {
            var actionsHtml = config.ToolbarActions.map(function(action) {
                var btnClass = { 'new': 'btn-success', 'delete': 'btn-danger' }[action.Id] || 'btn-info';
                return '<button class="btn ' + btnClass + ' me-1" data-action-id="' + action.Id + '"' +
                    (action.RequiresSelection ? ' data-requires-selection disabled' : '') +
                    ' title="' + action.Label + '">' +
                    (action.Icon ? '<i class="' + action.Icon + '"></i>' : action.Label) + '</button>';
            }).join('');

            var controlsHtml = '<button class="btn btn-secondary ms-3 me-1" data-action="column-chooser" title="Column Chooser">' +
                '<i class="bi bi-layout-split"></i></button>';

            if (!config.AutoSaveGridLayouts) {
                controlsHtml += '<button class="btn btn-secondary me-1" data-action="save-layout" title="Save Layout" style="display:none">' +
                    '<i class="bi bi-save"></i></button>';
            }

            controlsHtml += '<button class="btn btn-secondary me-1" data-action="reset-layout" title="Reset Layout">' +
                '<i class="bi bi-arrow-counterclockwise"></i></button>';

            $toolbar.html('<div class="d-flex align-items-center justify-content-end w-100">' + actionsHtml + controlsHtml + '</div>');
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

            var keyAttrs = config.Columns
                .filter(function(c) { return c.IsKey; })
                .map(function(c) { return ' data-key-' + c.Field.toLowerCase() + '="' + (GetFieldValue(row, c.Field) || '') + '"'; })
                .join('');

            var cells = VisibleColumns().map(function(col) {
                var value = GetFieldValue(row, col.Field);
                var tdClass = col.FormatterType === 'Boolean' ? ' class="text-center"' : '';
                var content = FormatCellValue(value, col, row);
                return '<td' + tdClass + ' data-field="' + col.Field + '">' + content + '</td>';
            }).join('');

            return '<tr data-row-id="' + rowId + '"' + keyAttrs +
                ' class="grid-row' + (isSelected ? ' table-active' : '') +
                '" style="cursor: pointer;">' + cells + '</tr>';
        }

        function UpdateRow($row, row) {
            var rowId = GetRowId(row);
            $row.toggleClass('table-active', state.selectedIds.indexOf(rowId) !== -1);

            $.each(config.Columns, function(_, col) {
                var value = GetFieldValue(row, col.Field);
                var $td = $row.find('td[data-field="' + col.Field + '"]');
                $td.html(FormatCellValue(value, col, row));
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
                '<div class="ms-auto ">Total: ' + state.totalRecords + '</div>' +
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

            $toolbar.off('click', '[data-action="column-chooser"]').on('click', '[data-action="column-chooser"]', function() {
                ShowColumnChooser();
            });

            $toolbar.off('click', '[data-action="save-layout"]').on('click', '[data-action="save-layout"]', function() {
                SaveGridState().then(function() {
                    TipsyBaboon.Common.ShowMessage('Grid layout saved', 'success', 2000);
                });
            });

            $toolbar.off('click', '[data-action="reset-layout"]').on('click', '[data-action="reset-layout"]', function() {
                ResetGridState();
                TipsyBaboon.Common.ShowMessage('Grid layout reset to default', 'info', 2000);
            });
        }

        function AttachTableEvents() {
            $content.on('click', 'th.sortable', function(e) {
                ToggleSort($(this).data('field'), e.shiftKey);
            });
        }

        function AttachRowEvents($row) {
            $row.on('click', function(e) {
                $(e.target).is('a, button') || SelectRow($(this).data('row-id'));
            });

            config.EnableRowNavigation && $row.on('dblclick', function(e) {
                if ($(e.target).is('a, button')) return;
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
                LoadData();
            });
        }

        function LoadData() {
            if (!config.ApiEndpoint) return;

            state.isLoading = true;
            ShowLoading();

            var params = {
                page: state.currentPage, pageSize: config.PageSize,
                formId: config.FormId, gridId: config.GridId
            };

            if (state.multiSort.length) {
                params.sortBy = state.multiSort.map(function(s) { return s.field; }).join(',');
                params.sortDir = state.multiSort.map(function(s) { return s.direction; }).join(',');
            } else if (state.sortColumn) {
                params.sortBy = state.sortColumn;
                params.sortDir = state.sortDirection;
            }

            config.ParentIdPropertyName && config.ParentEntityId && (params[config.ParentIdPropertyName] = config.ParentEntityId);
            $.extend(params, state.filters);

            Object.keys(state.columnFilters).forEach(function(field) {
                params['filter_' + field] = state.columnFilters[field];
            });

            $.ajax({ url: config.ApiEndpoint, data: params, dataType: 'json' })
                .done(function(result) {
                    if (!result.Success) {
                        state.isLoading = false;
                        state.data = [];
                        state.dataMap = {};
                        ShowError(result.Message || 'Server returned an error');
                        return;
                    }

                    state.data = result.Data.Items;
                    state.dataMap = {};
                    $.each(state.data, function(_, row) { state.dataMap[GetRowId(row)] = row; });
                    state.totalRecords = result.Data.TotalCount;
                    state.totalPages = Math.ceil(state.totalRecords / config.PageSize);
                    state.isLoading = false;

                    RenderTableBody(true);
                    UpdateSortIndicators();
                    RenderPagination();
                    UpdateToolbarState();
                })
                .fail(function(xhr) {
                    state.isLoading = false;
                    state.data = [];
                    state.dataMap = {};
                    (xhr.status === 401 || xhr.status === 403) ? ShowAuthError() : ShowError(xhr.statusText || 'Failed to load data');
                });
        }

        function SelectRow(rowId) {
            var previousId = state.focusedRowId;
            state.selectedIds = [rowId];
            state.focusedRowId = rowId;
            previousId && previousId !== rowId && UpdateRowSelection(previousId, false);
            UpdateRowSelection(rowId, true);
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
                        state.data = $.grep(state.data, function(row) { return GetRowId(row) !== rowId; });
                        state.focusedRowId === rowId && (state.focusedRowId = null);
                        state.selectedIds = $.grep(state.selectedIds, function(id) { return id !== rowId; });
                        state.totalRecords--;

                        state.data.length === 0 && state.currentPage > 1
                            ? GoToPage(state.currentPage - 1)
                            : state.data.length === 0 && ShowEmpty();

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

            MarkStateDirty();
            LoadData();
        }

        function GoToPage(page) {
            state.currentPage = page;
            LoadData();
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

        self.GetConfig = function() { return config; };

        self.DeleteRow = function(rowId) { DeleteRow(rowId); };

        self.SaveLayout = function() {
            return SaveGridState();
        };

        self.ResetLayout = function() {
            ResetGridState();
        };

        self.ShowColumnChooser = function() {
            ShowColumnChooser();
        };

        self.SetColumnFilter = function(field, value) {
            if (value) {
                state.columnFilters[field] = value;
            } else {
                delete state.columnFilters[field];
            }
            state.currentPage = 1;
            LoadData();
        };

        self.ClearColumnFilters = function() {
            state.columnFilters = {};
            RenderFilterRow();
            state.currentPage = 1;
            LoadData();
        };

        self.GetGridState = function() {
            return BuildStateDto();
        };

        function InitializeWithInlineData() {
            if (!config.InitialData || !config.InitialData.length) { ShowEmpty(); return; }

            state.data = config.InitialData;
            state.dataMap = {};
            $.each(state.data, function(_, row) { state.dataMap[GetRowId(row)] = row; });
            state.totalRecords = state.data.length;
            state.totalPages = 1;
            state.isLoading = false;

            RenderTableBody(true);
            UpdateToolbarState();
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
                btn && typeof btn.Action === 'function' && btn.Action(currentRowId, currentRowData);
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
