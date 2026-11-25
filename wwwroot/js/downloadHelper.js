window.pixciiDownload = {
    saveBytes: function (fileName, mimeType, bytes) {
        const blob = new Blob([bytes], { type: mimeType || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },

    saveText: function (fileName, mimeType, text) {
        const blob = new Blob([text], { type: mimeType || "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },

    copyText: async function (text) {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }

        const textarea = document.createElement("textarea");
        textarea.value = text;
        textarea.style.position = "fixed";
        textarea.style.left = "-9999px";
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();

        let copied = false;
        try {
            copied = document.execCommand("copy");
        } finally {
            document.body.removeChild(textarea);
        }
        return copied;
    }
};
