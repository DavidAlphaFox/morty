export function callHandler<T extends Event>(
  event: T,
  handler?: ((event: T) => void) | null
): void {
  if (typeof handler === 'function') {
    handler(event);
  }
}

export function chainHandlers<T extends Event>(
  event: T,
  ...handlers: Array<((event: T) => void) | null | undefined>
): void {
  handlers.forEach((handler) => {
    if (typeof handler === 'function') {
      handler(event);
    }
  });
}

export function composeHandlers<T extends Event>(
  ...handlers: Array<((event: T) => void) | null | undefined>
): (event: T) => void {
  return (event: T) => chainHandlers(event, ...handlers);
}
