import type { ValueType } from '@/shared/lib';

export interface NullableExample {
  id: string;
  type: ValueType;
  value: unknown;
  traits?: readonly unknown[];
}

export const nullableExamples: readonly NullableExample[] = [
  { id: 'nullString', type: { kind: 'string', nullable: true }, value: null },
  { id: 'emptyString', type: { kind: 'string', nullable: true }, value: '' },
  { id: 'zero', type: { kind: 'int', nullable: true }, value: 0 },
  { id: 'nullNumber', type: { kind: 'double', nullable: true }, value: null },
  { id: 'falseBoolean', type: { kind: 'bool', nullable: true }, value: false },
  {
    id: 'nullableList',
    type: { kind: 'list', nullable: true, element: { kind: 'string', nullable: true } },
    value: [null, 'alpha', ''],
  },
  {
    id: 'nullableDictionary',
    type: { kind: 'dictionary', nullable: true, value: { kind: 'double', nullable: true } },
    value: { alpha: null, beta: 0.5 },
  },
  {
    id: 'nullableChoice',
    type: { kind: 'int', nullable: true },
    value: null,
    traits: [
      {
        type: 'choice',
        options: [
          {
            value: null,
            label: { translations: { ru: 'Не задано (null)', en: 'Not set (null)' } },
          },
          { value: 0, label: { translations: { ru: 'Ноль', en: 'Zero' } } },
          { value: 10, label: { translations: { ru: 'Десять', en: 'Ten' } } },
        ],
      },
    ],
  },
];
