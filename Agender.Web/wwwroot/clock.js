export function getUtcOffsetMinutes() {
    return -new Date().getTimezoneOffset();
}
