export type ValueType =
  | { readonly kind: 'string' | 'bool' | 'int' | 'double'; readonly nullable: boolean }
  | { readonly kind: 'list'; readonly nullable: boolean; readonly element: ValueType }
  | { readonly kind: 'dictionary'; readonly nullable: boolean; readonly value: ValueType };

export interface ChoiceOption {
  readonly value: unknown;
  readonly label: { readonly translations: Readonly<Record<string, string>> };
}

function record(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

/** Unsupported descriptors are read-only, never guessed from their current value. */
export function parseValueType(value: unknown, depth = 0): ValueType | undefined {
  if (!record(value) || depth > 32 || typeof value.nullable !== 'boolean') return;
  const nullable = value.nullable;
  switch (value.kind) {
    case 'string':
    case 'bool':
    case 'int':
    case 'double':
      return { kind: value.kind, nullable };
    case 'list': {
      const element = parseValueType(value.element, depth + 1);
      return element ? { kind: 'list', nullable, element } : undefined;
    }
    case 'dictionary': {
      const child = parseValueType(value.value, depth + 1);
      return child ? { kind: 'dictionary', nullable, value: child } : undefined;
    }
  }
}

export function valueFits(type: ValueType, value: unknown): boolean {
  if (value === null) return type.nullable;
  switch (type.kind) {
    case 'string':
      return typeof value === 'string';
    case 'bool':
      return typeof value === 'boolean';
    case 'int':
      return (
        typeof value === 'number' &&
        Number.isInteger(value) &&
        value >= -2147483648 &&
        value <= 2147483647
      );
    case 'double':
      return typeof value === 'number' && Number.isFinite(value);
    case 'list':
      return Array.isArray(value) && value.every((item: unknown) => valueFits(type.element, item));
    case 'dictionary':
      return record(value) && Object.values(value).every((item) => valueFits(type.value, item));
  }
}

/** JSON equality: dictionary order is irrelevant; list order and primitive types matter. */
export function equalValue(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (Array.isArray(a) && Array.isArray(b))
    return a.length === b.length && a.every((item: unknown, i) => equalValue(item, b[i]));
  if (record(a) && record(b)) {
    const keys = Object.keys(a);
    return (
      keys.length === Object.keys(b).length &&
      keys.every((key) => Object.hasOwn(b, key) && equalValue(a[key], b[key]))
    );
  }
  return false;
}

// Each handler owns compatibility. Unknown and incompatible traits have no effect.
function choiceOptions(type: ValueType, trait: unknown): readonly ChoiceOption[] | undefined {
  // The current backend does not define structural choice equality for collections.
  if (type.kind === 'list' || type.kind === 'dictionary') return;
  if (!record(trait) || trait.type !== 'choice' || !Array.isArray(trait.options)) return;
  const options: ChoiceOption[] = [];
  for (const option of trait.options as unknown[]) {
    if (
      !record(option) ||
      !Object.hasOwn(option, 'value') ||
      !valueFits(type, option.value) ||
      !record(option.label) ||
      !record(option.label.translations) ||
      !Object.values(option.label.translations).every((label) => typeof label === 'string')
    )
      return;
    options.push({
      value: option.value,
      label: { translations: option.label.translations as Record<string, string> },
    });
  }
  return options;
}

export function resolveValueTraits(type: ValueType, traits: readonly unknown[]) {
  for (const trait of traits) {
    const choices = choiceOptions(type, trait);
    if (choices) return { choices };
  }
  return { choices: undefined };
}

export function initialValue(type: ValueType): unknown {
  if (type.nullable) return null;
  switch (type.kind) {
    case 'string':
      return '';
    case 'bool':
      return false;
    case 'int':
    case 'double':
      return 0;
    case 'list':
      return [];
    case 'dictionary':
      return {};
  }
}

export type ValueDraft =
  | { kind: 'scalar'; value: unknown }
  | { kind: 'list'; items: ValueDraft[] }
  | { kind: 'dictionary'; entries: { key: string; value: ValueDraft }[] };

export function createValueDraft(
  type: ValueType,
  value: unknown,
  traits: readonly unknown[] = [],
): ValueDraft {
  if (resolveValueTraits(type, traits).choices || value === null) return { kind: 'scalar', value };
  if (type.kind === 'list' && Array.isArray(value))
    return {
      kind: 'list',
      items: value.map((item: unknown) => createValueDraft(type.element, item)),
    };
  if (type.kind === 'dictionary' && record(value))
    return {
      kind: 'dictionary',
      entries: Object.entries(value).map(([key, item]) => ({
        key,
        value: createValueDraft(type.value, item),
      })),
    };
  return { kind: 'scalar', value };
}

export type DraftResult =
  | { ok: true; value: unknown }
  | { ok: false; error: 'invalidValue' | 'duplicateKey' | 'invalidChoice' };

export function readValueDraft(
  type: ValueType,
  draft: ValueDraft,
  traits: readonly unknown[] = [],
): DraftResult {
  let value: unknown;
  if (draft.kind === 'scalar') value = draft.value;
  else if (draft.kind === 'list' && type.kind === 'list') {
    const items: unknown[] = [];
    for (const item of draft.items) {
      const result = readValueDraft(type.element, item);
      if (!result.ok) return result;
      items.push(result.value);
    }
    value = items;
  } else if (draft.kind === 'dictionary' && type.kind === 'dictionary') {
    const entries: [string, unknown][] = [];
    const keys = new Set<string>();
    for (const entry of draft.entries) {
      if (keys.has(entry.key)) return { ok: false, error: 'duplicateKey' };
      keys.add(entry.key);
      const result = readValueDraft(type.value, entry.value);
      if (!result.ok) return result;
      entries.push([entry.key, result.value]);
    }
    value = Object.fromEntries(entries);
  } else return { ok: false, error: 'invalidValue' };
  if (!valueFits(type, value)) return { ok: false, error: 'invalidValue' };
  const { choices } = resolveValueTraits(type, traits);
  if (choices && !choices.some((option) => equalValue(option.value, value)))
    return { ok: false, error: 'invalidChoice' };
  return { ok: true, value };
}
