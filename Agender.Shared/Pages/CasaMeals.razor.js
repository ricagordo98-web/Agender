const DRAG_THRESHOLD_PX = 6;
const LONG_PRESS_MS = 320;
const LONG_PRESS_SLOP_PX = 10;
const AUTOSCROLL_EDGE_PX = 72;
const AUTOSCROLL_MAX_SPEED = 18;
const SETTLE_MS = 180;

let active = null;

export function init(root, dotNetRef) {
    active?.();

    const trash = root.querySelector('.ml-trash');
    const scroller = root.closest('.page') ?? document.scrollingElement;

    let press = null;
    let drag = null;
    let frame = null;
    let suppressClick = false;

    const invoke = (method, ...args) =>
        dotNetRef.invokeMethodAsync(method, ...args).catch((error) => console.error(`Agender: ${method} failed`, error));

    function cancelPress() {
        clearTimeout(press?.timer);
        press = null;
    }

    function onPointerDown(event) {
        if (drag || (event.pointerType === 'mouse' && event.button !== 0)) {
            return;
        }

        const slot = event.target.closest('.ml-slot[data-entry]');

        if (!slot || !root.contains(slot)) {
            return;
        }

        press = { slot, pointerId: event.pointerId, x: event.clientX, y: event.clientY, touch: event.pointerType !== 'mouse', timer: null };

        if (press.touch) {
            press.timer = setTimeout(() => {
                if (press) {
                    navigator.vibrate?.(12);
                    start(press.x, press.y);
                }
            }, LONG_PRESS_MS);
        }
    }

    function onPointerMove(event) {
        if (drag && event.pointerId === drag.pointerId) {
            event.preventDefault();
            drag.x = event.clientX;
            drag.y = event.clientY;
            place();
            updateTarget();
            return;
        }

        if (!press || event.pointerId !== press.pointerId) {
            return;
        }

        const distance = Math.hypot(event.clientX - press.x, event.clientY - press.y);

        if (press.touch) {
            if (distance > LONG_PRESS_SLOP_PX) {
                cancelPress();
            }
        } else if (distance >= DRAG_THRESHOLD_PX) {
            start(press.x, press.y);
            drag.x = event.clientX;
            drag.y = event.clientY;
            place();
            updateTarget();
        }
    }

    function onPointerUp(event) {
        if (drag && event.pointerId === drag.pointerId) {
            finish(true);
        } else if (press && event.pointerId === press.pointerId) {
            cancelPress();
        }
    }

    function onPointerCancel() {
        cancelPress();
        finish(false);
    }

    function onTouchMove(event) {
        if (drag) {
            event.preventDefault();
        }
    }

    function onKeyDown(event) {
        if (drag && event.key === 'Escape') {
            finish(false);
        }
    }

    function onClick(event) {
        if (suppressClick) {
            event.preventDefault();
            event.stopPropagation();
            suppressClick = false;
        }
    }

    function onDragStart(event) {
        event.preventDefault();
    }

    function onContextMenu(event) {
        if (drag || press?.touch) {
            event.preventDefault();
        }
    }

    function start(x, y) {
        const { slot, pointerId } = press;
        cancelPress();

        const rect = slot.getBoundingClientRect();
        const ghost = slot.cloneNode(true);
        ghost.removeAttribute('data-entry');
        ghost.removeAttribute('title');
        ghost.setAttribute('aria-hidden', 'true');
        ghost.tabIndex = -1;
        ghost.classList.add('ml-drag-ghost');
        ghost.style.width = `${rect.width}px`;
        ghost.style.height = `${rect.height}px`;
        ghost.style.transformOrigin = `${x - rect.left}px ${y - rect.top}px`;
        document.body.appendChild(ghost);

        drag = { slot, pointerId, ghost, dx: x - rect.left, dy: y - rect.top, x, y, target: null };
        slot.classList.add('ml-drag-source');
        root.dataset.dragKind = slot.dataset.kind;
        root.classList.add('ml-dragging');
        document.body.classList.add('ml-drag-active');
        place();
        updateTarget();
        frame = requestAnimationFrame(autoScroll);
    }

    function place() {
        drag.ghost.style.translate = `${drag.x - drag.dx}px ${drag.y - drag.dy}px`;
    }

    function targetAt(x, y) {
        const element = document.elementFromPoint(x, y);

        if (!element) {
            return null;
        }

        if (trash && trash.contains(element)) {
            return trash;
        }

        const slot = element.closest('.ml-slot[data-slot]');

        return slot && root.contains(slot) && slot !== drag.slot && slot.dataset.kind === drag.slot.dataset.kind ? slot : null;
    }

    function updateTarget() {
        const target = targetAt(drag.x, drag.y);

        if (target === drag.target) {
            return;
        }

        drag.target?.classList.remove('ml-drop-over', 'ml-drop-swap');
        drag.target = target;

        if (target === trash) {
            target.classList.add('ml-drop-over');
        } else if (target) {
            target.classList.add('ml-drop-over');
            target.classList.toggle('ml-drop-swap', target.hasAttribute('data-entry'));
        }

        drag.ghost.classList.toggle('ml-ghost-trash', target === trash);
    }

    function autoScroll() {
        if (!drag) {
            return;
        }

        const bounds = scroller === document.scrollingElement
            ? { top: 0, bottom: window.innerHeight }
            : scroller.getBoundingClientRect();
        const reserve = trash && root.classList.contains('ml-dragging') ? trash.offsetHeight + 24 : 0;
        let speed = 0;

        if (drag.y < bounds.top + AUTOSCROLL_EDGE_PX) {
            speed = -AUTOSCROLL_MAX_SPEED * (1 - Math.max(drag.y - bounds.top, 0) / AUTOSCROLL_EDGE_PX);
        } else if (drag.y > bounds.bottom - reserve - AUTOSCROLL_EDGE_PX && drag.y < bounds.bottom - reserve) {
            speed = AUTOSCROLL_MAX_SPEED * (1 - Math.max(bounds.bottom - reserve - drag.y, 0) / AUTOSCROLL_EDGE_PX);
        }

        if (speed !== 0) {
            scroller.scrollTop += speed;
            updateTarget();
        }

        frame = requestAnimationFrame(autoScroll);
    }

    function settle({ ghost, dx, dy }, destination, toTrash) {
        if (matchMedia('(prefers-reduced-motion: reduce)').matches || !ghost.animate) {
            ghost.remove();
            return;
        }

        const rect = destination.getBoundingClientRect();
        const end = toTrash
            ? { translate: `${rect.left + rect.width / 2 - dx}px ${rect.top + rect.height / 2 - dy}px`, scale: '0.1', opacity: 0 }
            : { translate: `${rect.left}px ${rect.top}px`, scale: '1', rotate: '0deg', opacity: 0.9 };

        ghost.animate([{ translate: ghost.style.translate }, end], { duration: SETTLE_MS, easing: 'cubic-bezier(.2,.8,.2,1)', fill: 'forwards' })
            .finished.finally(() => ghost.remove());
    }

    function finish(commit) {
        if (!drag) {
            return;
        }

        cancelAnimationFrame(frame);
        const { slot, target } = drag;
        const entry = slot.dataset.entry;

        settle(drag, commit && target ? target : slot, commit && target === trash);
        drag = null;

        target?.classList.remove('ml-drop-over', 'ml-drop-swap');
        slot.classList.remove('ml-drag-source');
        root.classList.remove('ml-dragging');
        delete root.dataset.dragKind;
        document.body.classList.remove('ml-drag-active');

        suppressClick = true;
        setTimeout(() => (suppressClick = false), 350);

        if (!commit || !target) {
            return;
        }

        if (target === trash) {
            invoke('RemoveDropped', entry);
        } else {
            invoke('MoveDropped', entry, target.dataset.date, Number(target.dataset.slot));
        }
    }

    root.addEventListener('pointerdown', onPointerDown);
    root.addEventListener('dragstart', onDragStart);
    root.addEventListener('contextmenu', onContextMenu);
    window.addEventListener('pointermove', onPointerMove, { passive: false });
    window.addEventListener('pointerup', onPointerUp);
    window.addEventListener('pointercancel', onPointerCancel);
    window.addEventListener('touchmove', onTouchMove, { passive: false });
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('click', onClick, true);

    active = () => {
        cancelPress();
        finish(false);
        root.removeEventListener('pointerdown', onPointerDown);
        root.removeEventListener('dragstart', onDragStart);
        root.removeEventListener('contextmenu', onContextMenu);
        window.removeEventListener('pointermove', onPointerMove);
        window.removeEventListener('pointerup', onPointerUp);
        window.removeEventListener('pointercancel', onPointerCancel);
        window.removeEventListener('touchmove', onTouchMove);
        window.removeEventListener('keydown', onKeyDown);
        window.removeEventListener('click', onClick, true);
        active = null;
    };
}

export function dispose() {
    active?.();
}
