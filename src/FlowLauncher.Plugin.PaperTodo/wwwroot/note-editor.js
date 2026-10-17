(() => {
    "use strict";

    const maxImageBytes = 32 * 1024 * 1024;
    const chunkSize = 512 * 1024;
    const pendingImages = new Map();
    let editor = null;
    let requestCounter = 0;

    function post(message) {
        window.chrome?.webview?.postMessage(message);
    }

    function showImageError(message) {
        console.error(message);
    }

    async function uploadBlob(blob, callback) {
        if (!blob || blob.size === 0) {
            callback("");
            return;
        }
        if (blob.size > maxImageBytes) {
            showImageError("图片超过 32 MB，无法插入。");
            callback("");
            return;
        }

        const requestId = `${Date.now()}-${++requestCounter}`;
        pendingImages.set(requestId, callback);
        const totalChunks = Math.ceil(blob.size / chunkSize);
        post({
            type: "imageBegin",
            requestId,
            totalChunks,
            fileName: blob.name || "image"
        });

        for (let index = 0; index < totalChunks; index++) {
            const chunk = blob.slice(index * chunkSize, (index + 1) * chunkSize);
            const dataUrl = await readAsDataUrl(chunk);
            const comma = dataUrl.indexOf(",");
            post({
                type: "imageChunk",
                requestId,
                index,
                data: comma >= 0 ? dataUrl.slice(comma + 1) : ""
            });
        }
    }

    function readAsDataUrl(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(String(reader.result || ""));
            reader.onerror = () => reject(reader.error || new Error("读取图片失败"));
            reader.readAsDataURL(blob);
        });
    }

    function createEditor(markdown) {
        if (typeof toastui === "undefined" || !toastui.Editor) {
            throw new Error("Markdown 编辑器资源加载失败");
        }

        toastui.Editor.setLanguage("zh-CN");
        editor = new toastui.Editor({
            el: document.querySelector("#editor"),
            height: "100%",
            initialEditType: "wysiwyg",
            previewStyle: "vertical",
            initialValue: markdown || "",
            usageStatistics: false,
            hideModeSwitch: false,
            autofocus: false,
            hooks: {
                addImageBlobHook: (blob, callback) => {
                    uploadBlob(blob, callback).catch(error => {
                        showImageError(error.message);
                        callback("");
                    });
                }
            },
            toolbarItems: [
                ["heading", "bold", "italic", "strike"],
                ["hr", "quote"],
                ["ul", "ol", "task", "indent", "outdent"],
                ["table", "image", "link"],
                ["code", "codeblock"]
            ]
        });

        editor.on("change", () => {
            post({
                type: "change",
                markdown: editor.getMarkdown()
            });
        });

        window.paperTodoGetMarkdown = () => editor?.getMarkdown() || "";
    }

    window.chrome?.webview?.addEventListener("message", event => {
        const message = event.data || {};
        try {
            if (message.type === "init") {
                if (!editor) {
                    createEditor(message.markdown || "");
                } else {
                    editor.setMarkdown(message.markdown || "", false);
                }
                return;
            }

            if (message.type === "imageSaved") {
                const callback = pendingImages.get(message.requestId);
                if (callback) {
                    pendingImages.delete(message.requestId);
                    callback(message.url, message.altText || "image");
                }
                return;
            }

            if (message.type === "imageError") {
                const callback = pendingImages.get(message.requestId);
                if (callback) {
                    pendingImages.delete(message.requestId);
                    callback("");
                }
                showImageError(message.message || "图片保存失败");
            }
        } catch (error) {
            showImageError(error.message);
        }
    });

    document.addEventListener("click", event => {
        const link = event.target?.closest?.("a[href]");
        if (!link) {
            return;
        }
        const href = link.getAttribute("href");
        if (!href) {
            return;
        }
        event.preventDefault();
        post({ type: "openLink", href });
    }, true);

    document.addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
            event.preventDefault();
            post({ type: "save", markdown: editor?.getMarkdown() || "" });
        }
    });

    if (window.chrome?.webview) {
        post({ type: "ready" });
    } else {
        createEditor(new URLSearchParams(window.location.search).get("preview") || "");
    }
})();
