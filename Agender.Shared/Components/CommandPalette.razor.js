export function scrollActiveIntoView(container) {
    container.querySelector('.palette-item.active')?.scrollIntoView({ block: 'nearest' });
}
