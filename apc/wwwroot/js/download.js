window.siad = window.siad || {};

// Abre un PDF recibido como base64 en una pestaña nueva (vista previa de
// NC/ND: el endpoint es POST, así que no se puede abrir por URL directa).
window.siad.abrirPdf = (base64, titulo) => {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: "application/pdf" });
    const url = URL.createObjectURL(blob);
    const win = window.open(url, "_blank");
    if (win && titulo) {
        win.document.title = titulo;
    }
    // Liberar el objeto cuando la pestaña ya lo cargó.
    setTimeout(() => URL.revokeObjectURL(url), 60000);
};

window.siad.downloadFromUrl = (url, fileName) => {
    const link = document.createElement("a");
    link.href = url;
    link.style.display = "none";

    if (fileName) {
        link.download = fileName;
    }

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
