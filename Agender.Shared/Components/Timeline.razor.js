const MINUTES_PER_DAY = 24 * 60;
const DRAG_THRESHOLD_PX = 4;
const AUTOSCROLL_EDGE_PX = 48;
const AUTOSCROLL_MAX_SPEED = 18;
const LONG_PRESS_MS = 350;
const LONG_PRESS_SLOP_PX = 8;
const TASK_DRAGGING_CLASS = 'task-dragging';

const instances = new WeakMap();

const pad = (value) => String(value).padStart(2, '0');
const formatMinute = (minute) => `${pad(Math.floor(minute / 60))}:${pad(minute % 60)}`;
const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

export function init(root, dotNetRef, readOnly = false) {
    instances.get(root)?.();

    const ppm = parseFloat(root.dataset.ppm);
    const defaultDuration = () => parseInt(root.dataset.defaultDuration, 10);
    const snap = () => parseInt(root.dataset.snap, 10);
    const snapRound = (minute) => Math.round(minute / snap()) * snap();
    const snapFloor = (minute) => Math.floor(minute / snap()) * snap();

    const columns = () => Array.from(root.querySelectorAll('.day-col'));
    const minuteAt = (column, clientY) => (clientY - column.getBoundingClientRect().top) / ppm;
    const columnAt = (clientX, fallback) =>
        columns().find((column) => {
            const rect = column.getBoundingClientRect();
            return clientX >= rect.left && clientX < rect.right;
        }) ?? fallback;

    const invoke = (method, ...args) =>
        dotNetRef.invokeMethodAsync(method, ...args).catch((error) => console.error(`Agender: ${method} failed`, error));

    let drag = null;
    let taskDrag = null;
    let mouseTask = null;
    let chip = null;
    let ghost = null;
    let suppressClick = false;
    let autoScrollFrame = null;
    let press = null;
    let lockScroll = false;

    function showGhost(column, start, end, colorClass) {
        if (!ghost) {
            ghost = document.createElement('div');
            document.body.appendChild(ghost);
        }

        const rect = column.getBoundingClientRect();
        ghost.className = `entry-ghost ${colorClass ?? ''}`;
        ghost.style.left = `${rect.left + 2}px`;
        ghost.style.width = `${rect.width - 4}px`;
        ghost.style.top = `${rect.top + start * ppm}px`;
        ghost.style.height = `${Math.max((end - start) * ppm, 20)}px`;
        ghost.textContent = `${formatMinute(start)} – ${formatMinute(end)}`;
    }

    function hideGhost() {
        ghost?.remove();
        ghost = null;
    }

    function updateDrag(clientX, clientY) {
        const pointerMinute = minuteAt(drag.column, clientY);

        if (drag.kind === 'move') {
            drag.column = columnAt(clientX, drag.column);
            const duration = drag.originEnd - drag.originStart;
            drag.start = clamp(snapRound(minuteAt(drag.column, clientY) - drag.grabOffset), 0, MINUTES_PER_DAY - duration);
            drag.end = drag.start + duration;
        } else if (drag.kind === 'top') {
            drag.start = clamp(snapRound(pointerMinute), 0, drag.originEnd - snap());
            drag.end = drag.originEnd;
        } else if (drag.kind === 'bottom') {
            drag.start = drag.originStart;
            drag.end = clamp(snapRound(pointerMinute), drag.originStart + snap(), MINUTES_PER_DAY);
        } else {
            const low = clamp(Math.min(drag.anchor, snapRound(pointerMinute)), 0, MINUTES_PER_DAY - snap());
            const high = clamp(Math.max(drag.anchor, snapRound(pointerMinute)), low + snap(), MINUTES_PER_DAY);
            drag.start = low;
            drag.end = high;
        }

        const overSidebar = drag.kind === 'move' && drag.linked && !!document.elementFromPoint(clientX, clientY)?.closest('.tasks');
        drag.returning = overSidebar;
        document.querySelector('.tasks')?.classList.toggle('return-target', overSidebar);

        if (overSidebar) {
            hideGhost();
            return;
        }

        showGhost(drag.column, drag.start, drag.end, drag.colorClass);
    }

    function autoScroll() {
        autoScrollFrame = null;
        if (!drag?.moved) {
            return;
        }

        const rect = root.getBoundingClientRect();
        const headHeight = root.querySelector('.tl-head')?.offsetHeight ?? 0;
        const topEdge = rect.top + headHeight + AUTOSCROLL_EDGE_PX;
        const bottomEdge = rect.bottom - AUTOSCROLL_EDGE_PX;
        let delta = 0;

        if (drag.lastY < topEdge) {
            delta = -AUTOSCROLL_MAX_SPEED * clamp((topEdge - drag.lastY) / AUTOSCROLL_EDGE_PX, 0.15, 1);
        } else if (drag.lastY > bottomEdge) {
            delta = AUTOSCROLL_MAX_SPEED * clamp((drag.lastY - bottomEdge) / AUTOSCROLL_EDGE_PX, 0.15, 1);
        }

        if (delta !== 0) {
            root.scrollTop += delta;
            updateDrag(drag.lastX, drag.lastY);
        }

        autoScrollFrame = requestAnimationFrame(autoScroll);
    }

    function beginDrag(event, state) {
        drag = { ...state, startX: event.clientX, startY: event.clientY, lastX: event.clientX, lastY: event.clientY, moved: false };
        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
        document.addEventListener('pointercancel', cancelDrag);
        document.activeElement?.blur?.();
        event.preventDefault();
    }

    function endDrag() {
        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup', onPointerUp);
        document.removeEventListener('pointercancel', cancelDrag);
        drag?.element?.classList.remove('dragging');
        document.querySelector('.tasks')?.classList.remove('return-target');
        lockScroll = false;
        if (autoScrollFrame) {
            cancelAnimationFrame(autoScrollFrame);
            autoScrollFrame = null;
        }
    }

    function cancelDrag() {
        endDrag();
        hideGhost();
        drag = null;
    }

    function onPointerMove(event) {
        if (!drag) {
            return;
        }

        drag.lastX = event.clientX;
        drag.lastY = event.clientY;

        if (!drag.moved) {
            if (Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) < DRAG_THRESHOLD_PX) {
                return;
            }

            drag.moved = true;
            drag.element?.classList.add('dragging');
            autoScrollFrame = requestAnimationFrame(autoScroll);
        }

        updateDrag(event.clientX, event.clientY);
    }

    async function onPointerUp() {
        const finished = drag;
        endDrag();
        drag = null;

        if (!finished) {
            return;
        }

        if (!finished.moved) {
            hideGhost();
            if (finished.kind === 'create') {
                const duration = defaultDuration();
                const start = clamp(finished.anchor, 0, MINUTES_PER_DAY - duration);
                await invoke('OnRangeSelected', finished.column.dataset.date, start, start + duration);
            }
            return;
        }

        suppressClick = true;
        setTimeout(() => (suppressClick = false), 0);

        try {
            if (finished.returning) {
                await invoke('OnEntryReturned', finished.id);
            } else if (finished.kind === 'create') {
                await invoke('OnRangeSelected', finished.column.dataset.date, finished.start, finished.end);
            } else {
                const changed = finished.start !== finished.originStart
                    || finished.end !== finished.originEnd
                    || finished.column.dataset.date !== finished.originDate;

                if (changed) {
                    await invoke('OnEntryMoved', finished.id, finished.column.dataset.date, finished.start, finished.end);
                }
            }
        } finally {
            hideGhost();
        }
    }

    function entryDragState(entry, edge, clientY) {
        const column = entry.closest('.day-col');
        const start = parseInt(entry.dataset.start, 10);
        const end = parseInt(entry.dataset.end, 10);

        return {
            kind: edge ?? 'move',
            id: entry.dataset.entryId,
            element: entry,
            column,
            originDate: column.dataset.date,
            originStart: start,
            originEnd: end,
            start,
            end,
            grabOffset: minuteAt(column, clientY) - start,
            colorClass: entry.dataset.color,
            linked: entry.dataset.taskLinked === 'true',
            returning: false,
        };
    }

    function onPointerDown(event) {
        if (event.button !== 0 || event.target.closest('.gevent')) {
            return;
        }

        if (event.pointerType === 'touch') {
            onTouchPointerDown(event);
            return;
        }

        const entry = event.target.closest('.entry');
        if (entry?.classList.contains('completed')) {
            return;
        }

        if (entry) {
            const edge = event.target.closest('.entry-resize')?.dataset.edge;
            beginDrag(event, entryDragState(entry, edge, event.clientY));
            return;
        }

        const column = event.target.closest('.day-col');
        if (column) {
            beginDrag(event, {
                kind: 'create',
                column,
                anchor: clamp(snapFloor(minuteAt(column, event.clientY)), 0, MINUTES_PER_DAY - snap()),
                start: 0,
                end: 0,
            });
        }
    }

    function onTouchPointerDown(event) {
        const entry = event.target.closest('.entry');
        if (!entry || entry.classList.contains('completed')) {
            return;
        }

        const edge = event.target.closest('.entry-resize')?.dataset.edge;
        if (edge) {
            lockScroll = true;
            beginDrag(event, entryDragState(entry, edge, event.clientY));
            return;
        }

        startLongPress(event, ({ x, y }) => {
            lockScroll = true;
            beginDrag({ clientX: x, clientY: y, preventDefault() {} }, entryDragState(entry, null, y));
            drag.moved = true;
            entry.classList.add('dragging');
            updateDrag(x, y);
            autoScrollFrame = requestAnimationFrame(autoScroll);
        });
    }

    function startLongPress(event, onLongPress) {
        cancelLongPress();
        press = {
            x: event.clientX,
            y: event.clientY,
            timer: setTimeout(() => {
                const started = press;
                cancelLongPress();
                navigator.vibrate?.(15);
                onLongPress(started);
            }, LONG_PRESS_MS),
        };

        document.addEventListener('pointermove', onLongPressMove);
        document.addEventListener('pointerup', cancelLongPress);
        document.addEventListener('pointercancel', cancelLongPress);
    }

    function cancelLongPress() {
        if (press) {
            clearTimeout(press.timer);
            press = null;
        }

        document.removeEventListener('pointermove', onLongPressMove);
        document.removeEventListener('pointerup', cancelLongPress);
        document.removeEventListener('pointercancel', cancelLongPress);
    }

    function onLongPressMove(event) {
        if (press && Math.hypot(event.clientX - press.x, event.clientY - press.y) > LONG_PRESS_SLOP_PX) {
            cancelLongPress();
        }
    }

    function onClick(event) {
        if (suppressClick) {
            event.stopPropagation();
            return;
        }

        const entry = event.target.closest('.entry');
        if (entry) {
            invoke('OnEntryClicked', entry.dataset.entryId);
            return;
        }

        const column = event.target.closest('.day-col');
        if (column && event.pointerType === 'touch') {
            const duration = defaultDuration();
            const start = clamp(snapFloor(minuteAt(column, event.clientY)), 0, MINUTES_PER_DAY - duration);
            invoke('OnRangeSelected', column.dataset.date, start, start + duration);
        }
    }

    function taskSlot(column, clientY) {
        const duration = defaultDuration();
        return clamp(snapFloor(minuteAt(column, clientY)), 0, MINUTES_PER_DAY - duration);
    }

    function onTaskPointerDown(event) {
        if (event.button !== 0) {
            return;
        }

        const card = event.target.closest?.('[data-task-id]');
        if (!card || event.target.closest('button')) {
            return;
        }

        if (event.pointerType === 'touch') {
            startLongPress(event, ({ x, y }) => beginTaskDrag(card, x, y, true));
            return;
        }

        mouseTask = { card, x: event.clientX, y: event.clientY };
        document.addEventListener('pointermove', onMouseTaskMove);
        document.addEventListener('pointerup', cancelMouseTask);
        document.addEventListener('pointercancel', cancelMouseTask);
    }

    function onMouseTaskMove(event) {
        if (!mouseTask || Math.hypot(event.clientX - mouseTask.x, event.clientY - mouseTask.y) < DRAG_THRESHOLD_PX) {
            return;
        }

        const { card } = mouseTask;
        cancelMouseTask();
        beginTaskDrag(card, event.clientX, event.clientY, false);
    }

    function cancelMouseTask() {
        mouseTask = null;
        document.removeEventListener('pointermove', onMouseTaskMove);
        document.removeEventListener('pointerup', cancelMouseTask);
        document.removeEventListener('pointercancel', cancelMouseTask);
    }

    function beginTaskDrag(card, x, y, hideDrawer) {
        taskDrag = { id: card.dataset.taskId, colorClass: card.dataset.color, card, column: null, start: 0 };
        card.classList.add('is-dragging');

        if (hideDrawer) {
            lockScroll = true;
            document.body.classList.add(TASK_DRAGGING_CLASS);
        } else {
            showChip(card.querySelector('.task-title')?.textContent ?? '');
        }

        document.addEventListener('pointermove', onTaskPointerMove);
        document.addEventListener('pointerup', onTaskPointerUp);
        document.addEventListener('pointercancel', endTaskDrag);
        updateTaskDrag(x, y);
    }

    function showChip(text) {
        chip = document.createElement('div');
        chip.className = 'task-drag-chip';
        chip.textContent = text;
        document.body.appendChild(chip);
    }

    function updateTaskDrag(x, y) {
        if (chip) {
            chip.style.left = `${x + 14}px`;
            chip.style.top = `${y + 14}px`;
        }

        const column = document.elementFromPoint(x, y)?.closest('.day-col');

        if (!column || !root.contains(column)) {
            taskDrag.column = null;
            hideGhost();
            return;
        }

        taskDrag.column = column;
        taskDrag.start = taskSlot(column, y);
        showGhost(column, taskDrag.start, taskDrag.start + defaultDuration(), taskDrag.colorClass);
    }

    function onTaskPointerMove(event) {
        if (taskDrag) {
            updateTaskDrag(event.clientX, event.clientY);
        }
    }

    function onTaskPointerUp() {
        const finished = taskDrag;
        endTaskDrag();

        if (finished?.column) {
            invoke('OnTaskDropped', finished.id, finished.column.dataset.date, finished.start);
        }
    }

    function endTaskDrag() {
        taskDrag?.card.classList.remove('is-dragging');
        taskDrag = null;
        lockScroll = false;
        chip?.remove();
        chip = null;
        hideGhost();
        document.body.classList.remove(TASK_DRAGGING_CLASS);
        document.removeEventListener('pointermove', onTaskPointerMove);
        document.removeEventListener('pointerup', onTaskPointerUp);
        document.removeEventListener('pointercancel', endTaskDrag);
    }

    function onTouchMove(event) {
        if (lockScroll) {
            event.preventDefault();
        }
    }

    function onContextMenu(event) {
        if (window.matchMedia('(pointer: coarse)').matches) {
            if (event.target.closest?.('.entry, [data-task-id]')) {
                event.preventDefault();
            }

            return;
        }

        const entry = event.target.closest?.('.entry');

        if (entry && root.contains(entry)) {
            event.preventDefault();
            invoke('OnEntryMenuRequested', entry.dataset.entryId, event.clientX, event.clientY);
        }
    }

    const fitObserver = new MutationObserver(scheduleFit);
    const resizeObserver = new ResizeObserver(scheduleFit);
    let fitQueued = false;

    function scheduleFit() {
        if (fitQueued) {
            return;
        }

        fitQueued = true;
        requestAnimationFrame(() => {
            fitQueued = false;
            fitObserver.disconnect();
            root.querySelectorAll('.entry:not(.compact)').forEach(fitEntry);
            fitObserver.observe(root, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['style', 'class'] });
        });
    }

    resizeObserver.observe(root);
    scheduleFit();

    if (readOnly) {
        instances.set(root, () => {
            fitObserver.disconnect();
            resizeObserver.disconnect();
        });
        return;
    }

    root.addEventListener('pointerdown', onPointerDown);
    root.addEventListener('click', onClick, true);
    document.addEventListener('pointerdown', onTaskPointerDown);
    document.addEventListener('touchmove', onTouchMove, { passive: false });
    document.addEventListener('contextmenu', onContextMenu);

    instances.set(root, () => {
        fitObserver.disconnect();
        resizeObserver.disconnect();
        cancelDrag();
        cancelLongPress();
        cancelMouseTask();
        endTaskDrag();
        root.removeEventListener('pointerdown', onPointerDown);
        root.removeEventListener('click', onClick, true);
        document.removeEventListener('pointerdown', onTaskPointerDown);
        document.removeEventListener('touchmove', onTouchMove);
        document.removeEventListener('contextmenu', onContextMenu);
    });
}

const MAX_FONT = 12.5;
const MIN_FONT = 10;

function fitEntry(entry) {
    const text = entry.querySelector('.entry-text');
    if (!text) {
        return;
    }

    const more = entry.querySelector('.entry-more');
    entry.removeAttribute('data-clipped');
    text.style.removeProperty('-webkit-line-clamp');

    const overflows = () => entry.scrollHeight > entry.clientHeight + 1;
    let size = MAX_FONT;
    entry.style.fontSize = `${size}px`;

    while (overflows() && size > MIN_FONT) {
        size = Math.max(MIN_FONT, size - 0.5);
        entry.style.fontSize = `${size}px`;
    }

    if (!overflows()) {
        return;
    }

    entry.setAttribute('data-clipped', 'true');

    const style = getComputedStyle(entry);
    const padding = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
    const time = entry.querySelector('.entry-time');
    const used = padding + (time?.offsetHeight ?? 0) + more.offsetHeight + parseFloat(getComputedStyle(text).marginTop);
    const lineHeight = parseFloat(getComputedStyle(text).lineHeight) || size * 1.3;
    const lines = Math.max(1, Math.floor((entry.clientHeight - used) / lineHeight));
    text.style.webkitLineClamp = String(lines);
}

export function scrollToMinute(root, minute) {
    const ppm = parseFloat(root.dataset.ppm);
    root.scrollTop = Math.max(0, minute * ppm);
}

export function dispose(root) {
    instances.get(root)?.();
    instances.delete(root);
}
