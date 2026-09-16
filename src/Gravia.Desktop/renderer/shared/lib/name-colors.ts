/** Shared identity for name badges and their surrounding accents. */
export function getNameColors(name: string) {
  let seed = 2166136261;
  for (const char of name.normalize('NFC').trim()) {
    seed = Math.imul(seed ^ (char.codePointAt(0) ?? 0), 16777619) >>> 0;
  }
  let colorSeed = seed ^ (seed >>> 16);
  colorSeed = Math.imul(colorSeed, 0x45d9f3b);
  colorSeed ^= colorSeed >>> 16;
  const hue = (colorSeed >>> 0) % 360;
  const accentHue = (hue + 100 + ((colorSeed >>> 16) % 61)) % 360;
  return {
    seed,
    hue,
    palette: [
      `hsl(${hue} 68% 26%)`,
      `hsl(${hue} 85% 72%)`,
      `hsl(${accentHue} 88% 68%)`,
      `hsl(${hue} 65% 94%)`,
    ],
  };
}
