const STEP_RATIO = 0.7;
const EDGE_PX = 2;
const attached = new WeakMap();

export function init(root) {
    const track = root?.querySelector(':scope > :not(.sa-btn)');

    if (!track || attached.has(root)) {
        return;
    }

    const update = () => {
        const max = track.scrollWidth - track.clientWidth;
        root.toggleAttribute('data-prev', track.scrollLeft > EDGE_PX);
        root.toggleAttribute('data-next', track.scrollLeft < max - EDGE_PX);
    };

    const step = (direction) => track.scrollBy({ left: direction * Math.max(track.clientWidth * STEP_RATIO, 80), behavior: 'smooth' });
    const onPrev = () => step(-1);
    const onNext = () => step(1);
    const prev = root.querySelector(':scope > .sa-prev');
    const next = root.querySelector(':scope > .sa-next');

    prev?.addEventListener('click', onPrev);
    next?.addEventListener('click', onNext);
    track.addEventListener('scroll', update, { passive: true });

    const resize = new ResizeObserver(update);
    resize.observe(track);
    const mutation = new MutationObserver(update);
    mutation.observe(track, { childList: true, subtree: true, characterData: true });

    attached.set(root, () => {
        prev?.removeEventListener('click', onPrev);
        next?.removeEventListener('click', onNext);
        track.removeEventListener('scroll', update);
        resize.disconnect();
        mutation.disconnect();
    });

    update();
}

export function dispose(root) {
    attached.get(root)?.();
    attached.delete(root);
}
