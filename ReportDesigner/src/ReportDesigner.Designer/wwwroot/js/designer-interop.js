// designer-interop.js  –  ES module loaded by JsInterop.cs

/**
 * Opens a file picker and returns { content, fileName }.
 */
export async function openFile(accept) {
    return new Promise((resolve, reject) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = accept || '.rdx,.rds,.xml';
        input.onchange = async () => {
            const file = input.files[0];
            if (!file) { reject('No file selected'); return; }
            const content = await file.text();
            resolve({ content, fileName: file.name });
        };
        input.click();
    });
}

/**
 * Sets a scaled drag image so the ghost matches the element at current zoom.
 */
export function attachScaledDragImage(elementId) {
    const el = document.getElementById(elementId);
    if (!el || el._scaledDragAttached) return;
    el._scaledDragAttached = true;
    el.addEventListener('dragstart', (e) => {
        const rect   = el.getBoundingClientRect();
        const clone  = el.cloneNode(true);
        clone.style.position = 'fixed';
        clone.style.top   = '-9999px';
        clone.style.left  = '-9999px';
        clone.style.width  = rect.width  + 'px';
        clone.style.height = rect.height + 'px';
        clone.style.transform = '';
        clone.style.opacity = '0.85';
        clone.style.pointerEvents = 'none';
        clone.style.zIndex = '99999';
        document.body.appendChild(clone);
        const offsetX = Math.max(0, e.clientX - rect.left);
        const offsetY = Math.max(0, e.clientY - rect.top);
        e.dataTransfer.setDragImage(clone, offsetX, offsetY);
        requestAnimationFrame(() => requestAnimationFrame(() => {
            if (clone.parentNode) clone.parentNode.removeChild(clone);
        }));
    }, false);
}

/**
 * Opens the HTML report in a hidden iframe and triggers the browser print dialog.
 * The user can then choose "Save as PDF" from the print dialog.
 */
export function printHtml(html) {
    const iframe = document.createElement('iframe');
    iframe.style.cssText = 'position:fixed;top:-9999px;left:-9999px;width:210mm;height:297mm;border:none;';
    document.body.appendChild(iframe);
    iframe.contentDocument.open();
    iframe.contentDocument.write(html);
    iframe.contentDocument.close();
    iframe.contentWindow.focus();
    setTimeout(() => {
        iframe.contentWindow.print();
        setTimeout(() => document.body.removeChild(iframe), 1000);
    }, 300);
}

/**
 * Triggers a browser download of textContent as fileName.
 */
export function saveFile(fileName, textContent) {
    const blob = new Blob([textContent], { type: 'text/xml;charset=utf-8' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}


export function downloadBase64(fileName, base64, mimeType) {
    // Decode base64 to binary
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++)
        bytes[i] = binary.charCodeAt(i);

    const blob = new Blob([bytes], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}


/**
 * Copies text to the system clipboard.
 */
export async function copyText(text) {
    try { await navigator.clipboard.writeText(text); } catch { /* ignore */ }
}

/**
 * Returns the DOMRect of an element by id.
 */
export function getBoundingRect(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return { left: 0, top: 0, width: 0, height: 0 };
    const r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
}

/**
 * Notifies the VS WebView2 host that the designer has unsaved changes.
 * The VSIX listens for window.chrome.webview messages.
 */
export function notifyHostDirty(isDirty) {
    if (window.chrome?.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ type: 'dirty', value: isDirty }));
    }
}

/**
 * Sends the serialized report XML to the VSIX host to save to disk.
 */
export function notifyHostSave(xml) {
    if (window.chrome?.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ type: 'save', value: xml }));
    }
}

/**
 * Asks the VS host to open a file dialog and return the chosen path + content.
 */
export function requestHostFile(fileType) {
    if (window.chrome?.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ type: 'openFile', fileType }));
        // Response handled asynchronously via DotNet.invokeMethodAsync from the host
    }
    return null;
}

// ── VS Host Bridge ──────────────────────────────────────────────────────────
// The VSIX calls window.rdHostBridge.onSaved() after writing the file.
// We expose this object globally and forward calls to Blazor JSInvokable methods.
window.rdHostBridge = {
    onSaved: () => {
        // Call back into Blazor - HostBridgeInterop.OnSavedByHostAsync
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('ReportDesigner.Designer', 'OnSavedByHost')
                .catch(err => console.warn('onSaved callback failed:', err));
        }
    },
    loadContent: (xml) => {
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('ReportDesigner.Designer', 'LoadReportFromHost', xml)
                .catch(err => console.warn('loadContent failed:', err));
        }
    },
    loadCompanionFile: (fileType, filePath, xml) => {
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('ReportDesigner.Designer', 'LoadSchemaFromHost', filePath, xml)
                .catch(err => console.warn('loadCompanionFile failed:', err));
        }
    }
};

// ── Global save shortcut (Ctrl+S) ───────────────────────────────────────────
window.rdDesigner = {
    saveFile,
    downloadBase64
};

document.addEventListener('keydown', e => {
    if ((e.ctrlKey || e.metaKey) && e.key === 's') {
        e.preventDefault();
        // Blazor component handles this via @onkeydown on the root div
        // but we also need to prevent browser save dialog
    }
});
