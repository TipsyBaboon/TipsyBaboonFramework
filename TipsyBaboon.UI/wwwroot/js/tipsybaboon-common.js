(function($) {
    'use strict';

    window.TipsyBaboon = window.TipsyBaboon || {};

    var EscapeHtml = function(str) {
        return !str ? '' : String(str)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    };

    var GetAntiForgeryToken = function() {
        return $('input[name="__RequestVerificationToken"]').val() || null;
    };

    var AlertClassMap = { success: 'success', error: 'danger', warning: 'warning', info: 'info' };
    var ScriptRegistry = {};

    window.TipsyBaboon.AwaitScripts = function(scripts) {
        return Promise.all($.map(scripts, function(src) {
            return ScriptRegistry[src] || (ScriptRegistry[src] = new Promise(function(resolve, reject) {
                var existing = document.querySelector('script[src="' + src + '"]');
                var mine = !existing;
                var el = existing || Object.assign(document.createElement('script'), { src: src, async: true });

                el.addEventListener('load', resolve);
                el.addEventListener('error', function() { reject(src); });
                mine && (document.head || document.body).appendChild(el);
                !mine && setTimeout(resolve, 2000);
                !mine && !el.async && resolve();
            }));
        }));
    };

    var messageTimeout = null;

    window.TipsyBaboon.Common = {

        EscapeHtml: EscapeHtml,

        GetAntiForgeryToken: GetAntiForgeryToken,

        ShowMessage: function(message, type, duration) {
            type = type || 'info';
            duration = duration !== undefined ? duration : 5000;

            var $container = $('#tipsybaboon-messages');
            if (!$container.length) {
                $container = $('<div>', {
                    id: 'tipsybaboon-messages',
                    css: { position: 'fixed', bottom: 20, right: 20, zIndex: 10000, maxWidth: 400 }
                }).appendTo('body');
            }

            var $existingAlert = $container.find('.alert');
            
            if ($existingAlert.length) {
                clearTimeout(messageTimeout);
                $existingAlert.find('.alert-message').text(message);
                $existingAlert.removeClass('alert-info alert-success alert-warning alert-danger')
                    .addClass('alert-' + (AlertClassMap[type] || 'info'));
            } else {
                var $alert = $('<div>', {
                    'class': 'alert alert-' + (AlertClassMap[type] || 'info') + ' alert-dismissible fade show',
                    role: 'alert'
                }).append(
                    $('<span>', { 'class': 'alert-message', text: message }),
                    $('<button>', { type: 'button', 'class': 'btn-close', 'data-bs-dismiss': 'alert', 'aria-label': 'Close' })
                ).appendTo($container);
                $existingAlert = $alert;
            }

            if (duration > 0) {
                messageTimeout = setTimeout(function() {
                    $existingAlert.removeClass('show');
                    setTimeout(function() { $existingAlert.remove(); }, 150);
                }, duration);
            }
        },

        ShowError: function(message, autoCloseMs) {
            autoCloseMs = autoCloseMs !== undefined ? autoCloseMs : 3000;
            
            var modalPromise = TipsyBaboon.Common.ShowModal({
                title: 'Error',
                message: message,
                buttons: [{ label: 'OK', cssClass: 'btn-danger' }],
                dismissible: true
            });
            
            if (autoCloseMs > 0) {
                setTimeout(function() {
                    var $modal = $('#tipsybaboon-confirm-modal');
                    var bsModal = bootstrap.Modal.getInstance($modal[0]);
                    bsModal && bsModal.hide();
                }, autoCloseMs);
            }
            
            return modalPromise;
        },

        ShowModal: function(options) {
            options = $.extend({ title: 'Notice', message: '', buttons: [{ label: 'OK', cssClass: 'btn-primary' }], dismissible: true }, options);

            return new Promise(function(resolve) {
                var $reusableModal = $('#tipsybaboon-confirm-modal');
                
                if (!$reusableModal.length) {
                    $reusableModal = $('<div class="modal fade" id="tipsybaboon-confirm-modal" tabindex="-1">' +
                        '<div class="modal-dialog modal-sm">' +
                        '<div class="modal-content">' +
                        '<div class="modal-header">' +
                        '<h5 class="modal-title"></h5>' +
                        '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
                        '</div>' +
                        '<div class="modal-body"></div>' +
                        '<div class="modal-footer"></div>' +
                        '</div>' +
                        '</div>' +
                        '</div>').appendTo('body');
                }

                var bsModal = bootstrap.Modal.getInstance($reusableModal[0]) || new bootstrap.Modal($reusableModal[0], {
                    backdrop: options.dismissible ? true : 'static',
                    keyboard: options.dismissible
                });

                $reusableModal.find('.modal-title').text(options.title);
                $reusableModal.find('.modal-body').text(options.message);
                
                var $footer = $reusableModal.find('.modal-footer').empty();
                $.each(options.buttons, function(_, btn) {
                    $('<button>', { type: 'button', 'class': 'btn ' + (btn.cssClass || 'btn-primary'), text: btn.label || 'OK' })
                        .on('click', function() {
                            typeof btn.action === 'function' && btn.action();
                            bsModal.hide();
                        })
                        .appendTo($footer);
                });

                var hiddenHandler = function() {
                    $reusableModal.off('hidden.bs.modal', hiddenHandler);
                    resolve();
                };
                $reusableModal.on('hidden.bs.modal', hiddenHandler);

                bsModal.show();
            });
        },

        Confirm: function(message, title) {
            var confirmed = false;
            return TipsyBaboon.Common.ShowModal({
                title: title || 'Confirm',
                message: message,
                dismissible: true,
                buttons: [
                    { label: 'Confirm', cssClass: 'btn-primary', action: function() { confirmed = true; } },
                    { label: 'Cancel', cssClass: 'btn-secondary' }
                ]
            }).then(function() { return confirmed; });
        },

        Alert: function(message, title) {
            return TipsyBaboon.Common.ShowModal({
                title: title || 'Alert',
                message: message,
                buttons: [{ label: 'OK', cssClass: 'btn-primary' }],
                dismissible: true
            });
        },

        ApiRequest: function(url, options) {
            options = options || {};
            var method = options.method || 'GET';
            var token = GetAntiForgeryToken();
            var headers = {};

            if (method !== 'GET' && !token) {
                console.error('[TipsyBaboon] Anti-forgery token not found for ' + method + ' request to ' + url);
            }
            token && (headers.RequestVerificationToken = token);

            return $.ajax({
                url: url,
                method: method,
                contentType: 'application/json',
                data: options.body,
                dataType: 'json',
                headers: headers
            }).catch(function(xhr) {
                var msg = 'Request failed';
                try { msg = (xhr.responseJSON || {}).Message || msg; } catch(e) {}
                throw new Error(msg);
            });
        },

        ParseGuid: function(value) {
            return value && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value) ? value : null;
        },

        FormatValue: function(value, type) {
            if (value == null) return '';
            var formatters = {
                datetime: function(v) { var d = new Date(v); return isNaN(d.getTime()) ? String(v) : d.toLocaleString(); },
                date: function(v) { var d = new Date(v); return isNaN(d.getTime()) ? String(v) : d.toLocaleString(); },
                'boolean': function(v) { return v ? 'Yes' : 'No'; }
            };
            return (formatters[type] || String)(value);
        },

        DeepEquals: function(a, b) {
            if (a === b) return true;
            if (a == null || b == null || typeof a !== typeof b) return false;
            if (Array.isArray(a)) {
                return Array.isArray(b) && a.length === b.length &&
                    a.every(function(v, i) { return TipsyBaboon.Common.DeepEquals(v, b[i]); });
            }
            if (typeof a === 'object') {
                var keys = Object.keys(a);
                return keys.length === Object.keys(b).length &&
                    keys.every(function(k) { return TipsyBaboon.Common.DeepEquals(a[k], b[k]); });
            }
            return false;
        },

        RequireStyle: function(url) {
            $('link[href="' + url + '"]').length || $('<link>', { rel: 'stylesheet', href: url }).appendTo('head');
        },

        InitFieldControl: function(selector, options) {
            options = options || {};
            $.each(options.styles || [], function(_, url) { TipsyBaboon.Common.RequireStyle(url); });
            var loader = window.TipsyBaboon.Loader || window.TipsyBaboon;
            var loadFn = loader.LoadAll || loader.AwaitScripts;
            loadFn($.map(options.scripts || [], function(item) {
                return typeof item === 'string' ? item : item.url;
            })).then(function() {
                $(selector).not('[data-tb-initialized]').each(function() {
                    $(this).attr('data-tb-initialized', 'true');
                    options.init && options.init(this);
                });
            });
        }
    };

    window.TipsyBaboon.UI = window.TipsyBaboon.UI || {};

    var ModalStack = [];

    window.TipsyBaboon.UI.OpenModal = function(url, onClose, onCreated) {
        $.get(url)
            .done(function(html) {
                if (!html || typeof html !== 'string' || !html.trim()) return;

                var $modal = $('<div class="modal fade" tabindex="-1">' +
                    '<div class="modal-dialog modal-xl"><div class="modal-content">' +
                    html + '</div></div></div>').appendTo('body');

                var bsModal = new bootstrap.Modal($modal[0], { backdrop: 'static', keyboard: false });
                var createdRecordData = null;
                
                // Hide previous modal in stack if exists (manually, not Bootstrap hide)
                if (ModalStack.length > 0) {
                    var $prevModal = ModalStack[ModalStack.length - 1].$modal;
                    $prevModal.css('display', 'none');
                    $prevModal.next('.modal-backdrop').css('display', 'none');
                    
                    // Inject "Close All" button before the close button in footer
                    var $footer = $modal.find('.modal-footer');
                    var $closeBtn = $footer.find('button[data-bs-dismiss="modal"]');
                    if ($closeBtn.length > 0) {
                        var $closeAllBtn = $('<button>', {
                            type: 'button',
                            'class': 'btn btn-warning me-auto',
                            text: 'Close All'
                        }).on('click', function() {
                            // Close all modals in stack
                            while (ModalStack.length > 0) {
                                var item = ModalStack.pop();
                                item.bsModal.hide();
                                item.$modal.remove();
                                typeof item.onClose === 'function' && item.onClose();
                            }
                        });
                        $closeBtn.before($closeAllBtn);
                    }
                }

                // Listen for form:saved event to capture created record data
                $modal.on('form:saved', function(e, data) {
                    if (data && data.parentIdMap) {
                        createdRecordData = data.parentIdMap;
                    }
                });

                // Add to stack before showing
                ModalStack.push({ $modal: $modal, bsModal: bsModal, onClose: onClose, onCreated: onCreated });
                
                bsModal.show();

                $modal.on('shown.bs.modal', function() {
                    var $dialog = $modal.find('.modal-dialog');
                    var $header = $modal.find('.modal-header');
                    $header.length && typeof $dialog.draggable === 'function' && (
                        $dialog.draggable({ handle: '.modal-header', cursor: 'move', opacity: 0.9 }),
                        $header.css('cursor', 'move')
                    );
                });

                $modal.on('hidden.bs.modal', function() {
                    // Remove from stack
                    var closingIndex = -1;
                    for (var i = 0; i < ModalStack.length; i++) {
                        if (ModalStack[i].$modal[0] === $modal[0]) {
                            closingIndex = i;
                            break;
                        }
                    }
                    
                    if (closingIndex >= 0) {
                        var closingItem = ModalStack.splice(closingIndex, 1)[0];
                        typeof closingItem.onClose === 'function' && closingItem.onClose();
                        typeof closingItem.onCreated === 'function' && createdRecordData && closingItem.onCreated(createdRecordData);
                    }
                    
                    $modal.remove();
                    
                    // Restore previous modal if exists
                    if (ModalStack.length > 0) {
                        var $restoreModal = ModalStack[ModalStack.length - 1].$modal;
                        $restoreModal.css('display', 'block');
                        $restoreModal.next('.modal-backdrop').css('display', 'block');
                    }
                });
            })
            .fail(function() {
                TipsyBaboon.Common.Alert('Failed to open record modal. Please try again.', 'Modal Error');
            });
    };

    $(document).ajaxError(function(_, jqXHR) {
        var message = 'An error occurred';
        try {
            var json = jqXHR.responseJSON || JSON.parse(jqXHR.responseText);
            message = json.Message || json.message || message;
        } catch(e) {
            jqXHR.statusText !== 'error' && (message = jqXHR.statusText);
        }
        TipsyBaboon.Common.ShowError(message);
    });

    // Global error handler for uncaught JavaScript exceptions
    window.addEventListener('error', function(event) {
        TipsyBaboon.Common.ShowError('An error occurred');
    });

    // Global handler for unhandled promise rejections
    window.addEventListener('unhandledrejection', function(event) {
        TipsyBaboon.Common.ShowError('An error occurred');
    });

})(jQuery);
