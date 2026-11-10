import { sitePattern } from "./site.js";

const toggle = document.querySelector("#enabled");
const site = document.querySelector("#site");
const status = document.querySelector("#status");

try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    const pattern = sitePattern(tab?.url);
    if (pattern) {
        site.textContent = new URL(tab.url).hostname;
        const permission = { origins: [pattern] };
        toggle.checked = await chrome.permissions.contains(permission);
        toggle.disabled = false;
        toggle.addEventListener("change", () => changeSitePermission(permission));
    } else {
        status.textContent = "请在要开启此功能的网站上打开扩展。";
    }
} catch {
    status.textContent = "无法读取当前站点，请重新打开扩展。";
}

async function changeSitePermission(permission) {
    const enabling = toggle.checked;
    toggle.disabled = true;
    status.textContent = "";

    try {
        if (enabling) {
            await chrome.permissions.request(permission);
        } else {
            await chrome.permissions.remove(permission);
        }
        toggle.checked = await chrome.permissions.contains(permission);
        if (toggle.checked !== enabling) {
            status.textContent = enabling ? "尚未授予站点访问权限。" : "无法移除站点访问权限。";
        }
    } catch {
        toggle.checked = !enabling;
        status.textContent = "无法更新站点权限，请重试。";
    } finally {
        toggle.disabled = false;
    }
}
