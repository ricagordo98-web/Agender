let handler = null;

export function init(dotNetRef) {
    dispose();

    handler = (event) => {
        const isPaletteKey = (event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === 'k';

        if (isPaletteKey) {
            event.preventDefault();
            dotNetRef.invokeMethodAsync('OnShortcut', 'Palette').catch((error) => console.error('Agender: shortcut failed', error));
        }
    };

    document.addEventListener('keydown', handler);
}

export function dispose() {
    if (handler) {
        document.removeEventListener('keydown', handler);
        handler = null;
    }
}
