/** 24-hour clock, always. */
export const hhmm = (d: Date) => d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', hour12: false })
export const hhmmss = (d: Date) => d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
