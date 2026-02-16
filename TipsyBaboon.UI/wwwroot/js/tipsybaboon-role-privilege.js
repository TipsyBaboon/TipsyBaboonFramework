// Role Privilege Field - Tab-based privilege assignment with event delegation
(function() {
    'use strict';

    window.TipsyBaboon = window.TipsyBaboon || {};
    window.TipsyBaboon.UI = window.TipsyBaboon.UI || {};

    window.TipsyBaboon.UI.RolePrivilege = {
        Initialize: function(containerId, apiEndpoint, parentId) {
            var $container = document.getElementById(containerId);
            if (!$container) return;

            var $loading = $container.querySelector('.role-privilege-loading');
            var $empty = $container.querySelector('.role-privilege-empty');
            var $content = $container.querySelector('.role-privilege-content');

            $loading.style.display = 'block';
            $empty.style.display = 'none';
            $content.style.display = 'none';

            fetch(`${apiEndpoint}?RoleId=${parentId}`)
                .then(function(response) {
                    if (!response.ok) throw new Error('Failed to load privileges');
                    return response.json();
                })
                .then(function(data) {
                    var result = data.Data || data;
                    var privileges = result.Items || result;

                    if (!privileges || privileges.length === 0) {
                        $loading.style.display = 'none';
                        $empty.style.display = 'block';
                        return;
                    }

                    RenderPrivileges($container, GroupPrivilegesByModel(privileges), containerId);
                    $loading.style.display = 'none';
                    $content.style.display = 'block';
                })
                .catch(function(error) {
                    console.error('Error loading privileges:', error);
                    $loading.style.display = 'none';
                    $empty.style.display = 'block';
                    window.TipsyBaboon.Common.ShowError('Failed to load privileges');
                });
        }
    };

    function GroupPrivilegesByModel(privileges) {
        var groups = {};
        privileges.forEach(function(priv) {
            var modelName = priv.ModelName;
            if (!groups[modelName]) groups[modelName] = [];
            groups[modelName].push(priv);
        });
        return groups;
    }

    function RenderPrivileges($container, privilegesByModel, containerId) {
        var $tabsContainer = $container.querySelector('.nav-tabs');
        var $contentContainer = $container.querySelector('.tab-content');

        $tabsContainer.innerHTML = '';
        $contentContainer.innerHTML = '';

        var modelNames = Object.keys(privilegesByModel).sort();
        var isFirst = true;

        modelNames.forEach(function(modelName) {
            var privileges = privilegesByModel[modelName];
            var safeModelName = modelName.replace(/\s+/g, '-');
            var tabId = containerId + '-tab-' + safeModelName;
            var panelId = containerId + '-panel-' + safeModelName;

            var $tab = document.createElement('li');
            $tab.className = 'nav-item';
            $tab.setAttribute('role', 'presentation');
            $tab.innerHTML = '<button class="nav-link' + (isFirst ? ' active' : '') + '" ' +
                'id="' + tabId + '" data-bs-toggle="tab" data-bs-target="#' + panelId + '" ' +
                'type="button" role="tab" aria-controls="' + panelId + '" ' +
                'aria-selected="' + (isFirst ? 'true' : 'false') + '">' + modelName + '</button>';
            $tabsContainer.appendChild($tab);

            var $panel = document.createElement('div');
            $panel.className = 'tab-pane fade' + (isFirst ? ' show active' : '');
            $panel.id = panelId;
            $panel.setAttribute('role', 'tabpanel');
            $panel.setAttribute('aria-labelledby', tabId);

            var $row = document.createElement('div');
            $row.className = 'row';

            privileges.sort(function(a, b) {
                return a.PrivilegeName.localeCompare(b.PrivilegeName);
            });

            privileges.forEach(function(privilege) {
                var checkboxId = containerId + '-privilege-' + privilege.PrivilegeId;

                var $col = document.createElement('div');
                $col.className = 'col-md-6 col-lg-4 mb-2';

                var $wrapper = document.createElement('div');
                $wrapper.setAttribute('data-module', 'Governance');
                $wrapper.setAttribute('data-model', 'RolePrivilegeView');

                var recordKeys = JSON.stringify({ 
                    RoleId: privilege.RoleId, 
                    PrivilegeId: privilege.PrivilegeId 
                });
                $wrapper.setAttribute('data-record-keys', recordKeys);

                var $formCheck = document.createElement('div');
                // Use Bootstrap switch style for toggle consistency with other assignment controls
                $formCheck.className = 'form-check form-switch';

                var $input = document.createElement('input');
                $input.className = 'form-check-input';
                $input.type = 'checkbox';
                $input.id = checkboxId;
                $input.setAttribute('data-field', 'IsAssigned');
                // Accessibility: indicate this checkbox is a switch
                $input.setAttribute('role', 'switch');

                if (privilege.IsAssigned) $input.checked = true;

                var $label = document.createElement('label');
                $label.className = 'form-check-label';
                $label.setAttribute('for', checkboxId);
                $label.textContent = privilege.PrivilegeName;

                $formCheck.appendChild($input);
                $formCheck.appendChild($label);
                $wrapper.appendChild($formCheck);
                $col.appendChild($wrapper);
                $row.appendChild($col);
            });

            $panel.appendChild($row);
            $contentContainer.appendChild($panel);
            isFirst = false;
        });
    }
})();
