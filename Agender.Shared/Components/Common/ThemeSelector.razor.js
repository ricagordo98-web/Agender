const STORAGE_KEY = 'agender-theme';
const SYSTEM = 'system';

export function getTheme() {
    try {
        return localStorage.getItem(STORAGE_KEY) ?? SYSTEM;
    } catch {
        return SYSTEM;
    }
}

export function setTheme(id) {
    if (id === SYSTEM) {
        delete document.documentElement.dataset.theme;
    } else {
        document.documentElement.dataset.theme = id;
    }

    try {
        if (id === SYSTEM) {
            localStorage.removeItem(STORAGE_KEY);
        } else {
            localStorage.setItem(STORAGE_KEY, id);
        }
    } catch {
        // Storage may be unavailable; the theme still applies for this session.
    }
}
