export interface AvatarProps {
  name: string;
  size?: 'sm' | 'md' | 'lg';
  tone?: 'navy' | 'blue';
}

export function getInitials(name: string): string {
  const parts = name
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  if (parts.length === 0) return 'U';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

export function Avatar({ name, size = 'md', tone = 'navy' }: AvatarProps) {
  const sizeClass =
    size === 'sm'
      ? 'h-8 w-8 text-xs'
      : size === 'lg'
      ? 'h-11 w-11 text-base'
      : 'h-9 w-9 text-xs';

  const toneClass =
    tone === 'blue'
      ? 'bg-[#2563EB] text-white'
      : 'bg-[#172554] text-white';

  return (
    <div
      aria-label={name}
      className={`inline-flex items-center justify-center rounded-full font-semibold select-none shrink-0 ${sizeClass} ${toneClass}`}
    >
      {getInitials(name)}
    </div>
  );
}
