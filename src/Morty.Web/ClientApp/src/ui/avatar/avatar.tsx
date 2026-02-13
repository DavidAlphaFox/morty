import type { ParentComponent, JSX } from 'solid-js';
import { splitProps, Show, createSignal } from 'solid-js';

export interface AvatarProps extends JSX.HTMLAttributes<HTMLDivElement> {
  src?: string;
  alt?: string;
  fallback?: string;
}

export interface AvatarImageProps {
  src?: string;
  alt?: string;
}

export const AvatarRoot: ParentComponent<AvatarProps> = (props) => {
  const [local, rest] = splitProps(props, ['src', 'alt', 'fallback']);
  const [imageLoaded, setImageLoaded] = createSignal(false);
  const [imageError, setImageError] = createSignal(false);

  return (
    <div {...rest}>
      <Show when={local.src}>
        <img
          src={local.src}
          alt={local.alt}
          classList={{ 'hidden': imageError() }}
          onLoad={() => setImageLoaded(true)}
          onError={() => setImageError(true)}
        />
      </Show>
      <Show when={!local.src || imageError() || !imageLoaded()}>
        <AvatarFallback>{local.fallback || rest.children}</AvatarFallback>
      </Show>
    </div>
  );
};

export const AvatarImage: ParentComponent<AvatarImageProps> = (props) => {
  const [local, rest] = splitProps(props, ['src', 'alt']);
  const [imageError, setImageError] = createSignal(false);

  return (
    <img
      src={local.src}
      alt={local.alt}
      classList={{ 'hidden': imageError() }}
      onError={() => setImageError(true)}
      {...rest}
    />
  );
};

export const AvatarFallback: ParentComponent = (props) => {
  return <div {...props} />;
};

export const Avatar: ParentComponent<AvatarProps> = (props) => {
  const [local, rest] = splitProps(props, ['src', 'alt', 'fallback', 'class', 'children']);

  return (
    <AvatarRoot
      src={local.src}
      alt={local.alt}
      fallback={local.fallback}
      class={`happy-avatar ${local.class || ''}`}
      {...rest}
    >
      {local.children}
    </AvatarRoot>
  );
};

export const AvatarParts = {
  Root: AvatarRoot,
  Image: AvatarImage,
  Fallback: AvatarFallback,
};
