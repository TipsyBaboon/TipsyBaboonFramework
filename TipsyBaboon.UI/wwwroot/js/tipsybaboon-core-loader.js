(function() {
    'use strict';

    var UI_CONTENT_PATH = '/_content/TipsyBaboon.UI';
    var CACHE_BUST_VERSION = new Date().getTime(); // Use timestamp for cache-busting
    var ScriptRegistry = {};
    var StyleRegistry = {};

    // Append cache-busting query string to a URL
    function AddCacheBust(url) {
        if (!url) return url;
        // Only add cache-bust to TipsyBaboon scripts, not external libs
        if (url.indexOf(UI_CONTENT_PATH) === 0) {
            return url + '?v=' + CACHE_BUST_VERSION;
        }
        return url;
    }

    // Core library definitions with detection and fallback paths
    var CoreLibs = {
        jquery: {
            detect: function() { return typeof window.jQuery !== 'undefined'; },
            fallback: UI_CONTENT_PATH + '/lib/jquery/jquery.min.js',
            priority: 1
        },
        jqueryui: {
            detect: function() { return typeof window.jQuery !== 'undefined' && typeof window.jQuery.ui !== 'undefined'; },
            fallback: UI_CONTENT_PATH + '/lib/jquery-ui/jquery-ui.min.js',
            depends: ['jquery'],
            priority: 2
        },
        jqueryValidate: {
            detect: function() { return typeof window.jQuery !== 'undefined' && typeof window.jQuery.fn.validate !== 'undefined'; },
            fallback: UI_CONTENT_PATH + '/lib/jquery-validation/jquery.validate.min.js',
            depends: ['jquery'],
            priority: 3
        },
        jqueryValidateUnobtrusive: {
            detect: function() { return typeof window.jQuery !== 'undefined' && typeof window.jQuery.validator !== 'undefined' && typeof window.jQuery.validator.unobtrusive !== 'undefined'; },
            fallback: UI_CONTENT_PATH + '/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js',
            depends: ['jquery', 'jqueryValidate'],
            priority: 4
        }
    };

    // TipsyBaboon's own scripts with dependencies
    var TipsyBaboonLibs = {
        common: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-common.js',
            depends: ['jquery'],
            priority: 10
        },
        formSave: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-form-save.js',
            depends: ['jquery', 'common'],
            priority: 11
        },
        grid: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-grid.js',
            depends: ['jquery', 'jqueryui', 'common'],
            priority: 12
        },
        gridReadonly: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-grid-readonly.js',
            depends: ['jquery', 'jqueryui', 'common'],
            priority: 12
        },
        permissionLevel: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-permission-level.js',
            depends: ['jquery', 'common'],
            priority: 13
        },
        rolePrivilege: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-role-privilege.js',
            depends: ['jquery', 'common'],
            priority: 13
        },
        layoutEditor: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-layout-editor.js',
            depends: ['jquery', 'jqueryui', 'common'],
            priority: 14
        },
        config: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-config.js',
            depends: ['jquery'],
            priority: 15
        },
        assignmentGrid: {
            path: UI_CONTENT_PATH + '/js/tipsybaboon-assignment-grid.js',
            depends: ['jquery', 'common'],
            priority: 15
        }
    };

    // Load a single script, returning a promise
    function LoadScript(src) {
        var cachedSrc = AddCacheBust(src);
        if (ScriptRegistry[cachedSrc]) return ScriptRegistry[cachedSrc];

        ScriptRegistry[cachedSrc] = new Promise(function(resolve, reject) {
            var existing = document.querySelector('script[src^="' + src + '"]');
            if (existing) {
                // Script tag exists - wait for it or assume loaded
                if (existing.hasAttribute('data-loaded')) {
                    resolve();
                    return;
                }
                existing.addEventListener('load', function() {
                    existing.setAttribute('data-loaded', 'true');
                    resolve();
                });
                existing.addEventListener('error', function() { reject(new Error('Failed to load: ' + src)); });
                // Timeout fallback for scripts that may have loaded before we attached
                setTimeout(resolve, 1500);
                return;
            }

            var script = document.createElement('script');
            script.src = cachedSrc;
            script.async = true;
            script.addEventListener('load', function() {
                script.setAttribute('data-loaded', 'true');
                resolve();
            });
            script.addEventListener('error', function() { reject(new Error('Failed to load: ' + src)); });
            (document.head || document.body).appendChild(script);
        });

        return ScriptRegistry[cachedSrc];
    }

    // Load a stylesheet (for future use)
    function LoadStyle(href) {
        var cachedHref = AddCacheBust(href);
        if (StyleRegistry[cachedHref]) return StyleRegistry[cachedHref];

        StyleRegistry[cachedHref] = new Promise(function(resolve) {
            var existing = document.querySelector('link[href^="' + href + '"]');
            if (existing) {
                resolve();
                return;
            }

            var link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = cachedHref;
            link.addEventListener('load', resolve);
            link.addEventListener('error', resolve); // Don't fail on missing stylesheets
            document.head.appendChild(link);
        });

        return StyleRegistry[cachedHref];
    }

    // Ensure a core library is loaded (detect or load fallback)
    function EnsureCoreLib(libName) {
        var lib = CoreLibs[libName];
        if (!lib) return Promise.reject(new Error('Unknown core lib: ' + libName));

        // Already loaded?
        if (lib.detect()) return Promise.resolve();

        // Need to load dependencies first
        var depsPromise = lib.depends 
            ? Promise.all(lib.depends.map(EnsureCoreLib))
            : Promise.resolve();

        return depsPromise.then(function() {
            // Check again after deps loaded
            if (lib.detect()) return Promise.resolve();
            return LoadScript(lib.fallback);
        });
    }

    // Ensure a TipsyBaboon library is loaded
    function EnsureTipsyBaboonLib(libName) {
        var lib = TipsyBaboonLibs[libName];
        if (!lib) return Promise.reject(new Error('Unknown TipsyBaboon lib: ' + libName));

        // Dependencies: mix of core and TipsyBaboon libs
        var depsPromise = lib.depends
            ? Promise.all(lib.depends.map(function(dep) {
                return CoreLibs[dep] ? EnsureCoreLib(dep) : EnsureTipsyBaboonLib(dep);
            }))
            : Promise.resolve();

        return depsPromise.then(function() {
            return LoadScript(lib.path);
        });
    }

    // Load all core dependencies (jQuery, jQueryUI, validation)
    function EnsureCoreDependencies() {
        return Promise.all([
            EnsureCoreLib('jquery'),
            EnsureCoreLib('jqueryui'),
            EnsureCoreLib('jqueryValidate'),
            EnsureCoreLib('jqueryValidateUnobtrusive')
        ]);
    }

    // Load standard form dependencies (core + common + formSave)
    function EnsureFormDependencies() {
        return EnsureCoreDependencies().then(function() {
            return EnsureTipsyBaboonLib('common');
        }).then(function() {
            return EnsureTipsyBaboonLib('formSave');
        });
    }

    // Load an array of scripts (can be paths or lib names)
    function LoadAll(scripts) {
        return Promise.all(scripts.map(function(script) {
            // If it's a known TipsyBaboon lib name, use that
            if (TipsyBaboonLibs[script]) return EnsureTipsyBaboonLib(script);
            // If it's a known core lib name, use that
            if (CoreLibs[script]) return EnsureCoreLib(script);
            // Otherwise treat as a path
            return LoadScript(script);
        }));
    }

    // Expose the loader
    window.TipsyBaboon = window.TipsyBaboon || {};
    window.TipsyBaboon.Loader = {
        EnsureCoreDependencies: EnsureCoreDependencies,
        EnsureFormDependencies: EnsureFormDependencies,
        EnsureCoreLib: EnsureCoreLib,
        EnsureTipsyBaboonLib: EnsureTipsyBaboonLib,
        LoadScript: LoadScript,
        LoadStyle: LoadStyle,
        LoadAll: LoadAll,
        CoreLibs: CoreLibs,
        TipsyBaboonLibs: TipsyBaboonLibs
    };

    // Also expose AwaitScripts for backward compatibility
    window.TipsyBaboon.AwaitScripts = function(scripts) {
        return LoadAll(scripts);
    };

})();
