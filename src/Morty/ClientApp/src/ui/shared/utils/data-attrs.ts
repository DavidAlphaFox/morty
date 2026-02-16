export function getDataState(isOpen: boolean): 'open' | 'closed' {
  return isOpen ? 'open' : 'closed';
}

export function getDataCheckedState(checked: boolean | 'indeterminate'): 'checked' | 'unchecked' | 'indeterminate' {
  if (checked === 'indeterminate') return 'indeterminate';
  return checked ? 'checked' : 'unchecked';
}

export function getDataSelectedState(selected: boolean): 'selected' | 'unselected' {
  return selected ? 'selected' : 'unselected';
}

export function getDataDisabledState(disabled: boolean): 'disabled' | 'enabled' {
  return disabled ? 'disabled' : 'enabled';
}

export function getDataOrientation(orientation?: 'horizontal' | 'vertical'): 'horizontal' | 'vertical' | undefined {
  return orientation;
}

export function getDataExpanded(expanded: boolean): 'true' | 'false' {
  return expanded ? 'true' : 'false';
}

export function mergeDataAttrs<T extends Record<string, any>>(
  props: T,
  dataAttrs: Record<string, string | boolean | undefined>
): T {
  return {
    ...props,
    ...dataAttrs,
  };
}
