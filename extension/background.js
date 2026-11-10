import { autoplayVideo } from "./player.js";
import { sitePattern } from "./site.js";

const checkingTabs = new Set();
const newTabKey = tabId => `new-tab:${tabId}`;

chrome.tabs.onCreated.addListener(tab => {
    if (tab.id >= 0 && !tab.discarded) {
        // Session storage survives the service worker going idle between tab creation and load.
        chrome.storage.session.set({ [newTabKey(tab.id)]: true }).catch(console.warn);
    }
});

chrome.tabs.onRemoved.addListener(tabId => {
    chrome.storage.session.remove(newTabKey(tabId)).catch(console.warn);
});

chrome.tabs.onUpdated.addListener((tabId, change, tab) => {
    if (change.status !== "complete" || checkingTabs.has(tabId)) {
        return;
    }

    checkingTabs.add(tabId);
    autoplayNewTab(tabId, tab.url)
        .catch(console.warn)
        .finally(() => checkingTabs.delete(tabId));
});

async function autoplayNewTab(tabId, url) {
    const key = newTabKey(tabId);
    const pending = await chrome.storage.session.get(key);
    if (!pending[key]) {
        return;
    }

    await chrome.storage.session.remove(key);
    const pattern = sitePattern(url);
    if (!pattern || !await chrome.permissions.contains({ origins: [pattern] })) {
        return;
    }

    try {
        await chrome.scripting.executeScript({
            target: { tabId },
            func: autoplayVideo,
            args: [url],
        });
    } catch {
        // The tab may have closed, navigated away, or lost its site permission.
    }
}
