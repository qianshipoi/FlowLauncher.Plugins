(function () {
    const fileInput = document.getElementById('file-input');
    const hasHost = !!(window.chrome && window.chrome.webview);

    // 默认代码视图；样式与菜单栏用 jsoneditor 自带，自定义按钮注入到菜单
    const editor = new JSONEditor(document.getElementById('editor'), {
        mode: 'code',
        modes: ['code', 'tree', 'view']
    });

    function tryGetCurrentJson() {
        try {
            return { ok: true, value: editor.get() };
        } catch (err) {
            return { ok: false, error: String(err.message || err) };
        }
    }

    function copyText(text) {
        if (hasHost) {
            window.chrome.webview.postMessage({ type: 'copy', text: text });
            return Promise.resolve(true);
        }
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text).then(function () { return true; });
        }
        return new Promise(function (resolve) {
            const ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.left = '-9999px';
            document.body.appendChild(ta);
            ta.select();
            try {
                resolve(document.execCommand('copy'));
            } catch (_) {
                resolve(false);
            } finally {
                document.body.removeChild(ta);
            }
        });
    }

    function addMenuButton(iconClass, title, onClick) {
        const menu = document.querySelector('.jsoneditor-menu');
        if (!menu) return;
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'jsoneditor-custom ' + iconClass;
        btn.title = title;
        btn.onclick = onClick;
        menu.appendChild(btn);
    }

    addMenuButton('jsoneditor-copy', '复制当前 JSON', function () {
        const result = tryGetCurrentJson();
        const text = result.ok
            ? JSON.stringify(result.value, null, 2)
            : (editor.getText() || '');
        if (!text) return;
        copyText(text);
    });

    addMenuButton('jsoneditor-import', '从文件导入 JSON', function () {
        fileInput.click();
    });

    addMenuButton('jsoneditor-export', '导出为 JSON 文件', function () {
        const result = tryGetCurrentJson();
        const text = result.ok
            ? JSON.stringify(result.value, null, 2)
            : (editor.getText() || '');
        if (!text) return;
        // 宿主弹「另存为」；浏览器预览回退为直接下载
        if (hasHost) {
            window.chrome.webview.postMessage({ type: 'export', text: text });
            return;
        }
        const blob = new Blob([text], { type: 'application/json;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'data.json';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    });

    fileInput.onchange = function () {
        const file = fileInput.files && fileInput.files[0];
        fileInput.value = '';
        if (!file) return;
        const reader = new FileReader();
        reader.onload = function () {
            editor.setMode('code');
            editor.setText(String(reader.result || ''));
        };
        reader.readAsText(file, 'utf-8');
    };

    function loadJson(json) {
        try {
            JSON.parse(json);
            editor.setMode('code');
            editor.setText(json);
        } catch (_) {
            // 无效 JSON 保持 code 模式，方便修复
            editor.setMode('code');
            editor.setText(json);
        }
    }

    if (hasHost) {
        window.chrome.webview.addEventListener('message', function (e) {
            const message = e.data;
            if (message && message.type === 'init') {
                loadJson(message.json || '');
            }
        });
        window.chrome.webview.postMessage({ type: 'ready' });
    }
})();
