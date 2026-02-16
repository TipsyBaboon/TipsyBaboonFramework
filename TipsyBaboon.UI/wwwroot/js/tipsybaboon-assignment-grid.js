/**
 * TipsyBaboon Assignment Grid - Interactive checkbox grids for User-Role assignments
 * Provides grid-style controls with checkboxes for managing relationship assignments
 */
window.TipsyBaboonAssignmentGrid = (function() {
    'use strict';

    const API_BASE = '/api';

    /**
     * Initialize a User-Role assignment grid (roles for a specific user)
     * @param {string} containerId - The container element ID
     * @param {string} userId - The user ID to load roles for
     */
    function initializeUserRoleGrid(containerId, userId) {
        if (!userId || userId === '' || userId === 'null') {
            renderEmptyState(containerId, 'No user selected');
            return;
        }

        // Load roles and current assignments
        Promise.all([
            fetch(`${API_BASE}/Core/Role`).then(r => { if (!r.ok) throw new Error('Failed to load roles: ' + r.statusText); return r.json(); }),
            fetch(`${API_BASE}/userRole/assignments/${userId}`).then(r => { if (!r.ok) throw new Error('Failed to load assignments: ' + r.statusText); return r.json(); })
        ]).then(([rolesResponse, assignmentsResponse]) => {
            const roles = rolesResponse.Items || rolesResponse;
            const assignments = assignmentsResponse.Assignments || assignmentsResponse;
            
            renderUserRoleGrid(containerId, userId, roles, assignments);
        }).catch(error => {
            console.error('Failed to load user-role data:', error);
            renderErrorState(containerId, 'Failed to load role data');
        });
    }

    /**
     * Initialize a Role-User assignment grid (users for a specific role)
     * @param {string} containerId - The container element ID
     * @param {string} roleId - The role ID to load users for
     */
    function initializeRoleUserGrid(containerId, roleId) {
        if (!roleId || roleId === '' || roleId === 'null') {
            renderEmptyState(containerId, 'No role selected');
            return;
        }

        // Load users and current assignments
        Promise.all([
            fetch(`${API_BASE}/Core/User`).then(r => { if (!r.ok) throw new Error('Failed to load users: ' + r.statusText); return r.json(); }),
            fetch(`${API_BASE}/userRole/assignments/role/${roleId}`).then(r => { if (!r.ok) throw new Error('Failed to load assignments: ' + r.statusText); return r.json(); })
        ]).then(([usersResponse, assignmentsResponse]) => {
            const users = usersResponse.Items || usersResponse;
            const assignments = assignmentsResponse.Assignments || assignmentsResponse;
            
            renderRoleUserGrid(containerId, roleId, users, assignments);
        }).catch(error => {
            console.error('Failed to load role-user data:', error);
            renderErrorState(containerId, 'Failed to load user data');
        });
    }

    /**
     * Render the user-role assignment grid
     */
    function renderUserRoleGrid(containerId, userId, roles, assignments) {
        const container = document.getElementById(containerId);
        const contentArea = container.querySelector('.assignment-grid-content');
        
        if (!roles || roles.length === 0) {
            contentArea.innerHTML = '<div class="text-center text-muted py-3">No roles available</div>';
            return;
        }

        const assignedRoleIds = new Set(assignments.map(a => a.RoleId));
        
        let html = '<div class="row g-2">';
        
        roles.forEach((role, index) => {
            const isAssigned = assignedRoleIds.has(role.Id);
            const checkboxId = `role_${role.id}`;
            
            html += `
                <div class="col-md-6 col-lg-4">
                    <div class="card h-100 assignment-card ${isAssigned ? 'border-success' : ''}">
                        <div class="card-body p-2">
                            <div class="form-check">
                                <input type="checkbox" class="form-check-input" 
                                       id="${checkboxId}" 
                                       value="${role.Id}"
                                       ${isAssigned ? 'checked' : ''}
                                       onchange="TipsyBaboonAssignmentGrid.toggleUserRole('${userId}', '${role.Id}', this)" />
                                <label class="form-check-label" for="${checkboxId}">
                                    <strong>${escapeHtml(role.Name || role.RoleName)}</strong>
                                    ${role.Description ? `<br><small class="text-muted">${escapeHtml(role.Description)}</small>` : ''}
                                </label>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });
        
        html += '</div>';
        contentArea.innerHTML = html;
    }

    /**
     * Render the role-user assignment grid  
     */
    function renderRoleUserGrid(containerId, roleId, users, assignments) {
        const container = document.getElementById(containerId);
        const contentArea = container.querySelector('.assignment-grid-content');
        
        if (!users || users.length === 0) {
            contentArea.innerHTML = '<div class="text-center text-muted py-3">No users available</div>';
            return;
        }

        const assignedUserIds = new Set(assignments.map(a => a.UserId));
        
        let html = '<div class="row g-2">';
        
        users.forEach((user, index) => {
            const isAssigned = assignedUserIds.has(user.Id);
            const checkboxId = `user_${user.id}`;
            
            html += `
                <div class="col-md-6 col-lg-4">
                    <div class="card h-100 assignment-card ${isAssigned ? 'border-success' : ''}">
                        <div class="card-body p-2">
                            <div class="form-check">
                                <input type="checkbox" class="form-check-input" 
                                       id="${checkboxId}" 
                                       value="${user.Id}"
                                       ${isAssigned ? 'checked' : ''}
                                       onchange="TipsyBaboonAssignmentGrid.toggleRoleUser('${roleId}', '${user.Id}', this)" />
                                <label class="form-check-label" for="${checkboxId}">
                                    <strong>${escapeHtml(user.UserName)}</strong>
                                    ${user.Email ? `<br><small class="text-muted">${escapeHtml(user.Email)}</small>` : ''}
                                </label>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });
        
        html += '</div>';
        contentArea.innerHTML = html;
    }

    /**
     * Toggle user-role assignment
     */
    function toggleUserRole(userId, roleId, checkbox) {
        const isAssigning = checkbox.checked;
        const action = isAssigning ? 'assign' : 'unassign';
        
        checkbox.disabled = true;
        const card = checkbox.closest('.assignment-card');
        card.style.opacity = '0.6';
        
        fetch(`${API_BASE}/userRole/${action}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                userId: userId,
                roleId: roleId
            })
        })
        .then(response => {
            if (!response.ok) throw new Error('Assignment toggle failed: ' + response.statusText);
            return response.json();
        })
        .then(data => {
            if (data.Success || data.success) {
                // Update visual state
                if (isAssigning) {
                    card.classList.add('border-success');
                } else {
                    card.classList.remove('border-success');
                }
                
                // Track change for form submission
                trackChange(userId, 'userRoles', roleId, isAssigning);
            } else {
                // Revert checkbox state on failure
                checkbox.checked = !isAssigning;
                showError('Failed to update role assignment');
            }
        })
        .catch(error => {
            console.error('Assignment toggle failed:', error);
            checkbox.checked = !isAssigning;
            showError('Failed to update role assignment');
        })
        .finally(() => {
            checkbox.disabled = false;
            card.style.opacity = '1';
        });
    }

    /**
     * Toggle role-user assignment
     */
    function toggleRoleUser(roleId, userId, checkbox) {
        const isAssigning = checkbox.checked;
        const action = isAssigning ? 'assign' : 'unassign';
        
        checkbox.disabled = true;
        const card = checkbox.closest('.assignment-card');
        card.style.opacity = '0.6';
        
        fetch(`${API_BASE}/userRole/${action}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                userId: userId,
                roleId: roleId
            })
        })
        .then(response => {
            if (!response.ok) throw new Error('Assignment toggle failed: ' + response.statusText);
            return response.json();
        })
        .then(data => {
            if (data.Success || data.success) {
                // Update visual state
                if (isAssigning) {
                    card.classList.add('border-success');
                } else {
                    card.classList.remove('border-success');
                }
                
                // Track change for form submission
                trackChange(roleId, 'roleUsers', userId, isAssigning);
            } else {
                // Revert checkbox state on failure
                checkbox.checked = !isAssigning;
                showError('Failed to update user assignment');
            }
        })
        .catch(error => {
            console.error('Assignment toggle failed:', error);
            checkbox.checked = !isAssigning;
            showError('Failed to update user assignment');
        })
        .finally(() => {
            checkbox.disabled = false;
            card.style.opacity = '1';
        });
    }

    /**
     * Track changes for form submission
     */
    function trackChange(entityId, propertyName, targetId, isAssigned) {
        // This could be enhanced to track changes in a hidden form field
        // for scenarios where the assignment grid is part of a larger form
    }

    /**
     * Render empty state
     */
    function renderEmptyState(containerId, message) {
        const container = document.getElementById(containerId);
        const contentArea = container.querySelector('.assignment-grid-content');
        contentArea.innerHTML = `<div class="text-center text-muted py-3">${escapeHtml(message)}</div>`;
    }

    /**
     * Render error state
     */
    function renderErrorState(containerId, message) {
        const container = document.getElementById(containerId);
        const contentArea = container.querySelector('.assignment-grid-content');
        contentArea.innerHTML = `
            <div class="alert alert-danger py-2 mb-0">
                <i class="bi bi-exclamation-triangle-fill me-2"></i>${escapeHtml(message)}
            </div>
        `;
    }

    /**
     * Show error message
     */
    function showError(message) {
        if (window.TipsyBaboon && window.TipsyBaboon.Common) {
            window.TipsyBaboon.Common.Alert(message, 'Assignment Grid');
        } else {
            // Fallback to browser alert if Common module not available
            alert(message);
        }
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(unsafe) {
        if (!unsafe) return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    // Public API
    return {
        initializeUserRoleGrid: initializeUserRoleGrid,
        initializeRoleUserGrid: initializeRoleUserGrid,
        toggleUserRole: toggleUserRole,
        toggleRoleUser: toggleRoleUser
    };
})();