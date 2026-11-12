export function isCompact() {
    return window.matchMedia('(max-width: 640px)').matches;
}

export function revealActive(container) {
    const active = container?.querySelector('.active');

    if (active) {
        active.scrollIntoView({ block: 'nearest', inline: 'center' });
    }
}
