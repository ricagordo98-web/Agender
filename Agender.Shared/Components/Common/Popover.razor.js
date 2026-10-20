const GAP_PX = 8;
const MARGIN_PX = 12;
const MIN_ROOM_BELOW_PX = 320;

export function init(trigger, panel) {
    panel.addEventListener('beforetoggle', (event) => {
        if (event.newState === 'open') {
            place(trigger, panel);
        }
    });
}

export function close(panel) {
    panel.hidePopover?.();
}

function place(trigger, panel) {
    const rect = trigger.getBoundingClientRect();
    const width = parseInt(panel.dataset.width, 10);
    const roomBelow = window.innerHeight - rect.bottom;
    const placeAbove = roomBelow < MIN_ROOM_BELOW_PX && rect.top > roomBelow;

    let left = rect.right - width;
    if (left < MARGIN_PX) {
        left = Math.min(rect.left, window.innerWidth - width - MARGIN_PX);
    }

    panel.style.left = `${Math.max(MARGIN_PX, left)}px`;
    panel.style.right = 'auto';

    if (placeAbove) {
        panel.style.top = 'auto';
        panel.style.bottom = `${window.innerHeight - rect.top + GAP_PX}px`;
        panel.style.maxHeight = `${rect.top - GAP_PX - MARGIN_PX}px`;
    } else {
        panel.style.bottom = 'auto';
        panel.style.top = `${rect.bottom + GAP_PX}px`;
        panel.style.maxHeight = `${roomBelow - GAP_PX - MARGIN_PX}px`;
    }
}
