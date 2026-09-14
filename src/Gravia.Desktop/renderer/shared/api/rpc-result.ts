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
