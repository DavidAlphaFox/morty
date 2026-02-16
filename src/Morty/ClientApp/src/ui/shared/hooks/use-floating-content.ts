import { createEffect, Accessor, onCleanup } from 'solid-js';

export interface FloatingPosition {
  top: number;
  left: number;
}

export interface UseFloatingContentOptions {
  triggerRef: Accessor<HTMLElement | undefined>;
  contentRef: Accessor<HTMLElement | undefined>;
  isOpen: Accessor<boolean>;
  offset?: number;
  placement?: 'top' | 'bottom' | 'left' | 'right';
  strategy?: 'absolute' | 'fixed';
}

export function useFloatingContent(options: UseFloatingContentOptions) {
  const {
    triggerRef,
    contentRef,
    isOpen,
    offset = 8,
    placement = 'bottom',
    strategy = 'fixed',
  } = options;

  const updatePosition = () => {
    const trigger = triggerRef();
    const content = contentRef();

    if (!trigger || !content || !isOpen()) return;

    const triggerRect = trigger.getBoundingClientRect();
    const contentRect = content.getBoundingClientRect();

    let top = 0;
    let left = 0;

    switch (placement) {
      case 'top':
        top = triggerRect.top - contentRect.height - offset;
        left = triggerRect.left;
        break;
      case 'bottom':
        top = triggerRect.bottom + offset;
        left = triggerRect.left;
        break;
      case 'left':
        top = triggerRect.top;
        left = triggerRect.left - contentRect.width - offset;
        break;
      case 'right':
        top = triggerRect.top;
        left = triggerRect.right + offset;
        break;
    }

    content.style.position = strategy;
    content.style.top = `${top}px`;
    content.style.left = `${left}px`;
  };

  createEffect(() => {
    if (isOpen()) {
      queueMicrotask(updatePosition);

      const handleScroll = () => updatePosition();
      const handleResize = () => updatePosition();

      window.addEventListener('scroll', handleScroll, true);
      window.addEventListener('resize', handleResize);

      onCleanup(() => {
        window.removeEventListener('scroll', handleScroll, true);
        window.removeEventListener('resize', handleResize);
      });
    }
  });

  return { updatePosition };
}
