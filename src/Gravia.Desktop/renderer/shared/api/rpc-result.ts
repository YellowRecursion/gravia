export type RpcErrorData = Readonly<Record<string, unknown>>;

interface RpcSuccessMetadata {
  readonly isSuccess: true;
  readonly errorCode: null;
  readonly errorMessage: null;
  readonly traceId: null;
  readonly errorData: null;
}

export type RpcSuccess<TData = undefined> = RpcSuccessMetadata &
  ([TData] extends [undefined] ? { readonly data?: undefined } : { readonly data: TData });

export interface RpcFailure {
  readonly isSuccess: false;
  readonly errorCode: string;
  readonly errorMessage: string;
  readonly traceId: string;
  readonly errorData: RpcErrorData | null;
  readonly data?: unknown;
}

export type RpcResult<TData = undefined> = RpcSuccess<TData> | RpcFailure;

export interface RpcFieldError {
  readonly field: string;
  readonly code: string;
  readonly message: string;
}

export class RpcError extends Error {
  readonly code: string;
  readonly traceId: string;
  readonly data: RpcErrorData | null;

  constructor(failure: RpcFailure) {
    super(failure.errorMessage);
    this.name = 'RpcError';
    this.code = failure.errorCode;
    this.traceId = failure.traceId;
    this.data = failure.errorData;
  }
}

/** Converts the Host result envelope into the value/error contract expected by TanStack Query. */
export function unwrapRpcResult<TData>(result: RpcResult<TData>): TData {
  if (!result.isSuccess) throw new RpcError(result);
  return result.data as TData;
}

export function getRpcFieldErrors(error: unknown): readonly RpcFieldError[] {
  if (!(error instanceof RpcError)) return [];
  const errors = error.data?.errors;
  if (!Array.isArray(errors)) return [];
  return errors.filter(isRpcFieldError);
}

function isRpcFieldError(value: unknown): value is RpcFieldError {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<RpcFieldError>;
  return (
    typeof candidate.field === 'string' &&
    typeof candidate.code === 'string' &&
    typeof candidate.message === 'string'
  );
}
