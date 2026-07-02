window.siad = window.siad || {};

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
