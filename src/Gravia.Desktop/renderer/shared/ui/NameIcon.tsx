import { getNameColors } from '@/shared/lib';

/** A deterministic, decorative name badge. No persistence, network or random state. */
export function NameIcon({
  name,
  size = 32,
  className,
}: {
  name: string;
  size?: number;
  className?: string;
}) {
  const colors = getNameColors(name);
  const { palette } = colors;
  let seed = colors.seed;
  const tiles = Array.from({ length: 4 }, (_, index) => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    const shape = (seed >>> 16) % 4;
    const rotation = ((seed >>> 24) % 4) * 90;
    const fill = palette[1 + ((seed >>> 8) % 3)];
    return (
      <g
        key={index}
        transform={`translate(${4 + (index % 2) * 13} ${4 + Math.floor(index / 2) * 13})`}
        fill={fill}
      >
        <g transform={`rotate(${rotation} 5.5 5.5)`}>
          {shape === 0 ? (
            <rect width="11" height="11" rx="2" />
          ) : shape === 1 ? (
            <circle cx="5.5" cy="5.5" r="5.5" />
          ) : shape === 2 ? (
            <path d="M0 0H11V11Z" />
          ) : (
            <path d="M0 0H11A11 11 0 0 1 0 11Z" />
          )}
        </g>
      </g>
    );
  });
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 32 32"
      aria-hidden="true"
      focusable="false"
      style={{ width: size, height: size, minWidth: size, flexShrink: 0, verticalAlign: 'middle' }}
    >
      <rect width="32" height="32" rx="7" fill={palette[0]} />
      {tiles}
      <rect
        x="0.5"
        y="0.5"
        width="31"
        height="31"
        rx="6.5"
        fill="none"
        stroke="#ffffff"
        strokeOpacity="0.12"
      />
    </svg>
  );
}
