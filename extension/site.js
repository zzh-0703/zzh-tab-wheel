export function sitePattern(url) {
    if (!url) {
        return null;
    }

    const { protocol, hostname } = new URL(url);
    if (protocol !== "http:" && protocol !== "https:") {
        return null;
    }

    return `*://${hostname}/*`;
}
