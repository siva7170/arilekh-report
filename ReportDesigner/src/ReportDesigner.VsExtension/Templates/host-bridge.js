/**
 * host-bridge.js
 * Injected by VSIX via AddScriptToExecuteOnDocumentCreatedAsync.
 * Safe to include in standalone browser too – chrome.webview won't exist there.
 */
(function () {
    'use strict';

    const isVsHost = !!(window.chrome && window.chrome.webview);

    function postToHost(type, value) {
        if (isVsHost) {
            window.chrome.webview.postMessage(
                JSON.stringify({ type: type, value: value ?? '' }));
        }
    }

    // Callbacks registered by Blazor
    var _onLoadContent   = null;
    var _onLoadCompanion = null;
    var _onSaved         = null;

    window.rdHostBridge = {

        // ── VS → Blazor ───────────────────────────────────────────────

        loadContent: function (xmlContent) {
            if (typeof _onLoadContent === 'function') {
                _onLoadContent(xmlContent);
            } else {
                // Blazor not ready yet – queue it
                window.rdHostBridge._pendingContent = xmlContent;
            }
        },

        loadCompanionFile: function (filePath, xmlContent) {
            if (typeof _onLoadCompanion === 'function')
                _onLoadCompanion(filePath, xmlContent);
        },

        onSaved: function () {
            if (typeof _onSaved === 'function') _onSaved();
        },

        // ── Blazor → VS ───────────────────────────────────────────────

        notifyDirty: function (isDirty) {
            postToHost('dirty', isDirty ? 'true' : 'false');
        },

        requestSave: function (xmlContent) {
            postToHost('save', xmlContent);
        },

        requestOpenFile: function (fileType) {
            postToHost('openFile', fileType);
        },

        // ── Registration (called by Blazor on startup) ─────────────────

        onLoadContent: function (fn) {
            _onLoadContent = fn;
            // Flush any content that arrived before Blazor was ready
            if (window.rdHostBridge._pendingContent) {
                fn(window.rdHostBridge._pendingContent);
                window.rdHostBridge._pendingContent = null;
            }
        },

        onLoadCompanion: function (fn) { _onLoadCompanion = fn; },
        onSavedCallback: function (fn) { _onSaved = fn; },

        _pendingContent:   null,
        _pendingCompanion: null,
    };

    // ── Signal VS host that the page is ready ─────────────────────────
    // We wait for DOMContentLoaded so Blazor has started booting,
    // then signal after a short delay to let Blazor register its handlers.

    function signalReady() {
        postToHost('ready', '1');
        console.log('[rdHostBridge] ready signal sent. VS host:', isVsHost);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            // Give Blazor WASM ~2s to boot and register handlers
            setTimeout(signalReady, 2000);
        });
    } else {
        setTimeout(signalReady, 2000);
    }

})();
